export interface ActivityTypeColor {
  type: string;
  color: string;
}

export interface MapLocation {
  name: string;
  lat: number;
  lon: number;
  zoom: number;
}

export type MapKind = 'heatmap' | 'linemap';

export const baseLayerIds = ['light', 'streets', 'dark', 'satellite'] as const;
export type BaseLayerId = typeof baseLayerIds[number];

export const isBaseLayerId = (value: unknown): value is BaseLayerId =>
  baseLayerIds.some(id => id === value);

export interface LatLng {
  lat: number;
  lng: number;
}

export function getElement<T extends HTMLElement>(id: string): T {
  const element = document.getElementById(id);
  if (!element) throw new Error(`Missing #${id} element.`);
  return element as T;
}

export function toTypeColorLookup(types: ActivityTypeColor[]): Record<string, string> {
  return Object.fromEntries(types.map(t => [t.type, t.color]));
}
