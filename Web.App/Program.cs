using System.Text;
using GpxWorldMap.Config;
using GpxWorldMap.Processing;
using GpxWorldMap.Processing.ActivityFeed;
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
    /// Loads activities and computes every page's data in Api.Processing
    /// (ActivityIngestion / SiteDataBuilder), then wraps that data in four pages, all
    /// served locally. A shared nav bar links Weekly Mileage, Maps, and Activities; the
    /// two maps are reached only through the Maps page's Line Map / Heatmap toggle:
    ///
    ///   - a Strava-style weekly mileage chart, filterable by Run/Bike/Other and
    ///     scrollable back through the full history (WeeklyMileageDataBuilder /
    ///     WeeklyMileageHtmlBuilder) -- served as the site's home page ("/"),
    ///   - a frequency heatmap of every recorded GPS point (HeatmapDataBuilder /
    ///     HeatmapHtmlBuilder), and
    ///   - a line map of every route, colored by category (Run/Bike/Other) and
    ///     weighted/opacity-scaled by how many times that stretch of road/trail has been travelled
    ///     (LineMapDataBuilder / LineMapHtmlBuilder), and
    ///   - an Activities feed: one card per activity (route line map, distance, time,
    ///     and pace/speed), newest first, fetching older activities as JSON pages while
    ///     scrolling (ActivityFeedDataBuilder / ActivityFeedHtmlBuilder).
    ///
    /// Usage:
    ///   dotnet run -- [activity-folder] [activities.csv] [heatmap-output.html] [linemap-output.html] [weekly-mileage-output.html] [--config path.json] [--no-cache]
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            var config = AppConfigLoader.Load(args);

            var activities = ActivityIngestion.Load(config.GpxFolder, config.CsvFile, config.CacheFile);
            if (activities == null)
                return 1; // ActivityIngestion already printed why

            var data = SiteDataBuilder.Build(activities);

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
            // Map data is served separately from each map page, so the page (and its
            // basemap tiles) can render while the much larger data is still downloading.
            string heatmapDataHref = Path.ChangeExtension(heatmapHref, ".data.json");
            string lineMapDataHref = Path.ChangeExtension(lineMapHref, ".data.json");
            const string mapsHref = "maps.html";
            const string activitiesHref = "activities.html";

            string heatmapHtml = HeatmapHtmlBuilder.BuildHtml(heatmapDataHref, categoryColors, locations, config.CartoApiKey);
            string linemapHtml = LineMapHtmlBuilder.BuildHtml(
                lineMapDataHref, categoryColors, locations, config.CartoApiKey, ActivityFeedDataBuilder.ActivityUrlTemplate);

            var weeklyMileage = data.WeeklyMileage;
            string weeklyMileageHtml = WeeklyMileageHtmlBuilder.BuildHtml(
                weeklyMileage.WeeksJs, weeklyMileage.SeriesByTypeJs, weeklyMileage.ActivitiesByWeekJs, categoryColors,
                ActivityFeedDataBuilder.ActivityUrlTemplate, config.CartoApiKey);
            string mapsHtml = MapSwitcherHtmlBuilder.BuildHtml(heatmapHref, lineMapHref);

            var feed = data.ActivityFeed;
            var feedPageUrls = feed.PagesJson.Select((_, i) => $"activity-feed/page-{i}.json").ToList();
            string activitiesHtml = ActivityFeedHtmlBuilder.BuildHtml(feed.TotalActivities, feedPageUrls, config.CartoApiKey);
            string indexHtml = IndexHtmlBuilder.BuildHtml(new List<(string Key, string Label, string Href)>
            {
                ("weeklymileage", "Charts", weeklyMileageHref),
                ("maps", "Maps", mapsHref),
                ("activities", "Activities", activitiesHref)
            }, "weeklymileage");

            WriteFile(config.OutputHeatmapHtml, heatmapHtml);
            WriteFile(Path.ChangeExtension(config.OutputHeatmapHtml, ".data.json"), data.Heatmap.DataJson);
            WriteFile(config.OutputLineMapHtml, linemapHtml);
            WriteFile(Path.ChangeExtension(config.OutputLineMapHtml, ".data.json"), data.LineMap.DataJson);
            WriteFile(config.OutputWeeklyMileageHtml, weeklyMileageHtml);

            var servedFiles = new Dictionary<string, string>
            {
                [weeklyMileageHref] = weeklyMileageHtml,
                [heatmapHref] = heatmapHtml,
                [heatmapDataHref] = data.Heatmap.DataJson,
                [lineMapHref] = linemapHtml,
                [lineMapDataHref] = data.LineMap.DataJson,
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
    }
}
