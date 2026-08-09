using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;

/// <summary>
/// Reads every *.gpx.gz file in a folder, decompresses it, extracts tracks/routes/waypoints,
/// optionally joins each one against a Strava-style activities CSV (matched by Activity ID,
/// which is expected to be the GPX file's base name), and serves a single interactive Leaflet
/// map (OpenStreetMap tiles) of everything from a local web server.
///
/// Usage:
///   dotnet run -- <gpx-folder> [activities.csv] [output.html]
///
/// The CSV and output-html arguments are both optional and can be given in either order;
/// whichever argument ends in ".csv" is treated as the CSV file.
/// </summary>
class Program
{
    record ActivityRecord(string Id, string Date, string Name, string Type, string Description);

    class ActivityMeta
    {
        public string Name = "";
        public string Type = "";
        public string Date = "";
        public string Description = "";
        public string Color = "#3388ff";
    }

    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: dotnet run -- <gpx-folder> [activities.csv] [output.html]");
        }

        string gpxFolder = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        string? csvPath = null;
        string outputHtml = "map.html";

        foreach (var arg in args.Skip(1))
        {
            if (arg.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) && csvPath == null)
                csvPath = arg;
            else
                outputHtml = arg;
        }

        if (!Directory.Exists(gpxFolder))
        {
            Console.WriteLine($"Folder not found: {gpxFolder}");
            return 1;
        }

        Dictionary<string, ActivityRecord> activitiesById = new();
        if (csvPath != null)
        {
            if (!File.Exists(csvPath))
            {
                Console.WriteLine($"CSV file not found: {csvPath}");
                return 1;
            }
            var records = ParseActivitiesCsv(csvPath);
            foreach (var r in records)
            {
                if (!string.IsNullOrWhiteSpace(r.Id))
                    activitiesById[r.Id] = r; // last one wins if duplicate IDs
            }
            Console.WriteLine($"Loaded {activitiesById.Count} activities from {csvPath}");
        }

        var files = Directory.GetFiles(gpxFolder, "*.gpx.gz", SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
        {
            Console.WriteLine($"No .gpx.gz files found in {gpxFolder}");
            return 1;
        }
        Console.WriteLine($"Found {files.Length} .gpx.gz file(s) in {gpxFolder}");

        var features = new List<string>();
        var matchedIds = new HashSet<string>();
        int unmatchedGpxCount = 0;

        foreach (var file in files.OrderBy(f => f))
        {
            string name = Path.GetFileName(file);
            string id = StripGpxGz(name);

            var meta = new ActivityMeta { Name = id, Type = "Unknown", Date = "", Description = "" };
            if (activitiesById.TryGetValue(id, out var record))
            {
                meta.Name = string.IsNullOrWhiteSpace(record.Name) ? id : record.Name;
                meta.Type = string.IsNullOrWhiteSpace(record.Type) ? "Unknown" : record.Type;
                meta.Date = record.Date;
                meta.Description = record.Description;
                matchedIds.Add(id);
            }
            else if (csvPath != null)
            {
                unmatchedGpxCount++;
                Console.WriteLine($"  Note: no CSV row found for GPX file '{name}' (looked for Activity ID '{id}')");
            }
            meta.Color = ColorForType(meta.Type);

            try
            {
                string gpxXml = DecompressGz(file);
                var parsed = ParseGpx(gpxXml);

                int segCount = 0;
                foreach (var seg in parsed.Segments)
                {
                    if (seg.Count < 2) continue;
                    features.Add(BuildLineStringFeature(seg, meta));
                    segCount++;
                }
                foreach (var pt in parsed.Points)
                {
                    features.Add(BuildPointFeature(pt, meta));
                }

                Console.WriteLine($"  {name}: {segCount} track segment(s), {parsed.Points.Count} waypoint(s) [{meta.Type}]");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Skipping {name}: {ex.Message}");
            }
        }

        if (csvPath != null)
        {
            int csvOnlyCount = activitiesById.Count - matchedIds.Count;
            Console.WriteLine($"\n{matchedIds.Count} GPX file(s) matched a CSV row.");
            Console.WriteLine($"{csvOnlyCount} CSV row(s) have no corresponding GPX file (expected, per your note).");
            if (unmatchedGpxCount > 0)
                Console.WriteLine($"{unmatchedGpxCount} GPX file(s) had no matching CSV row (check that filenames are the Activity ID).");
        }

        if (features.Count == 0)
        {
            Console.WriteLine("No usable track points/waypoints found across all files.");
            return 1;
        }

        string geoJson = "{\"type\":\"FeatureCollection\",\"features\":[" + string.Join(",", features) + "]}";
        string html = BuildHtml(geoJson);
        File.WriteAllText(outputHtml, html, Encoding.UTF8);
        Console.WriteLine($"\nWrote {Path.GetFullPath(outputHtml)}");

        // Serve over http://localhost instead of file:// so tile requests carry a Referer
        // header -- OpenStreetMap's tile policy rejects requests with none.
        int port = GetFreePort();
        string url = $"http://localhost:{port}/";
        var htmlBytes = Encoding.UTF8.GetBytes(html);

        var listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();

        Console.WriteLine($"Serving the map at {url}");
        Console.WriteLine("Press Ctrl+C to stop the server when you're done viewing it.");
        TryOpenBrowser(url);

        while (listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = listener.GetContext(); }
            catch (HttpListenerException) { break; }

            using var response = ctx.Response;
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = htmlBytes.Length;
            response.OutputStream.Write(htmlBytes, 0, htmlBytes.Length);
        }

        return 0;
    }

    // ---------- CSV parsing ----------

    static List<ActivityRecord> ParseActivitiesCsv(string path)
    {
        var rows = ReadCsvRows(path);
        if (rows.Count == 0) return new();

        var header = rows[0].Select(h => h.Trim()).ToList();
        int idxId = FindColumn(header, "Activity ID");
        int idxDate = FindColumn(header, "Activity Date");
        int idxName = FindColumn(header, "Activity Name");
        int idxType = FindColumn(header, "Activity Type");
        int idxDesc = FindColumn(header, "Activity Description");

        if (idxId < 0)
        {
            Console.WriteLine("Warning: couldn't find an 'Activity ID' column in the CSV header.");
        }

        var result = new List<ActivityRecord>();
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            string Get(int idx) => idx >= 0 && idx < row.Count ? row[idx] : "";
            result.Add(new ActivityRecord(
                Get(idxId).Trim(),
                Get(idxDate).Trim(),
                Get(idxName).Trim(),
                Get(idxType).Trim(),
                Get(idxDesc).Trim()));
        }
        return result;
    }

    static int FindColumn(List<string> header, string name)
    {
        for (int i = 0; i < header.Count; i++)
            if (string.Equals(header[i], name, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    // Minimal RFC-4180-ish CSV reader: handles quoted fields containing commas,
    // escaped quotes (""), and embedded newlines.
    static List<List<string>> ReadCsvRows(string path)
    {
        var rows = new List<List<string>>();
        var field = new StringBuilder();
        var row = new List<string>();
        bool inQuotes = false;

        using var reader = new StreamReader(path, Encoding.UTF8);
        int ci;
        while ((ci = reader.Read()) != -1)
        {
            char c = (char)ci;
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (reader.Peek() == '"') { field.Append('"'); reader.Read(); }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (c == '\r') { /* skip, \n handles the line break */ }
                else if (c == '\n')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                }
                else field.Append(c);
            }
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }

    // ---------- GPX parsing ----------

    static string StripGpxGz(string fileName)
    {
        string noExt = Path.GetFileNameWithoutExtension(fileName); // "12345.gpx"
        return Path.GetFileNameWithoutExtension(noExt);             // "12345"
    }

    static string DecompressGz(string path)
    {
        using var fileStream = File.OpenRead(path);
        using var gzip = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    class ParsedGpx
    {
        public List<List<(double Lat, double Lon)>> Segments = new();
        public List<(double Lat, double Lon, string Name)> Points = new();
    }

    static ParsedGpx ParseGpx(string xml)
    {
        var doc = XDocument.Parse(xml);
        var result = new ParsedGpx();

        foreach (var seg in doc.Descendants().Where(e => e.Name.LocalName == "trkseg"))
        {
            var pts = seg.Descendants().Where(e => e.Name.LocalName == "trkpt")
                .Select(ReadPt).Where(p => p.HasValue).Select(p => p!.Value).ToList();
            if (pts.Count > 0) result.Segments.Add(pts);
        }

        foreach (var rte in doc.Descendants().Where(e => e.Name.LocalName == "rte"))
        {
            var pts = rte.Descendants().Where(e => e.Name.LocalName == "rtept")
                .Select(ReadPt).Where(p => p.HasValue).Select(p => p!.Value).ToList();
            if (pts.Count > 0) result.Segments.Add(pts);
        }

        foreach (var wpt in doc.Descendants().Where(e => e.Name.LocalName == "wpt"))
        {
            var p = ReadPt(wpt);
            if (p.HasValue)
            {
                var nameEl = wpt.Elements().FirstOrDefault(e => e.Name.LocalName == "name");
                result.Points.Add((p.Value.Lat, p.Value.Lon, nameEl?.Value ?? ""));
            }
        }

        return result;
    }

    static (double Lat, double Lon)? ReadPt(XElement el)
    {
        var latAttr = el.Attribute("lat");
        var lonAttr = el.Attribute("lon");
        if (latAttr == null || lonAttr == null) return null;
        if (double.TryParse(latAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
            double.TryParse(lonAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
            return (lat, lon);
        return null;
    }

    // ---------- Colors ----------

    static readonly Dictionary<string, string> KnownTypeColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Run"] = "#e6194b",
        ["TrailRun"] = "#f58231",
        ["Walk"] = "#3cb44b",
        ["Hike"] = "#469990",
        ["Ride"] = "#4363d8",
        ["VirtualRide"] = "#42d4f4",
        ["GravelRide"] = "#9a6324",
        ["MountainBikeRide"] = "#800000",
        ["Swim"] = "#911eb4",
        ["Workout"] = "#808000",
        ["WeightTraining"] = "#000075",
        ["Yoga"] = "#f032e6",
        ["AlpineSki"] = "#a9a9a9",
        ["Snowboard"] = "#000000",
        ["Kayaking"] = "#009999",
        ["Rowing"] = "#bcf60c",
        ["RockClimbing"] = "#800080",
    };

    static string ColorForType(string type)
    {
        if (KnownTypeColors.TryGetValue(type, out var c)) return c;
        // Deterministic fallback color for unrecognized types, so the same
        // type always gets the same color across runs.
        string[] palette =
        {
            "#e6194b", "#3cb44b", "#4363d8", "#f58231", "#911eb4",
            "#42d4f4", "#f032e6", "#469990", "#9a6324", "#800000",
            "#808000", "#000075", "#e6beff", "#ffd8b1"
        };
        int hash = 0;
        foreach (char ch in type) hash = hash * 31 + ch;
        int idx = Math.Abs(hash) % palette.Length;
        return palette[idx];
    }

    // ---------- GeoJSON / HTML building ----------

    static string BuildLineStringFeature(List<(double Lat, double Lon)> seg, ActivityMeta meta)
    {
        var coords = string.Join(",", seg.Select(p => $"[{Fmt(p.Lon)},{Fmt(p.Lat)}]"));
        return "{\"type\":\"Feature\",\"properties\":" + PropsJson(meta) +
               ",\"geometry\":{\"type\":\"LineString\",\"coordinates\":[" + coords + "]}}";
    }

    static string BuildPointFeature((double Lat, double Lon, string Name) pt, ActivityMeta meta)
    {
        var pointMeta = meta;
        if (!string.IsNullOrWhiteSpace(pt.Name))
        {
            pointMeta = new ActivityMeta
            {
                Name = pt.Name,
                Type = meta.Type,
                Date = meta.Date,
                Description = meta.Description,
                Color = meta.Color
            };
        }
        return "{\"type\":\"Feature\",\"properties\":" + PropsJson(pointMeta) +
               ",\"geometry\":{\"type\":\"Point\",\"coordinates\":[" + Fmt(pt.Lon) + "," + Fmt(pt.Lat) + "]}}";
    }

    static string PropsJson(ActivityMeta meta)
    {
        string popup = "<b>" + EscapeHtml(meta.Name) + "</b><br>" +
                        EscapeHtml(meta.Type) +
                        (string.IsNullOrWhiteSpace(meta.Date) ? "" : " &middot; " + EscapeHtml(meta.Date));
        if (!string.IsNullOrWhiteSpace(meta.Description))
            popup += "<br><br>" + EscapeHtml(meta.Description).Replace("\n", "<br>");

        return "{\"name\":" + JsonStr(meta.Name) +
               ",\"type\":" + JsonStr(meta.Type) +
               ",\"color\":" + JsonStr(meta.Color) +
               ",\"popup\":" + JsonStr(popup) + "}";
    }

    static string Fmt(double d) => d.ToString("R", CultureInfo.InvariantCulture);

    static string JsonStr(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    static string EscapeHtml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    static void TryOpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else
                Process.Start("xdg-open", url);
        }
        catch
        {
            Console.WriteLine($"Couldn't auto-open a browser. Open this URL manually: {url}");
        }
    }

    static string BuildHtml(string geoJson)
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
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; OpenStreetMap contributors'
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
