# GpxWorldMap

Reads every `*.gpx.gz` file in a folder, decompresses it, pulls out the tracks/routes/
waypoints, optionally joins each one against a Strava-style activities CSV, and serves
a single **interactive** world map (OpenStreetMap tiles via Leaflet.js) showing all of
them, color-coded by activity type, with a legend and popups.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download) (or later)
- Internet access when the map is open in your browser (it loads map tiles and
  Leaflet.js from public CDNs) — no internet needed to run the C# program itself.

## Run it

```bash
cd GpxWorldMap
dotnet run -- "path/to/gpx-folder" "path/to/activities.csv" map.html
```

- First argument: folder containing your `.gpx.gz` files (defaults to current folder)
- Second/third arguments are optional and can be given in either order: whichever one
  ends in `.csv` is treated as the activities CSV; the other is the output HTML
  filename (defaults to `map.html`). You can also omit the CSV entirely and just run
  `dotnet run -- "path/to/gpx-folder"` like before.

The program writes `map.html` to disk **and** serves it from a local web server
(e.g. `http://localhost:53214/`), opening it in your default browser automatically.

**Why a server, and not just double-clicking the HTML file?** OpenStreetMap's tile
servers now reject tile requests that have no `Referer` header, and browsers never
send one for pages opened via `file://` — that's what causes the `403 Access Blocked`
error. Loading the page from `http://localhost` instead gives every tile request a
normal referer, which satisfies their policy.

Leave the program running while you're viewing the map; press `Ctrl+C` in the
terminal when you're done to stop the server.

## Joining GPX files with your activities CSV

The CSV is expected to have (at least) these columns, matching Strava's export format:
`Activity ID`, `Activity Date`, `Activity Name`, `Activity Type`, `Activity Description`.

Matching works by **Activity ID**: each GPX file's base name (with `.gpx.gz` stripped)
is looked up against the `Activity ID` column. This is how Strava's bulk export names
files — e.g. `activities/9876543210.gpx.gz` pairs with the CSV row where
`Activity ID` = `9876543210`. If your files are named differently, rename them to
match their Activity ID, or tell me your naming pattern and I'll adjust the matching
logic.

- GPX files with no matching CSV row are still plotted, just labeled "Unknown".
- CSV rows with no matching GPX file are simply skipped — expected, since you said not
  every activity has GPS data.
- The console output at the end tells you how many matched, how many CSV-only rows
  there were, and flags any GPX files it couldn't match (so you can spot a naming
  mismatch quickly).

## What you get on the map

- Each track/waypoint is colored by **Activity Type** (Run, Ride, Hike, etc., with a
  fixed set of common Strava types pre-colored, and a deterministic fallback color for
  anything else) — same type always gets the same color.
- A **legend** in the bottom-right lists each type and its color.
- Clicking a track or point shows a **popup** with its name, type, date, and
  description (if present).

## What it does under the hood

1. Finds every `*.gpx.gz` in the given folder (top-level only).
2. Decompresses each with `GZipStream`.
3. Parses the inner GPX XML looking for `<trkpt>` (inside `<trkseg>`), `<rtept>`
   (inside `<rte>`), and standalone `<wpt>` elements — matches by local element name,
   so it works for both GPX 1.0 and 1.1 files regardless of namespace.
4. If given, parses the CSV with a small hand-written reader that handles quoted
   fields containing commas, escaped quotes, and embedded newlines (Strava's activity
   descriptions often contain all three).
5. Builds one GeoJSON `LineString` per track segment/route and a `Point` per
   waypoint, each carrying its color, type, and a pre-built popup HTML string
   (with any CSV text HTML-escaped to avoid breaking the page).
6. Embeds that GeoJSON directly into an HTML template that loads Leaflet + OSM tiles,
   draws everything, fits the map view to bounds, and builds the legend from whatever
   types actually appear in the data.

## Tweaking it

- If you'd rather have a static PNG/SVG instead of an interactive HTML map (e.g.
  for embedding in a report), that's a different approach (rendering onto a raster
  or vector map projection) — let me know and I can put that together too.

