import type { FeatureCollection, LineString } from 'geojson';
import '../Shared/mapPage.css';
import { addActivityTypeControl } from '../../Controls/ActivityType/ActivityTypeControl';
import { createActivityDetailsControl } from '../../Controls/ActivityDetails/ActivityDetailsControl';
import { addBookmarkControl } from '../../Controls/Bookmark/BookmarkControl';
import { addHomeControl } from '../../Controls/Home/HomeControl';
import { createTimelineControl } from '../../Controls/Timeline/TimelineControl';
import { readPageData } from '../../Shared/pageData';
import { getElement, toTypeColorLookup } from '../../Shared/types';
import { createBaseMap, type MapPageSettings } from '../Shared/baseMap';
import { listenForMapState } from '../Shared/mapState';

interface RouteProperties {
  minCount: number;
  maxCount: number;
  weight: number;
  opacity: number;
}

interface LineActivity {
  id: string;
  date: string | null;
  type: string;
  /** [south, west, north, east] */
  bounds: [number, number, number, number];
  segments: L.LatLngExpression[][];
}

interface DatedLineActivity extends LineActivity {
  date: string;
}

/** Mirrors LineMapHtmlBuilder.cs / LineMapDataBuilder.cs. */
interface LineMapPageData {
  /** Only populated when no activities are dated (static, frequency-weighted routes). */
  lineDataByType: Record<string, FeatureCollection<LineString, RouteProperties>>;
  undatedActivities: LineActivity[];
  timelineActivities: DatedLineActivity[];
  /** Served URL of one activity's details JSON, with an "{id}" placeholder. */
  activityDetailsUrl: string;
  settings: MapPageSettings;
}

const { lineDataByType, undatedActivities, timelineActivities, activityDetailsUrl, settings } = readPageData<LineMapPageData>();
const map = createBaseMap(settings);
const typeColors = toTypeColorLookup(settings.activityTypes);
const sharedRenderer = L.canvas({ padding: 0.5 });
const hasTimeline = timelineActivities.length > 0;
const activityDetails = createActivityDetailsControl(activityDetailsUrl, settings.tileUrlQuery);

const visibleTypes = new Set(settings.activityTypes.map(t => t.type));
const staticLayersByType = new Map<string, L.GeoJSON>();
const activityLayers = new Map<LineActivity, L.Polyline>();
let visibleActivityCount = 0;

function addStaticLineLayers(): void {
  for (const [type, data] of Object.entries(lineDataByType)) {
    const color = typeColors[type] ?? '#000000';
    const layer = L.geoJSON(data, {
      renderer: sharedRenderer,
      style: feature => ({
        color,
        weight: feature?.properties.weight,
        opacity: feature?.properties.opacity,
        lineCap: 'round',
        smoothFactor: 1.5,
      }),
    } as L.GeoJSONOptions<RouteProperties>).addTo(map);

    layer.on('click', (e: L.LeafletMouseEvent) => {
      const clicked = e.propagatedFrom as L.Polyline & { feature: { properties: RouteProperties } };
      const { minCount, maxCount } = clicked.feature.properties;
      const times = minCount === maxCount
        ? `traveled ${minCount} time${minCount === 1 ? '' : 's'}`
        : `traveled ${minCount}-${maxCount} times`;
      clicked.bindPopup(`${type} &middot; ${times}`).openPopup(e.latlng);
    });

    staticLayersByType.set(type, layer);
  }
}

function syncActivityLayer(activity: LineActivity): void {
  const [south, west, north, east] = activity.bounds;
  const bounds = L.latLngBounds([south, west], [north, east]);
  const shouldShow = visibleTypes.has(activity.type) && bounds.intersects(map.getBounds());

  let layer = activityLayers.get(activity);
  if (shouldShow && !layer) {
    const created = L.polyline(activity.segments, {
      renderer: sharedRenderer,
      color: typeColors[activity.type] ?? '#000000',
      weight: 2,
      opacity: 0.62,
      lineCap: 'round',
      smoothFactor: 1.5,
    });
    created.on('click', (e: L.LeafletMouseEvent) => {
      created.bindPopup(() => activityPopup(activity)).openPopup(e.latlng);
    });
    activityLayers.set(activity, created);
    layer = created;
  }

  if (!layer) return;
  if (shouldShow && !map.hasLayer(layer)) map.addLayer(layer);
  else if (!shouldShow && map.hasLayer(layer)) map.removeLayer(layer);
}

/** "Run · 2024-05-01" plus a link that opens the activity's full details. */
function activityPopup(activity: LineActivity): HTMLElement {
  const content = document.createElement('div');
  content.append(`${activity.type} · ${activity.date ?? 'Undated'}`);
  const details = document.createElement('button');
  details.type = 'button';
  details.className = 'activity-popup-details';
  details.textContent = 'View activity details';
  details.addEventListener('click', () => {
    map.closePopup();
    activityDetails.open(activity.id);
  });
  content.append(details);
  return content;
}

function syncVisibleActivities(): void {
  undatedActivities.forEach(syncActivityLayer);
  timelineActivities.slice(0, visibleActivityCount).forEach(syncActivityLayer);
}

function renderTimeline(count: number): void {
  if (count < visibleActivityCount) {
    for (const activity of timelineActivities.slice(count, visibleActivityCount)) {
      const layer = activityLayers.get(activity);
      if (layer && map.hasLayer(layer)) map.removeLayer(layer);
    }
  } else {
    timelineActivities.slice(visibleActivityCount, count).forEach(syncActivityLayer);
  }
  visibleActivityCount = count;
}

if (hasTimeline) {
  undatedActivities.forEach(syncActivityLayer);
  map.on('moveend', syncVisibleActivities);
} else {
  addStaticLineLayers();
}

addBookmarkControl(map, settings.locations);
const activityTypeControl = addActivityTypeControl(map, settings.activityTypes, (type, visible) => {
  if (visible) visibleTypes.add(type);
  else visibleTypes.delete(type);

  if (hasTimeline) {
    syncVisibleActivities();
    return;
  }
  const layer = staticLayersByType.get(type);
  if (!layer) return;
  if (visible) map.addLayer(layer);
  else map.removeLayer(layer);
});
addHomeControl(map, settings.homeUrl);
const timeline = createTimelineControl(getElement('map'), timelineActivities, renderTimeline);
listenForMapState(map, timeline, activityTypeControl);
