/// <summary>Fully-resolved settings for one run: where the GPX files are, the
/// optional CSV to join against, and where to write the map.</summary>
public class AppConfig
{
    public string GpxFolder = "";
    public string? CsvFile;
    public string OutputHtml = "map.html";
}
