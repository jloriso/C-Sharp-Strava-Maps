## GpxWorldMap

GpxWorldMap reads `.gpx.gz`, `.tcx.gz`, and `.fit.gz` activity files, optionally
matches them with a Strava activities CSV, writes generated HTML pages to disk, and
serves an interactive local site. The root page is a shared index shell: it loads
the app navigation once and displays one content page at a time.

### Pages and controls

- **Weekly Mileage** shows separate, vertically stacked Run, Bike, and Other charts.
  Each chart has its own date slider and Start, Older, Newer, and Latest buttons.
  The chart window shows up to 26 weeks; Older/Newer move it by 12 weeks.
- **Maps** shows one map at a time, opening on the Line Map. Its Line Map / Heatmap
  toggle is the only way to reach the two maps (they are not in the top navigation);
  switching retains the timeline date, activity type selection, map center, and zoom.
  The current map stays visible until the other has loaded and rendered; both stay
  loaded afterwards, so switching back is immediate. On first load a spinning globe
  is shown until the map has rendered, then the map and toggle appear together.
- **Heatmap** displays activity points by category. It opens at Latest with every
  activity shown; the timeline steps back through dated activities chronologically,
  and undated activities remain as a baseline. Start/Older sit to the
  left of the slider and Newer/Latest to the right.
- **Line Map** shows route activity in the current viewport, also opening at Latest
  with all routes drawn. Its chronological timeline uses the same controls. Panning
  or zooming recalculates which revealed routes intersect the viewport.
- **Activities** is a feed of every activity with a route, newest first, grouped
  under month headings. Each card shows the activity description, the route on its
  own line map (a green start dot and a checkered finish dot), the distance, the time
  (the CSV's Moving Time, falling back to Elapsed Time), the pace (runs, walks, and
  hikes) or speed (everything else), and the shoes or other gear used. Shoes come
  from the Activity Gear column, matched to `shoes.csv` in the same folder as the
  activities CSV to show the shoe's name, brand, and model. The play button in each
  map's bottom-left corner draws the route out from start to finish (pause and replay
  as needed).
  Scrolling down keeps loading older activities until the very first one. The
  activities are fetched 20 at a time from `activity-feed/page-N.json`, and each card's map
  only exists while it is near the screen.

Both maps include category visibility checkboxes, a Home button, and a Jump to
control for these preset views (a view is highlighted only while the map is still
showing it):

| Location | Latitude | Longitude | Zoom |
|---|---:|---:|---:|
| Chicago (default) | 41.9 | -87.64 | 11 |
| Kalamazoo | 42.2 | -85.6 | 12 |
| USA | 38.0 | -94.8 | 5 |
| World | 20.0 | 0.0 | 3 |

### Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or later.
- [Node.js](https://nodejs.org) 20 or later (with npm). Building `Web.App` runs
  `npm ci` (first build only) and `npm run build` automatically to compile the
  TypeScript pages.
- Internet access in the browser to load Leaflet, Chart.js, and map tiles from
  public CDNs.

### Run

Copy `gpxworldmap.config.example.json` to `gpxworldmap.config.json`, set the input
paths, then run:

```sh
dotnet run --project Web.App
```

Alternatively, pass positional arguments:

```sh
dotnet run --project Web.App -- "path/to/activity-folder" "path/to/activities.csv" \
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
GpxWorldMap.sln                             # solution containing every project below
Directory.Build.props                       # shared target framework / nullable settings
Web.App/Program.cs                          # entry point: input processing, data building, page routing
Api.Config/                                 # config models and CLI/config loading
Api.FileReader/FileTypeReaders/             # CSV, GPX, TCX, FIT, and gzip readers
Api.Models/                                 # activity metadata, tracks, parsed geometry
Api.Shared/                                 # activity IDs, categories, distance, locations
Web.Rendering/                              # HTML template loader and map rendering helpers
Web.Server/                                 # local HTTP server and browser launch
Web.App/package.json, tsconfig.json         # frontend (TypeScript + Vite) build
Web.App/vite.config.ts                      # shared Vite config (single-file HTML output)
Web.App/scripts/build-pages.mjs             # builds each page into Web.App/dist/
Web.App/Index/                              # root shell page with one app navigation
Web.App/Features/Shared/                    # shared TS types, page-data reader, iframe messages
Web.App/Features/Controls/<Name>/           # one folder per control (<Name>Control.ts + .css)
Web.App/Features/Charts/WeeklyMileage/      # mileage aggregation, chart builder, page
Web.App/Features/Maps/Shared/               # base map setup and shared map settings
Web.App/Features/Maps/Heatmap/              # chronological heatmap data and page
Web.App/Features/Maps/Linemap/              # chronological route data and page
Web.App/Features/Maps/MapSwitcher/          # map toggle wrapper page
Web.App/Features/Activities/ActivityFeed/   # paged per-activity feed data and page
```

Each page is an `.html` + `.ts` (+ `.css`) next to its C# `*HtmlBuilder`. The C#
builder only serializes data into the page's
`<script id="page-data" type="application/json">{{PAGE_DATA_JSON}}</script>` block
(via `PageDataBuilder`); all markup and behavior live in TypeScript. Controls are
typed modules that pages import, e.g. `addBookmarkControl(map, locations, ...)`.

### Frontend development

Building `Web.App` (or the solution) compiles the frontend automatically. To work on
the TypeScript directly, run these from `Web.App/`:

```sh
npm install          # first time only
npm run typecheck    # tsc --noEmit
npm run build        # type check + build every page into dist/
```

To add a page, create its `.html`/`.ts` and add it to the `pages` list in
`scripts/build-pages.mjs`. Pass `-p:SkipClientBuild=true` to `dotnet build` to skip
the npm step.

### Tuning

- Edit the preset views in `Api.Shared/DefaultLocations.cs`.
- Change heatmap rounding and weight clipping in `HeatmapDataBuilder.Build(...)`.
- Change line-map grid size and frequency tiers in `LineMapDataBuilder.cs`.
- Change the mileage chart window and navigation step in
  `Web.App/Features/Charts/WeeklyMileage/weeklymileage.ts`.
- Change the activity feed page size and route simplification tolerance in
  `ActivityFeedDataBuilder.cs`.
