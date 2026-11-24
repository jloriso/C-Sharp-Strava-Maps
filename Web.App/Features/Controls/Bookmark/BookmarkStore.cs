using System.Text.Json;
using Api.Models;

namespace Web.App.Features.Controls.Bookmark
{
    /// <summary>
    /// Reads and writes the "Jump to" locations shown by BookmarkControl.ts on the heatmap
    /// and line map pages. They live in bookmarks.json next to this file, which the build
    /// copies to the output directory (without overwriting a newer copy there), so the
    /// program can update the list at runtime. The first location is where maps load into.
    /// </summary>
    public static class BookmarkStore
    {
        public static string FilePath { get; } =
            Path.Combine(AppContext.BaseDirectory, "Features", "Controls", "Bookmark", "bookmarks.json");

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IncludeFields = true,
            WriteIndented = true
        };

        private static List<MapLocation> Fallback() => new()
        {
            new MapLocation { Name = "World", Lat = 20.0, Lon = 0.0, Zoom = 3 }
        };

        public static List<MapLocation> Load()
        {
            if (!File.Exists(FilePath)) return Fallback();

            try
            {
                var locations = JsonSerializer.Deserialize<List<MapLocation>>(File.ReadAllText(FilePath), SerializerOptions);
                return locations is { Count: > 0 } ? locations : Fallback();
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"  Note: couldn't read bookmarks from {FilePath} ({ex.Message}); using defaults.");
                return Fallback();
            }
        }

        public static void Save(IEnumerable<MapLocation> locations)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(locations, SerializerOptions));
        }
    }
}
