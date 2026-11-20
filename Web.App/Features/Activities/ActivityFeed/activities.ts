import './activities.css';
import { addBaseTiles } from '../../Maps/Shared/baseMap';
import { readPageData } from '../../Shared/pageData';
import { getElement } from '../../Shared/types';

/** Mirrors ActivityFeedHtmlBuilder.cs. */
interface ActivityFeedPageData {
  totalActivities: number;
  /** JSON pages of activities, newest first. */
  pageUrls: string[];
  tileUrlQuery: string;
}

/** Mirrors ActivityFeedDataBuilder.cs. */
interface FeedActivity {
  id: string;
  name: string;
  type: string;
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

interface FeedPage {
  activities: FeedActivity[];
}

const metersPerMile = 1609.344;
/** Activity types (besides the Run category) shown with a pace rather than a speed. */
const paceTypes = new Set(['Walk', 'Hike']);
/** How far beyond the viewport to fetch more activities / keep card maps alive. */
const loadAheadPx = 1200;
const mapMarginPx = 600;

const data = readPageData<ActivityFeedPageData>();
const feed = getElement('feed');
const status = getElement('feed-status');
const sentinel = getElement('feed-sentinel');
getElement('feed-count').textContent =
  `${data.totalActivities.toLocaleString()} ${data.totalActivities === 1 ? 'activity' : 'activities'}`;

let nextPage = 0;
let loading = false;
let lastMonth: string | null | undefined;

// Card maps are only created near the viewport and removed once far away, so scrolling
// through hundreds of activities doesn't keep hundreds of Leaflet maps alive.
const cardActivities = new Map<Element, FeedActivity>();
const cardMaps = new Map<Element, L.Map>();
const mapObserver = new IntersectionObserver(entries => {
  for (const entry of entries) {
    if (entry.isIntersecting) showMap(entry.target);
    else hideMap(entry.target);
  }
}, { rootMargin: `${mapMarginPx}px 0px` });

new IntersectionObserver(entries => {
  if (entries.some(e => e.isIntersecting)) void loadMore();
}, { rootMargin: `0px 0px ${loadAheadPx}px 0px` }).observe(sentinel);

async function loadMore(): Promise<void> {
  if (loading || nextPage >= data.pageUrls.length) return;
  loading = true;
  setStatus('loading');
  try {
    const response = await fetch(data.pageUrls[nextPage]);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const page = await response.json() as FeedPage;
    page.activities.forEach(appendCard);
    nextPage++;
    loading = false;
    setStatus(nextPage >= data.pageUrls.length ? 'done' : 'idle');
    // The sentinel observer only fires on changes, so keep filling while it's still in range.
    if (sentinel.getBoundingClientRect().top < window.innerHeight + loadAheadPx) void loadMore();
  } catch (error) {
    loading = false;
    console.error('Failed to load activities', error);
    setStatus('error');
  }
}

function setStatus(state: 'idle' | 'loading' | 'done' | 'error'): void {
  status.replaceChildren();
  if (state === 'loading') {
    const spinner = document.createElement('span');
    spinner.className = 'feed-spinner';
    status.append(spinner, nextPage === 0 ? 'Loading activities…' : 'Loading older activities…');
  } else if (state === 'done') {
    status.textContent = data.totalActivities === 0 ? 'No activities with a route were found.' : "That's every activity.";
  } else if (state === 'error') {
    const retry = document.createElement('button');
    retry.type = 'button';
    retry.className = 'feed-retry';
    retry.textContent = 'Retry';
    retry.addEventListener('click', () => void loadMore());
    status.append("Couldn't load more activities.", retry);
  }
}

function appendCard(activity: FeedActivity): void {
  const date = activity.date ? new Date(activity.date) : null;
  const month = date ? date.toLocaleDateString('en-US', { month: 'long', year: 'numeric' }) : null;
  if (month !== lastMonth) {
    const heading = document.createElement('h3');
    heading.className = 'month-heading';
    heading.textContent = month ?? 'Undated';
    feed.append(heading);
    lastMonth = month;
  }

  const card = document.createElement('article');
  card.className = 'activity-card';

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
  when.textContent = date ? formatDateTime(date) : 'No date';
  title.append(name, when);
  header.append(badge, title);

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

  const mapElement = document.createElement('div');
  mapElement.className = 'activity-map';
  mapElement.setAttribute('role', 'img');
  mapElement.setAttribute('aria-label', `Route map of ${activity.name}`);

  card.append(header, stats, mapElement);
  feed.append(card);
  cardActivities.set(mapElement, activity);
  mapObserver.observe(mapElement);
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

function showMap(element: Element): void {
  const activity = cardActivities.get(element);
  if (!activity || cardMaps.has(element)) return;

  const map = L.map(element as HTMLElement, {
    zoomControl: false,
    scrollWheelZoom: false,
    // On touch devices one-finger drags should scroll the feed, not pan the map.
    dragging: !L.Browser.mobile,
  });
  L.control.zoom({ position: 'bottomright' }).addTo(map);
  addBaseTiles(map, data.tileUrlQuery);

  const bounds = L.latLngBounds([]);
  let first: L.LatLngTuple | undefined;
  let last: L.LatLngTuple | undefined;
  for (const flat of activity.segments) {
    const latLngs: L.LatLngTuple[] = [];
    for (let i = 0; i + 1 < flat.length; i += 2) latLngs.push([flat[i], flat[i + 1]]);
    if (latLngs.length === 0) continue;
    L.polyline(latLngs, { color: activity.color, weight: 3, opacity: 0.9 }).addTo(map);
    latLngs.forEach(p => bounds.extend(p));
    first ??= latLngs[0];
    last = latLngs[latLngs.length - 1];
  }
  if (first && last) {
    L.circleMarker(first, { radius: 5, color: 'white', weight: 2, fillColor: '#2e9e44', fillOpacity: 1 })
      .bindTooltip('Start').addTo(map);
    L.circleMarker(last, { radius: 5, color: 'white', weight: 2, fillColor: '#d62d20', fillOpacity: 1 })
      .bindTooltip('Finish').addTo(map);
  }

  if (bounds.isValid()) map.fitBounds(bounds, { padding: [20, 20], maxZoom: 16 });
  else map.setView([20, 0], 2);
  cardMaps.set(element, map);
}

function hideMap(element: Element): void {
  cardMaps.get(element)?.remove();
  cardMaps.delete(element);
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

if (data.pageUrls.length === 0) setStatus('done');
else void loadMore();
