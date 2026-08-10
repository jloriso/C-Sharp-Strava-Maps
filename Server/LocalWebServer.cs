using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>Serves the generated map page over http://localhost and opens it in
/// the default browser. Serving locally (rather than opening the HTML file
/// directly via file://) matters because OpenStreetMap's tile servers reject
/// requests with no Referer header, and browsers never send one for file://
/// pages.</summary>
public static class LocalWebServer
{
    public static void Serve(string html)
    {
        int port = GetFreePort();
        string url = $"http://localhost:{port}/";
        var htmlBytes = Encoding.UTF8.GetBytes(html);

        var listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();

        Console.WriteLine($"Serving the map at {url}");
        Console.WriteLine("Press Ctrl+C to stop the server when you're done viewing it.");
        TryOpenBrowser(url);

        while (listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = listener.GetContext(); }
            catch (HttpListenerException) { break; }

            using var response = ctx.Response;
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = htmlBytes.Length;
            response.OutputStream.Write(htmlBytes, 0, htmlBytes.Length);
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
