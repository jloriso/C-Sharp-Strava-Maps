using System.Globalization;
using System.Linq;
using System.Xml.Linq;

/// <summary>Parses GPX XML (already decompressed) into tracks/routes/waypoints.</summary>
public static class GpxReader
{
    public static ParsedGpx ParseGpx(string xml)
    {
        var doc = XDocument.Parse(xml);
        var result = new ParsedGpx();

        foreach (var seg in doc.Descendants().Where(e => e.Name.LocalName == "trkseg"))
        {
            var pts = seg.Descendants().Where(e => e.Name.LocalName == "trkpt")
                .Select(ReadPt).Where(p => p.HasValue).Select(p => p!.Value).ToList();
            if (pts.Count > 0) result.Segments.Add(pts);
        }

        foreach (var rte in doc.Descendants().Where(e => e.Name.LocalName == "rte"))
        {
            var pts = rte.Descendants().Where(e => e.Name.LocalName == "rtept")
                .Select(ReadPt).Where(p => p.HasValue).Select(p => p!.Value).ToList();
            if (pts.Count > 0) result.Segments.Add(pts);
        }

        foreach (var wpt in doc.Descendants().Where(e => e.Name.LocalName == "wpt"))
        {
            var p = ReadPt(wpt);
            if (p.HasValue)
            {
                var nameEl = wpt.Elements().FirstOrDefault(e => e.Name.LocalName == "name");
                result.Points.Add((p.Value.Lat, p.Value.Lon, nameEl?.Value ?? ""));
            }
        }

        return result;
    }

    static (double Lat, double Lon)? ReadPt(XElement el)
    {
        var latAttr = el.Attribute("lat");
        var lonAttr = el.Attribute("lon");
        if (latAttr == null || lonAttr == null) return null;
        if (double.TryParse(latAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
            double.TryParse(lonAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
            return (lat, lon);
        return null;
    }
}
