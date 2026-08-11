using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>Builds the JS for a Leaflet control that lets the user jump the map to
/// one of a fixed set of named locations (each with its own lat/lon/zoom), using one
/// button per location rather than a dropdown -- every option is visible and
/// reachable in a single click. Used by both the heatmap and line map pages, since
/// the "jump to" behavior itself doesn't depend on what's actually drawn on the
/// map.
///
/// The control's actual JS/markup lives in Controls/Templates/bookmarkControl.js
/// (loaded via HtmlTemplateLoader), not as a C# verbatim string in this file -- this
/// class is only responsible for producing the two dynamic pieces (the location
/// lookup table and the button markup) that get spliced into it.</summary>
public static class BookmarkControlScript
{
    public static string Build(IEnumerable<MapLocation> locations, string defaultSelection = "Chicago")
    {
        var buttons = new StringBuilder();
        var lookup = new StringBuilder("{");
        bool first = true;
        foreach (var loc in locations)
        {
            string activeClass = loc.Name == defaultSelection ? " active" : "";
            buttons.Append("<button type=\"button\" class=\"bookmark-btn").Append(activeClass)
                   .Append("\" data-location=\"").Append(Esc(loc.Name)).Append("\">")
                   .Append(Esc(loc.Name)).Append("</button>");

            if (!first) lookup.Append(",");
            first = false;
            lookup.Append('"').Append(Esc(loc.Name)).Append("\":[")
                  .Append(Fmt(loc.Lat)).Append(',').Append(Fmt(loc.Lon)).Append(',').Append(loc.Zoom).Append(']');
        }
        lookup.Append("}");

        return HtmlTemplateLoader.Load("Controls/Templates", "bookmarkControl.js")
            .Replace("{{BOOKMARK_LOCATIONS_JS}}", lookup.ToString())
            .Replace("{{BOOKMARK_BUTTONS_HTML}}", buttons.ToString());
    }

    static string Fmt(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
