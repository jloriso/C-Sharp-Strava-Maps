namespace GpxWorldMap.Rendering;

/// <summary>Picks a consistent color per activity type, so e.g. every Run is the
/// same color and every Ride is the same (different) color.</summary>
public static class ActivityColors
{
    static readonly Dictionary<string, string> KnownTypeColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Run"] = "#fc4c02",
        ["Ride"] = "#00b0ff",
        ["Swim"] = "#911eb4",
        ["Other"] = "#808080",
    };

    static readonly string[] FallbackPalette =
    {
        "#e6194b", "#3cb44b", "#4363d8", "#f58231", "#911eb4",
        "#42d4f4", "#f032e6", "#469990", "#9a6324", "#800000",
        "#808000", "#000075", "#e6beff", "#ffd8b1"
    };

    public static string ColorForType(string type)
    {
        if (KnownTypeColors.TryGetValue(type, out var c)) return c;

        // Deterministic fallback for unrecognized types, so the same type
        // always gets the same color across runs.
        int hash = 0;
        foreach (char ch in type) hash = hash * 31 + ch;
        int idx = Math.Abs(hash) % FallbackPalette.Length;
        return FallbackPalette[idx];
    }
}