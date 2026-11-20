import { isEmbedded, isValidLatLng, postToParent, type SetMapStateMessage } from '../../Shared/messages';
import type { ActivityTypeControl } from '../../Controls/ActivityType/ActivityTypeControl';
import type { TimelineControl } from '../../Controls/Timeline/TimelineControl';

const tileLoadTimeoutMs = 2000;

/**
 * Connects a map page to the MapSwitcher parent: reports view changes, applies the state
 * sent before the page is revealed, and confirms once that state has rendered.
 */
export function listenForMapState(map: L.Map, timeline: TimelineControl, activityTypes: ActivityTypeControl): void {
  if (!isEmbedded()) return;

  map.on('moveend', () => {
    const center = map.getCenter();
    postToParent({ type: 'gpxworldmap-map-view', center: { lat: center.lat, lng: center.lng }, zoom: map.getZoom() });
  });

  window.addEventListener('message', (event: MessageEvent<SetMapStateMessage>) => {
    if (event.source !== window.parent || event.origin !== window.location.origin) return;
    const message = event.data;
    if (message?.type !== 'gpxworldmap-set-map-state') return;

    const zoom = Number(message.zoom);
    if (isValidLatLng(message.center) && Number.isFinite(zoom)) {
      map.setView([message.center.lat, message.center.lng], zoom, { animate: false });
    }
    activityTypes.setHiddenTypes(Array.isArray(message.hiddenTypes) ? message.hiddenTypes : []);
    timeline.setPosition({ count: Number(message.count) || 0, date: message.date ?? null }, false);

    void whenRendered(map).then(() =>
      postToParent({ type: 'gpxworldmap-map-state-applied', requestId: message.requestId }));
  });

  postToParent({ type: 'gpxworldmap-map-ready' });
}

/** Resolves once visible tiles have loaded (or a timeout passes) and canvas layers have painted. */
async function whenRendered(map: L.Map): Promise<void> {
  const loading: L.GridLayer[] = [];
  map.eachLayer(layer => {
    if (layer instanceof L.GridLayer && layer.isLoading()) loading.push(layer);
  });
  await Promise.race([
    Promise.all(loading.map(layer => new Promise<void>(resolve => layer.once('load', () => resolve())))),
    delay(tileLoadTimeoutMs),
  ]);
  await nextFrame();
  await nextFrame();
}

const delay = (ms: number) => new Promise<void>(resolve => setTimeout(resolve, ms));

// requestAnimationFrame can be throttled in hidden frames, so fall back to a timer.
const nextFrame = () => new Promise<void>(resolve => {
  const timer = setTimeout(resolve, 50);
  requestAnimationFrame(() => { clearTimeout(timer); resolve(); });
});