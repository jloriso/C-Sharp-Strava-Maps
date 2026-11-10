## GpxWorldMap

GpxWorldMap reads `.gpx.gz`, `.tcx.gz`, and `.fit.gz` activity files, optionally
matches them with a Strava activities CSV, writes generated HTML pages to disk, and
serves an interactive local site. The root page is a shared index shell: it loads
the app navigation once and displays one content page at a time.

### Pages and controls

- **Weekly Mileage** shows separate, vertically stacked Run, Bike, and Other charts.
  Each chart has its own date slider and Start, Older, Newer, and Latest buttons.
  The chart window shows up to 26 weeks; Older/Newer move it by 12 weeks.
- **Maps** shows one map at a time. Its in-map toggle switches between Heatmap and
  Line Map while retaining the timeline position, map center, and zoom.
- **Heatmap** displays activity points by category. Dated activities are revealed
  chronologically; undated activities remain as a baseline. Start/Older sit to the
  left of the slider and Newer/Latest to the right.
- **Line Map** shows route activity in the current viewport. Its chronological
  timeline uses the same controls. Panning or zooming recalculates which revealed
  routes intersect the viewport.

Both maps include category visibility checkboxes, a Home button, and a Jump to
control for these preset views:

| Location | Latitude | Longitude | Zoom |
|---|---:|---:|---:|
| Chicago (default) | 41.9 | -87.64 | 11 |
| Kalamazoo | 42.2 | -85.6 | 12 |
| USA | 38.0 | -94.8 | 5 |
| World | 20.0 | 0.0 | 3 |

### Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or later.
- Internet access in the browser to load Leaflet, Chart.js, and map tiles from
  public CDNs.

### Run

Copy `gpxworldmap.config.example.json` to `gpxworldmap.config.json`, set the input
paths, then run:

```sh
dotnet run
```

Alternatively, pass positional arguments:

```sh
dotnet run -- "path/to/activity-folder" "path/to/activities.csv" \
  heatmap.html linemap.html weekly-mileage.html
```

The first argument is the activity folder. Later arguments can include a CSV path
in any position; the other positional values set heatmap, line-map, and weekly
mileage output paths, in that order. Use `--config path/to/other.json` to load a
configuration file from another location. Positional arguments override values
from the config file.

The app writes the three generated content pages to their configured paths and
serves those pages plus the Maps page locally. Open the root URL printed by the
program (for example, `http://localhost:<port>/`). Serving over HTTP is needed for
map tiles; browser requests from `file://` pages do not include the expected
Referer header.

### Configuration

The configuration fields are:

| Field | Purpose |
|---|---|
| `GpxFolder` | Folder containing activity files; searches this folder only, not subfolders. |
| `CsvFile` | Optional activities CSV used to match metadata and dates. |
| `OutputHeatmapHtml` | Heatmap output path. |
| `OutputLineMapHtml` | Line-map output path. |
| `OutputWeeklyMileageHtml` | Weekly mileage output path. |
| `CartoApiKey` | Optional CARTO tile API key. |

Output paths default to `heatmaps/standard_heatmap.html`, `heatmaps/linemap.html`,
and `heatmaps/weekly_mileage.html`. The local server routes each generated page by
its filename, while the root index shell remains the application entry point.

### Activity data and maps

The optional CSV recognizes these headers (case-insensitive): `Activity ID`,
`Activity Date`, `Activity Name`, `Activity Type`, `Activity Description`, and
`Filename`. Matching uses the numeric ID in `Filename` first and falls back to
`Activity ID`. Missing or unparseable dates are omitted from the mileage chart and
chronological map timeline; if no dates are available, maps remain in their static
all-activity view.

The heatmap rounds coordinates to six decimal places and weights repeated points
for contrast. The line map snaps routes to a 25-meter grid, counts distinct
activities on each edge, and combines adjacent edges into lines with one of six
frequency tiers. Route layers are created and drawn only when an activity is
revealed in the timeline and its route bounds intersect the current map bounds.

### Project layout

```text
Program.cs                                  # input processing, data building, page routing
Config/                                     # config models and CLI/config loading
FileReader/FileTypeReaders/                 # CSV, GPX, TCX, FIT, and gzip readers
Models/                                     # activity metadata, tracks, parsed geometry
Shared/                                     # activity IDs, categories, distance, locations
Rendering/                                  # HTML template loader and map rendering helpers
Server/                                     # local HTTP server and browser launch
Index/index.html                            # root shell with one app navigation
Web.Features/Controls/                      # injected navigation and Leaflet controls
Web.Features/Controls/Templates/            # control JavaScript templates
Web.Features/Charts/WeeklyMileage/          # mileage aggregation, chart builder, template
Web.Features/Maps/Heatmap/                  # chronological heatmap data and page
Web.Features/Maps/Linemap/                  # chronological route data and page
Web.Features/Maps/MapSwitcher/              # map toggle wrapper page
```

The controls under `Web.Features/Controls` own their markup and JavaScript templates;
page builders inject them where needed. The index shell injects app navigation once,
instead of duplicating it on every content page.

### Tuning

- Edit the preset views in `Shared/DefaultLocations.cs`.
- Change heatmap rounding and weight clipping in `HeatmapDataBuilder.Build(...)`.
- Change line-map grid size and frequency tiers in `LineMapDataBuilder.cs`.
- Change the mileage chart window and navigation step in
  `Web.Features/Charts/WeeklyMileage/weeklymileage.html`.
