using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>Reads the activities CSV export and extracts just the columns this
/// project needs, ignoring however many other columns the export has.</summary>
public static class CsvReader
{
    public static List<ActivityRecord> ParseActivitiesCsv(string path)
    {
        var rows = ReadCsvRows(path);
        if (rows.Count == 0) return new();

        var header = rows[0].Select(h => h.Trim()).ToList();
        int idxId = FindColumn(header, "Activity ID");
        int idxDate = FindColumn(header, "Activity Date");
        int idxName = FindColumn(header, "Activity Name");
        int idxType = FindColumn(header, "Activity Type");
        int idxDesc = FindColumn(header, "Activity Description");
        int idxFilename = FindColumn(header, "Filename");

        if (idxId < 0)
            Console.WriteLine("Warning: couldn't find an 'Activity ID' column in the CSV header.");
        if (idxFilename < 0)
            Console.WriteLine("Warning: couldn't find a 'Filename' column in the CSV header; will match on Activity ID only.");

        var result = new List<ActivityRecord>();
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            string Get(int idx) => idx >= 0 && idx < row.Count ? row[idx] : "";
            result.Add(new ActivityRecord(
                Get(idxId).Trim(),
                Get(idxDate).Trim(),
                Get(idxName).Trim(),
                Get(idxType).Trim(),
                Get(idxDesc).Trim(),
                Get(idxFilename).Trim()));
        }
        return result;
    }

    /// <summary>"activities/20483719126.fit.gz" -> "20483719126". Works regardless
    /// of extension (.fit.gz, .gpx.gz, .tcx.gz, ...) since it just takes everything
    /// before the first '.' in the base filename.</summary>
    public static string ExtractIdFromFilename(string filenameField) => ActivityId.FromPath(filenameField);

    static int FindColumn(List<string> header, string name)
    {
        for (int i = 0; i < header.Count; i++)
            if (string.Equals(header[i], name, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    /// <summary>Minimal RFC-4180-ish CSV reader: handles quoted fields containing
    /// commas, escaped quotes (""), and embedded newlines -- all of which show up
    /// in Strava's "Activity Description" column.</summary>
    static List<List<string>> ReadCsvRows(string path)
    {
        var rows = new List<List<string>>();
        var field = new StringBuilder();
        var row = new List<string>();
        bool inQuotes = false;

        using var reader = new StreamReader(path, Encoding.UTF8);
        int ci;
        while ((ci = reader.Read()) != -1)
        {
            char c = (char)ci;
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (reader.Peek() == '"') { field.Append('"'); reader.Read(); }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (c == '\r') { /* skip, \n handles the line break */ }
                else if (c == '\n')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                }
                else field.Append(c);
            }
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
