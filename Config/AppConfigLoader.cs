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
    /// if given. So a config file makes the command line optional; the command
    /// line still lets you override it for a one-off run.
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
                    if (!string.IsNullOrWhiteSpace(fromFile.OutputHtml)) config.OutputHtml = fromFile.OutputHtml!;
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
        foreach (var arg in positional.Skip(1))
        {
            if (arg.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                config.CsvFile = arg;
            else
                config.OutputHtml = arg;
        }

        if (string.IsNullOrWhiteSpace(config.GpxFolder))
            config.GpxFolder = Directory.GetCurrentDirectory();
        if (string.IsNullOrWhiteSpace(config.OutputHtml))
            config.OutputHtml = "map.html";

        // Always show exactly what's about to be used, so a wrong default never
        // fails silently -- this is what actually points you at the real problem
        // when something doesn't match what you expect.
        Console.WriteLine($"GPX/TCX/FIT folder: {Path.GetFullPath(config.GpxFolder)}");
        Console.WriteLine($"CSV file: {(config.CsvFile != null ? Path.GetFullPath(config.CsvFile) : "(none)")}");
        Console.WriteLine($"Output HTML: {Path.GetFullPath(config.OutputHtml)}");

        return config;
    }

    /// <summary>Looks for the config file relative to the current working directory
    /// first, then relative to the running program's own directory. The second
    /// check matters because some IDEs/launchers run the app with a working directory
    /// other than the project folder (e.g. the build output folder), which would
    /// otherwise cause the config file to silently not be found even though it's
    /// sitting right next to your source files.</summary>
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
