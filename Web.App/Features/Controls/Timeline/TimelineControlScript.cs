using GpxWorldMap.Rendering;

namespace GpxWorldMap.Web.Features.Controls
{
    public static class TimelineControlScript
    {
        public static string Build() => HtmlTemplateLoader.Load("Features/Controls/Timeline", "TimelineControl.js");
    }
}