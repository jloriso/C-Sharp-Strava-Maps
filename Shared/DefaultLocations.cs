using System.Collections.Generic;

/// <summary>The fixed set of locations offered by the "jump to" control on both the
/// heatmap and line map pages.</summary>
public static class DefaultLocations
{
    public static List<MapLocation> Standard() => new()
    {
        new MapLocation { Name = "Chicago", Lat = 42.0707, Lon = -87.7368, Zoom = 10 },
        new MapLocation { Name = "Kalamazoo", Lat = 42.2, Lon = -85.6, Zoom = 11 },
        new MapLocation { Name = "USA", Lat = 38.0, Lon = -94.8, Zoom = 5 },
        new MapLocation { Name = "World", Lat = 20.0, Lon = 0.0, Zoom = 3 },
    };
}
