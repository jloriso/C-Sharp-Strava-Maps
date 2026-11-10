using System.Text;
using GpxWorldMap.Rendering;

namespace GpxWorldMap.Web.Features.Charts.WeeklyMileage
{
    /// <summary>Builds the weekly mileage chart page -- served as the site's home page
    /// (see LocalWebServer.Serve) -- by loading the Features/WeeklyMileage/
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
            List<(string Type, string Color)> categories)
        {
            string typeColorsJs = BuildTypeColorsJs(categories);

            return HtmlTemplateLoader.Load("Features/WeeklyMileage", "weeklymileage.html")
                .Replace("{{WEEKS_JS}}", weeksJs)
                .Replace("{{SERIES_BY_TYPE_JS}}", seriesByTypeJs)
                .Replace("{{TYPE_COLORS_JS}}", typeColorsJs);
        }

        private static string BuildTypeColorsJs(List<(string Type, string Color)> categories)
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
    }
}