using GpxWorldMap.Rendering;
using GpxWorldMap.Web.Features.Controls;

namespace GpxWorldMap.Web.Index
{
    public static class IndexHtmlBuilder
    {
        public static string BuildHtml(
            IEnumerable<(string Key, string Label, string Href)> links,
            string initialPage)
        {
            string navigationScript = AppNavigationControlScript.Build(links, initialPage);
            return HtmlTemplateLoader.Load("Index", "index.html")
                .Replace("{{APP_NAVIGATION_CONTROL_SCRIPT}}", navigationScript);
        }
    }
}