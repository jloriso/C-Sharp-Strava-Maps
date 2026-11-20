using GpxWorldMap.Rendering;

namespace GpxWorldMap.Web.Features.Charts.WeeklyMileage
{
    /// <summary>Builds the weekly mileage chart page by loading the built
    /// weeklymileage.html (from weeklymileage.html + weeklymileage.ts) and injecting the
    /// server-computed weekly series. Keep the property names in sync with the
    /// WeeklyMileagePageData interface in weeklymileage.ts.</summary>
    public static class WeeklyMileageHtmlBuilder
    {
        public static string BuildHtml(
            string weeksJs,
            string seriesByTypeJs,
            List<(string Type, string Color)> categories)
        {
            return new PageDataBuilder()
                .AddRaw("weeks", weeksJs)
                .AddRaw("seriesByType", seriesByTypeJs)
                .Add("activityTypes", categories.Select(c => new { c.Type, c.Color }))
                .ApplyTo(HtmlTemplateLoader.Load("Features/Charts/WeeklyMileage", "weeklymileage.html"));
        }
    }
}