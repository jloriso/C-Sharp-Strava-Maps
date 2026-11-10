using GpxWorldMap.Rendering;

namespace GpxWorldMap.Web.Features.Controls
{
    public static class MapTypeControlScript
    {
        public static string Build(string activeMap)
        {
            string heatmapActive = activeMap == "heatmap" ? " active" : "";
            string lineMapActive = activeMap == "linemap" ? " active" : "";
            string buttons = "<button type=\"button\" class=\"map-type-btn" + heatmapActive
                + "\" data-map=\"heatmap\" aria-pressed=\"" + (activeMap == "heatmap" ? "true" : "false")
                + "\">Heatmap</button><button type=\"button\" class=\"map-type-btn" + lineMapActive
                + "\" data-map=\"linemap\" aria-pressed=\"" + (activeMap == "linemap" ? "true" : "false")
                + "\">Line Map</button>";

            return HtmlTemplateLoader.Load("Controls/Templates", "MapTypeControl.js")
                .Replace("{{MAP_TYPE_BUTTONS_HTML}}", buttons);
        }
    }
}