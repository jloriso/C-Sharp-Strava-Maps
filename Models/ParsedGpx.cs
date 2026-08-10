using System.Collections.Generic;

/// <summary>Track segments and standalone waypoints extracted from one activity
/// file, regardless of whether it came from a GPX, TCX, or FIT source.</summary>
public class ParsedGpx
{
    public List<List<(double Lat, double Lon)>> Segments = new();
    public List<(double Lat, double Lon, string Name)> Points = new();
}
