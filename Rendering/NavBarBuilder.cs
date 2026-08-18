using System.Text;

/// <summary>
/// Builds the small top nav bar shared by all three generated pages (Weekly
/// Mileage / Heatmap / Line Map), so each page can link to the other two.
///
/// Hrefs are passed in rather than hardcoded, since your configured output
/// filenames (AppConfig.OutputHeatmapHtml, OutputLineMapHtml,
/// OutputWeeklyMileageHtml) can be anything -- hardcoding "heatmap.html" here
/// would silently break navigation the moment you rename an output file in your
/// config. Program.cs computes each href once (Path.GetFileName of the
/// configured output path, or "/" for the Weekly Mileage page since it's served
/// as the site's home page -- see LocalWebServer.Serve) and passes them into
/// whichever two of the three page builders need them.
/// </summary>
public static class NavBarBuilder
{
    public static string BuildHtml(string activePage, string weeklyMileageHref, string heatmapHref, string lineMapHref)
    {
        var sb = new StringBuilder("<nav class=\"app-nav\">");
        AppendLink(sb, "Weekly Mileage", weeklyMileageHref, activePage == "weeklymileage");
        AppendLink(sb, "Heatmap", heatmapHref, activePage == "heatmap");
        AppendLink(sb, "Line Map", lineMapHref, activePage == "linemap");
        sb.Append("</nav>");
        return sb.ToString();
    }

    static void AppendLink(StringBuilder sb, string label, string href, bool active)
    {
        sb.Append("<a class=\"nav-link").Append(active ? " active" : "").Append("\" href=\"").Append(Esc(href)).Append("\">")
            .Append(Esc(label)).Append("</a>");
    }

    static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}