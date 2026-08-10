using System.Globalization;
using System.Linq;
using System.Xml.Linq;

/// <summary>Parses TCX XML (already decompressed) into tracks. TCX structures GPS
/// data as Activity/Lap/Track/Trackpoint/Position/LatitudeDegrees+LongitudeDegrees;
/// each Track becomes one segment (similar to a GPX trkseg).</summary>
public static class TcxReader
{
    public static ParsedGpx ParseTcx(string xml)
    {
        var doc = XDocument.Parse(xml);
        var result = new ParsedGpx();

        foreach (var track in doc.Descendants().Where(e => e.Name.LocalName == "Track"))
        {
            var pts = track.Elements().Where(e => e.Name.LocalName == "Trackpoint")
                .Select(ReadTrackpoint)
                .Where(p => p.HasValue)
                .Select(p => p!.Value)
                .ToList();
            if (pts.Count > 0) result.Segments.Add(pts);
        }

        return result;
    }

    static (double Lat, double Lon)? ReadTrackpoint(XElement trackpoint)
    {
        var position = trackpoint.Elements().FirstOrDefault(e => e.Name.LocalName == "Position");
        if (position == null) return null;

        var latEl = position.Elements().FirstOrDefault(e => e.Name.LocalName == "LatitudeDegrees");
        var lonEl = position.Elements().FirstOrDefault(e => e.Name.LocalName == "LongitudeDegrees");
        if (latEl == null || lonEl == null) return null;

        if (double.TryParse(latEl.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
            double.TryParse(lonEl.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
            return (lat, lon);
        return null;
    }
}
