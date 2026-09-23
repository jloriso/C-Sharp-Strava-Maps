namespace GpxWorldMap.Rendering;

/// <summary>
/// Loads an HTML page template that lives in its own file under
/// Features/feature/ (e.g. heatmap.html, linemap.html), rather than being
/// embedded as a giant C# verbatim string inside the builder class itself.
///
/// Templates are compiled in as <b>embedded resources</b> (see the
/// &lt;EmbeddedResource Include="Features/*/*.html" /&gt; entry in the
/// .csproj), so the page markup ships inside the single compiled assembly --
/// there's no risk of a template file going missing at runtime regardless of the
/// working directory, or whether the app was run via `dotnet run` or published as
/// a standalone executable.
/// </summary>
public static class HtmlTemplateLoader
{
    public static string Load(string templatesFolder, string templateFileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, templatesFolder, templateFileName);

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Couldn't find template '{templateFileName}' at '{path}'. Make sure " +
                $"{templatesFolder}/* is marked as <Content CopyToOutputDirectory=\"PreserveNewest\"> " +
                "in the .csproj and that the project has been rebuilt since the file was added.");
        }

        return File.ReadAllText(path);
    }
}