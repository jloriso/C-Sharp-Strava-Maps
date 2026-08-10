using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

/// <summary>
/// Loads an HTML page template that lives in its own file under
/// Rendering/Templates/ (e.g. heatmap.html, linemap.html), rather than being
/// embedded as a giant C# verbatim string inside the builder class itself.
///
/// Templates are compiled in as <b>embedded resources</b> (see the
/// &lt;EmbeddedResource Include="Rendering/Templates/*.html" /&gt; entry in the
/// .csproj), so the page markup ships inside the single compiled assembly --
/// there's no risk of a template file going missing at runtime regardless of the
/// working directory, or whether the app was run via `dotnet run` or published as
/// a standalone executable.
/// </summary>
public static class HtmlTemplateLoader
{
    public static string Load(string templateFileName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Embedded resource names are namespace-qualified (e.g.
        // "GpxWorldMap.Rendering.Templates.heatmap.html"), so match by suffix
        // rather than hardcoding the full name -- keeps this working even if the
        // project's root namespace or folder nesting ever changes.
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("." + templateFileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
        {
            throw new InvalidOperationException(
                $"Couldn't find embedded template '{templateFileName}'. Make sure " +
                "Rendering/Templates/*.html is marked as <EmbeddedResource> in the .csproj " +
                "and that the file name matches exactly (case-insensitive).");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}