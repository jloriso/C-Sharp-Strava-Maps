using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Aggregates raw track points into weighted heatmap points, one bucket per
/// activity type -- the same idea as the reference Python heatmapController's
/// Counter/defaultdict(Counter) approach: round every point to a fixed decimal
/// precision and count how many times each rounded coordinate occurs, so spots
/// you've passed through repeatedly naturally end up "hotter". Keeping counts
/// per type (rather than one combined bucket) is what lets the page's
/// activity-type checkboxes turn each type's heat contribution on/off
/// independently.
/// </summary>
public static class HeatmapDataBuilder
{
    public class Result
    {
        /// <summary>JS object literal: {"Run": [[lat,lon,weight], ...], "Ride": [...]}.</summary>
        public string ByTypeJs = "{}";
    }

    public static Result Build(IEnumerable<ActivityTrack> tracks, int precision = 6)
    {
        var byType = new Dictionary<string, Dictionary<(double Lat, double Lon), int>>();

        foreach (var track in tracks)
        {
            if (!byType.TryGetValue(track.Type, out var counts))
            {
                counts = new Dictionary<(double, double), int>();
                byType[track.Type] = counts;
            }
            foreach (var (lat, lon) in track.AllPoints)
            {
                var key = (Math.Round(lat, precision), Math.Round(lon, precision));
                counts[key] = counts.TryGetValue(key, out var c) ? c + 1 : 1;
            }
        }

        return new Result { ByTypeJs = ToByTypeJs(byType) };
    }

    static string ToByTypeJs(Dictionary<string, Dictionary<(double Lat, double Lon), int>> byType)
    {
        var sb = new StringBuilder("{");
        bool firstType = true;
        foreach (var kv in byType)
        {
            if (!firstType) sb.Append(",");
            firstType = false;
            sb.Append(JsonStr(kv.Key)).Append(":").Append(ToPointArrayJs(kv.Value));
        }
        sb.Append("}");
        return sb.ToString();
    }

    static string ToPointArrayJs(Dictionary<(double Lat, double Lon), int> counts)
    {
        var sb = new StringBuilder("[");
        bool first = true;
        foreach (var kv in counts)
        {
            if (!first) sb.Append(",");
            first = false;
            sb.Append('[').Append(Fmt(kv.Key.Lat)).Append(',').Append(Fmt(kv.Key.Lon)).Append(',').Append(kv.Value).Append(']');
        }
        sb.Append("]");
        return sb.ToString();
    }

    static string Fmt(double d) => d.ToString("F6", CultureInfo.InvariantCulture);
    static string JsonStr(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
