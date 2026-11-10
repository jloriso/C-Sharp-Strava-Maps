using System.Text.Encodings.Web;
using GpxWorldMap.Rendering;

namespace GpxWorldMap.Web.Features.Maps.MapSwitcher
{
    public static class MapSwitcherHtmlBuilder
    {
        public static string BuildHtml(
            string heatmapHref,
            string lineMapHref)
        {
            var encoder = HtmlEncoder.Default;

            return HtmlTemplateLoader.Load("Features/MapSwitcher", "maps.html")
                .Replace("{{HEATMAP_HREF}}", encoder.Encode(heatmapHref))
                .Replace("{{LINEMAP_HREF}}", encoder.Encode(lineMapHref));
        }
    }
}