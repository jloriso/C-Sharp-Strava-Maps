using System.Text;
using GpxWorldMap.Models;

namespace GpxWorldMap.FileInterpreter
{
    /// <summary>
    /// On-disk cache of parsed activity files, so unchanged .gpx.gz/.tcx.gz/.fit.gz files
    /// aren't decompressed and parsed again on every run. Entries are keyed by file name
    /// and are only reused when the file's size and last-write time still match. Stored
    /// in a compact binary format; any unreadable or outdated cache is simply ignored.
    /// </summary>
    public static class ParsedActivityCache
    {
        // Bump when the binary layout or the parsers' output changes, to invalidate old caches.
        private const int FormatVersion = 1;
        private static readonly byte[] Magic = "GWMC"u8.ToArray();

        public sealed record Entry(long Length, long LastWriteUtcTicks, ParsedGpx Parsed);

        public static Dictionary<string, Entry> Load(string path)
        {
            var entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return entries;

            try
            {
                using var stream = new BufferedStream(File.OpenRead(path), 1 << 16);
                using var reader = new BinaryReader(stream, Encoding.UTF8);
                if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic) || reader.ReadInt32() != FormatVersion)
                    return entries;

                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string fileName = reader.ReadString();
                    long length = reader.ReadInt64();
                    long ticks = reader.ReadInt64();
                    var parsed = new ParsedGpx();

                    int segmentCount = reader.ReadInt32();
                    for (int s = 0; s < segmentCount; s++)
                    {
                        int pointCount = reader.ReadInt32();
                        var segment = new List<(double Lat, double Lon)>(pointCount);
                        for (int p = 0; p < pointCount; p++)
                            segment.Add((reader.ReadDouble(), reader.ReadDouble()));
                        parsed.Segments.Add(segment);
                    }

                    int waypointCount = reader.ReadInt32();
                    for (int w = 0; w < waypointCount; w++)
                        parsed.Points.Add((reader.ReadDouble(), reader.ReadDouble(), reader.ReadString()));

                    entries[fileName] = new Entry(length, ticks, parsed);
                }
                return entries;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Note: ignoring unreadable activity cache {path} ({ex.Message}).");
                return new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public static void Save(string path, IReadOnlyDictionary<string, Entry> entries)
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // Write to a temp file first so an interrupted run can't leave a half-written cache.
            string tempPath = path + ".tmp";
            using (var stream = new BufferedStream(File.Create(tempPath), 1 << 16))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(entries.Count);
                foreach (var (fileName, entry) in entries)
                {
                    writer.Write(fileName);
                    writer.Write(entry.Length);
                    writer.Write(entry.LastWriteUtcTicks);

                    writer.Write(entry.Parsed.Segments.Count);
                    foreach (var segment in entry.Parsed.Segments)
                    {
                        writer.Write(segment.Count);
                        foreach (var (lat, lon) in segment)
                        {
                            writer.Write(lat);
                            writer.Write(lon);
                        }
                    }

                    writer.Write(entry.Parsed.Points.Count);
                    foreach (var (lat, lon, name) in entry.Parsed.Points)
                    {
                        writer.Write(lat);
                        writer.Write(lon);
                        writer.Write(name ?? "");
                    }
                }
            }
            File.Move(tempPath, path, overwrite: true);
        }
    }
}
