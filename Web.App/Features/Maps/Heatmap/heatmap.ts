import type { HeatLatLngTuple, HeatLayer, HeatMapOptions } from 'leaflet';
import '../Shared/mapPage.css';
import { addActivityTypeControl } from '../../Controls/ActivityType/ActivityTypeControl';
import { addBookmarkControl } from '../../Controls/Bookmark/BookmarkControl';
import { addHomeControl } from '../../Controls/Home/HomeControl';
import { createTimelineControl } from '../../Controls/Timeline/TimelineControl';
import { readPageData } from '../../Shared/pageData';
import { getElement } from '../../Shared/types';
import { createBaseMap, type MapPageSettings } from '../Shared/baseMap';
import { listenForMapState } from '../Shared/mapState';

type HeatPoint = HeatLatLngTuple;

interface HeatmapActivity {
  date: string;
  type: string;
  points: HeatPoint[];
}

/** Mirrors HeatmapHtmlBuilder.cs / HeatmapDataBuilder.cs. */
interface HeatmapPageData {
  undatedPointsByType: Record<string, HeatPoint[]>;
  timelineActivities: HeatmapActivity[];
  settings: MapPageSettings;
}

const { undatedPointsByType, timelineActivities, settings } = readPageData<HeatmapPageData>();
const map = createBaseMap(settings);

const heatOptions: HeatMapOptions = {
  radius: 8,
  blur: 10,
  maxZoom: 15,
  minOpacity: 0.3,
  gradient: { 0.2: 'blue', 0.4: 'lime', 0.6: 'orange', 0.85: 'red' },
};

// Layers stay on the map and hidden types get no points: leaflet.heat 0.2.0 throws if a layer
// is removed while a redraw is pending, or if setLatLngs is called while it is off the map.
const allTypes = new Set([...Object.keys(undatedPointsByType), ...timelineActivities.map(a => a.type)]);
const heatLayersByType = new Map<string, HeatLayer>();
for (const type of allTypes) heatLayersByType.set(type, L.heatLayer([], heatOptions).addTo(map));
const hiddenTypes = new Set<string>();
let pointsByType = new Map<string, HeatPoint[]>();

function drawType(type: string): void {
  heatLayersByType.get(type)?.setLatLngs(hiddenTypes.has(type) ? [] : pointsByType.get(type) ?? []);
}

function renderTimeline(activityCount: number): void {
  pointsByType = new Map<string, HeatPoint[]>();
  for (const [type, points] of Object.entries(undatedPointsByType)) pointsByType.set(type, points.slice());
  for (const activity of timelineActivities.slice(0, activityCount)) {
    const points = pointsByType.get(activity.type) ?? [];
    points.push(...activity.points);
    pointsByType.set(activity.type, points);
  }
  heatLayersByType.forEach((_, type) => drawType(type));
}

addBookmarkControl(map, settings.locations);
const activityTypeControl = addActivityTypeControl(map, settings.activityTypes, (type, visible) => {
  if (visible) hiddenTypes.delete(type);
  else hiddenTypes.add(type);
  drawType(type);
});
addHomeControl(map, settings.homeUrl);
const timeline = createTimelineControl(getElement('map'), timelineActivities, renderTimeline);
listenForMapState(map, timeline, activityTypeControl);
