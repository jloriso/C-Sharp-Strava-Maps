using Web.Rendering;

namespace Web.App.Features.Activities.ActivityFeed
{
    /// <summary>Builds the Activities feed page by loading the built activities.html
    /// (from activities.html + activities.ts) and injecting the feed settings. The
    /// activities themselves are fetched page by page from <paramref name="pageUrls"/>.
    /// Keep the property names in sync with ActivityFeedPageData in activities.ts.</summary>
    public static class ActivityFeedHtmlBuilder
    {
        public static string BuildHtml(int totalActivities, IEnumerable<string> pageUrls, string? cartoApiKey)
        {
            return new PageDataBuilder()
                .Add("totalActivities", totalActivities)
                .Add("pageUrls", pageUrls)
                .Add("tileUrlQuery", string.IsNullOrWhiteSpace(cartoApiKey) ? "" : "?key=" + Uri.EscapeDataString(cartoApiKey))
                .ApplyTo(HtmlTemplateLoader.Load("Features/Activities/ActivityFeed", "activities.html"));
        }
    }
}
