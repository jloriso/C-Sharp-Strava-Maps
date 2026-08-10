using System.Collections.Generic;
using System.Text;

/// <summary>Builds the standalone line map page: one colored, frequency-weighted
/// polyline layer per activity type (well-worn routes render bold, rare ones stay
/// thin but visible), each toggleable via the activity-type checkboxes, plus the
/// "jump to" location control. Defaults to a Chicago-centered view, matching the
/// reference Python linemapController.</summary>
public static class LineMapHtmlBuilder
{
    public static string BuildHtml(
        string lineDataByTypeJs,
        List<(string Type, string Color)> types,
        List<MapLocation> locations)
    {
        string bookmarkScript = BookmarkControlScript.Build(locations);
        string typeControlScript = ActivityTypeControlScript.Build(types);
        string typeColorsJs = BuildTypeColorsJs(types);

        return @"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8' />
<title>Route Line Map</title>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
<style>
  html, body, #map { height: 100%; margin: 0; }
  .legend { background: white; padding: 8px 12px; font: 13px sans-serif; line-height: 1.6; border-radius: 4px; box-shadow: 0 1px 4px rgba(0,0,0,0.3); }
  .legend .swatch { display: inline-block; width: 10px; height: 10px; margin-right: 6px; border-radius: 2px; }
  .bookmark-control select { font: 13px sans-serif; }
</style>
</head>
<body>
<div id='map'></div>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<script>
  // One GeoJSON FeatureCollection of frequency-scored route edges per activity
  // type -- each edge already carries its own weight/opacity, computed server-side
  // from how many distinct activities of that type crossed it.
  var lineDataByType = " + lineDataByTypeJs + @";
  var typeColors = " + typeColorsJs + @";

  var map = L.map('map', { preferCanvas: true }).setView([42.0707, -87.7368], 10);

  L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
    maxZoom: 19, subdomains: 'abcd', detectRetina: true,
    attribution: '&copy; OpenStreetMap contributors &copy; CARTO'
  }).addTo(map);

  // One layer per activity type, all shown by default; the checkboxes below
  // toggle each one independently.
  var lineLayersByType = {};
  Object.keys(lineDataByType).forEach(function (type) {
    var color = typeColors[type] || '#000000';
    var layer = L.geoJSON(lineDataByType[type], {
      style: function (feature) {
        return {
          color: color,
          weight: feature.properties.weight,
          opacity: feature.properties.opacity,
          lineCap: 'round'
        };
      },
      onEachFeature: function (feature, lyr) {
        var times = feature.properties.count === 1 ? 'once' : (feature.properties.count + ' times');
        lyr.bindPopup(type + ' &middot; traveled ' + times);
      }
    }).addTo(map);
    lineLayersByType[type] = layer;
  });

  window.onActivityTypeToggle = function (type, checked) {
    var layer = lineLayersByType[type];
    if (!layer) return;
    if (checked) map.addLayer(layer); else map.removeLayer(layer);
  };
" + bookmarkScript + typeControlScript + @"
</script>
</body>
</html>";
    }

    static string BuildTypeColorsJs(List<(string Type, string Color)> types)
    {
        var sb = new StringBuilder("{");
        bool first = true;
        foreach (var (type, color) in types)
        {
            if (!first) sb.Append(",");
            first = false;
            sb.Append('"').Append(type.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\":\"").Append(color).Append('"');
        }
        sb.Append("}");
        return sb.ToString();
    }
}
