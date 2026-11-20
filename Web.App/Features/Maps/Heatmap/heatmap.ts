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

const allTypes = new Set([...Object.keys(undatedPointsByType), ...timelineActivities.map(a => a.type)]);
const heatLayersByType = new Map<string, HeatLayer>();
for (const type of allTypes) heatLayersByType.set(type, L.heatLayer([], heatOptions).addTo(map));

function renderTimeline(activityCount: number): void {
  const pointsByType = new Map<string, HeatPoint[]>();
  for (const [type, points] of Object.entries(undatedPointsByType)) pointsByType.set(type, points.slice());
  for (const activity of timelineActivities.slice(0, activityCount)) {
    const points = pointsByType.get(activity.type) ?? [];
    points.push(...activity.points);
    pointsByType.set(activity.type, points);
  }
  for (const [type, layer] of heatLayersByType) layer.setLatLngs(pointsByType.get(type) ?? []);
}

addBookmarkControl(map, settings.locations, settings.defaultLocation);
addActivityTypeControl(map, settings.activityTypes, (type, visible) => {
  const layer = heatLayersByType.get(type);
  if (!layer) return;
  if (visible) map.addLayer(layer);
  else map.removeLayer(layer);
});
addHomeControl(map, settings.homeUrl);
const timeline = createTimelineControl(getElement('map'), timelineActivities, renderTimeline);
listenForMapState(map, timeline);
