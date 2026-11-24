import './maps.css';
import { createMapTypeControl } from '../../Controls/MapType/MapTypeControl';
import { isValidLatLng, postToFrame, type MapPageMessage, type SetMapStateMessage } from '../../Shared/messages';
import { readPageData } from '../../Shared/pageData';
import { getElement, isBaseLayerId, type MapKind } from '../../Shared/types';

/** Mirrors MapSwitcherHtmlBuilder.cs. */
interface MapSwitcherPageData {
  heatmapHref: string;
  linemapHref: string;
}

interface MapFrame {
  kind: MapKind;
  iframe: HTMLIFrameElement;
  ready: boolean;
}

/** Fallback in case a map page never confirms it applied the state. */
const revealTimeoutMs = 5000;
/** Fallback in case the first map never reports that it is ready. */
const loadingTimeoutMs = 15000;

const data = readPageData<MapSwitcherPageData>();
const stage = getElement('map-stage');
const mapUrls: Record<MapKind, string> = { heatmap: data.heatmapHref, linemap: data.linemapHref };
const mapTitles: Record<MapKind, string> = { heatmap: 'Heatmap', linemap: 'Line Map' };

/** Timeline, view, activity type, and basemap selection carried over when switching maps. */
const state: Omit<SetMapStateMessage, 'type' | 'requestId'> =
  { count: 0, date: null, center: null, zoom: null, hiddenTypes: [], baseLayer: null };
const frames = new Map<MapKind, MapFrame>();
let activeKind: MapKind = 'linemap';
let targetKind: MapKind | null = null;
let requestId = 0;
let revealTimer: ReturnType<typeof setTimeout> | undefined;

/** Shows the page (map, toggle) all at once and removes the globe loader from maps.html. */
function finishLoading(): void {
  clearTimeout(loadingTimer);
  document.body.classList.remove('is-loading');
  document.getElementById('page-loader')?.remove();
}
const loadingTimer = setTimeout(finishLoading, loadingTimeoutMs);

function createFrame(kind: MapKind, visible: boolean): MapFrame {
  const iframe = document.createElement('iframe');
  iframe.className = 'map-frame';
  iframe.title = mapTitles[kind];
  iframe.src = mapUrls[kind];
  setFrameVisible(iframe, visible);
  stage.prepend(iframe);
  const frame = { kind, iframe, ready: false };
  frames.set(kind, frame);
  return frame;
}

function setFrameVisible(iframe: HTMLIFrameElement, visible: boolean): void {
  iframe.classList.toggle('is-hidden', !visible);
  if (visible) iframe.removeAttribute('aria-hidden');
  else iframe.setAttribute('aria-hidden', 'true');
}

/**
 * Switches maps without a blank screen: the current map stays visible while the target loads
 * (in a hidden iframe), applies the current timeline position and view, and renders.
 * Loaded maps are kept, so switching back is immediate.
 */
function selectMap(kind: MapKind): void {
  mapTypeControl.setActive(kind);
  clearTimeout(revealTimer);
  if (kind === activeKind) {
    targetKind = null;
    return;
  }
  targetKind = kind;
  const frame = frames.get(kind) ?? createFrame(kind, false);
  if (frame.ready) syncTarget(frame);
}

function syncTarget(frame: MapFrame): void {
  requestId++;
  postToFrame(frame.iframe, { type: 'gpxworldmap-set-map-state', requestId, ...state });
  clearTimeout(revealTimer);
  revealTimer = setTimeout(() => reveal(frame.kind), revealTimeoutMs);
}

function reveal(kind: MapKind): void {
  if (targetKind !== kind) return;
  clearTimeout(revealTimer);
  for (const frame of frames.values()) setFrameVisible(frame.iframe, frame.kind === kind);
  activeKind = kind;
  targetKind = null;
}

window.addEventListener('message', (event: MessageEvent<MapPageMessage>) => {
  if (event.origin !== window.location.origin || !event.data) return;
  const frame = [...frames.values()].find(f => f.iframe.contentWindow === event.source);
  if (!frame) return;
  const message = event.data;

  switch (message.type) {
    case 'gpxworldmap-map-ready':
      frame.ready = true;
      if (frame.kind === activeKind) finishLoading();
      if (frame.kind === targetKind) syncTarget(frame);
      return;
    case 'gpxworldmap-map-state-applied':
      if (frame.kind === targetKind && message.requestId === requestId) reveal(frame.kind);
      return;
  }

  // Position and view updates only come from the visible map.
  if (frame.kind !== activeKind) return;
  if (message.type === 'gpxworldmap-timeline-position') {
    state.count = Math.max(0, Number(message.count) || 0);
    state.date = typeof message.date === 'string' ? message.date : null;
  } else if (message.type === 'gpxworldmap-map-view') {
    if (!isValidLatLng(message.center) || !Number.isFinite(Number(message.zoom))) return;
    state.center = { lat: message.center.lat, lng: message.center.lng };
    state.zoom = Number(message.zoom);
  } else if (message.type === 'gpxworldmap-activity-types') {
    if (!Array.isArray(message.hiddenTypes)) return;
    state.hiddenTypes = message.hiddenTypes.filter(type => typeof type === 'string');
  } else if (message.type === 'gpxworldmap-base-layer') {
    if (!isBaseLayerId(message.baseLayer)) return;
    state.baseLayer = message.baseLayer;
  }

  // The visible map changed while the target was still preparing; resend the latest state.
  const target = targetKind ? frames.get(targetKind) : undefined;
  if (target?.ready) syncTarget(target);
});

const mapTypeControl = createMapTypeControl(stage, activeKind, selectMap);
createFrame(activeKind, true);