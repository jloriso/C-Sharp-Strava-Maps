using GpxWorldMap.Models;
using GpxWorldMap.Rendering;
using GpxWorldMap.Web.Features.Maps.Shared;

namespace GpxWorldMap.Web.Features.Maps.Linemap
{
    /// <summary>Builds the standalone line map page by loading the built linemap.html
    /// (from linemap.html + linemap.ts) and injecting its settings, plus the separately
    /// served data file the page fetches after it renders. Keep the property names in
    /// sync with the LineMapPageData / LineMapData interfaces in linemap.ts.</summary>
    public static class LineMapHtmlBuilder
    {
        public static string BuildHtml(
            string dataUrl,
            List<(string Type, string Color)> types,
            List<MapLocation> locations,
            string? cartoApiKey,
            string activityDetailsUrl)
        {
            return new PageDataBuilder()
                .Add("dataUrl", dataUrl)
                .Add("activityDetailsUrl", activityDetailsUrl)
                .Add("settings", MapPageSettings.Create(types, locations, cartoApiKey))
                .ApplyTo(HtmlTemplateLoader.Load("Features/Maps/Linemap", "linemap.html"));
        }

        public static string BuildDataJson(string lineDataByTypeJs, string undatedActivitiesJs, string timelineActivitiesJs)
        {
            return new PageDataBuilder()
                .AddRaw("lineDataByType", lineDataByTypeJs)
                .AddRaw("undatedActivities", undatedActivitiesJs)
                .AddRaw("timelineActivities", timelineActivitiesJs)
                .ToJson();
        }
    }
}