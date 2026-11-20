import type { ActivityTypeColor, MapLocation } from '../../Shared/types';

/** Settings shared by the heatmap and line map pages. Mirrors MapPageSettings.cs. */
export interface MapPageSettings {
  activityTypes: ActivityTypeColor[];
  locations: MapLocation[];
  defaultLocation: string;
  initialView: MapLocation;
  tileUrlQuery: string;
  homeUrl: string;
}

export function createBaseMap(settings: MapPageSettings): L.Map {
  const { lat, lon, zoom } = settings.initialView;
  const map = L.map('map', { zoomControl: false }).setView([lat, lon], zoom);
  L.control.zoom({ position: 'bottomright' }).addTo(map);

  L.tileLayer(`https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png${settings.tileUrlQuery}`, {
    maxZoom: 19,
    subdomains: 'abcd',
    detectRetina: true,
    attribution: '&copy; OpenStreetMap contributors &copy; CARTO',
  }).addTo(map);

  return map;
}
