using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Aggregates activities into total mileage per week, broken out into three fixed
/// categories (Run, Bike, Other -- see ActivityCategory), for the weekly mileage
/// chart. Weeks run Monday-to-Sunday and are emitted as a single continuous range
/// from the earliest to the latest dated activity -- including weeks with zero
/// mileage -- so the client renders (and scrolls through) one unbroken timeline
/// instead of one with gaps wherever a week had no activity. That continuity is
/// also what makes "scroll back to the beginning" straightforward on the client:
/// it's just sliding the same fixed-length array window back to index 0, not
/// stitching together separate date ranges.
///
/// All three categories are always present in the output, even if one has zero
/// activities in this dataset -- this keeps the chart's checkboxes and stacked
/// segments stable and predictable rather than appearing/disappearing based on
/// what happens to be in the data.
///
/// Activities with no parseable date (no CSV provided, a blank Activity Date
/// column, or a date string that couldn't be parsed) can't be placed on the
/// timeline and are excluded -- SkippedForMissingDate reports how many.
/// </summary>
public static class WeeklyMileageDataBuilder
{
    public class Result
    {
        /// <summary>JS array literal of ISO week-start (Monday) date strings,
        /// e.g. ["2024-01-01","2024-01-08",...], one per week in the full range.</summary>
        public string WeeksJs = "[]";

        /// <summary>JS object literal: {"Run":[0,3.2,5.1,...],"Bike":[...],"Other":[...]},
        /// each array the same length as, and index-aligned with, WeeksJs's weeks.
        /// Always contains all three of ActivityCategory.OrderedCategories, in that
        /// order, even if a category has zero activities in this dataset.</summary>
        public string SeriesByTypeJs = "{}";

        public int SkippedForMissingDate;
    }

    public static Result Build(IEnumerable<ActivityTrack> tracks)
    {
        var dated = new List<ActivityTrack>();
        int skipped = 0;
        foreach (var t in tracks)
        {
            if (t.ActivityDate.HasValue) dated.Add(t);
            else skipped++;
        }

        if (dated.Count == 0)
            return new Result { SkippedForMissingDate = skipped };

        DateTime minWeek = StartOfWeek(dated.Min(t => t.ActivityDate!.Value));
        DateTime maxWeek = StartOfWeek(dated.Max(t => t.ActivityDate!.Value));

        var weeks = new List<DateTime>();
        for (var w = minWeek; w <= maxWeek; w = w.AddDays(7)) weeks.Add(w);

        var weekIndex = new Dictionary<DateTime, int>();
        for (int i = 0; i < weeks.Count; i++) weekIndex[weeks[i]] = i;

        // Pre-seed all three fixed categories (zero-filled) up front, in the fixed
        // display order, so they're always present -- and always in the same
        // order -- regardless of which raw activity types actually show up.
        var seriesByCategory = new Dictionary<string, double[]>();
        foreach (var category in ActivityCategory.OrderedCategories)
            seriesByCategory[category] = new double[weeks.Count];

        foreach (var track in dated)
        {
            string category = ActivityCategory.CategoryForType(track.Type);
            int idx = weekIndex[StartOfWeek(track.ActivityDate!.Value)];
            seriesByCategory[category][idx] += GeoDistance.MetersToMiles(track.DistanceMeters);
        }

        return new Result
        {
            WeeksJs = BuildWeeksJs(weeks),
            SeriesByTypeJs = BuildSeriesJs(seriesByCategory),
            SkippedForMissingDate = skipped
        };
    }

    /// <summary>Monday of the week containing <paramref name="date"/>, with time
    /// stripped.</summary>
    static DateTime StartOfWeek(DateTime date)
    {
        date = date.Date;
        int diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    static string BuildWeeksJs(List<DateTime> weeks)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < weeks.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append('"').Append(weeks[i].ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('"');
        }
        sb.Append("]");
        return sb.ToString();
    }

    static string BuildSeriesJs(Dictionary<string, double[]> seriesByCategory)
    {
        var sb = new StringBuilder("{");
        bool first = true;
        // Iterate in the fixed category order, not dictionary enumeration order,
        // so the JS object's keys (and therefore stacked-bar segment order) are
        // always Run, Bike, Other -- stable across every run.
        foreach (var category in ActivityCategory.OrderedCategories)
        {
            if (!first) sb.Append(",");
            first = false;
            var values = seriesByCategory[category];
            sb.Append(JsonStr(category)).Append(":[");
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(values[i].ToString("F2", CultureInfo.InvariantCulture));
            }
            sb.Append("]");
        }
        sb.Append("}");
        return sb.ToString();
    }

    static string JsonStr(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
