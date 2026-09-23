using System.IO;
using System.IO.Compression;
using System.Text;

/// <summary>Decompresses .gz files as either text (for GPX/TCX, which are XML)
/// or raw bytes (for FIT, which is binary).</summary>
public static class GzHelper
{
    public static string DecompressText(string path)
    {
        using var fileStream = File.OpenRead(path);
        using var gzip = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static byte[] DecompressBytes(string path)
    {
        using var fileStream = File.OpenRead(path);
        using var gzip = new GZipStream(fileStream, CompressionMode.Decompress);
        using var ms = new MemoryStream();
        gzip.CopyTo(ms);
        return ms.ToArray();
    }
}
