import type { LatLng, MapKind } from './types';

/** Messages exchanged between the map pages and the MapSwitcher page that hosts them in an iframe. */

/** Map page -> parent: the timeline slider moved. */
export interface TimelinePositionMessage {
  type: 'gpxworldmap-timeline-position';
  count: number;
}

/** Map page -> parent: switch to the other map, carrying over the current view. */
export interface SwitchMapMessage {
  type: 'gpxworldmap-switch-map';
  map: MapKind;
  count: number;
  center: LatLng;
  zoom: number;
}

/** Parent -> map page: restore timeline position and view after loading. */
export interface SetMapStateMessage {
  type: 'gpxworldmap-set-map-state';
  count: number;
  center: LatLng | null;
  zoom: number | null;
}

export type MapPageMessage = TimelinePositionMessage | SwitchMapMessage;

export const isEmbedded = (): boolean => window.self !== window.top;

export function postToParent(message: MapPageMessage): void {
  if (isEmbedded()) window.parent.postMessage(message, window.location.origin);
}

export function postToFrame(frame: HTMLIFrameElement, message: SetMapStateMessage): void {
  frame.contentWindow?.postMessage(message, window.location.origin);
}

export function isValidLatLng(value: unknown): value is LatLng {
  const candidate = value as Partial<LatLng> | null;
  return !!candidate && Number.isFinite(candidate.lat) && Number.isFinite(candidate.lng);
}
