using System.Collections.Generic;

/// <summary>One row from the activities CSV (only the columns we actually use).</summary>
public record ActivityRecord(
    string Id,
    string Date,
    string Name,
    string Type,
    string Description,
    string Filename);

/// <summary>
/// The metadata attached to a single plotted track/waypoint -- either taken from a
/// matched CSV row, or a fallback ("Unknown" type, filename as name) when there's
/// no matching row.
/// </summary>
public class ActivityMeta
{
    public string Name = "";
    public string Type = "";
    public string Date = "";
    public string Description = "";
    public string Color = "#3388ff";
}

/// <summary>Track segments and standalone waypoints extracted from one activity
/// file, regardless of whether it came from a GPX, TCX, or FIT source.</summary>
public class ParsedGpx
{
    public List<List<(double Lat, double Lon)>> Segments = new();
    public List<(double Lat, double Lon, string Name)> Points = new();
}

