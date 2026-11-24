using System.Globalization;
using System.Text;
using GpxWorldMap.Config;
using GpxWorldMap.FileInterpreter.FileTypeReaders;
using GpxWorldMap.Models;
using GpxWorldMap.Rendering;
using GpxWorldMap.Server;
using GpxWorldMap.Shared;
using GpxWorldMap.Web.Features.Activities.ActivityFeed;
using GpxWorldMap.Web.Features.Charts.WeeklyMileage;
using GpxWorldMap.Web.Features.Controls.Bookmark;
using GpxWorldMap.Web.Features.Maps.Heatmap;
using GpxWorldMap.Web.Features.Maps.Linemap;
using GpxWorldMap.Web.Features.Maps.MapSwitcher;
using GpxWorldMap.Web.Index;

namespace GpxWorldMap
{
    /// <summary>
    /// Reads every *.gpx.gz, *.tcx.gz, and *.fit.gz file in a folder, decompresses it,
    /// extracts tracks/routes/waypoints, optionally joins each one against a Strava-style
    /// activities CSV (matched by the numeric ID in the CSV's Filename column, falling
    /// back to Activity ID), and produces four pages, all served locally. A shared nav
    /// bar links Weekly Mileage, Maps, and Activities; the two maps are reached only
    /// through the Maps page's Line Map / Heatmap toggle:
    ///
    ///   - a Strava-style weekly mileage chart, filterable by Run/Bike/Other and
    ///     scrollable back through the full history (WeeklyMileageDataBuilder /
    ///     WeeklyMileageHtmlBuilder) -- served as the site's home page ("/"),
    ///   - a frequency heatmap of every recorded GPS point (HeatmapDataBuilder /
    ///     HeatmapHtmlBuilder), and
    ///   - a line map of every route, colored by category (Run/Bike/Other) and
    ///     weighted/opacity-scaled by how many times that stretch of road/trail has been travelled
    ///     (RouteFrequencyMapBuilder / LineMapHtmlBuilder), and
    ///   - an Activities feed: one card per activity (route line map, distance, time,
    ///     and pace/speed), newest first, fetching older activities as JSON pages while
    ///     scrolling (ActivityFeedDataBuilder / ActivityFeedHtmlBuilder).
    ///
    /// Usage:
    ///   dotnet run -- [activity-folder] [activities.csv] [heatmap-output.html] [linemap-output.html] [weekly-mileage-output.html] [--config path.json]
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            var config = AppConfigLoader.Load(args);

            if (!Directory.Exists(config.GpxFolder))
            {
                Console.WriteLine($"Folder not found: {config.GpxFolder}");
                return 1;
            }

            var (activitiesByFilenameId, activitiesByActivityId, csvActivities) = LoadActivities(config.CsvFile);
            if (config.CsvFile != null && activitiesByFilenameId == null)
                return 1; // LoadActivities already printed the error
            var csvRecords = csvActivities ?? new List<ActivityRecord>();

            var files = FindActivityFiles(config.GpxFolder);
            if (files.Length == 0 && csvRecords.Count == 0)
            {
                Console.WriteLine($"No .gpx.gz, .tcx.gz, or .fit.gz files found in {config.GpxFolder}");
                return 1;
            }
            if (files.Length > 0)
                Console.WriteLine($"Found {files.Length} activity file(s) in {config.GpxFolder}");
            else
                Console.WriteLine("No activity files found; weekly mileage will use the CSV activity rows.");

            var tracks = new List<ActivityTrack>();
            var matchedIds = new HashSet<string>();
            int unmatchedGpxCount = 0;
            const int segmentLogInterval = 100;
            int totalTrackedSegments = 0;
            int nextSegmentLogThreshold = segmentLogInterval;

            foreach (var file in files.OrderBy(f => f))
            {
                string name = Path.GetFileName(file);
                string id = ActivityId.FromPath(name);

                var meta = ResolveMeta(id, activitiesByFilenameId!, activitiesByActivityId!);
                if (activitiesByFilenameId!.ContainsKey(id) || activitiesByActivityId!.ContainsKey(id))
                    matchedIds.Add(id);
                else if (config.CsvFile != null)
                {
                    unmatchedGpxCount++;
                    Console.WriteLine($"  Note: no CSV row found for file '{name}' (looked for ID '{id}')");
                }

                try
                {
                    var parsed = ParseActivityFile(file, name);
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
                    foreach (var seg in parsed.Segments)
                    {
                        if (seg.Count < 2) continue;
                        track.Segments.Add(seg);
                        track.AllPoints.AddRange(seg);
                        distanceMeters += GeoDistance.PolylineLengthMeters(seg);
                    }
                    foreach (var pt in parsed.Points)
                        track.AllPoints.Add((pt.Lat, pt.Lon));
                    track.DistanceMeters = distanceMeters;

                    if (track.Segments.Count > 0)
                    {
                        totalTrackedSegments += track.Segments.Count;
                        while (totalTrackedSegments >= nextSegmentLogThreshold)
                        {
                            Console.WriteLine($"  Tracked {nextSegmentLogThreshold} total segment(s) so far...");
                            nextSegmentLogThreshold += segmentLogInterval;
                        }
                    }

                    if (track.AllPoints.Count > 0) tracks.Add(track);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Skipping {name}: {ex.Message}");
                }
            }

            if (totalTrackedSegments > 0)
                Console.WriteLine($"Tracked {totalTrackedSegments} total segment(s).");

            if (config.CsvFile != null)
                PrintMatchSummary(activitiesByFilenameId!, activitiesByActivityId!, matchedIds, unmatchedGpxCount);

            var weeklyMileageTracks = BuildWeeklyMileageTracks(tracks, csvRecords);
            if (tracks.Count == 0 && !weeklyMileageTracks.Any(track => track.ActivityDate.HasValue))
            {
                Console.WriteLine("No usable track points or dated CSV activities found.");
                return 1;
            }

            // Fixed Run/Bike/Other categories for maps + weekly mileage -- see
            // ActivityCategory. Always all three, in this order, regardless of which
            // raw types are actually present.
            var categoryColors = ActivityCategory.OrderedCategories
                .Select(c => (Type: c, Color: ActivityCategory.ColorForCategory(c))).ToList();

            var locations = BookmarkStore.Load();

            // The index shell owns navigation; generated pages are loaded into its iframe.
            string heatmapHref = Path.GetFileName(config.OutputHeatmapHtml);
            string lineMapHref = Path.GetFileName(config.OutputLineMapHtml);
            string weeklyMileageHref = Path.GetFileName(config.OutputWeeklyMileageHtml);
            const string mapsHref = "maps.html";
            const string activitiesHref = "activities.html";

            Console.WriteLine("\nBuilding heatmap...");
            var heat = HeatmapDataBuilder.Build(tracks);
            string heatmapHtml = HeatmapHtmlBuilder.BuildHtml(
                heat.UndatedByTypeJs, heat.TimelineActivitiesJs, categoryColors, locations, config.CartoApiKey);

            Console.WriteLine("Building line map (computing route frequency)...");
            var lineData = LineMapDataBuilder.Build(tracks);
            var linemapHtml = LineMapHtmlBuilder.BuildHtml(
                lineData.ByTypeJs, lineData.UndatedActivitiesJs, lineData.TimelineActivitiesJs,
                categoryColors, locations, config.CartoApiKey, ActivityFeedDataBuilder.ActivityUrlTemplate);

            Console.WriteLine("Building weekly mileage chart...");
            var weeklyMileage = WeeklyMileageDataBuilder.Build(weeklyMileageTracks);
            if (weeklyMileage.SkippedForMissingDate > 0)
                Console.WriteLine($"  Note: {weeklyMileage.SkippedForMissingDate} activity file(s) had no parseable date " +
                                  "(no CSV provided, a blank Activity Date, or an unparseable one) and were excluded from the mileage chart.");
            var weeklyMileageHtml = WeeklyMileageHtmlBuilder.BuildHtml(
                weeklyMileage.WeeksJs, weeklyMileage.SeriesByTypeJs, weeklyMileage.ActivitiesJs, categoryColors,
                ActivityFeedDataBuilder.ActivityUrlTemplate, config.CartoApiKey);
            string mapsHtml = MapSwitcherHtmlBuilder.BuildHtml(
                heatmapHref, lineMapHref);

            Console.WriteLine("Building activity feed...");
            var feed = ActivityFeedDataBuilder.Build(tracks, LoadShoes(config.CsvFile));
            var feedPageUrls = feed.PagesJson.Select((_, i) => $"activity-feed/page-{i}.json").ToList();
            string activitiesHtml = ActivityFeedHtmlBuilder.BuildHtml(feed.TotalActivities, feedPageUrls, config.CartoApiKey);
            string indexHtml = IndexHtmlBuilder.BuildHtml(new List<(string Key, string Label, string Href)>
            {
                ("weeklymileage", "Charts", weeklyMileageHref),
                ("maps", "Maps", mapsHref),
                ("activities", "Activities", activitiesHref)
            }, "weeklymileage");

            WriteFile(config.OutputHeatmapHtml, heatmapHtml);
            WriteFile(config.OutputLineMapHtml, linemapHtml);
            WriteFile(config.OutputWeeklyMileageHtml, weeklyMileageHtml);

            var servedFiles = new Dictionary<string, string>
            {
                [weeklyMileageHref] = weeklyMileageHtml,
                [heatmapHref] = heatmapHtml,
                [lineMapHref] = linemapHtml,
                [mapsHref] = mapsHtml,
                [activitiesHref] = activitiesHtml,
            };
            for (int i = 0; i < feedPageUrls.Count; i++)
                servedFiles[feedPageUrls[i]] = feed.PagesJson[i];
            foreach (var (activityId, json) in feed.ActivityJsonById)
                servedFiles[ActivityFeedDataBuilder.ActivityUrl(activityId)] = json;

            LocalWebServer.Serve(indexHtml, servedFiles);
            return 0;
        }

        private static void WriteFile(string path, string content)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, content, Encoding.UTF8);
            Console.WriteLine($"Wrote {Path.GetFullPath(path)}");
        }

        private static string[] FindActivityFiles(string folder)
        {
            return Directory.GetFiles(folder, "*.gz", SearchOption.TopDirectoryOnly)
                .Where(f => f.EndsWith(".gpx.gz", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".tcx.gz", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".fit.gz", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        private static ParsedGpx ParseActivityFile(string path, string name)
        {
            if (name.EndsWith(".gpx.gz", StringComparison.OrdinalIgnoreCase))
                return GpxReader.ParseGpx(GzHelper.DecompressText(path));
            if (name.EndsWith(".tcx.gz", StringComparison.OrdinalIgnoreCase))
                return TcxReader.ParseTcx(GzHelper.DecompressText(path));
            if (name.EndsWith(".fit.gz", StringComparison.OrdinalIgnoreCase))
                return FitReader.ParseFit(GzHelper.DecompressBytes(path));

            throw new InvalidOperationException("Unrecognized activity file extension.");
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