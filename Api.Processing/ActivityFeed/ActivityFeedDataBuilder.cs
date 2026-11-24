using System.Globalization;
using System.Text.Json;
using GpxWorldMap.Models;
using GpxWorldMap.Shared;

namespace GpxWorldMap.Processing.ActivityFeed
{
    /// <summary>
    /// Splits every activity with a route into JSON pages for the Activities feed,
    /// newest first (undated activities last), so the page can fetch older activities
    /// as the user scrolls instead of embedding them all up front. Each activity's route
    /// is simplified (Douglas-Peucker) and rounded to keep the pages small. Keep the
    /// property names in sync with FeedActivity / FeedPage in activities.ts.
    /// </summary>
    public static class ActivityFeedDataBuilder
    {
        public const int PageSize = 20;

        /// <summary>~2 m at the equator: well below what's visible on a card-sized map.</summary>
        private const double SimplifyToleranceDegrees = 0.00002;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public class Result
        {
            public int TotalActivities;

            /// <summary>Serialized JSON for each page, oldest page last.</summary>
            public List<string> PagesJson = new();

            /// <summary>Each activity's JSON on its own (same shape as a page entry), keyed by activity id,
            /// for pages that show one activity's details on demand (e.g. the Line Map popup).</summary>
            public Dictionary<string, string> ActivityJsonById = new(StringComparer.Ordinal);
        }

        /// <summary>URL template for a single activity's JSON; "{id}" is the URI-escaped activity id.</summary>
        public const string ActivityUrlTemplate = "activity-feed/activities/{id}.json";

        /// <summary>Served-file path for one activity's JSON (escaped the same way the browser requests it).</summary>
        public static string ActivityUrl(string activityId) =>
            ActivityUrlTemplate.Replace("{id}", Uri.EscapeDataString(activityId));

        public static Result Build(IEnumerable<ActivityTrack> tracks, IEnumerable<ShoeRecord> shoes)
        {
            var shoesByGear = new Dictionary<string, ShoeRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (var shoe in shoes) shoesByGear.TryAdd(shoe.GearLabel, shoe);

            var ordered = tracks
                .Where(t => t.Segments.Count > 0)
                .OrderBy(t => t.ActivityDate.HasValue ? 0 : 1)
                .ThenByDescending(t => t.ActivityDate)
                .ThenByDescending(t => t.ActivityId, StringComparer.Ordinal)
                .ToList();

            var result = new Result { TotalActivities = ordered.Count };
            var activitiesJson = ordered
                .Select(t => JsonSerializer.Serialize(ToFeedActivity(t, shoesByGear), SerializerOptions))
                .ToList();
            for (int i = 0; i < ordered.Count; i++)
                result.ActivityJsonById.TryAdd(ordered[i].ActivityId, activitiesJson[i]);
            for (int start = 0; start < ordered.Count; start += PageSize)
                result.PagesJson.Add("{\"activities\":[" + string.Join(",", activitiesJson.Skip(start).Take(PageSize)) + "]}");
            return result;
        }

        private static object ToFeedActivity(ActivityTrack track, Dictionary<string, ShoeRecord> shoesByGear)
        {
            object? gear = null;
            if (!string.IsNullOrWhiteSpace(track.Gear))
            {
                gear = shoesByGear.TryGetValue(ShoeRecord.NormalizeLabel(track.Gear), out var shoe)
                    ? new { Kind = "shoe", Name = shoe.Name.Length > 0 ? shoe.Name : shoe.Model, Detail = ShoeRecord.NormalizeLabel($"{shoe.Brand} {shoe.Model}") }
                    : new { Kind = "gear", Name = track.Gear.Trim(), Detail = "" };
            }

            string category = ActivityCategory.CategoryForType(track.Type);
            return new
            {
                Id = track.ActivityId,
                Name = string.IsNullOrWhiteSpace(track.Name) ? track.ActivityId : track.Name,
                track.Type,
                Description = string.IsNullOrWhiteSpace(track.Description) ? null : track.Description.Trim(),
                Gear = gear,
                Category = category,
                Color = ActivityCategory.ColorForCategory(category),
                Date = track.ActivityDate?.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
                DistanceMeters = Math.Round(track.DistanceMeters, 1),
                track.MovingSeconds,
                track.ElapsedSeconds,
                // Each segment is a flat [lat, lon, lat, lon, ...] array.
                Segments = track.Segments.Select(seg => Simplify(seg)
                    .SelectMany(p => new[] { Math.Round(p.Lat, 5), Math.Round(p.Lon, 5) })
                    .ToArray()).ToList()
            };
        }

        /// <summary>Iterative Douglas-Peucker simplification in degree space.</summary>
        private static List<(double Lat, double Lon)> Simplify(List<(double Lat, double Lon)> points)
        {
            if (points.Count <= 2) return points;

            var keep = new bool[points.Count];
            keep[0] = keep[^1] = true;
            var stack = new Stack<(int Start, int End)>();
            stack.Push((0, points.Count - 1));

            while (stack.Count > 0)
            {
                var (start, end) = stack.Pop();
                double maxDistance = 0;
                int index = -1;
                for (int i = start + 1; i < end; i++)
                {
                    double d = DistanceToSegment(points[i], points[start], points[end]);
                    if (d > maxDistance) { maxDistance = d; index = i; }
                }
                if (index >= 0 && maxDistance > SimplifyToleranceDegrees)
                {
                    keep[index] = true;
                    stack.Push((start, index));
                    stack.Push((index, end));
                }
            }

            var simplified = new List<(double Lat, double Lon)>();
            for (int i = 0; i < points.Count; i++)
                if (keep[i]) simplified.Add(points[i]);
            return simplified;
        }

        private static double DistanceToSegment((double Lat, double Lon) p, (double Lat, double Lon) a, (double Lat, double Lon) b)
        {
            double dx = b.Lon - a.Lon, dy = b.Lat - a.Lat;
            double lengthSquared = dx * dx + dy * dy;
            double t = lengthSquared == 0 ? 0 : Math.Clamp(((p.Lon - a.Lon) * dx + (p.Lat - a.Lat) * dy) / lengthSquared, 0, 1);
            double ex = p.Lon - (a.Lon + t * dx), ey = p.Lat - (a.Lat + t * dy);
            return Math.Sqrt(ex * ex + ey * ey);
        }
    }
}
