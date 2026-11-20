using System.Globalization;
using System.Text.Json;
using GpxWorldMap.Models;
using GpxWorldMap.Shared;

namespace GpxWorldMap.Web.Features.Maps.Heatmap
{
    /// <summary>
    /// Builds chronological, per-activity heatmap batches while preserving the full-map
    /// point weights when all dated activities have been revealed.
    /// </summary>
    public static class HeatmapDataBuilder
    {
        public class Result
        {
            public string UndatedByTypeJs = "{}";
            public string TimelineActivitiesJs = "[]";
        }

        private sealed class ActivityBucket
        {
            public string Type { get; init; } = "";
            public DateTime? Date { get; init; }
            public Dictionary<(double Lat, double Lon), int> Counts { get; init; } = new();
        }

        private sealed class TypeScale
        {
            public double ClampMax { get; init; }
            public double Denominator { get; init; }
        }

        /// <param name="precision">
        /// Decimal places for rounding coordinates before counting repeats.
        /// </param>
        /// <param name="clampPercentile">
        /// Per-type percentile used to cap very high counts before normalization.
        /// Typical values: 90-98. Lower = stronger contrast.
        /// </param>
        public static Result Build(
            IEnumerable<ActivityTrack> tracks,
            int precision = 6,
            double clampPercentile = 95.0)
        {
            var activities = new List<ActivityBucket>();
            var totalsByType = new Dictionary<string, Dictionary<(double Lat, double Lon), int>>();

            foreach (var track in tracks)
            {
                var category = ActivityCategory.CategoryForType(track.Type);
                if (!totalsByType.TryGetValue(category, out var totals))
                {
                    totals = new Dictionary<(double, double), int>();
                    totalsByType[category] = totals;
                }

                var activityCounts = new Dictionary<(double Lat, double Lon), int>();
                foreach (var (lat, lon) in track.AllPoints)
                {
                    var key = (Math.Round(lat, precision), Math.Round(lon, precision));
                    activityCounts[key] = activityCounts.TryGetValue(key, out var activityCount) ? activityCount + 1 : 1;
                    totals[key] = totals.TryGetValue(key, out var totalCount) ? totalCount + 1 : 1;
                }

                activities.Add(new ActivityBucket
                {
                    Type = category,
                    Date = track.ActivityDate,
                    Counts = activityCounts
                });
            }

            var scales = totalsByType.ToDictionary(
                pair => pair.Key,
                pair => CreateScale(pair.Value, clampPercentile));

            var datedActivities = activities
                .Where(activity => activity.Date.HasValue)
                .OrderBy(activity => activity.Date)
                .ToList();
            var baselineActivities = activities.Where(activity => !activity.Date.HasValue).ToList();

            // If no dates are available, keep all activities visible as a static map.
            if (datedActivities.Count == 0)
                baselineActivities = activities;

            var undatedPoints = new Dictionary<string, List<double[]>>();
            foreach (var activity in baselineActivities)
                AddPoints(undatedPoints, activity.Type, ToPoints(activity, totalsByType, scales));

            var timeline = datedActivities.Select(activity => new
            {
                date = activity.Date!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                type = activity.Type,
                points = ToPoints(activity, totalsByType, scales)
            });

            return new Result
            {
                UndatedByTypeJs = JsonSerializer.Serialize(undatedPoints),
                TimelineActivitiesJs = JsonSerializer.Serialize(timeline)
            };
        }

        private static TypeScale CreateScale(
            Dictionary<(double Lat, double Lon), int> counts,
            double clampPercentile)
        {
            double clampMax = Math.Max(1.0, Percentile(counts.Values, clampPercentile));
            double denominator = Math.Log(1.0 + clampMax);
            return new TypeScale { ClampMax = clampMax, Denominator = denominator > 0.0 ? denominator : 1.0 };
        }

        private static List<double[]> ToPoints(
            ActivityBucket activity,
            Dictionary<string, Dictionary<(double Lat, double Lon), int>> totalsByType,
            Dictionary<string, TypeScale> scales)
        {
            var points = new List<double[]>(activity.Counts.Count);
            var totals = totalsByType[activity.Type];
            var scale = scales[activity.Type];
            foreach (var point in activity.Counts)
            {
                double weight = Math.Log(1.0 + Math.Min(totals[point.Key], scale.ClampMax)) / scale.Denominator;
                if (weight < 0.05) weight = 0.05;
                weight *= (double)point.Value / totals[point.Key];
                points.Add(new[] { point.Key.Lat, point.Key.Lon, weight });
            }

            return points;
        }

        private static void AddPoints(Dictionary<string, List<double[]>> byType, string type, List<double[]> points)
        {
            if (!byType.TryGetValue(type, out var allPoints))
            {
                allPoints = new List<double[]>();
                byType[type] = allPoints;
            }

            allPoints.AddRange(points);
        }

        private static double Percentile(IEnumerable<int> values, double percentile)
        {
            var arr = values.OrderBy(v => v).ToArray();
            if (arr.Length == 0) return 1.0;

            percentile = Math.Clamp(percentile, 0.0, 100.0);

            if (arr.Length == 1) return arr[0];

            double rank = (percentile / 100.0) * (arr.Length - 1);
            int lo = (int)Math.Floor(rank);
            int hi = (int)Math.Ceiling(rank);

            if (lo == hi) return arr[lo];

            double t = rank - lo;
            return arr[lo] + (arr[hi] - arr[lo]) * t;
        }
    }
}