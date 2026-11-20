import { isEmbedded, isValidLatLng, type SetMapStateMessage } from '../../Shared/messages';
import type { TimelineControl } from '../../Controls/Timeline/TimelineControl';

/** Applies timeline position and map view sent by the MapSwitcher parent page. */
export function listenForMapState(map: L.Map, timeline: TimelineControl): void {
  window.addEventListener('message', (event: MessageEvent<SetMapStateMessage>) => {
    if (!isEmbedded() || event.source !== window.parent || event.origin !== window.location.origin) return;
    if (event.data?.type !== 'gpxworldmap-set-map-state') return;

    timeline.setCount(Number(event.data.count) || 0, false);
    const zoom = Number(event.data.zoom);
    if (isValidLatLng(event.data.center) && Number.isFinite(zoom)) {
      map.setView([event.data.center.lat, event.data.center.lng], zoom);
    }
  });
}
