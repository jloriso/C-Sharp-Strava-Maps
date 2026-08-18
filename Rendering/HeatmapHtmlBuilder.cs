using System.Collections.Generic;

/// <summary>Builds the standalone heatmap page by loading the Rendering/Templates/
/// heatmap.html template and substituting its {{TOKEN}} placeholders with the
/// server-computed heat data, control scripts, and shared nav bar (linking to the
/// Weekly Mileage home page and the Line Map page).</summary>
public static class HeatmapHtmlBuilder
{
    public static string BuildHtml(
        string pointsByTypeJs,
        List<(string Type, string Color)> types,
        List<MapLocation> locations,
        string heatmapHref,
        string lineMapHref,
        string weeklyMileageHref)
    {
        string bookmarkScript = BookmarkControlScript.Build(locations);
        string typeControlScript = ActivityTypeControlScript.Build(types);
        string homeControlScript = HomeControlScript.Build("/");
        var initial = locations.FirstOrDefault() ?? new MapLocation { Lat = 20.0, Lon = 0.0, Zoom = 3 };
        string initialViewJs = $"[{(initial.Lat)}, {(initial.Lon)}], {initial.Zoom}";
        string navBarHtml = NavBarBuilder.BuildHtml("heatmap", weeklyMileageHref, heatmapHref, lineMapHref);

        return HtmlTemplateLoader.Load("Rendering/Templates","heatmap.html")
            .Replace("{{POINTS_BY_TYPE_JS}}", pointsByTypeJs)
            .Replace("{{BOOKMARK_CONTROL_SCRIPT}}", bookmarkScript)
            .Replace("{{ACTIVITY_TYPE_CONTROL_SCRIPT}}", typeControlScript)
            .Replace("{{HOME_CONTROL_SCRIPT}}", homeControlScript)
            .Replace("{{INITIAL_VIEW_JS}}", initialViewJs)
            .Replace("{{NAV_BAR_HTML}}", navBarHtml);
    }
}