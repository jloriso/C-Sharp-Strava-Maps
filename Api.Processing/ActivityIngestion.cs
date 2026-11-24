using System.Globalization;
using Api.FileReader;
using Api.FileReader.FileTypeReaders;
using Api.Models;
using Api.Shared;

namespace Api.Processing
{
    /// <summary>Everything the page data builders need, produced by ActivityIngestion.</summary>
    public sealed class IngestedActivities
    {
        /// <summary>Activities with at least one GPS point (maps and the activity feed).</summary>
        public List<ActivityTrack> Tracks { get; init; } = new();

        /// <summary>Tracks plus CSV-only activities (no file, or a file with no route),
        /// so the mileage chart counts every activity the CSV knows about.</summary>
        public List<ActivityTrack> WeeklyMileageTracks { get; init; } = new();

        public List<ShoeRecord> Shoes { get; init; } = new();
    }

    /// <summary>
    /// Reads every activity file in a folder (in parallel, with an optional parsed-file
    /// cache), joins each one against an optional Strava-style activities CSV (matched by
    /// the numeric ID in the CSV's Filename column, falling back to Activity ID), and
    /// assembles the ActivityTracks used by every page.
    /// </summary>
    public static class ActivityIngestion
    {
        /// <summary>Returns null (after printing why) when there is nothing usable to show.</summary>
        public static IngestedActivities? Load(string gpxFolder, string? csvFile, string? cacheFile)
        {
            if (!Directory.Exists(gpxFolder))
            {
                Console.WriteLine($"Folder not found: {gpxFolder}");
                return null;
            }

            var (activitiesByFilenameId, activitiesByActivityId, csvActivities) = LoadActivities(csvFile);
            if (activitiesByFilenameId == null || activitiesByActivityId == null || csvActivities == null)
                return null; // LoadActivities already printed the error

            var files = ActivityFileLoader.FindActivityFiles(gpxFolder);
            if (files.Length == 0 && csvActivities.Count == 0)
            {
                Console.WriteLine($"No .gpx.gz, .tcx.gz, or .fit.gz files found in {gpxFolder}");
                return null;
            }
            if (files.Length > 0)
                Console.WriteLine($"Found {files.Length} activity file(s) in {gpxFolder}");
            else
                Console.WriteLine("No activity files found; weekly mileage will use the CSV activity rows.");

            var parseTimer = System.Diagnostics.Stopwatch.StartNew();
            var loaded = ActivityFileLoader.LoadAll(files, cacheFile);
            if (files.Length > 0)
                Console.WriteLine($"Parsed {files.Length} activity file(s) in {parseTimer.Elapsed.TotalSeconds:F1}s " +
                                  $"({loaded.FromCache} from cache).");

            var tracks = new List<ActivityTrack>();
            var matchedIds = new HashSet<string>();
            int unmatchedGpxCount = 0;
            int totalTrackedSegments = 0;

            foreach (var file in loaded.Files)
            {
                string name = file.FileName;
                string id = ActivityId.FromPath(name);

                var meta = ResolveMeta(id, activitiesByFilenameId, activitiesByActivityId);
                if (activitiesByFilenameId.ContainsKey(id) || activitiesByActivityId.ContainsKey(id))
                    matchedIds.Add(id);
                else if (csvFile != null)
                {
                    unmatchedGpxCount++;
                    Console.WriteLine($"  Note: no CSV row found for file '{name}' (looked for ID '{id}')");
                }

                if (file.Parsed == null)
                {
                    Console.WriteLine($"  Skipping {name}: {file.Error}");
                    continue;
                }

                var track = new ActivityTrack
                {
                    ActivityId = id,
                    Name = meta.Name,
                    Type = meta.Type,
                    Description = meta.Description,
                    Gear = meta.Gear,
                    ElapsedSeconds = meta.ElapsedSeconds,
                    MovingSeconds = meta.MovingSeconds,
                    ActivityDate = ParseActivityDate(meta.Date)
                };

                double distanceMeters = 0;
                foreach (var seg in file.Parsed.Segments)
                {
                    if (seg.Count < 2) continue;
                    track.Segments.Add(seg);
                    track.AllPoints.AddRange(seg);
                    distanceMeters += GeoDistance.PolylineLengthMeters(seg);
                }
                foreach (var pt in file.Parsed.Points)
                    track.AllPoints.Add((pt.Lat, pt.Lon));
                track.DistanceMeters = distanceMeters;
                totalTrackedSegments += track.Segments.Count;

                if (track.AllPoints.Count > 0) tracks.Add(track);
            }

            if (totalTrackedSegments > 0)
                Console.WriteLine($"Tracked {totalTrackedSegments} total segment(s).");

            if (csvFile != null)
                PrintMatchSummary(activitiesByFilenameId, activitiesByActivityId, matchedIds, unmatchedGpxCount);

            var weeklyMileageTracks = BuildWeeklyMileageTracks(tracks, csvActivities);
            if (tracks.Count == 0 && !weeklyMileageTracks.Any(track => track.ActivityDate.HasValue))
            {
                Console.WriteLine("No usable track points or dated CSV activities found.");
                return null;
            }

            return new IngestedActivities
            {
                Tracks = tracks,
                WeeklyMileageTracks = weeklyMileageTracks,
                Shoes = LoadShoes(csvFile)
            };
        }

        private static (
            Dictionary<string, ActivityRecord>?,
            Dictionary<string, ActivityRecord>?,
            List<ActivityRecord>?) LoadActivities(string? csvFile)
        {
            var byFilenameId = new Dictionary<string, ActivityRecord>();
            var byActivityId = new Dictionary<string, ActivityRecord>();

            if (csvFile == null)
                return (byFilenameId, byActivityId, new List<ActivityRecord>());

            if (!File.Exists(csvFile))
            {
                Console.WriteLine($"CSV file not found: {csvFile}");
                return (null, null, null);
            }

            var records = CsvReader.ParseActivitiesCsv(csvFile);
            foreach (var r in records)
            {
                string filenameId = CsvReader.ExtractIdFromFilename(r.Filename);
                if (!string.IsNullOrWhiteSpace(filenameId))
                    byFilenameId[filenameId] = r; // last one wins if duplicates
                if (!string.IsNullOrWhiteSpace(r.Id))
                    byActivityId[r.Id] = r;
            }
            Console.WriteLine($"Loaded {records.Count} activities from {csvFile}");
            return (byFilenameId, byActivityId, records);
        }

        private static List<ActivityTrack> BuildWeeklyMileageTracks(
            IEnumerable<ActivityTrack> tracks,
            IEnumerable<ActivityRecord> csvRecords)
        {
            var result = tracks.ToList();
            var tracksById = new Dictionary<string, ActivityTrack>(StringComparer.Ordinal);
            foreach (var track in result.OrderByDescending(track => track.Segments.Count > 0))
                tracksById.TryAdd(track.ActivityId, track);

            var processed = new HashSet<string>(StringComparer.Ordinal);
            int rowIndex = 0;
            foreach (var record in csvRecords)
            {
                string filenameId = CsvReader.ExtractIdFromFilename(record.Filename);
                string identity = !string.IsNullOrWhiteSpace(record.Id)
                    ? record.Id
                    : !string.IsNullOrWhiteSpace(filenameId) ? filenameId : $"row-{rowIndex}";
                rowIndex++;
                if (!processed.Add(identity)) continue;

                tracksById.TryGetValue(filenameId, out var filenameTrack);
                tracksById.TryGetValue(record.Id, out var activityIdTrack);
                var matchedTrack = filenameTrack?.Segments.Count > 0
                    ? filenameTrack
                    : activityIdTrack?.Segments.Count > 0
                        ? activityIdTrack
                        : filenameTrack ?? activityIdTrack;
                if (matchedTrack?.Segments.Count > 0) continue;

                string activityId = matchedTrack?.ActivityId
                    ?? (!string.IsNullOrWhiteSpace(filenameId) ? filenameId
                        : !string.IsNullOrWhiteSpace(record.Id) ? record.Id
                        : $"csv-activity-{rowIndex}");
                string type = string.IsNullOrWhiteSpace(record.Type) ? "Unknown" : record.Type;
                string category = ActivityCategory.CategoryForType(type);
                var activity = new ActivityTrack
                {
                    ActivityId = activityId,
                    Name = string.IsNullOrWhiteSpace(record.Name) ? activityId : record.Name,
                    Type = type,
                    Description = record.Description,
                    Gear = record.Gear,
                    Color = ActivityCategory.ColorForCategory(category),
                    ActivityDate = ParseActivityDate(record.Date),
                    DistanceMeters = ParseActivityDistanceMeters(record.Distance) ?? matchedTrack?.DistanceMeters ?? 0,
                    ElapsedSeconds = ParseSeconds(record.ElapsedTime),
                    MovingSeconds = ParseSeconds(record.MovingTime)
                };

                if (matchedTrack != null)
                    result.RemoveAll(track => track.ActivityId == matchedTrack.ActivityId && track.Segments.Count == 0);
                result.Add(activity);
                tracksById[activityId] = activity;
            }

            return result;
        }

        private static double? ParseActivityDistanceMeters(string distance)
        {
            // Strava's activities.csv exports Distance in kilometers; the chart converts meters to miles.
            if (!double.TryParse(distance, NumberStyles.Float, CultureInfo.InvariantCulture, out var kilometers)
                && !double.TryParse(distance, NumberStyles.Float, CultureInfo.CurrentCulture, out kilometers))
                return null;
            return double.IsFinite(kilometers) && kilometers >= 0 ? kilometers * 1000 : null;
        }

        /// <summary>Reads shoes.csv from the activities CSV's folder (where Strava's
        /// export puts it), so the activity feed can show shoe details. Optional.</summary>
        private static List<ShoeRecord> LoadShoes(string? csvFile)
        {
            if (csvFile == null) return new();
            string path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(csvFile)) ?? "", "shoes.csv");
            if (!File.Exists(path)) return new();

            var shoes = CsvReader.ParseShoesCsv(path);
            Console.WriteLine($"Loaded {shoes.Count} shoe(s) from {path}");
            return shoes;
        }

        private static ActivityMeta ResolveMeta(
            string id,
            Dictionary<string, ActivityRecord> byFilenameId,
            Dictionary<string, ActivityRecord> byActivityId
            )
        {
            var meta = new ActivityMeta { Name = id, Type = "Unknown", Date = "", Description = "" };

            if (byFilenameId.TryGetValue(id, out var record) || byActivityId.TryGetValue(id, out record))
            {
                meta.Name = string.IsNullOrWhiteSpace(record.Name) ? id : record.Name;
                meta.Type = string.IsNullOrWhiteSpace(record.Type) ? "Unknown" : record.Type;
                meta.Date = record.Date;
                meta.Description = record.Description;
                meta.ElapsedSeconds = ParseSeconds(record.ElapsedTime);
                meta.MovingSeconds = ParseSeconds(record.MovingTime);
                meta.Gear = record.Gear;
            }

            return meta;
        }

        /// <summary>Parses the CSV's free-form Activity Date column into a DateTime for
        /// the weekly mileage chart. Tries invariant culture first (handles the most
        /// common Strava export format), then falls back to the current culture, since
        /// export locale can vary; returns null (rather than throwing) if neither
        /// parses, so one malformed date doesn't take down the whole run -- that
        /// activity is simply excluded from the mileage chart (see
        /// WeeklyMileageDataBuilder.SkippedForMissingDate).</summary>
        private static DateTime? ParseActivityDate(string date)
        {
            if (string.IsNullOrWhiteSpace(date)) return null;
            if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1)) return d1;
            if (DateTime.TryParse(date, CultureInfo.CurrentCulture, DateTimeStyles.None, out var d2)) return d2;
            return null;
        }

        private static double? ParseSeconds(string value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var s) && s > 0 ? s : null;

        private static void PrintMatchSummary(
            Dictionary<string, ActivityRecord> byFilenameId,
            Dictionary<string, ActivityRecord> byActivityId,
            HashSet<string> matchedIds,
            int unmatchedGpxCount)
        {
            int totalCsvRows = byFilenameId.Values.Select(r => r.Id)
                .Union(byActivityId.Values.Select(r => r.Id))
                .Distinct()
                .Count();
            int csvOnlyCount = totalCsvRows - matchedIds.Count;

            Console.WriteLine($"\n{matchedIds.Count} activity file(s) matched a CSV row.");
            Console.WriteLine($"~{csvOnlyCount} CSV row(s) have no corresponding activity file");
            if (unmatchedGpxCount > 0)
                Console.WriteLine($"{unmatchedGpxCount} activity file(s) had no matching CSV row (check the Filename/Activity ID columns for that activity).");
        }
    }
}
