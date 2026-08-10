using System.IO;

/// <summary>Extracts the leading ID from a filename or path by taking everything
/// before the first '.', e.g. "20483719126.fit.gz" -> "20483719126". Used both
/// for the CSV's Filename column and for naming activity files on disk.</summary>
public static class ActivityId
{
    public static string FromPath(string pathOrFilename)
    {
        if (string.IsNullOrWhiteSpace(pathOrFilename)) return "";
        string baseName = Path.GetFileName(pathOrFilename.Trim());
        int dot = baseName.IndexOf('.');
        return dot >= 0 ? baseName.Substring(0, dot) : baseName;
    }
}
