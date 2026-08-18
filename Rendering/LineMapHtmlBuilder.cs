using System.Collections.Generic;
using System.Text;

/// <summary>Builds the standalone line map page by loading the Rendering/Templates/
/// linemap.html template and substituting its {{TOKEN}} placeholders with the
/// server-computed route data, control scripts, and shared nav bar (linking to the
/// Weekly Mileage home page and the Heatmap page).</summary>
public static class LineMapHtmlBuilder
{
    public static string BuildHtml(
        string lineDataByTypeJs,
        List<(string Type, string Color)> types,
        List<MapLocation> locations,
        string heatmapHref,
        string lineMapHref,
        string weeklyMileageHref)
    {
        string bookmarkScript = BookmarkControlScript.Build(locations);
        string typeControlScript = ActivityTypeControlScript.Build(types);
        string typeColorsJs = BuildTypeColorsJs(types);
        string homeControlScript = HomeControlScript.Build("/");
        var initial = locations.FirstOrDefault() ?? new MapLocation { Lat = 20.0, Lon = 0.0, Zoom = 3 };
        string initialViewJs = $"[{(initial.Lat)}, {(initial.Lon)}], {initial.Zoom}";
        string navBarHtml = NavBarBuilder.BuildHtml("linemap", weeklyMileageHref, heatmapHref, lineMapHref);

        return HtmlTemplateLoader.Load("Rendering/Templates","linemap.html")
            .Replace("{{LINE_DATA_BY_TYPE_JS}}", lineDataByTypeJs)
            .Replace("{{TYPE_COLORS_JS}}", typeColorsJs)
            .Replace("{{BOOKMARK_CONTROL_SCRIPT}}", bookmarkScript)
            .Replace("{{ACTIVITY_TYPE_CONTROL_SCRIPT}}", typeControlScript)
            .Replace("{{HOME_CONTROL_SCRIPT}}", homeControlScript)
            .Replace("{{INITIAL_VIEW_JS}}", initialViewJs)
            .Replace("{{NAV_BAR_HTML}}", navBarHtml);
    }

    static string BuildTypeColorsJs(List<(string Type, string Color)> types)
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