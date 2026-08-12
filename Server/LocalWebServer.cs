using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>Serves the generated map pages over http://localhost and opens the
/// index in the default browser. Serving locally (rather than opening the HTML
/// files directly via file://) matters because OpenStreetMap's tile servers reject
/// requests with no Referer header, and browsers never send one for file://
/// pages.</summary>
public static class LocalWebServer
{
    /// <param name="pages">Maps a URL path (e.g. "heatmap.html") to its full HTML
    /// content. An index page linking to each is served at "/".</param>
    public static void Serve(Dictionary<string, string> pages)
    {
        int port = GetFreePort();
        string baseUrl = $"http://localhost:{port}/";
        string indexHtml = BuildIndexHtml(pages.Keys);

        var listener = new HttpListener();
        listener.Prefixes.Add(baseUrl);
        listener.Start();

        Console.WriteLine($"Serving maps at {baseUrl}");
        foreach (var name in pages.Keys)
            Console.WriteLine($"  {baseUrl}{name}");
        Console.WriteLine("Press Ctrl+C to stop the server when you're done viewing it.");
        TryOpenBrowser(baseUrl);

        while (listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = listener.GetContext(); }
            catch (HttpListenerException) { break; }

            string path = ctx.Request.Url?.AbsolutePath.TrimStart('/') ?? "";
            string? html = path.Length == 0 ? indexHtml : (pages.TryGetValue(path, out var page) ? page : null);

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

    static string BuildIndexHtml(IEnumerable<string> pageNames)
    {
        string template = HtmlTemplateLoader.Load("Index", "index.html");

        var links = new StringBuilder();
        foreach (var name in pageNames)
            links.Append($"<li><a href=\"{WebUtility.HtmlEncode(name)}\">{WebUtility.HtmlEncode(name)}</a></li>");

        return template
            .Replace("{{APP_TITLE}}", "GpxWorldMap")
            .Replace("{{GENERATED_AT}}", DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
            .Replace("{{MAP_LINKS_HTML}}", links.ToString());
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
