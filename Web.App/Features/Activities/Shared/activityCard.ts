import './activityCard.css';
import { addRoutePlayerControl } from '../../Controls/RoutePlayer/RoutePlayerControl';
import { addBaseTiles } from '../../Maps/Shared/baseMap';

/** One activity as served by ActivityFeedDataBuilder.cs (feed pages and per-activity files). */
export interface FeedActivity {
  id: string;
  name: string;
  type: string;
  description: string | null;
  /** Shoes are matched against shoes.csv; other gear is shown as named in the CSV. */
  gear: { kind: 'shoe' | 'gear'; name: string; detail: string } | null;
  category: string;
  color: string;
  /** Local date-time ("2024-05-01T07:30:00"), or null when the activity has no date. */
  date: string | null;
  distanceMeters: number;
  movingSeconds: number | null;
  elapsedSeconds: number | null;
  /** Each segment is a flat [lat, lon, lat, lon, ...] array. */
  segments: number[][];
}

export interface ActivityCard {
  card: HTMLElement;
  /** Empty container for the route map; fill it with showActivityMap. */
  mapElement: HTMLElement | null;
}

export interface ActivityMap {
  destroy(): void;
}

export interface ActivityDetailsView {
  card: HTMLElement;
  mapElement: HTMLElement | null;
  showMap(): void;
  hideMap(): void;
  destroy(): void;
}

const metersPerMile = 1609.344;
/** Activity types (besides the Run category) shown with a pace rather than a speed. */
const paceTypes = new Set(['Walk', 'Hike']);

/** Creates the shared activity card and manages its optional route map lifecycle. */
export function createActivityDetailsView(
  activity: FeedActivity,
  tileUrlQuery: string,
  includeMap = true,
): ActivityDetailsView {
  const { card, mapElement } = createActivityCard(activity, includeMap);
  let activityMap: ActivityMap | null = null;

  function hideMap(): void {
    activityMap?.destroy();
    activityMap = null;
  }

  return {
    card,
    mapElement,
    showMap() {
      if (mapElement && !activityMap) activityMap = showActivityMap(mapElement, activity, tileUrlQuery);
    },
    hideMap,
    destroy: hideMap,
  };
}

/** Builds an activity card: title, date, description, stats, gear, and optionally a map container. */
export function createActivityCard(activity: FeedActivity, includeMap = true): ActivityCard {
  const card = document.createElement('article');
  card.className = 'activity-card';
  card.style.setProperty('--activity-color', activity.color);

  const header = document.createElement('header');
  header.className = 'activity-header';
  const badge = document.createElement('span');
  badge.className = 'activity-type-badge';
  badge.style.background = activity.color;
  badge.textContent = activity.type;
  const title = document.createElement('div');
  title.className = 'activity-title';
  const name = document.createElement('h2');
  name.textContent = activity.name;
  const when = document.createElement('div');
  when.className = 'activity-date';
  when.textContent = activity.date ? formatDateTime(new Date(activity.date)) : 'No date';
  title.append(name, when);
  header.append(badge, title);
  card.append(header);

  if (activity.description) {
    const description = document.createElement('p');
    description.className = 'activity-description';
    description.textContent = activity.description;
    card.append(description);
  }

  const stats = document.createElement('dl');
  stats.className = 'activity-stats';
  const miles = activity.distanceMeters / metersPerMile;
  const seconds = activity.movingSeconds ?? activity.elapsedSeconds;
  const usePace = activity.category === 'Run' || paceTypes.has(activity.type);
  stats.append(
    stat('Distance', `${miles.toFixed(2)} mi`),
    stat('Time', seconds ? formatDuration(seconds) : '—'),
    usePace
      ? stat('Pace', seconds && miles >= 0.01 ? `${formatDuration(seconds / miles)} /mi` : '—')
      : stat('Speed', seconds && miles >= 0.01 ? `${(miles / (seconds / 3600)).toFixed(1)} mph` : '—'),
  );
  card.append(stats);
  if (activity.gear) card.append(gearLine(activity.gear));

  let mapElement: HTMLElement | null = null;
  if (includeMap) {
    mapElement = document.createElement('div');
    mapElement.className = 'activity-map';
    mapElement.setAttribute('role', 'img');
    mapElement.setAttribute('aria-label', `Route map of ${activity.name}`);
    card.append(mapElement);
  }

  return { card, mapElement };
}

/** Draws the activity's route (with start/finish markers and a play button) into element. */
export function showActivityMap(element: HTMLElement, activity: FeedActivity, tileUrlQuery: string): ActivityMap {
  const map = L.map(element, {
    zoomControl: false,
    scrollWheelZoom: false,
    // On touch devices one-finger drags should scroll the page, not pan the map.
    dragging: !L.Browser.mobile,
  });
  L.control.zoom({ position: 'bottomright' }).addTo(map);
  addBaseTiles(map, tileUrlQuery);

  const bounds = L.latLngBounds([]);
  const segments: L.LatLngTuple[][] = [];
  const routeLayers: L.Polyline[] = [];
  for (const flat of activity.segments) {
    const latLngs: L.LatLngTuple[] = [];
    for (let i = 0; i + 1 < flat.length; i += 2) latLngs.push([flat[i], flat[i + 1]]);
    if (latLngs.length === 0) continue;
    segments.push(latLngs);
    routeLayers.push(L.polyline(latLngs, { color: activity.color, weight: 3, opacity: 0.9 }).addTo(map));
    latLngs.forEach(p => bounds.extend(p));
  }
  if (segments.length > 0) {
    const lastSegment = segments[segments.length - 1];
    routeMarker(segments[0][0], 'start').addTo(map);
    routeMarker(lastSegment[lastSegment.length - 1], 'finish').addTo(map);
  }

  if (bounds.isValid()) map.fitBounds(bounds, { padding: [20, 20], maxZoom: 16 });
  else map.setView([20, 0], 2);
  const player = addRoutePlayerControl(map, segments, routeLayers, activity.color, playbackDuration(activity));

  return {
    destroy() {
      player.destroy();
      map.remove();
    },
  };
}

function stat(label: string, value: string): HTMLElement {
  const wrapper = document.createElement('div');
  const dt = document.createElement('dt');
  dt.textContent = label;
  const dd = document.createElement('dd');
  dd.textContent = value;
  wrapper.append(dt, dd);
  return wrapper;
}

function gearLine(gear: NonNullable<FeedActivity['gear']>): HTMLElement {
  const line = document.createElement('div');
  line.className = 'activity-gear';
  const label = document.createElement('span');
  label.className = 'gear-label';
  label.textContent = gear.kind === 'shoe' ? 'Shoes' : 'Gear';
  const name = document.createElement('span');
  name.className = 'gear-name';
  name.textContent = gear.name;
  line.append(label, name);
  if (gear.detail) {
    const detail = document.createElement('span');
    detail.className = 'gear-detail';
    detail.textContent = gear.detail;
    line.append(detail);
  }
  return line;
}

function routeMarker(point: L.LatLngTuple, kind: 'start' | 'finish'): L.Marker {
  const size = kind === 'start' ? 18 : 14;
  return L.marker(point, {
    icon: L.divIcon({ className: `route-marker route-${kind}`, iconSize: [size, size] }),
    // The finish sits on top so loops still show it inside the (larger) start ring.
    zIndexOffset: kind === 'finish' ? 1000 : 0,
    keyboard: false,
  }).bindTooltip(kind === 'start' ? 'Start' : 'Finish', { direction: 'top', offset: [0, -size / 2] });
}

/**
 * Playback length: linear up to 5 mi (4–5 s), so short routes stay watchable, then
 * logarithmic (+1.5 s per doubling of distance, capped at 8 s), so long ones don't drag.
 */
function playbackDuration(activity: FeedActivity): number {
  const miles = activity.distanceMeters / metersPerMile;
  if (miles <= 5) return Math.max(4000, 3000 + miles * 400);
  return Math.min(8000, 5000 + 1500 * Math.log2(miles / 5));
}

function formatDateTime(date: Date): string {
  const day = date.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' });
  const time = date.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
  return `${day} · ${time}`;
}

/** 3725 -> "1:02:05", 512 -> "8:32". */
function formatDuration(totalSeconds: number): string {
  const rounded = Math.round(totalSeconds);
  const hours = Math.floor(rounded / 3600);
  const minutes = Math.floor((rounded % 3600) / 60);
  const seconds = rounded % 60;
  const ss = String(seconds).padStart(2, '0');
  return hours > 0 ? `${hours}:${String(minutes).padStart(2, '0')}:${ss}` : `${minutes}:${ss}`;
}
