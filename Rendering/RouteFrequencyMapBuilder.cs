using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Builds the "well-worn route" line data for the line map, one FeatureCollection
/// per activity type. This is a C# port of the reference Python linemapController's
/// grid-snap + percentile-clip approach: every activity's points are snapped to a
/// small grid (collapsing consecutive duplicate cells, which both smooths GPS
/// jitter and identifies shared road/trail segments across activities of the same
/// type), then each grid edge is scored by how many distinct activities of that
/// type crossed it. Weight and opacity scale with that score -- clipped at a
/// percentile so one mega-common commute doesn't wash out the rest of the scale --
/// so a well-worn route renders bold and a rarely-traveled one stays thin but
/// still visible.
/// </summary>
public static class RouteFrequencyMapBuilder
{
    const double GridSizeMeters = 15.0;
    const double ClipPercentile = 0.9;
    const double MinWeight = 1.2, MaxWeight = 3.5;
    const double MinOpacity = 0.35, MaxOpacity = 0.95;

    public class Result
    {
        /// <summary>JS object literal: {"Run": {FeatureCollection...}, "Ride": {...}}.</summary>
        public string ByTypeJs = "{}";
    }

    public static Result Build(IEnumerable<ActivityTrack> tracks)
    {
        var sb = new StringBuilder("{");
        bool firstType = true;
        foreach (var group in tracks.GroupBy(t => t.Type))
        {
            if (!firstType) sb.Append(",");
            firstType = false;
            sb.Append(JsonStr(group.Key)).Append(":").Append(BuildTypeFeatureCollection(group.ToList()));
        }
        sb.Append("}");
        return new Result { ByTypeJs = sb.ToString() };
    }

    static string BuildTypeFeatureCollection(List<ActivityTrack> tracksOfType)
    {
        var allPoints = tracksOfType.SelectMany(t => t.AllPoints).ToList();
        if (allPoints.Count == 0) return "{\"type\":\"FeatureCollection\",\"features\":[]}";

        double refLat = allPoints.Average(p => p.Lat);
        var (latStep, lonStep) = GridSteps(refLat);

        // edge key -> (endpoints, set of distinct activity ids that crossed it)
        var edges = new Dictionary<(long, long, long, long), (double Lat1, double Lon1, double Lat2, double Lon2, HashSet<string> ActivityIds)>();

        foreach (var track in tracksOfType)
        {
            var seenEdgesThisActivity = new HashSet<(long, long, long, long)>();
            foreach (var seg in track.Segments)
            {
                var cells = SnapAndCollapse(seg, latStep, lonStep);
                for (int i = 0; i + 1 < cells.Count; i++)
                {
                    var key = EdgeKey(cells[i], cells[i + 1]);
                    if (!seenEdgesThisActivity.Add(key)) continue; // out-and-back shouldn't double-count itself

                    if (!edges.TryGetValue(key, out var edge))
                    {
                        var (lat1, lon1) = Unsnap(cells[i], latStep, lonStep);
                        var (lat2, lon2) = Unsnap(cells[i + 1], latStep, lonStep);
                        edge = (lat1, lon1, lat2, lon2, new HashSet<string>());
                        edges[key] = edge;
                    }
                    edge.ActivityIds.Add(track.ActivityId);
                }
            }
        }

        if (edges.Count == 0) return "{\"type\":\"FeatureCollection\",\"features\":[]}";

        var counts = edges.Values.Select(e => e.ActivityIds.Count).OrderBy(c => c).ToList();
        int clipIdx = Math.Min((int)(counts.Count * ClipPercentile), counts.Count - 1);
        double clipValue = Math.Max(counts[clipIdx], 1);

        var features = new StringBuilder("[");
        bool first = true;
        foreach (var edge in edges.Values)
        {
            int count = edge.ActivityIds.Count;
            double normalized = Math.Min(count / clipValue, 1.0);
            double weight = MinWeight + normalized * (MaxWeight - MinWeight);
            double opacity = MinOpacity + normalized * (MaxOpacity - MinOpacity);

            if (!first) features.Append(",");
            first = false;
            features.Append("{\"type\":\"Feature\",\"properties\":{")
                     .Append("\"count\":").Append(count)
                     .Append(",\"weight\":").Append(weight.ToString("F2", CultureInfo.InvariantCulture))
                     .Append(",\"opacity\":").Append(opacity.ToString("F2", CultureInfo.InvariantCulture))
                     .Append("},\"geometry\":{\"type\":\"LineString\",\"coordinates\":[")
                     .Append('[').Append(Fmt(edge.Lon1)).Append(',').Append(Fmt(edge.Lat1)).Append("],")
                     .Append('[').Append(Fmt(edge.Lon2)).Append(',').Append(Fmt(edge.Lat2)).Append(']')
                     .Append("]}}");
        }
        features.Append("]");

        return "{\"type\":\"FeatureCollection\",\"features\":" + features + "}";
    }

    static (double LatStep, double LonStep) GridSteps(double refLat)
    {
        const double metersPerDegreeLat = 111_320.0;
        double metersPerDegreeLon = Math.Max(metersPerDegreeLat * Math.Cos(refLat * Math.PI / 180.0), 1.0);
        return (GridSizeMeters / metersPerDegreeLat, GridSizeMeters / metersPerDegreeLon);
    }

    /// <summary>Snap coordinates to grid cells, collapsing consecutive duplicates --
    /// this doubles as simplification for GPS jitter.</summary>
    static List<(long, long)> SnapAndCollapse(List<(double Lat, double Lon)> points, double latStep, double lonStep)
    {
        var cells = new List<(long, long)>();
        foreach (var p in points)
        {
            var cell = ToCell(p, latStep, lonStep);
            if (cells.Count == 0 || cells[^1] != cell) cells.Add(cell);
        }
        return cells;
    }

    static (long, long) ToCell((double Lat, double Lon) p, double latStep, double lonStep) =>
        ((long)Math.Round(p.Lat / latStep), (long)Math.Round(p.Lon / lonStep));

    static (double Lat, double Lon) Unsnap((long Lat, long Lon) cell, double latStep, double lonStep) =>
        (cell.Lat * latStep, cell.Lon * lonStep);

    /// <summary>An undirected edge key -- the same physical edge always produces the
    /// same key regardless of which direction it was travelled.</summary>
    static (long, long, long, long) EdgeKey((long Lat, long Lon) a, (long Lat, long Lon) b)
    {
        bool aFirst = a.Lat < b.Lat || (a.Lat == b.Lat && a.Lon <= b.Lon);
        return aFirst ? (a.Lat, a.Lon, b.Lat, b.Lon) : (b.Lat, b.Lon, a.Lat, a.Lon);
    }

    static string Fmt(double d) => d.ToString("F6", CultureInfo.InvariantCulture);
    static string JsonStr(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
