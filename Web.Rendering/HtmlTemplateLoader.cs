namespace GpxWorldMap.Rendering
{
    /// <summary>
    /// Loads a built HTML page template from the application's output directory
    /// (e.g. Features/Maps/Heatmap/heatmap.html). Web.App's Vite build emits one
    /// self-contained HTML file per page into Web.App/dist/, and the .csproj copies
    /// those files next to the executable with the dist/ prefix removed.
    /// </summary>
    public static class HtmlTemplateLoader
    {
        public static string Load(string templatesFolder, string templateFileName)
        {
            string path = Path.Combine(AppContext.BaseDirectory, templatesFolder, templateFileName);

            if (!File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"Couldn't find template '{templateFileName}' at '{path}'. Make sure the page " +
                    "is listed in Web.App/scripts/build-pages.mjs and rebuild Web.App (which runs " +
                    "`npm run build` and copies dist/**/*.html to the output directory).");
            }

            return File.ReadAllText(path);
        }
    }
}