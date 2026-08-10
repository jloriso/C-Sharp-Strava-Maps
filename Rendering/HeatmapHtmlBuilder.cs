using System.Collections.Generic;

/// <summary>Builds the standalone heatmap page: one Leaflet.heat layer per activity
/// type, each toggleable via the activity-type checkboxes, plus the "jump to"
/// location control. Defaults to a Chicago-centered view, matching the reference
/// Python heatmapController.</summary>
public static class HeatmapHtmlBuilder
{
    public static string BuildHtml(
        string pointsByTypeJs,
        List<(string Type, string Color)> types,
        List<MapLocation> locations)
    {
        string bookmarkScript = BookmarkControlScript.Build(locations);
        string typeControlScript = ActivityTypeControlScript.Build(types);

        return @"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8' />
<title>Activity Heatmap</title>
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
<script src='https://unpkg.com/leaflet.heat@0.2.0/dist/leaflet-heat.js'></script>
<script>
  // Weighted [lat, lon, weight] points per activity type -- built server-side by
  // rounding every recorded GPS point to a fixed precision and counting repeats,
  // so frequently-visited spots show up hotter.
  var pointsByType = " + pointsByTypeJs + @";

  var map = L.map('map').setView([42.0707, -87.7368], 10);

  L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
    maxZoom: 19, subdomains: 'abcd', detectRetina: true,
    attribution: '&copy; OpenStreetMap contributors &copy; CARTO'
  }).addTo(map);

  var heatOptions = {
    radius: 8, blur: 10, maxZoom: 15, minOpacity: 0.3,
    gradient: { 0.2: 'blue', 0.4: 'lime', 0.6: 'orange', 0.85: 'red' }
  };

  // One heat layer per activity type, all shown by default; the checkboxes below
  // toggle each one independently.
  var heatLayersByType = {};
  Object.keys(pointsByType).forEach(function (type) {
    heatLayersByType[type] = L.heatLayer(pointsByType[type], heatOptions).addTo(map);
  });

  window.onActivityTypeToggle = function (type, checked) {
    var layer = heatLayersByType[type];
    if (!layer) return;
    if (checked) map.addLayer(layer); else map.removeLayer(layer);
  };
" + bookmarkScript + typeControlScript + @"
</script>
</body>
</html>";
    }
}
