using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>Builds the JS for a Leaflet control that lets the user jump the map to
/// one of a fixed set of named locations (each with its own lat/lon/zoom). Used by
/// both the heatmap and line map pages, since the "jump to" behavior itself doesn't
/// depend on what's actually drawn on the map.</summary>
public static class BookmarkControlScript
{
    public static string Build(IEnumerable<MapLocation> locations, string defaultSelection = "Chicago")
    {
        var options = new StringBuilder();
        var lookup = new StringBuilder("{");
        bool first = true;
        foreach (var loc in locations)
        {
            string selectedAttr = loc.Name == defaultSelection ? " selected" : "";
            options.Append("<option value=\"").Append(Esc(loc.Name)).Append('"').Append(selectedAttr).Append('>')
                   .Append(Esc(loc.Name)).Append("</option>");

            if (!first) lookup.Append(",");
            first = false;
            lookup.Append('"').Append(Esc(loc.Name)).Append("\":[")
                  .Append(Fmt(loc.Lat)).Append(',').Append(Fmt(loc.Lon)).Append(',').Append(loc.Zoom).Append(']');
        }
        lookup.Append("}");

        return @"
  var bookmarkLocations = " + lookup + @";
  var bookmarkControl = L.control({ position: 'topright' });
  bookmarkControl.onAdd = function () {
    var div = L.DomUtil.create('div', 'legend bookmark-control');
    div.innerHTML = '<strong>Jump to</strong><br>' +
      '<select id=""bookmark-select"">" + options + @"</select>';
    L.DomEvent.disableClickPropagation(div);
    return div;
  };
  bookmarkControl.addTo(map);
  document.getElementById('bookmark-select').addEventListener('change', function (e) {
    var loc = bookmarkLocations[e.target.value];
    if (loc) map.setView([loc[0], loc[1]], loc[2]);
  });
";
    }

    static string Fmt(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
