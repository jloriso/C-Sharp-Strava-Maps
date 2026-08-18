using System;
using System.Collections.Generic;

/// <summary>
/// Collapses the many raw Strava/Garmin activity type strings (Run, TrailRun,
/// Ride, VirtualRide, GravelRide, MountainBikeRide, Swim, Hike, Yoga, ...) down to
/// three broad categories -- Run, Bike, Other -- used by the weekly mileage chart.
/// The heatmap and line map still use the full per-type granularity via
/// ActivityColors; this simplification is specific to the mileage chart, where
/// three fixed, always-present categories make for a clean, predictable set of
/// checkboxes/stacked-bar segments regardless of how many distinct raw types show
/// up in any given dataset.
/// </summary>
public static class ActivityCategory
{
    /// <summary>Fixed display/processing order for the mileage chart -- always
    /// exactly these three categories, in this order, regardless of what's
    /// actually present in the data.</summary>
    public static readonly string[] OrderedCategories = { "Run", "Bike", "Other" };

    static readonly HashSet<string> RunTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Run", "TrailRun", "VirtualRun",
    };

    static readonly HashSet<string> BikeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ride", "VirtualRide", "GravelRide", "MountainBikeRide", "EBikeRide", "Velomobile", "Handcycle",
    };

    public static string CategoryForType(string rawType)
    {
        if (RunTypes.Contains(rawType)) return "Run";
        if (BikeTypes.Contains(rawType)) return "Bike";
        return "Other";
    }

    public static string ColorForCategory(string category) => category switch
    {
        "Run" => "#fc4c02",
        "Bike" => "#00b0ff",
        _ => "#8a8a8a",
    };
}