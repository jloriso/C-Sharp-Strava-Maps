using GpxWorldMap.Models;
using GpxWorldMap.Rendering;
using GpxWorldMap.Web.Features.Maps.Shared;

namespace GpxWorldMap.Web.Features.Maps.Heatmap
{
    /// <summary>Builds the standalone heatmap page by loading the built heatmap.html
    /// (from heatmap.html + heatmap.ts) and injecting its settings, plus the separately
    /// served data file the page fetches after it renders. Keep the property names in
    /// sync with the HeatmapPageData / HeatmapData interfaces in heatmap.ts.</summary>
    public static class HeatmapHtmlBuilder
    {
        public static string BuildHtml(
            string dataUrl,
            List<(string Type, string Color)> types,
            List<MapLocation> locations,
            string? cartoApiKey)
        {
            return new PageDataBuilder()
                .Add("dataUrl", dataUrl)
                .Add("settings", MapPageSettings.Create(types, locations, cartoApiKey))
                .ApplyTo(HtmlTemplateLoader.Load("Features/Maps/Heatmap", "heatmap.html"));
        }

        public static string BuildDataJson(string undatedPointsByTypeJs, string timelineActivitiesJs)
        {
            return new PageDataBuilder()
                .AddRaw("undatedPointsByType", undatedPointsByTypeJs)
                .AddRaw("timelineActivities", timelineActivitiesJs)
                .ToJson();
        }
    }
}