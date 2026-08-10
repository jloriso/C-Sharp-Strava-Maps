using System.Collections.Generic;

/// <summary>One activity's parsed geometry plus the metadata (type/color) needed to
/// group and color it for both the heatmap and the line map. Built once per activity
/// in Program.cs and then shared by both outputs, so parsing only happens once.</summary>
public class ActivityTrack
{
    public string ActivityId = "";
    public string Type = "";
    public string Color = "#3388ff";
    public List<List<(double Lat, double Lon)>> Segments = new();

    /// <summary>Every point across all segments and waypoints, flattened -- used by
    /// the heatmap, which doesn't care about segment boundaries.</summary>
    public List<(double Lat, double Lon)> AllPoints = new();
}
