/// <summary>One row from the activities CSV (only the columns we actually use).</summary>
public record ActivityRecord(
    string Id,
    string Date,
    string Name,
    string Type,
    string Description,
    string Filename);
