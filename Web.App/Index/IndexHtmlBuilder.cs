using GpxWorldMap.Rendering;

namespace GpxWorldMap.Web.Index
{
    /// <summary>Builds the root shell page (nav bar + content iframe). Mirrors
    /// IndexPageData in index.ts.</summary>
    public static class IndexHtmlBuilder
    {
        public static string BuildHtml(
            IEnumerable<(string Key, string Label, string Href)> links,
            string initialPage)
        {
            return new PageDataBuilder()
                .Add("links", links.Select(link => new { link.Key, link.Label, link.Href }))
                .Add("initialPage", initialPage)
                .ApplyTo(HtmlTemplateLoader.Load("Index", "index.html"));
        }
    }
}