using System.Text;
using GpxWorldMap.Models;
using GpxWorldMap.Shared;

namespace GpxWorldMap.FileInterpreter.FileTypeReaders
{
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
            // Strava exports have two "Elapsed Time" columns; both are in seconds, and
            // FindColumn takes the first.
            int idxElapsed = FindColumn(header, "Elapsed Time");
            int idxMoving = FindColumn(header, "Moving Time");
            int idxGear = FindColumn(header, "Activity Gear");
            int idxDistance = FindColumn(header, "Distance");
            if (idxDistance < 0) idxDistance = FindColumn(header, "Activity Distance");

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
                    Get(idxFilename).Trim(),
                    Get(idxElapsed).Trim(),
                    Get(idxMoving).Trim(),
                    Get(idxGear).Trim(),
                    Get(idxDistance).Trim()));
            }
            return result;
        }

        /// <summary>Reads the Strava export's shoes.csv (Shoe Name, Shoe Brand, Shoe Model, ...).</summary>
        public static List<ShoeRecord> ParseShoesCsv(string path)
        {
            var rows = ReadCsvRows(path);
            if (rows.Count == 0) return new();

            var header = rows[0].Select(h => h.Trim()).ToList();
            int idxName = FindColumn(header, "Shoe Name");
            int idxBrand = FindColumn(header, "Shoe Brand");
            int idxModel = FindColumn(header, "Shoe Model");

            var result = new List<ShoeRecord>();
            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                string Get(int idx) => idx >= 0 && idx < row.Count ? row[idx].Trim() : "";
                if (Get(idxName).Length > 0 || Get(idxModel).Length > 0)
                    result.Add(new ShoeRecord(Get(idxName), Get(idxBrand), Get(idxModel)));
            }
            return result;
        }

        /// <summary>"activities/20483719126.fit.gz" -> "20483719126". Works regardless
        /// of extension (.fit.gz, .gpx.gz, .tcx.gz, ...) since it just takes everything
        /// before the first '.' in the base filename.</summary>
        public static string ExtractIdFromFilename(string filenameField) => ActivityId.FromPath(filenameField);

        private static int FindColumn(List<string> header, string name)
        {
            for (int i = 0; i < header.Count; i++)
                if (string.Equals(header[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        /// <summary>Minimal RFC-4180-ish CSV reader: handles quoted fields containing
        /// commas, escaped quotes (""), and embedded newlines -- all of which show up
        /// in Strava's "Activity Description" column.</summary>
        private static List<List<string>> ReadCsvRows(string path)
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
}