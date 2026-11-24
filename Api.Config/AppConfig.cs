namespace Api.Config
{
    /// <summary>Fully-resolved settings for one run: where the GPX files are, the
    /// optional CSV to join against, and where to write the two map outputs.</summary>
    public class AppConfig
    {
        public string GpxFolder = "";
        public string? CsvFile;
        public string OutputHeatmapHtml = "heatmaps/standard_heatmap.html";
        public string OutputLineMapHtml = "heatmaps/linemap.html";
        public string OutputWeeklyMileageHtml = "heatmaps/weekly_mileage.html";
        public string? CartoApiKey;

        /// <summary>Parsed-activity cache file (see ParsedActivityCache), or null when
        /// caching is disabled with --no-cache. Defaults to .cache/parsed-activities.bin
        /// next to the heatmap output.</summary>
        public string? CacheFile;
    }
}