using System;
using System.Collections.Generic;

/// <summary>Great-circle distance calculations between GPS coordinates, used to
/// compute each activity's total mileage for the weekly mileage chart.</summary>
public static class GeoDistance
{
    const double EarthRadiusMeters = 6_371_000.0;
    const double MetersPerMile = 1609.344;

    /// <summary>Haversine distance between two lat/lon points, in meters. Accurate
    /// enough for activity-length totals (the ~0.5% error versus a full ellipsoidal
    /// model is far smaller than typical GPS positional error itself).</summary>
    public static double MetersBetween(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                   + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                                               * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    /// <summary>Total length, in meters, of a polyline -- the sum of the distances
    /// between each consecutive pair of points.</summary>
    public static double PolylineLengthMeters(List<(double Lat, double Lon)> points)
    {
        double total = 0;
        for (int i = 0; i + 1 < points.Count; i++)
            total += MetersBetween(points[i].Lat, points[i].Lon, points[i + 1].Lat, points[i + 1].Lon);
        return total;
    }

    public static double MetersToMiles(double meters) => meters / MetersPerMile;

    static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}