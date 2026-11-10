using System.Text.Encodings.Web;
using GpxWorldMap.Rendering;

namespace GpxWorldMap.Web.Features.Maps.MapSwitcher
{
    public static class MapSwitcherHtmlBuilder
    {
        public static string BuildHtml(
            string weeklyMileageHref,
            string heatmapHref,
            string lineMapHref,
            string mapsHref)
        {
            string navBarHtml = NavBarBuilder.BuildHtml("maps", weeklyMileageHref, heatmapHref, lineMapHref, mapsHref);
            var encoder = HtmlEncoder.Default;

            return HtmlTemplateLoader.Load("Features/MapSwitcher", "maps.html")
                .Replace("{{NAV_BAR_HTML}}", navBarHtml)
                .Replace("{{HEATMAP_HREF}}", encoder.Encode(heatmapHref))
                .Replace("{{LINEMAP_HREF}}", encoder.Encode(lineMapHref));
        }
    }
}