using System.Text.Json;
using GpxWorldMap.Rendering;

namespace GpxWorldMap.Web.Features.Controls
{
    public static class AppNavigationControlScript
    {
        public static string Build(IEnumerable<(string Key, string Label, string Href)> links, string initialPage)
        {
            var linksJs = JsonSerializer.Serialize(links.Select(link => new
            {
                key = link.Key,
                label = link.Label,
                href = link.Href
            }));

            return HtmlTemplateLoader.Load("Controls/Templates", "AppNavigationControl.js")
                .Replace("{{NAVIGATION_LINKS_JS}}", linksJs)
                .Replace("{{INITIAL_PAGE_JS}}", JsonSerializer.Serialize(initialPage));
        }
    }
}