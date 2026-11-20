using GpxWorldMap.Rendering;

namespace GpxWorldMap.Web.Features.Maps.MapSwitcher
{
    /// <summary>Builds the page that hosts the heatmap/line map in an iframe and
    /// switches between them. Mirrors MapSwitcherPageData in maps.ts.</summary>
    public static class MapSwitcherHtmlBuilder
    {
        public static string BuildHtml(
            string heatmapHref,
            string lineMapHref)
        {
            return new PageDataBuilder()
                .Add("heatmapHref", heatmapHref)
                .Add("linemapHref", lineMapHref)
                .ApplyTo(HtmlTemplateLoader.Load("Features/Maps/MapSwitcher", "maps.html"));
        }
    }
}