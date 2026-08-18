using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Reads every *.gpx.gz, *.tcx.gz, and *.fit.gz file in a folder, decompresses it,
/// extracts tracks/routes/waypoints, optionally joins each one against a Strava-style
/// activities CSV (matched by the numeric ID in the CSV's Filename column, falling
/// back to Activity ID), and produces three pages, all served locally and linked
/// via a shared nav bar:
///
///   - a Strava-style weekly mileage chart, filterable by Run/Bike/Other and
///     scrollable back through the full history (WeeklyMileageDataBuilder /
///     WeeklyMileageHtmlBuilder) -- served as the site's home page ("/"),
///   - a frequency heatmap of every recorded GPS point (HeatmapDataBuilder /
///     HeatmapHtmlBuilder), and
///   - a line map of every route, colored by category (Run/Bike/Other) and
///     weighted/opacity-scaled by how many times that stretch of road/trail has been travelled
///     (RouteFrequencyMapBuilder / LineMapHtmlBuilder).
///
/// Usage:
///   dotnet run -- [activity-folder] [activities.csv] [heatmap-output.html] [linemap-output.html] [weekly-mileage-output.html] [--config path.json]
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        var config = AppConfigLoader.Load(args);

        if (!Directory.Exists(config.GpxFolder))
        {
            Console.WriteLine($"Folder not found: {config.GpxFolder}");
            return 1;
        }

        var (activitiesByFilenameId, activitiesByActivityId) = LoadActivities(config.CsvFile);
        if (config.CsvFile != null && activitiesByFilenameId == null)
            return 1; // LoadActivities already printed the error

        var files = FindActivityFiles(config.GpxFolder);
        if (files.Length == 0)
        {
            Console.WriteLine($"No .gpx.gz, .tcx.gz, or .fit.gz files found in {config.GpxFolder}");
            return 1;
        }
        Console.WriteLine($"Found {files.Length} activity file(s) in {config.GpxFolder}");

        var tracks = new List<ActivityTrack>();
        var matchedIds = new HashSet<string>();
        int unmatchedGpxCount = 0;

        foreach (var file in files.OrderBy(f => f))
        {
            string name = Path.GetFileName(file);
            string id = ActivityId.FromPath(name);

            var meta = ResolveMeta(id, activitiesByFilenameId!, activitiesByActivityId!, config.CsvFile != null);
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
                    Type = meta.Type,
                    Color = meta.Color,
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

                if (track.AllPoints.Count > 0) tracks.Add(track);

                Console.WriteLine($"  {name}: {track.Segments.Count} track segment(s), {parsed.Points.Count} waypoint(s) [{meta.Type}]");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Skipping {name}: {ex.Message}");
            }
        }

        if (config.CsvFile != null)
            PrintMatchSummary(activitiesByFilenameId!, activitiesByActivityId!, matchedIds, unmatchedGpxCount);

        if (tracks.Count == 0)
        {
            Console.WriteLine("No usable track points/waypoints found across all files.");
            return 1;
        }

        // Fixed Run/Bike/Other categories for maps + weekly mileage -- see
        // ActivityCategory. Always all three, in this order, regardless of which
        // raw types are actually present.
        var categoryColors = ActivityCategory.OrderedCategories
            .Select(c => (Type: c, Color: ActivityCategory.ColorForCategory(c))).ToList();

        var locations = DefaultLocations.Standard();

        // Every page's nav bar needs to know where the other two live. The
        // Weekly Mileage chart is served at the site root ("/"), since it's now
        // the home page (see LocalWebServer.Serve below); the other two use
        // whatever filenames you've configured, so hrefs are computed from the
        // actual configured paths rather than hardcoded, and stay correct no
        // matter what you rename OutputHeatmapHtml/OutputLineMapHtml to.
        string heatmapHref = Path.GetFileName(config.OutputHeatmapHtml);
        string lineMapHref = Path.GetFileName(config.OutputLineMapHtml);
        const string weeklyMileageHref = "/";

        Console.WriteLine("\nBuilding heatmap...");
        var heat = HeatmapDataBuilder.Build(tracks);
        string heatmapHtml = HeatmapHtmlBuilder.BuildHtml(heat.ByTypeJs, categoryColors, locations, heatmapHref, lineMapHref, weeklyMileageHref);

        Console.WriteLine("Building line map (computing route frequency)...");
        var lineData = RouteFrequencyMapBuilder.Build(tracks);
        string linemapHtml = LineMapHtmlBuilder.BuildHtml(lineData.ByTypeJs, categoryColors, locations, heatmapHref, lineMapHref, weeklyMileageHref);

        Console.WriteLine("Building weekly mileage chart...");
        var weeklyMileage = WeeklyMileageDataBuilder.Build(tracks);
        if (weeklyMileage.SkippedForMissingDate > 0)
            Console.WriteLine($"  Note: {weeklyMileage.SkippedForMissingDate} activity file(s) had no parseable date " +
                               "(no CSV provided, a blank Activity Date, or an unparseable one) and were excluded from the mileage chart.");
        string weeklyMileageHtml = WeeklyMileageHtmlBuilder.BuildHtml(
            weeklyMileage.WeeksJs, weeklyMileage.SeriesByTypeJs, categoryColors, heatmapHref, lineMapHref, weeklyMileageHref);

        WriteFile(config.OutputHeatmapHtml, heatmapHtml);
        WriteFile(config.OutputLineMapHtml, linemapHtml);
        WriteFile(config.OutputWeeklyMileageHtml, weeklyMileageHtml);

        // The Weekly Mileage page is served as the home page ("/"); the heatmap
        // and line map are served under their configured filenames alongside it.
        LocalWebServer.Serve(weeklyMileageHtml, new Dictionary<string, string>
        {
            [heatmapHref] = heatmapHtml,
            [lineMapHref] = linemapHtml,
        });
        return 0;
    }

    static void WriteFile(string path, string content)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, content, Encoding.UTF8);
        Console.WriteLine($"Wrote {Path.GetFullPath(path)}");
    }

    static string[] FindActivityFiles(string folder)
    {
        return Directory.GetFiles(folder, "*.gz", SearchOption.TopDirectoryOnly)
            .Where(f => f.EndsWith(".gpx.gz", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".tcx.gz", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".fit.gz", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    static ParsedGpx ParseActivityFile(string path, string name)
    {
        if (name.EndsWith(".gpx.gz", StringComparison.OrdinalIgnoreCase))
            return GpxReader.ParseGpx(GzHelper.DecompressText(path));
        if (name.EndsWith(".tcx.gz", StringComparison.OrdinalIgnoreCase))
            return TcxReader.ParseTcx(GzHelper.DecompressText(path));
        if (name.EndsWith(".fit.gz", StringComparison.OrdinalIgnoreCase))
            return FitReader.ParseFit(GzHelper.DecompressBytes(path));

        throw new InvalidOperationException("Unrecognized activity file extension.");
    }

    static (Dictionary<string, ActivityRecord>?, Dictionary<string, ActivityRecord>?) LoadActivities(string? csvFile)
    {
        var byFilenameId = new Dictionary<string, ActivityRecord>();
        var byActivityId = new Dictionary<string, ActivityRecord>();

        if (csvFile == null)
            return (byFilenameId, byActivityId);

        if (!File.Exists(csvFile))
        {
            Console.WriteLine($"CSV file not found: {csvFile}");
            return (null, null);
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
        return (byFilenameId, byActivityId);
    }

    static ActivityMeta ResolveMeta(
        string id,
        Dictionary<string, ActivityRecord> byFilenameId,
        Dictionary<string, ActivityRecord> byActivityId,
        bool csvProvided)
    {
        var meta = new ActivityMeta { Name = id, Type = "Unknown", Date = "", Description = "" };

        if (byFilenameId.TryGetValue(id, out var record) || byActivityId.TryGetValue(id, out record))
        {
            meta.Name = string.IsNullOrWhiteSpace(record.Name) ? id : record.Name;
            meta.Type = string.IsNullOrWhiteSpace(record.Type) ? "Unknown" : record.Type;
            meta.Date = record.Date;
            meta.Description = record.Description;
        }

        meta.Color = ActivityColors.ColorForType(meta.Type);
        return meta;
    }

    /// <summary>Parses the CSV's free-form Activity Date column into a DateTime for
    /// the weekly mileage chart. Tries invariant culture first (handles the most
    /// common Strava export format), then falls back to the current culture, since
    /// export locale can vary; returns null (rather than throwing) if neither
    /// parses, so one malformed date doesn't take down the whole run -- that
    /// activity is simply excluded from the mileage chart (see
    /// WeeklyMileageDataBuilder.SkippedForMissingDate).</summary>
    static DateTime? ParseActivityDate(string date)
    {
        if (string.IsNullOrWhiteSpace(date)) return null;
        if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1)) return d1;
        if (DateTime.TryParse(date, CultureInfo.CurrentCulture, DateTimeStyles.None, out var d2)) return d2;
        return null;
    }

    static void PrintMatchSummary(
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
        Console.WriteLine($"~{csvOnlyCount} CSV row(s) have no corresponding activity file (expected, per your note).");
        if (unmatchedGpxCount > 0)
            Console.WriteLine($"{unmatchedGpxCount} activity file(s) had no matching CSV row (check the Filename/Activity ID columns for that activity).");
    }
}
