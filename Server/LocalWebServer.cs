using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>Serves the generated pages over http://localhost and opens the home
/// page in the default browser. Serving locally (rather than opening the HTML
/// files directly via file://) matters for the heatmap/line map pages, since
/// OpenStreetMap's tile servers reject requests with no Referer header, and
/// browsers never send one for file:// pages.</summary>
public static class LocalWebServer
{
    /// <param name="indexHtml">Served at the site root ("/") -- the Weekly Mileage
    /// chart, which acts as the app's home page.</param>
    /// <param name="otherPages">Maps a URL path (e.g. "heatmap.html") to its full
    /// HTML content, for every other page (currently the heatmap and line map).</param>
    public static void Serve(string indexHtml, Dictionary<string, string> otherPages)
    {
        int port = GetFreePort();
        string baseUrl = $"http://localhost:{port}/";

        var listener = new HttpListener();
        listener.Prefixes.Add(baseUrl);
        listener.Start();

        Console.WriteLine($"Serving pages at {baseUrl}");
        Console.WriteLine($"  {baseUrl}  (Weekly Mileage -- home page)");
        foreach (var name in otherPages.Keys)
            Console.WriteLine($"  {baseUrl}{name}");
        Console.WriteLine("Press Ctrl+C to stop the server when you're done viewing it.");
        TryOpenBrowser(baseUrl);

        while (listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = listener.GetContext(); }
            catch (HttpListenerException) { break; }

            string path = ctx.Request.Url?.AbsolutePath.TrimStart('/') ?? "";
            string? html = path.Length == 0 ? indexHtml : (otherPages.TryGetValue(path, out var page) ? page : null);

            using var response = ctx.Response;
            if (html == null)
            {
                response.StatusCode = 404;
                html = "Not found.";
            }
            var bytes = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
        }
    }

    static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    static void TryOpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else
                Process.Start("xdg-open", url);
        }
        catch
        {
            Console.WriteLine($"Couldn't auto-open a browser. Open this URL manually: {url}");
        }
    }
}
