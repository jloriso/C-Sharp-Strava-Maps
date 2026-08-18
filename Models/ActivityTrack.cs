using System;
using System.Collections.Generic;

/// <summary>One activity's parsed geometry plus the metadata (type/color/date/
/// distance) needed by the heatmap, line map, and weekly mileage chart. Built
/// once per activity in Program.cs and then shared by all three outputs, so
/// parsing only happens once.</summary>
public class ActivityTrack
{
    public string ActivityId = "";
    public string Type = "";
    public string Color = "#3388ff";
    public List<List<(double Lat, double Lon)>> Segments = new();

    /// <summary>Every point across all segments and waypoints, flattened -- used by
    /// the heatmap, which doesn't care about segment boundaries.</summary>
    public List<(double Lat, double Lon)> AllPoints = new();

    /// <summary>Parsed from the CSV's Activity Date column, if present and
    /// parseable. Null when no CSV was provided, the row had no date, or the date
    /// string couldn't be parsed -- WeeklyMileageDataBuilder excludes such
    /// activities from the mileage chart since they can't be placed on a
    /// timeline.</summary>
    public DateTime? ActivityDate;

    /// <summary>Total great-circle distance of this activity's tracks, in meters
    /// (see Shared/GeoDistance.cs). Computed once in Program.cs after parsing, so
    /// the weekly mileage chart doesn't need to re-walk every segment itself.</summary>
    public double DistanceMeters;
}