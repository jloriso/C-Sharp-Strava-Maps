/// <summary>
/// The metadata attached to a single activity -- either taken from a matched CSV
/// row, or a fallback ("Unknown" type, filename as name) when there's no matching
/// row.
/// </summary>
public class ActivityMeta
{
    public string Name = "";
    public string Type = "";
    public string Date = "";
    public string Description = "";
    public string Color = "#3388ff";
}
