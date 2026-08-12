using System;

/// <summary>Builds JS for a Leaflet "Home" button control that navigates back to
/// the local server index page.</summary>
public static class HomeControlScript
{
    public static string Build(string homeUrl = "/")
    {
        if (string.IsNullOrWhiteSpace(homeUrl))
            homeUrl = "/";

        string escaped = homeUrl
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");

        return HtmlTemplateLoader.Load("Controls/Templates", "HomeControl.js")
            .Replace("{{HOME_URL_JS}}", $"\"{escaped}\"");
    }
}