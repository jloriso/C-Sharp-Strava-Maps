using System.Collections.Generic;
using System.Text;

/// <summary>
/// Builds the JS for a Leaflet control with one checkbox per activity type (colored
/// swatch + label) so the user can show/hide each type independently. The actual
/// show/hide behavior is supplied by the page itself via a
/// <c>window.onActivityTypeToggle(type, checked)</c> callback, since the heatmap
/// and line map pages toggle different kinds of layers (heat layers vs. polyline
/// layers) -- this class only owns the checkbox UI, not what happens when one is
/// clicked.
///
/// The control's actual JS/markup lives in Controls/Templates/activityTypeControl.js
/// (loaded via HtmlTemplateLoader), not as a C# verbatim string in this file -- this
/// class is only responsible for producing the one dynamic piece (the per-type
/// checkbox rows) that gets spliced into it.
/// </summary>
public static class ActivityTypeControlScript
{
    public static string Build(IEnumerable<(string Type, string Color)> types)
    {
        var rows = new StringBuilder();
        foreach (var (type, color) in types)
        {
            rows.Append("<label style=\"display:block;\">")
                .Append("<input type=\"checkbox\" class=\"activity-type-toggle\" data-type=\"").Append(Esc(type)).Append("\" checked>")
                .Append("<span class=\"swatch\" style=\"background:").Append(color).Append(";\"></span>")
                .Append(Esc(type))
                .Append("</label>");
        }

        return HtmlTemplateLoader.Load("Controls/Templates", "activityTypeControl.js")
            .Replace("{{ACTIVITY_TYPE_ROWS_HTML}}", rows.ToString());
    }

    static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}