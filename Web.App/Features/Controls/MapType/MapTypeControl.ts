import './MapTypeControl.css';
import '../../Shared/legend.css';
import { isEmbedded, postToParent } from '../../Shared/messages';
import type { MapKind } from '../../Shared/types';

const mapTypes: { kind: MapKind; label: string }[] = [
  { kind: 'heatmap', label: 'Heatmap' },
  { kind: 'linemap', label: 'Line Map' },
];

/**
 * Heatmap / Line Map toggle. Only shown when the page is embedded in the MapSwitcher,
 * which performs the actual switch and carries over the timeline position and view.
 */
export function addMapTypeControl(map: L.Map, activeMap: MapKind, getTimelineCount: () => number): void {
  if (!isEmbedded()) return;

  const control = new L.Control({ position: 'topleft' });
  control.onAdd = () => {
    const container = L.DomUtil.create('div', 'legend map-type-control');
    const buttonList = document.createElement('div');
    buttonList.className = 'map-type-buttons';
    container.append(buttonList);

    for (const { kind, label } of mapTypes) {
      const isActive = kind === activeMap;
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'map-type-btn';
      button.classList.toggle('active', isActive);
      button.setAttribute('aria-pressed', String(isActive));
      button.textContent = label;
      button.addEventListener('click', () => {
        if (isActive) return;
        const center = map.getCenter();
        postToParent({
          type: 'gpxworldmap-switch-map',
          map: kind,
          count: getTimelineCount(),
          center: { lat: center.lat, lng: center.lng },
          zoom: map.getZoom(),
        });
      });
      buttonList.append(button);
    }

    L.DomEvent.disableClickPropagation(container);
    return container;
  };
  control.addTo(map);
}
