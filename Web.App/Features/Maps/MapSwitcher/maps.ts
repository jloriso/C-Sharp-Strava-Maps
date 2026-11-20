import './maps.css';
import { isValidLatLng, postToFrame, type MapPageMessage } from '../../Shared/messages';
import { readPageData } from '../../Shared/pageData';
import { getElement, type LatLng, type MapKind } from '../../Shared/types';

/** Mirrors MapSwitcherHtmlBuilder.cs. */
interface MapSwitcherPageData {
  heatmapHref: string;
  linemapHref: string;
}

const data = readPageData<MapSwitcherPageData>();
const mapFrame = getElement<HTMLIFrameElement>('map-frame');
const mapUrls: Record<MapKind, string> = { heatmap: data.heatmapHref, linemap: data.linemapHref };
const mapTitles: Record<MapKind, string> = { heatmap: 'Heatmap', linemap: 'Line Map' };

let timelineCount = 0;
let mapCenter: LatLng | null = null;
let mapZoom: number | null = null;

mapFrame.addEventListener('load', () => {
  postToFrame(mapFrame, { type: 'gpxworldmap-set-map-state', count: timelineCount, center: mapCenter, zoom: mapZoom });
});

window.addEventListener('message', (event: MessageEvent<MapPageMessage>) => {
  if (event.source !== mapFrame.contentWindow || event.origin !== window.location.origin || !event.data) return;
  const message = event.data;

  if (message.type === 'gpxworldmap-timeline-position') {
    timelineCount = Math.max(0, Number(message.count) || 0);
  } else if (message.type === 'gpxworldmap-switch-map') {
    if (!Object.hasOwn(mapUrls, message.map)) return;
    timelineCount = Math.max(0, Number(message.count) || 0);
    if (isValidLatLng(message.center)) mapCenter = { lat: message.center.lat, lng: message.center.lng };
    if (Number.isFinite(Number(message.zoom))) mapZoom = Number(message.zoom);
    showMap(message.map);
  }
});

function showMap(kind: MapKind): void {
  mapFrame.title = mapTitles[kind];
  mapFrame.src = mapUrls[kind];
}

showMap('heatmap');
