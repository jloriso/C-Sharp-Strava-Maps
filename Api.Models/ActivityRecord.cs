namespace GpxWorldMap.Models
{
    /// <summary>One row from the activities CSV (only the columns we actually use).
    /// ElapsedTime/MovingTime are the raw seconds values ("" when the column is absent);
    /// Distance is the raw kilometer value; Gear is the Activity Gear column, e.g.
    /// "New Balance 880 880 V11" (brand, model, name).</summary>
    public record ActivityRecord(
        string Id,
        string Date,
        string Name,
        string Type,
        string Description,
        string Filename,
        string ElapsedTime = "",
        string MovingTime = "",
        string Gear = "",
        string Distance = "");
}