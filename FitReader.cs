using System;
using System.Collections.Generic;

/// <summary>
/// A minimal parser for Garmin's FIT binary format, just enough to pull GPS
/// track points out of activity files. FIT is a compact binary format (unlike
/// GPX/TCX, which are XML) -- see https://developer.garmin.com/fit/protocol/
/// for the full spec. This intentionally only implements what's needed to find
/// "Record" messages (global message number 20) and read their position_lat /
/// position_long fields (field numbers 0 and 1, stored as signed 32-bit
/// "semicircles"); every other field is skipped using its declared size rather
/// than interpreted, since we don't need it.
///
/// Known limitations (uncommon in practice, but worth knowing):
/// - Multiple chained FIT files concatenated in one blob aren't handled (only
///   the first is read).
/// - Developer fields are skipped correctly but their contents are never used.
/// </summary>
public static class FitReader
{
    struct FieldDef
    {
        public byte FieldNumber;
        public byte Size;
        public bool IsDeveloperField;
    }

    class DefinitionMessage
    {
        public bool BigEndian;
        public ushort GlobalMessageNumber;
        public List<FieldDef> Fields = new();
    }

    const int RecordGlobalMessageNumber = 20;
    const int InvalidSint32 = 0x7FFFFFFF;

    public static ParsedGpx ParseFit(byte[] data)
    {
        var result = new ParsedGpx();
        var points = new List<(double Lat, double Lon)>();

        if (data.Length < 12) return result; // too short to be a valid FIT file

        byte headerSize = data[0];
        if (headerSize < 12 || headerSize > data.Length) return result;

        uint dataSize = ReadUInt32LE(data, 4);
        int dataStart = headerSize;
        int dataEnd = (int)Math.Min((long)dataStart + dataSize, data.Length);

        // Local message type (0-15) -> the Definition Message that describes
        // how to read subsequent Data Messages using that same local type.
        var definitions = new Dictionary<int, DefinitionMessage>();

        int pos = dataStart;
        while (pos < dataEnd)
        {
            byte header = data[pos++];
            bool compressedTimestamp = (header & 0x80) != 0;
            int localType;

            if (compressedTimestamp)
            {
                // Compressed Timestamp Header messages are always Data Messages.
                localType = (header >> 5) & 0x3;
            }
            else
            {
                bool isDefinition = (header & 0x40) != 0;
                localType = header & 0xF;

                if (isDefinition)
                {
                    bool hasDeveloperFields = (header & 0x20) != 0;
                    if (pos + 5 > dataEnd) break;
                    var def = ReadDefinitionMessage(data, pos, hasDeveloperFields, out int consumed);
                    pos += consumed;
                    definitions[localType] = def;
                    continue;
                }
            }

            if (!definitions.TryGetValue(localType, out var dataDef))
                break; // referenced a definition we haven't seen -- stop safely

            if (pos >= dataEnd) break;
            var (lat, lon, dataConsumed) = ReadDataMessage(data, pos, dataDef);
            pos += dataConsumed;

            if (dataDef.GlobalMessageNumber == RecordGlobalMessageNumber && lat.HasValue && lon.HasValue)
                points.Add((lat.Value, lon.Value));
        }

        if (points.Count > 0) result.Segments.Add(points);
        return result;
    }

    static DefinitionMessage ReadDefinitionMessage(byte[] data, int pos, bool hasDeveloperFields, out int consumed)
    {
        int start = pos;
        pos++; // reserved byte
        byte architecture = data[pos]; pos++;
        bool bigEndian = architecture == 1;

        ushort globalNum = bigEndian ? ReadUInt16BE(data, pos) : ReadUInt16LE(data, pos);
        pos += 2;

        byte numFields = data[pos]; pos++;
        var def = new DefinitionMessage { BigEndian = bigEndian, GlobalMessageNumber = globalNum };

        for (int i = 0; i < numFields; i++)
        {
            def.Fields.Add(new FieldDef { FieldNumber = data[pos], Size = data[pos + 1], IsDeveloperField = false });
            pos += 3; // field number, size, base type
        }

        if (hasDeveloperFields)
        {
            byte numDev = data[pos]; pos++;
            for (int i = 0; i < numDev; i++)
            {
                def.Fields.Add(new FieldDef { FieldNumber = data[pos], Size = data[pos + 1], IsDeveloperField = true });
                pos += 3; // field number, size, developer data index
            }
        }

        consumed = pos - start;
        return def;
    }

    static (double? lat, double? lon, int consumed) ReadDataMessage(byte[] data, int pos, DefinitionMessage def)
    {
        int start = pos;
        double? lat = null, lon = null;

        foreach (var field in def.Fields)
        {
            if (!field.IsDeveloperField && field.Size == 4 && (field.FieldNumber == 0 || field.FieldNumber == 1))
            {
                int raw = def.BigEndian ? ReadInt32BE(data, pos) : ReadInt32LE(data, pos);
                if (raw != InvalidSint32)
                {
                    double degrees = raw * (180.0 / 2147483648.0); // semicircles -> degrees
                    if (field.FieldNumber == 0) lat = degrees;
                    else lon = degrees;
                }
            }
            pos += field.Size;
        }

        return (lat, lon, pos - start);
    }

    static ushort ReadUInt16LE(byte[] d, int o) => (ushort)(d[o] | (d[o + 1] << 8));
    static ushort ReadUInt16BE(byte[] d, int o) => (ushort)((d[o] << 8) | d[o + 1]);
    static uint ReadUInt32LE(byte[] d, int o) => (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24));
    static int ReadInt32LE(byte[] d, int o) => d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24);
    static int ReadInt32BE(byte[] d, int o) => (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];
}
