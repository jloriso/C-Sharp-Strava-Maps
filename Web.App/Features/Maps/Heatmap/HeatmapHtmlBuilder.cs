using GpxWorldMap.Models;
using GpxWorldMap.Rendering;
using GpxWorldMap.Web.Features.Maps.Shared;

namespace GpxWorldMap.Web.Features.Maps.Heatmap
{
    /// <summary>Builds the standalone heatmap page by loading the built heatmap.html
    /// (from heatmap.html + heatmap.ts) and injecting its page data. Keep the property
    /// names in sync with the HeatmapPageData interface in heatmap.ts.</summary>
    public static class HeatmapHtmlBuilder
    {
        public static string BuildHtml(
            string undatedPointsByTypeJs,
            string timelineActivitiesJs,
            List<(string Type, string Color)> types,
            List<MapLocation> locations,
            string? cartoApiKey)
        {
            return new PageDataBuilder()
                .AddRaw("undatedPointsByType", undatedPointsByTypeJs)
                .AddRaw("timelineActivities", timelineActivitiesJs)
                .Add("settings", MapPageSettings.Create(types, locations, cartoApiKey))
                .ApplyTo(HtmlTemplateLoader.Load("Features/Maps/Heatmap", "heatmap.html"));
        }
    }
}