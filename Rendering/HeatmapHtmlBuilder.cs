using System.Collections.Generic;

/// <summary>Builds the standalone heatmap page by loading the Rendering/Templates/
/// heatmap.html template and substituting its {{TOKEN}} placeholders with the
/// server-computed heat data and control scripts. The page's actual markup/CSS/JS
/// structure lives in that .html file, not in this class -- this class is only
/// responsible for producing the dynamic pieces that get spliced into it.</summary>
public static class HeatmapHtmlBuilder
{
    public static string BuildHtml(
        string pointsByTypeJs,
        List<(string Type, string Color)> types,
        List<MapLocation> locations)
    {
        string bookmarkScript = BookmarkControlScript.Build(locations);
        string typeControlScript = ActivityTypeControlScript.Build(types);

        return HtmlTemplateLoader.Load("heatmap.html")
            .Replace("{{POINTS_BY_TYPE_JS}}", pointsByTypeJs)
            .Replace("{{BOOKMARK_CONTROL_SCRIPT}}", bookmarkScript)
            .Replace("{{ACTIVITY_TYPE_CONTROL_SCRIPT}}", typeControlScript);
    }
}