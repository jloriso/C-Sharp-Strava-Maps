import type { HeatLatLngTuple, HeatLayer, HeatMapOptions } from 'leaflet';
import '../Shared/mapPage.css';
import { addActivityTypeControl } from '../../Controls/ActivityType/ActivityTypeControl';
import { addBookmarkControl } from '../../Controls/Bookmark/BookmarkControl';
import { addHomeControl } from '../../Controls/Home/HomeControl';
import { createTimelineControl } from '../../Controls/Timeline/TimelineControl';
import { readPageData } from '../../Shared/pageData';
import { getElement } from '../../Shared/types';
import { createBaseMap, type MapPageSettings } from '../Shared/baseMap';
import { loadMapData } from '../Shared/mapData';
import { listenForMapState } from '../Shared/mapState';

type HeatPoint = HeatLatLngTuple;

interface HeatmapActivity {
  date: string;
  type: string;
  points: HeatPoint[];
}

/** Mirrors HeatmapHtmlBuilder.BuildHtml. */
interface HeatmapPageData {
  /** URL of the HeatmapData file, fetched once the map has been created. */
  dataUrl: string;
  settings: MapPageSettings;
}

/** Mirrors HeatmapDataBuilder.cs (Api.Processing). */
interface HeatmapData {
  undatedPointsByType: Record<string, HeatPoint[]>;
  timelineActivities: HeatmapActivity[];
}

const heatOptions: HeatMapOptions = {
  radius: 8,
  blur: 10,
  maxZoom: 15,
  minOpacity: 0.3,
  gradient: { 0.2: 'blue', 0.4: 'lime', 0.6: 'orange', 0.85: 'red' },
};

const { dataUrl, settings } = readPageData<HeatmapPageData>();
const { map, baseLayerControl } = createBaseMap(settings);
addBookmarkControl(map, settings.locations);
addHomeControl(map, settings.homeUrl);

void loadMapData<HeatmapData>(dataUrl).then(showActivities);

function showActivities({ undatedPointsByType, timelineActivities }: HeatmapData): void {
  // Layers stay on the map and hidden types get no points: leaflet.heat 0.2.0 throws if a layer
  // is removed while a redraw is pending, or if setLatLngs is called while it is off the map.
  const allTypes = new Set([...Object.keys(undatedPointsByType), ...timelineActivities.map(a => a.type)]);
  const heatLayersByType = new Map<string, HeatLayer>();
  for (const type of allTypes) heatLayersByType.set(type, L.heatLayer([], heatOptions).addTo(map));
  const hiddenTypes = new Set<string>();

  // Each type's points: its undated points, then the points of every revealed timeline activity in
  // order. Moving the timeline appends to or truncates these arrays instead of rebuilding them.
  const pointsByType = new Map<string, HeatPoint[]>();
  for (const type of allTypes) pointsByType.set(type, (undatedPointsByType[type] ?? []).slice());
  /** Length of each activity's type array just before that activity's points were appended. */
  const startIndex = new Array<number>(timelineActivities.length);
  let visibleCount = 0;

  const drawType = (type: string) =>
    heatLayersByType.get(type)?.setLatLngs(hiddenTypes.has(type) ? [] : pointsByType.get(type) ?? []);

  function renderTimeline(count: number): void {
    const changedTypes = new Set<string>();
    if (count > visibleCount) {
      for (let i = visibleCount; i < count; i++) {
        const activity = timelineActivities[i];
        const points = pointsByType.get(activity.type)!;
        startIndex[i] = points.length;
        for (const point of activity.points) points.push(point);
        changedTypes.add(activity.type);
      }
    } else {
      // Walking backwards leaves each type truncated to where its earliest hidden activity began.
      for (let i = visibleCount - 1; i >= count; i--) {
        const activity = timelineActivities[i];
        pointsByType.get(activity.type)!.length = startIndex[i];
        changedTypes.add(activity.type);
      }
    }
    visibleCount = count;
    changedTypes.forEach(drawType);
  }

  heatLayersByType.forEach((_, type) => drawType(type));
  const activityTypeControl = addActivityTypeControl(map, settings.activityTypes, (type, visible) => {
    if (visible) hiddenTypes.delete(type);
    else hiddenTypes.add(type);
    drawType(type);
  });
  const timeline = createTimelineControl(getElement('map'), timelineActivities, renderTimeline);
  map.invalidateSize(); // the timeline bar, added below the map, can shrink it
  listenForMapState(map, timeline, activityTypeControl, baseLayerControl);
}
