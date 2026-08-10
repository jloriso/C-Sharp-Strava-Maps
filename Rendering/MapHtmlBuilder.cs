/// <summary>Builds the Leaflet map page: base layers, GeoJSON overlay, popups,
/// and the activity-type legend.</summary>
public static class MapHtmlBuilder
{
    public static string BuildHtml(string geoJson)
    {
        return @"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8' />
<title>GPX World Map</title>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
<style>
  html, body, #map { height: 100%; margin: 0; }
  .legend { background: white; padding: 8px 12px; font: 13px sans-serif; line-height: 1.6; border-radius: 4px; box-shadow: 0 1px 4px rgba(0,0,0,0.3); }
  .legend .swatch { display: inline-block; width: 10px; height: 10px; margin-right: 6px; border-radius: 2px; }
</style>
</head>
<body>
<div id='map'></div>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<script>
  var data = " + geoJson + @";

  var map = L.map('map').setView([20, 0], 2);

  // Lighter, low-clutter basemaps by default so colored routes stand out.
  // (Served from the same local page, so tile requests still carry a referer.)
  var positron = L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
    maxZoom: 19, subdomains: 'abcd', detectRetina: true,
    attribution: '&copy; OpenStreetMap contributors &copy; CARTO'
  }).addTo(map);
  var voyager = L.tileLayer('https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png', {
    maxZoom: 19, subdomains: 'abcd', detectRetina: true,
    attribution: '&copy; OpenStreetMap contributors &copy; CARTO'
  });
  var darkMatter = L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
    maxZoom: 19, subdomains: 'abcd', detectRetina: true,
    attribution: '&copy; OpenStreetMap contributors &copy; CARTO'
  });
  var osmStreets = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19, attribution: '&copy; OpenStreetMap contributors'
  });

  L.control.layers({
    'Light (default)': positron,
    'Voyager': voyager,
    'Dark': darkMatter,
    'OSM Streets (detailed)': osmStreets
  }).addTo(map);

  var layer = L.geoJSON(data, {
    style: function (feature) {
      return { color: feature.properties.color, weight: 3, opacity: 0.85 };
    },
    pointToLayer: function (feature, latlng) {
      return L.circleMarker(latlng, {
        radius: 5,
        color: feature.properties.color,
        fillColor: feature.properties.color,
        fillOpacity: 0.9
      });
    },
    onEachFeature: function (feature, lyr) {
      if (feature.properties && feature.properties.popup) {
        lyr.bindPopup(feature.properties.popup);
      }
    }
  }).addTo(map);

  var bounds = layer.getBounds();
  if (bounds.isValid()) {
    map.fitBounds(bounds, { padding: [20, 20] });
  }

  var typeColors = {};
  data.features.forEach(function (f) {
    if (f.properties && f.properties.type) typeColors[f.properties.type] = f.properties.color;
  });
  if (Object.keys(typeColors).length > 0) {
    var legend = L.control({ position: 'bottomright' });
    legend.onAdd = function () {
      var div = L.DomUtil.create('div', 'legend');
      var html = '<strong>Activity Type</strong><br>';
      Object.keys(typeColors).sort().forEach(function (t) {
        html += '<span class=""swatch"" style=""background:' + typeColors[t] + ';""></span>' + t + '<br>';
      });
      div.innerHTML = html;
      return div;
    };
    legend.addTo(map);
  }
</script>
</body>
</html>";
    }
}
