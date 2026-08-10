using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>Turns parsed GPX geometry + activity metadata into GeoJSON Feature
/// strings, ready to be joined into a FeatureCollection.</summary>
public static class GeoJsonBuilder
{
    public static string BuildLineStringFeature(List<(double Lat, double Lon)> seg, ActivityMeta meta)
    {
        var coords = string.Join(",", seg.Select(p => $"[{Fmt(p.Lon)},{Fmt(p.Lat)}]"));
        return "{\"type\":\"Feature\",\"properties\":" + PropsJson(meta) +
               ",\"geometry\":{\"type\":\"LineString\",\"coordinates\":[" + coords + "]}}";
    }

    public static string BuildPointFeature((double Lat, double Lon, string Name) pt, ActivityMeta meta)
    {
        var pointMeta = meta;
        if (!string.IsNullOrWhiteSpace(pt.Name))
        {
            pointMeta = new ActivityMeta
            {
                Name = pt.Name,
                Type = meta.Type,
                Date = meta.Date,
                Description = meta.Description,
                Color = meta.Color
            };
        }
        return "{\"type\":\"Feature\",\"properties\":" + PropsJson(pointMeta) +
               ",\"geometry\":{\"type\":\"Point\",\"coordinates\":[" + Fmt(pt.Lon) + "," + Fmt(pt.Lat) + "]}}";
    }

    static string PropsJson(ActivityMeta meta)
    {
        string popup = "<b>" + EscapeHtml(meta.Name) + "</b><br>" +
                        EscapeHtml(meta.Type) +
                        (string.IsNullOrWhiteSpace(meta.Date) ? "" : " &middot; " + EscapeHtml(meta.Date));
        if (!string.IsNullOrWhiteSpace(meta.Description))
        {
            string desc = meta.Description.Replace("\r\n", "\n").Replace("\r", "\n");
            popup += "<br><br>" + EscapeHtml(desc).Replace("\n", "<br>");
        }

        return "{\"name\":" + JsonStr(meta.Name) +
               ",\"type\":" + JsonStr(meta.Type) +
               ",\"color\":" + JsonStr(meta.Color) +
               ",\"popup\":" + JsonStr(popup) + "}";
    }

    static string Fmt(double d) => d.ToString("R", CultureInfo.InvariantCulture);

    // Proper JSON string escaping -- notably including newlines/carriage returns and
    // other control characters. Without this, a literal line break embedded in a CSV
    // field (e.g. a multi-line Activity Description) would land inside a JS string
    // literal verbatim, which is invalid syntax and breaks the whole page.
    static string JsonStr(string s)
    {
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    static string EscapeHtml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}