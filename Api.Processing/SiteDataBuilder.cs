using GpxWorldMap.Processing.ActivityFeed;
using GpxWorldMap.Processing.Heatmap;
using GpxWorldMap.Processing.Linemap;
using GpxWorldMap.Processing.WeeklyMileage;

namespace GpxWorldMap.Processing
{
    /// <summary>Finished, serialized data for every page; the web layer only wraps
    /// it in HTML shells and serves it.</summary>
    public sealed class SiteData
    {
        public required HeatmapDataBuilder.Result Heatmap { get; init; }
        public required LineMapDataBuilder.Result LineMap { get; init; }
        public required WeeklyMileageDataBuilder.Result WeeklyMileage { get; init; }
        public required ActivityFeedDataBuilder.Result ActivityFeed { get; init; }
    }

    public static class SiteDataBuilder
    {
        /// <summary>Runs every page's data builder concurrently (they only read the tracks).</summary>
        public static SiteData Build(IngestedActivities activities)
        {
            Console.WriteLine("\nBuilding heatmap, line map, weekly mileage chart, and activity feed...");
            var timer = System.Diagnostics.Stopwatch.StartNew();

            var heatTask = Task.Run(() => HeatmapDataBuilder.Build(activities.Tracks));
            var lineTask = Task.Run(() => LineMapDataBuilder.Build(activities.Tracks));
            var weeklyTask = Task.Run(() => WeeklyMileageDataBuilder.Build(activities.WeeklyMileageTracks));
            var feedTask = Task.Run(() => ActivityFeedDataBuilder.Build(activities.Tracks, activities.Shoes));
            Task.WaitAll(heatTask, lineTask, weeklyTask, feedTask);

            Console.WriteLine($"Built page data in {timer.Elapsed.TotalSeconds:F1}s.");
            if (weeklyTask.Result.SkippedForMissingDate > 0)
                Console.WriteLine($"  Note: {weeklyTask.Result.SkippedForMissingDate} activity file(s) had no parseable date " +
                                  "(no CSV provided, a blank Activity Date, or an unparseable one) and were excluded from the mileage chart.");

            return new SiteData
            {
                Heatmap = heatTask.Result,
                LineMap = lineTask.Result,
                WeeklyMileage = weeklyTask.Result,
                ActivityFeed = feedTask.Result
            };
        }
    }
}
