/// <summary>Fully-resolved settings for one run: where the GPX files are, the
/// optional CSV to join against, and where to write the two map outputs.</summary>
public class AppConfig
{
    public string GpxFolder = "";
    public string? CsvFile;
    public string OutputHeatmapHtml = "heatmaps/standard_heatmap.html";
    public string OutputLineMapHtml = "heatmaps/line_map.html";
}
