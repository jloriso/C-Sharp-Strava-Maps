using GpxWorldMap.Models;
using GpxWorldMap.Rendering;
using GpxWorldMap.Web.Features.Maps.Shared;

namespace GpxWorldMap.Web.Features.Maps.Heatmap
{
    /// <summary>Builds the standalone heatmap page by loading the built heatmap.html
    /// (from heatmap.html + heatmap.ts) and injecting its settings. The data file the
    /// page fetches after it renders comes from HeatmapDataBuilder (Api.Processing).
    /// Keep the property names in sync with the HeatmapPageData interface in heatmap.ts.</summary>
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
    }
}