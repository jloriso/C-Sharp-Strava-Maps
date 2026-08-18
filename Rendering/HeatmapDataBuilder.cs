using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Aggregates raw track points into weighted heatmap points, one bucket per
/// category (Run/Bike/Other). Points are rounded to a fixed precision and counted;
/// then each category's counts are transformed into normalized heat weights using
/// percentile clipping + log scaling for better visual contrast.
/// </summary>
public static class HeatmapDataBuilder
{
    public class Result
    {
        /// <summary>JS object literal: {"Run": [[lat,lon,weight], ...], "Ride": [...]}.</summary>
        public string ByTypeJs = "{}";
    }

    /// <param name="precision">
    /// Decimal places for rounding coordinates before counting repeats.
    /// </param>
    /// <param name="clampPercentile">
    /// Per-type percentile used to cap very high counts before normalization.
    /// Typical values: 90-98. Lower = stronger contrast.
    /// </param>
    public static Result Build(
        IEnumerable<ActivityTrack> tracks,
        int precision = 6,
        double clampPercentile = 95.0)
    {
        var byType = new Dictionary<string, Dictionary<(double Lat, double Lon), int>>();

        foreach (var track in tracks)
        {
            // Collapse raw activity types into fixed map categories.
            var category = ActivityCategory.CategoryForType(track.Type);
            if (!byType.TryGetValue(category, out var counts))
            {
                counts = new Dictionary<(double, double), int>();
                byType[category] = counts;
            }

            foreach (var (lat, lon) in track.AllPoints)
            {
                var key = (Math.Round(lat, precision), Math.Round(lon, precision));
                counts[key] = counts.TryGetValue(key, out var c) ? c + 1 : 1;
            }
        }

        return new Result { ByTypeJs = ToByTypeJs(byType, clampPercentile) };
    }

    static string ToByTypeJs(
        Dictionary<string, Dictionary<(double Lat, double Lon), int>> byType,
        double clampPercentile)
    {
        var sb = new StringBuilder("{");
        bool firstType = true;

        foreach (var kv in byType)
        {
            if (!firstType) sb.Append(",");
            firstType = false;

            sb.Append(JsonStr(kv.Key))
              .Append(":")
              .Append(ToPointArrayJs(kv.Value, clampPercentile));
        }

        sb.Append("}");
        return sb.ToString();
    }

    static string ToPointArrayJs(
        Dictionary<(double Lat, double Lon), int> counts,
        double clampPercentile)
    {
        if (counts.Count == 0) return "[]";

        double clampMax = Percentile(counts.Values, clampPercentile);
        if (clampMax < 1.0) clampMax = 1.0;

        double denom = Math.Log(1.0 + clampMax);
        if (denom <= 0.0) denom = 1.0;

        var sb = new StringBuilder("[");
        bool first = true;

        foreach (var kv in counts)
        {
            if (!first) sb.Append(",");
            first = false;

            double clipped = Math.Min(kv.Value, clampMax);
            double weight = Math.Log(1.0 + clipped) / denom;

            // Keep tiny values visible and avoid exact zero.
            if (weight < 0.05) weight = 0.05;

            sb.Append('[')
              .Append(Fmt(kv.Key.Lat)).Append(',')
              .Append(Fmt(kv.Key.Lon)).Append(',')
              .Append(Fmt(weight))
              .Append(']');
        }

        sb.Append("]");
        return sb.ToString();
    }

    static double Percentile(IEnumerable<int> values, double percentile)
    {
        var arr = values.OrderBy(v => v).ToArray();
        if (arr.Length == 0) return 1.0;

        percentile = Math.Clamp(percentile, 0.0, 100.0);

        if (arr.Length == 1) return arr[0];

        double rank = (percentile / 100.0) * (arr.Length - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);

        if (lo == hi) return arr[lo];

        double t = rank - lo;
        return arr[lo] + (arr[hi] - arr[lo]) * t;
    }

    static string Fmt(double d) => d.ToString("F6", CultureInfo.InvariantCulture);
    static string JsonStr(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}