using GpxWorldMap.FileInterpreter.FileTypeReaders;
using GpxWorldMap.Models;

namespace GpxWorldMap.FileInterpreter
{
    /// <summary>
    /// Finds and parses every activity file in a folder. Files are parsed in parallel, and
    /// unchanged files are read from <see cref="ParsedActivityCache"/> instead of being
    /// decompressed and parsed again.
    /// </summary>
    public static class ActivityFileLoader
    {
        /// <summary>One activity file's parse result: either <see cref="Parsed"/> or <see cref="Error"/> is set.</summary>
        public sealed record LoadedFile(string FileName, ParsedGpx? Parsed, string? Error);

        public sealed record LoadResult(List<LoadedFile> Files, int FromCache);

        public static string[] FindActivityFiles(string folder)
        {
            return Directory.GetFiles(folder, "*.gz", SearchOption.TopDirectoryOnly)
                .Where(f => f.EndsWith(".gpx.gz", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".tcx.gz", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".fit.gz", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f)
                .ToArray();
        }

        public static ParsedGpx ParseActivityFile(string path)
        {
            if (path.EndsWith(".gpx.gz", StringComparison.OrdinalIgnoreCase))
                return GpxReader.ParseGpx(GzHelper.DecompressText(path));
            if (path.EndsWith(".tcx.gz", StringComparison.OrdinalIgnoreCase))
                return TcxReader.ParseTcx(GzHelper.DecompressText(path));
            if (path.EndsWith(".fit.gz", StringComparison.OrdinalIgnoreCase))
                return FitReader.ParseFit(GzHelper.DecompressBytes(path));

            throw new InvalidOperationException("Unrecognized activity file extension.");
        }

        /// <summary>Parses <paramref name="files"/>, returning results in the same order.</summary>
        /// <param name="cachePath">Parsed-activity cache file, or null to disable caching.</param>
        public static LoadResult LoadAll(IReadOnlyList<string> files, string? cachePath)
        {
            var cache = cachePath != null
                ? ParsedActivityCache.Load(cachePath)
                : new Dictionary<string, ParsedActivityCache.Entry>(StringComparer.OrdinalIgnoreCase);

            var results = new LoadedFile[files.Count];
            var entries = new ParsedActivityCache.Entry?[files.Count];
            var fromCache = new bool[files.Count];

            Parallel.For(0, files.Count, i =>
            {
                string path = files[i];
                string name = Path.GetFileName(path);
                try
                {
                    var info = new FileInfo(path);
                    long ticks = info.LastWriteTimeUtc.Ticks;
                    if (cache.TryGetValue(name, out var cached) && cached.Length == info.Length && cached.LastWriteUtcTicks == ticks)
                    {
                        entries[i] = cached;
                        fromCache[i] = true;
                        results[i] = new LoadedFile(name, cached.Parsed, null);
                        return;
                    }

                    var parsed = ParseActivityFile(path);
                    entries[i] = new ParsedActivityCache.Entry(info.Length, ticks, parsed);
                    results[i] = new LoadedFile(name, parsed, null);
                }
                catch (Exception ex)
                {
                    results[i] = new LoadedFile(name, null, ex.Message);
                }
            });

            int cachedCount = fromCache.Count(hit => hit);
            if (cachePath != null)
            {
                var updated = new Dictionary<string, ParsedActivityCache.Entry>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < files.Count; i++)
                    if (entries[i] != null) updated[results[i].FileName] = entries[i]!;

                // Rewrite only when something was parsed fresh or files were removed.
                if (cachedCount != updated.Count || updated.Count != cache.Count)
                {
                    try { ParsedActivityCache.Save(cachePath, updated); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Console.WriteLine($"  Note: couldn't write activity cache {cachePath} ({ex.Message}).");
                    }
                }
            }

            return new LoadResult(results.ToList(), cachedCount);
        }
    }
}
