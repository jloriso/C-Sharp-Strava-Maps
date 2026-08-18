/// <summary>Shape of the optional JSON config file. All fields are optional --
/// anything left out just falls back to the command line or the defaults.</summary>
class ConfigFile
{
    public string? GpxFolder { get; set; }
    public string? CsvFile { get; set; }
    public string? OutputHeatmapHtml { get; set; }
    public string? OutputLineMapHtml { get; set; }
    public string? OutputWeeklyMileageHtml { get; set; }
}
