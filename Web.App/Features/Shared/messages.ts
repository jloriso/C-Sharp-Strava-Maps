import type { LatLng } from './types';

/** Messages exchanged between the map pages and the MapSwitcher page that hosts them in iframes. */

/** Timeline position: the first `count` activities are shown; `date` is the newest shown (null = none). */
export interface TimelinePosition {
  count: number;
  date: string | null;
}

/** Map page -> parent: the map page has loaded and is listening for state. */
export interface MapReadyMessage {
  type: 'gpxworldmap-map-ready';
}

/** Map page -> parent: the timeline slider moved. */
export interface TimelinePositionMessage extends TimelinePosition {
  type: 'gpxworldmap-timeline-position';
}

/** Map page -> parent: the map was panned or zoomed. */
export interface MapViewMessage {
  type: 'gpxworldmap-map-view';
  center: LatLng;
  zoom: number;
}

/** Map page -> parent: activity type checkboxes changed. */
export interface ActivityTypesMessage {
  type: 'gpxworldmap-activity-types';
  hiddenTypes: string[];
}

/** Map page -> parent: a SetMapStateMessage has been applied and the map has rendered it. */
export interface MapStateAppliedMessage {
  type: 'gpxworldmap-map-state-applied';
  requestId: number;
}

/** Parent -> map page: apply the timeline position and view of the previously shown map. */
export interface SetMapStateMessage extends TimelinePosition {
  type: 'gpxworldmap-set-map-state';
  requestId: number;
  center: LatLng | null;
  zoom: number | null;
  hiddenTypes: string[];
}

export type MapPageMessage =
  | MapReadyMessage
  | TimelinePositionMessage
  | MapViewMessage
  | ActivityTypesMessage
  | MapStateAppliedMessage;

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