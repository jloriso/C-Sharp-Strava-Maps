## GpxWorldMap

Reads every *.gpx.gz, *.tcx.gz, and *.fit.gz file in a folder, decompresses it,
pulls out the tracks/routes/waypoints, optionally joins each one against a
Strava-style activities CSV, and produces **two** standalone interactive map pages
(Leaflet.js, OpenStreetMap/CARTO tiles):

- **Heatmap** — every recorded GPS point aggregated into a frequency heatmap.
- **Line Map** — every route drawn as a colored line, weighted/opacity-scaled by how
  many times that stretch of road/trail has been traveled (well-worn routes render
  bold; rarely-traveled ones stay thin but visible).

This mirrors a reference Python project (`heatmapController.py` /
`linemapController.py`), reimplemented in C#.

Both pages default to a **Chicago-centered view**, have a **checkbox per activity
type** (colored to match that type) to show/hide it independently, and a **"Jump
to" control** that pans/zooms the map to one of four preset locations:

| Location | Lat | Lon | Zoom |
|---|---|---|---|
| USA | 38.0 | -94.8 | 5 |
| Chicago (default) | 42.0707 | -87.7368 | 10 |
| Kalamazoo | 42.2 | -85.6 | 11 |
| World | 20.0 | 0.0 | 3 |

### Requirements
- [.NET 8 SDK](https://dotnet.microsoft.com/download) (or later)
- Internet access when a map is open in your browser (loads Leaflet/tiles from
  public CDNs) — no internet needed to run the C# program itself.

### Run it

**Option A — config file (shortest):**
```
cp gpxworldmap.config.example.json gpxworldmap.config.json
# edit gpxworldmap.config.json with your actual paths
dotnet run
```

**Option B — command-line args:**
```
dotnet run -- "path/to/activity-folder" "path/to/activities.csv" heatmap.html linemap.html
```
- First argument: folder containing your .gpx.gz / .tcx.gz / .fit.gz files.
- Remaining arguments can appear in any order: whichever ends in `.csv` is the
  activities CSV; the first non-csv one is the heatmap output path, the second is
  the line map output path.
- Use a config file at a different path with `--config path/to/other.json`.

The program writes both HTML files to disk **and** serves them from a local web
server (an index page at `http://localhost:<port>/` links to both), since
OpenStreetMap's tile servers reject requests with no Referer header — something
browsers never send for pages opened via `file://`.

### Project layout

```
GpxWorldMap/
├── Program.cs                       # entry point -- orchestrates everything else
├── Config/
│   ├── AppConfig.cs                 # resolved settings for one run
│   ├── ConfigFile.cs                # shape of the optional JSON config file
│   └── AppConfigLoader.cs           # loads/merges config file and CLI args
├── Models/
│   ├── ActivityRecord.cs            # one row from the activities CSV
│   ├── ActivityMeta.cs              # name/type/date/description/color for an activity
│   ├── ParsedGpx.cs                 # segments + waypoints extracted from a file
│   ├── ActivityTrack.cs             # one activity's geometry + type/color, shared
│   │                                 by both map outputs
│   └── MapLocation.cs               # a named jump-to location (lat/lon/zoom)
├── Readers/
│   ├── CsvReader.cs                 # reads and parses the activities CSV
│   ├── GzHelper.cs                  # gzip decompression (text or raw bytes)
│   ├── GpxReader.cs                 # parses GPX XML
│   ├── TcxReader.cs                 # parses TCX XML (Garmin Training Center format)
│   └── FitReader.cs                 # parses FIT's binary format
├── Rendering/
│   ├── ActivityColors.cs            # consistent color-per-activity-type logic
│   ├── HeatmapDataBuilder.cs        # aggregates points into weighted heat data per type
│   ├── RouteFrequencyMapBuilder.cs  # grid-snaps routes and scores edges by frequency
│   ├── BookmarkControlScript.cs     # JS for the "jump to location" control
│   ├── ActivityTypeControlScript.cs # JS for the per-type checkbox control
│   ├── HeatmapHtmlBuilder.cs        # assembles the heatmap page
│   └── LineMapHtmlBuilder.cs        # assembles the line map page
├── Server/
│   └── LocalWebServer.cs            # serves both pages locally and opens your browser
└── Shared/
    ├── ActivityId.cs                # shared "extract ID from filename" logic
    └── DefaultLocations.cs          # the four preset jump-to locations
```

### How the two maps are built

**Heatmap** (`HeatmapDataBuilder`): every recorded GPS point is rounded to a fixed
precision and counted, per activity type, the same way the reference Python
`Counter`/`defaultdict(Counter)` does — repeatedly-visited spots naturally end up
"hotter". Each type gets its own `L.heatLayer`, toggled independently by its
checkbox.

**Line Map** (`RouteFrequencyMapBuilder`): a C# port of the reference Python
`linemapController`'s grid-snap + percentile-clip approach. Every activity's points
are snapped to a ~15m grid (collapsing consecutive duplicate cells, which both
smooths GPS jitter and identifies shared segments across activities), then each
grid edge is scored by how many distinct activities *of that type* crossed it.
Weight (1.2–3.5px) and opacity (0.35–0.95) scale with that score, clipped at the
90th percentile so one mega-common commute doesn't wash out the rest of the scale.
Each type gets its own colored polyline layer, toggled independently by its
checkbox.

### Joining GPX files with your activities CSV

Same matching rules as before: the tool reads five columns from your CSV (Activity
ID, Activity Date, Activity Name, Activity Type, Activity Description, Filename),
matching primarily by the numeric ID embedded in the **Filename** column and
falling back to **Activity ID**. Files with no matching row are still plotted,
labeled "Unknown"; CSV rows with no matching file are simply skipped.

### Tweaking it
- Grid size (15m), clip percentile (90th), and weight/opacity ranges for the line
  map are constants at the top of `RouteFrequencyMapBuilder.cs`.
- Heatmap point-rounding precision (6 decimal places) is a parameter on
  `HeatmapDataBuilder.Build(...)`.
- The four jump-to locations are in `Shared/DefaultLocations.cs` — add, remove, or
  edit as needed.
