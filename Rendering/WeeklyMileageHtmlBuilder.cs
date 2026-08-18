using System.Collections.Generic;
using System.Text;

/// <summary>Builds the weekly mileage chart page -- served as the site's home page
/// (see LocalWebServer.Serve) -- by loading the Rendering/Templates/
/// weeklymileage.html template and substituting its {{TOKEN}} placeholders with
/// the server-computed weekly series, per-category checkbox markup, and shared
/// nav bar (linking to the Heatmap and Line Map pages).
///
/// Unlike the heatmap/line map pages, this page has no Leaflet map underneath it,
/// so its activity-category checkboxes are built directly here as plain HTML
/// rather than reusing Controls/ActivityTypeControlScript -- that control
/// specifically builds a Leaflet map control (it calls L.control(...).addTo(map)),
/// which doesn't apply to a page with no map object.</summary>
public static class WeeklyMileageHtmlBuilder
{
    public static string BuildHtml(
        string weeksJs,
        string seriesByTypeJs,
        List<(string Type, string Color)> categories,
        string heatmapHref,
        string lineMapHref,
        string weeklyMileageHref)
    {
        string typeColorsJs = BuildTypeColorsJs(categories);
        string checkboxesHtml = BuildCheckboxesHtml(categories);
        string navBarHtml = NavBarBuilder.BuildHtml("weeklymileage", weeklyMileageHref, heatmapHref, lineMapHref);

        return HtmlTemplateLoader.Load("Rendering/Templates", "weeklymileage.html")
            .Replace("{{WEEKS_JS}}", weeksJs)
            .Replace("{{SERIES_BY_TYPE_JS}}", seriesByTypeJs)
            .Replace("{{TYPE_COLORS_JS}}", typeColorsJs)
            .Replace("{{ACTIVITY_TYPE_CHECKBOXES_HTML}}", checkboxesHtml)
            .Replace("{{NAV_BAR_HTML}}", navBarHtml);
    }

    static string BuildCheckboxesHtml(List<(string Type, string Color)> categories)
    {
        var sb = new StringBuilder();
        foreach (var (type, color) in categories)
        {
            sb.Append("<label><input type=\"checkbox\" class=\"activity-type-toggle\" data-type=\"")
              .Append(Esc(type)).Append("\" checked><span class=\"swatch\" style=\"background:")
              .Append(color).Append(";\"></span>").Append(Esc(type)).Append("</label>\n");
        }
        return sb.ToString();
    }

    static string BuildTypeColorsJs(List<(string Type, string Color)> categories)
    {
        var sb = new StringBuilder("{");
        bool first = true;
        foreach (var (type, color) in categories)
        {
            if (!first) sb.Append(",");
            first = false;
            sb.Append('"').Append(type.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\":\"").Append(color).Append('"');
        }
        sb.Append("}");
        return sb.ToString();
    }

    static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
