using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public static class AppConfigLoader
{
    const string DefaultConfigFileName = "gpxworldmap.config.json";

    /// <summary>
    /// Resolves settings by layering, in order: built-in defaults, then the JSON
    /// config file (default "gpxworldmap.config.json", or a path given via
    /// "--config &lt;path&gt;"), then any positional command-line args, which win
    /// if given.
    ///
    /// Positional args are: [gpx-folder] [activities.csv] [heatmap-output.html]
    /// [linemap-output.html] [weekly-mileage-output.html] -- the csv is recognized
    /// by its ".csv" extension and can appear anywhere after the folder; the
    /// first, second, and third non-csv positional args after the folder are
    /// taken as the heatmap, line map, and weekly mileage output paths respectively.
    /// </summary>
    public static AppConfig Load(string[] args)
    {
        string configArg = DefaultConfigFileName;
        var positional = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config" && i + 1 < args.Length)
            {
                configArg = args[i + 1];
                i++; // consume the value too
            }
            else
            {
                positional.Add(args[i]);
            }
        }

        var config = new AppConfig();
        string? resolvedConfigPath = ResolveConfigPath(configArg);

        if (resolvedConfigPath != null)
        {
            try
            {
                string json = File.ReadAllText(resolvedConfigPath);
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var fromFile = JsonSerializer.Deserialize<ConfigFile>(json, opts);
                if (fromFile != null)
                {
                    if (!string.IsNullOrWhiteSpace(fromFile.GpxFolder)) config.GpxFolder = fromFile.GpxFolder!;
                    if (!string.IsNullOrWhiteSpace(fromFile.CsvFile)) config.CsvFile = fromFile.CsvFile;
                    if (!string.IsNullOrWhiteSpace(fromFile.OutputHeatmapHtml)) config.OutputHeatmapHtml = fromFile.OutputHeatmapHtml!;
                    if (!string.IsNullOrWhiteSpace(fromFile.OutputLineMapHtml)) config.OutputLineMapHtml = fromFile.OutputLineMapHtml!;
                    if (!string.IsNullOrWhiteSpace(fromFile.OutputWeeklyMileageHtml)) config.OutputWeeklyMileageHtml = fromFile.OutputWeeklyMileageHtml!;
                }
                Console.WriteLine($"Loaded config from {resolvedConfigPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: found config file '{resolvedConfigPath}' but couldn't read it: {ex.Message}");
                Console.WriteLine("Falling back to command-line args / defaults.");
            }
        }
        else
        {
            Console.WriteLine(
                $"No config file found (looked for '{configArg}' in the current folder " +
                $"[{Directory.GetCurrentDirectory()}] and next to the program " +
                $"[{AppContext.BaseDirectory}]). Using command-line args / defaults.");
        }

        // Positional args, if given, override whatever the config file set.
        if (positional.Count > 0) config.GpxFolder = positional[0];
        int outputsSeen = 0;
        foreach (var arg in positional.Skip(1))
        {
            if (arg.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                config.CsvFile = arg;
                continue;
            }

            switch (outputsSeen)
            {
                case 0: config.OutputHeatmapHtml = arg; break;
                case 1: config.OutputLineMapHtml = arg; break;
                default: config.OutputWeeklyMileageHtml = arg; break;
            }
            outputsSeen++;
        }

        if (string.IsNullOrWhiteSpace(config.GpxFolder))
            config.GpxFolder = Directory.GetCurrentDirectory();

        // Always show exactly what's about to be used, so a wrong default never
        // fails silently -- this is what actually points you at the real problem
        // when something doesn't match what you expect.
        Console.WriteLine($"GPX/TCX/FIT folder: {Path.GetFullPath(config.GpxFolder)}");
        Console.WriteLine($"CSV file: {(config.CsvFile != null ? Path.GetFullPath(config.CsvFile) : "(none)")}");
        Console.WriteLine($"Heatmap output: {Path.GetFullPath(config.OutputHeatmapHtml)}");
        Console.WriteLine($"Line map output: {Path.GetFullPath(config.OutputLineMapHtml)}");
        Console.WriteLine($"Weekly mileage output: {Path.GetFullPath(config.OutputWeeklyMileageHtml)}");

        return config;
    }

    /// <summary>Looks for the config file relative to the current working directory
    /// first, then relative to the running program's own directory.</summary>
    static string? ResolveConfigPath(string configArg)
    {
        if (Path.IsPathRooted(configArg))
            return File.Exists(configArg) ? configArg : null;

        string cwdCandidate = Path.Combine(Directory.GetCurrentDirectory(), configArg);
        if (File.Exists(cwdCandidate)) return cwdCandidate;

        string exeDirCandidate = Path.Combine(AppContext.BaseDirectory, configArg);
        if (File.Exists(exeDirCandidate)) return exeDirCandidate;

        return null;
    }
}
