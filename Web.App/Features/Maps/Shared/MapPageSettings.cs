using GpxWorldMap.Models;

namespace GpxWorldMap.Web.Features.Maps.Shared
{
    /// <summary>Settings shared by the heatmap and line map pages. Mirrors the
    /// MapPageSettings interface in baseMap.ts.</summary>
    public static class MapPageSettings
    {
        public static object Create(
            List<(string Type, string Color)> types,
            List<MapLocation> locations,
            string? cartoApiKey)
        {
            var initial = locations.FirstOrDefault() ?? new MapLocation { Name = "", Lat = 20.0, Lon = 0.0, Zoom = 3 };

            return new
            {
                ActivityTypes = types.Select(t => new { t.Type, t.Color }),
                Locations = locations.Select(ToJson),
                InitialView = ToJson(initial),
                TileUrlQuery = string.IsNullOrWhiteSpace(cartoApiKey) ? "" : "?key=" + Uri.EscapeDataString(cartoApiKey),
                HomeUrl = "/"
            };
        }

        private static object ToJson(MapLocation location) =>
            new { location.Name, location.Lat, location.Lon, location.Zoom };
    }
}
