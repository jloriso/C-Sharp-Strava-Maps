using System.Collections.Generic;
using System.Text;

/// <summary>Builds the standalone line map page by loading the Rendering/Templates/
/// linemap.html template and substituting its {{TOKEN}} placeholders with the
/// server-computed route data and control scripts. The page's actual markup/CSS/JS
/// structure lives in that .html file, not in this class -- this class is only
/// responsible for producing the dynamic pieces that get spliced into it.</summary>
public static class LineMapHtmlBuilder
{
    public static string BuildHtml(
        string lineDataByTypeJs,
        List<(string Type, string Color)> types,
        List<MapLocation> locations)
    {
        string bookmarkScript = BookmarkControlScript.Build(locations);
        string typeControlScript = ActivityTypeControlScript.Build(types);
        string typeColorsJs = BuildTypeColorsJs(types);
        var initial = locations.FirstOrDefault() ?? new MapLocation { Lat = 20.0, Lon = 0.0, Zoom = 3 };
        string initialViewJs = $"[{(initial.Lat)}, {(initial.Lon)}], {initial.Zoom}";

        return HtmlTemplateLoader.Load("linemap.html")
            .Replace("{{LINE_DATA_BY_TYPE_JS}}", lineDataByTypeJs)
            .Replace("{{TYPE_COLORS_JS}}", typeColorsJs)
            .Replace("{{BOOKMARK_CONTROL_SCRIPT}}", bookmarkScript)
            .Replace("{{ACTIVITY_TYPE_CONTROL_SCRIPT}}", typeControlScript)
            .Replace("{{INITIAL_VIEW_JS}}", initialViewJs);
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