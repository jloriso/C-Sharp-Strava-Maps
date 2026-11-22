import { baseLayerIds, type ActivityTypeColor, type BaseLayerId, type MapLocation } from '../../Shared/types';

/** Settings shared by the heatmap and line map pages. Mirrors MapPageSettings.cs. */
export interface MapPageSettings {
  activityTypes: ActivityTypeColor[];
  locations: MapLocation[];
  initialView: MapLocation;
  tileUrlQuery: string;
  homeUrl: string;
}

export interface BaseLayerControl {
  getSelected(): BaseLayerId;
  setSelected(id: BaseLayerId): void;
  onChange(listener: (id: BaseLayerId) => void): void;
}

export interface BaseMap {
  map: L.Map;
  baseLayerControl: BaseLayerControl;
}

export function createBaseMap(settings: MapPageSettings): BaseMap {
  const { lat, lon, zoom } = settings.initialView;
  const map = L.map('map', { zoomControl: false }).setView([lat, lon], zoom);
  map.attributionControl.setPrefix(false);
  L.control.zoom({ position: 'bottomright' }).addTo(map);
  const baseLayerControl = addBaseLayerControl(map, settings.tileUrlQuery);
  return { map, baseLayerControl };
}

/** Adds the default CARTO light basemap used by activity preview maps. */
export function addBaseTiles(map: L.Map, tileUrlQuery: string): L.TileLayer {
  map.attributionControl.setPrefix(false);
  return cartoLayer('light_all', tileUrlQuery).addTo(map);
}

function addBaseLayerControl(map: L.Map, tileUrlQuery: string): BaseLayerControl {
  const osmAttribution = '&copy; OpenStreetMap contributors';
  const labels: Record<BaseLayerId, string> = {
    light: 'Light (recommended)',
    streets: 'Streets',
    dark: 'Dark',
    satellite: 'Satellite',
  };
  const layers: Record<BaseLayerId, L.TileLayer> = {
    light: cartoLayer('light_all', tileUrlQuery),
    streets: L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: osmAttribution,
    }),
    dark: cartoLayer('dark_all', tileUrlQuery),
    satellite: L.tileLayer(
      'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
      {
        maxZoom: 19,
        attribution: 'Tiles &copy; Esri &mdash; Source: Esri, Maxar, Earthstar Geographics, and the GIS User Community',
      },
    ),
  };
  const labelToId = new Map(baseLayerIds.map(id => [labels[id], id]));
  let selected: BaseLayerId = 'light';
  let applyingState = false;
  let onChange = (_id: BaseLayerId) => {};

  layers.light.addTo(map);
  L.control.layers(
    Object.fromEntries(baseLayerIds.map(id => [labels[id], layers[id]])),
    undefined,
    { position: 'bottomright' },
  ).addTo(map);
  map.on('baselayerchange', event => {
    const id = labelToId.get(event.name);
    if (!id) return;
    selected = id;
    if (!applyingState) onChange(id);
  });

  return {
    getSelected: () => selected,
    setSelected(id) {
      if (id === selected) return;
      applyingState = true;
      try {
        map.removeLayer(layers[selected]);
        map.addLayer(layers[id]);
        selected = id;
      } finally {
        applyingState = false;
      }
    },
    onChange(listener) {
      onChange = listener;
    },
  };
}

function cartoLayer(style: 'light_all' | 'dark_all', tileUrlQuery: string): L.TileLayer {
  return L.tileLayer(`https://{s}.basemaps.cartocdn.com/${style}/{z}/{x}/{y}{r}.png${tileUrlQuery}`, {
    maxZoom: 19,
    subdomains: 'abcd',
    detectRetina: true,
    attribution: '&copy; OpenStreetMap contributors &copy; CARTO',
  });
}
