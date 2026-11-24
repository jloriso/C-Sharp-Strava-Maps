using GpxWorldMap.Models;
using GpxWorldMap.Rendering;
using GpxWorldMap.Web.Features.Maps.Shared;

namespace GpxWorldMap.Web.Features.Maps.Linemap
{
    /// <summary>Builds the standalone line map page by loading the built linemap.html
    /// (from linemap.html + linemap.ts) and injecting its settings. The data file the
    /// page fetches after it renders comes from LineMapDataBuilder (Api.Processing).
    /// Keep the property names in sync with the LineMapPageData interface in linemap.ts.</summary>
    public static class LineMapHtmlBuilder
    {
        public static string BuildHtml(
            string dataUrl,
            List<(string Type, string Color)> types,
            List<MapLocation> locations,
            string? cartoApiKey,
            string activityDetailsUrl)
        {
            return new PageDataBuilder()
                .Add("dataUrl", dataUrl)
                .Add("activityDetailsUrl", activityDetailsUrl)
                .Add("settings", MapPageSettings.Create(types, locations, cartoApiKey))
                .ApplyTo(HtmlTemplateLoader.Load("Features/Maps/Linemap", "linemap.html"));
        }
    }
}