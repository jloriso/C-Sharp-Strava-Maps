# GpxWorldMap

Reads every `*.gpx.gz`, `*.tcx.gz`, and `*.fit.gz` file in a folder, decompresses it,
pulls out the tracks/routes/waypoints, optionally joins each one against a
Strava-style activities CSV, and serves a single **interactive** world map
(OpenStreetMap tiles via Leaflet.js) showing all of them, color-coded by activity
type, with a legend and popups.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download) (or later)
- Internet access when the map is open in your browser (it loads map tiles and
  Leaflet.js from public CDNs) — no internet needed to run the C# program itself.

## Run it

**Option A — config file (shortest):**

```bash
cp gpxworldmap.config.example.json gpxworldmap.config.json
# edit gpxworldmap.config.json with your actual paths
dotnet run
```

**Option B — command-line args (no config file needed):**

```bash
dotnet run -- "path/to/activity-folder" "path/to/activities.csv" map.html
```

- First argument: folder containing your `.gpx.gz` / `.tcx.gz` / `.fit.gz` files
  (defaults to current folder)
- Second/third arguments are optional and can be given in either order: whichever one
  ends in `.csv` is treated as the activities CSV; the other is the output HTML
  filename (defaults to `map.html`).
- Any command-line args you do pass override the config file's values, so you can
  keep a config file for your usual folder and still point at a different CSV
  one-off: `dotnet run -- other-activities.csv`.
- Use a config file at a different path with `--config path/to/other.json`.

Every run prints exactly which config file (if any) it found and the final
GPX/TCX/FIT folder, CSV file, and output path it's about to use — so if something
doesn't match what you expect, that's the first place to look. The config file is
searched for both in your current directory and next to the running program itself,
since some IDEs/launchers use a different working directory than the project folder.

The program writes `map.html` to disk **and** serves it from a local web server
(e.g. `http://localhost:53214/`), opening it in your default browser automatically.

**Why a server, and not just double-clicking the HTML file?** OpenStreetMap's tile
servers now reject tile requests that have no `Referer` header, and browsers never
send one for pages opened via `file://` — that's what causes the `403 Access Blocked`
error. Loading the page from `http://localhost` instead gives every tile request a
normal referer, which satisfies their policy.

Leave the program running while you're viewing the map; press `Ctrl+C` in the
terminal when you're done to stop the server.

## Project layout

The code is split by responsibility so each file is easy to find and change:

| File | Responsibility |
|---|---|
| `Program.cs` | Entry point — orchestrates everything else |
| `AppConfig.cs` | Loads/merges `gpxworldmap.config.json` and command-line args |
| `Models.cs` | Plain data types (`ActivityRecord`, `ActivityMeta`, `ParsedGpx`) |
| `CsvReader.cs` | Reads and parses the activities CSV |
| `ActivityId.cs` | Shared "extract ID from filename" logic (CSV rows and files alike) |
| `GzHelper.cs` | Gzip decompression (text for XML formats, bytes for FIT) |
| `GpxReader.cs` | Parses GPX XML |
| `TcxReader.cs` | Parses TCX XML (Garmin Training Center format) |
| `FitReader.cs` | Parses FIT's binary format (see below) |
| `ActivityColors.cs` | Consistent color-per-activity-type logic |
| `GeoJsonBuilder.cs` | Builds GeoJSON features (with escaped popup HTML) |
| `MapHtmlBuilder.cs` | Builds the Leaflet map page (tiles, legend, styling) |
| `LocalWebServer.cs` | Serves the page locally and opens your browser |

## File formats

All three of Strava's (and most other platforms') export formats are supported:

- **GPX** — plain XML, straightforward to parse.
- **TCX** — Garmin's XML format; structurally similar to GPX (Track → Trackpoint →
  Position → LatitudeDegrees/LongitudeDegrees instead of GPX's trkseg → trkpt).
- **FIT** — Garmin's compact **binary** format, not XML. There's no built-in .NET
  support for it, so `FitReader.cs` is a small hand-written parser that reads just
  enough of the format to pull GPS points out of "Record" messages (position_lat /
  position_long fields). It skips everything else in the file (heart rate, power,
  cadence, developer fields, etc.) since none of that is needed for plotting a route.
  This covers the vast majority of real-world activity files, but if you have an
  unusual FIT file that doesn't plot correctly, let me know and I can dig into it
  further — the FIT spec allows some edge cases (like chained files) this doesn't
  handle.

## Joining GPX files with your activities CSV

Your CSV can have any number of extra columns (Strava exports often have 90+); this
tool only reads five of them: `Activity ID`, `Activity Date`, `Activity Name`,
`Activity Type`, `Activity Description`, and `Filename`.

Matching works primarily by the **Filename** column, e.g. `activities/20483719126.fit.gz`
— the ID (`20483719126`) is extracted from that path regardless of its extension
(`.fit.gz`, `.gpx.gz`, `.tcx.gz`, whatever) and compared against each GPX file's base
name. If a row has no usable Filename, it falls back to matching on the `Activity ID`
column instead.

- GPX files with no matching CSV row are still plotted, just labeled "Unknown".
- CSV rows with no matching GPX file are simply skipped — expected, since not every
  activity has GPS data (e.g. it was originally a manual entry, or uploaded as a
  format you don't have a GPX export for).
- The console output at the end tells you how many matched, roughly how many CSV-only
  rows there were, and flags any GPX files it couldn't match (so you can spot a
  naming mismatch quickly).

## Map style

The default basemap is **CARTO Positron** — a light, low-detail style with muted
colors and minimal labeling, so your colored routes are easy to see and click. A
layer switcher (top-right) lets you flip between:

- **Light (default)** — Positron, minimal/clean
- **Voyager** — a balanced style with more labels
- **Dark** — dark background, good contrast for bright route colors
- **OSM Streets (detailed)** — standard, busier OpenStreetMap tiles

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

