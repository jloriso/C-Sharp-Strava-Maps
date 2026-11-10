using System.Text;
using GpxWorldMap.Controls;
using GpxWorldMap.Models;
using GpxWorldMap.Rendering;
using GpxWorldMap.Web.Features.Controls;

namespace GpxWorldMap.Web.Features.Maps.Linemap
{
    /// <summary>Builds the standalone line map page by loading the Features/Linemap/
    /// linemap.html template and substituting its {{TOKEN}} placeholders with the
    /// server-computed route data, control scripts, and shared nav bar (linking to the
    /// Weekly Mileage home page and the Heatmap page).</summary>
    public static class LineMapHtmlBuilder
    {
        public static string BuildHtml(
            string lineDataByTypeJs,
            string undatedActivitiesJs,
            string timelineActivitiesJs,
            List<(string Type, string Color)> types,
            List<MapLocation> locations,
            string? cartoApiKey)
        {
            string bookmarkScript = BookmarkControlScript.Build(locations);
            string typeControlScript = ActivityTypeControlScript.Build(types);
            string typeColorsJs = BuildTypeColorsJs(types);
            string homeControlScript = HomeControlScript.Build("/");
            string mapTypeControlScript = MapTypeControlScript.Build("linemap");
            string timelineControlScript = TimelineControlScript.Build();
            var initial = locations.FirstOrDefault() ?? new MapLocation { Lat = 20.0, Lon = 0.0, Zoom = 3 };
            string initialViewJs = $"[{(initial.Lat)}, {(initial.Lon)}], {initial.Zoom}";
            string cartoApiQuery = string.IsNullOrWhiteSpace(cartoApiKey)
                ? ""
                : "?key=" + Uri.EscapeDataString(cartoApiKey);

            return HtmlTemplateLoader.Load("Features/Linemap","linemap.html")
                .Replace("{{LINE_DATA_BY_TYPE_JS}}", lineDataByTypeJs)
                .Replace("{{UNDATED_ACTIVITIES_JS}}", undatedActivitiesJs)
                .Replace("{{TIMELINE_ACTIVITIES_JS}}", timelineActivitiesJs)
                .Replace("{{TYPE_COLORS_JS}}", typeColorsJs)
                .Replace("{{BOOKMARK_CONTROL_SCRIPT}}", bookmarkScript)
                .Replace("{{ACTIVITY_TYPE_CONTROL_SCRIPT}}", typeControlScript)
                .Replace("{{HOME_CONTROL_SCRIPT}}", homeControlScript)
                .Replace("{{MAP_TYPE_CONTROL_SCRIPT}}", mapTypeControlScript)
                .Replace("{{TIMELINE_CONTROL_SCRIPT}}", timelineControlScript)
                .Replace("{{INITIAL_VIEW_JS}}", initialViewJs)
                .Replace("{{CARTO_API_QUERY}}", cartoApiQuery);
        }

        private static string BuildTypeColorsJs(List<(string Type, string Color)> types)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var (type, color) in types)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append('"').Append(type.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\":\"").Append(color).Append('"');
            }
            sb.Append("}");
            return sb.ToString();
        }
    }
}