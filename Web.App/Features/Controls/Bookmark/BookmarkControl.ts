import './BookmarkControl.css';
import '../../Shared/legend.css';
import type { MapLocation } from '../../Shared/types';

/** Max distance in screen pixels between the map center and a location for it to count as "at" it. */
const matchTolerancePx = 2;

/**
 * Leaflet control with one button per named location that jumps the map there. A location
 * is highlighted only while the map is still showing it; panning or zooming away clears it.
 */
export function addBookmarkControl(map: L.Map, locations: MapLocation[]): void {
  const buttons = new Map<MapLocation, HTMLButtonElement>();

  const isAt = (location: MapLocation) => {
    if (map.getZoom() !== location.zoom) return false;
    const center = map.latLngToContainerPoint(map.getCenter());
    return center.distanceTo(map.latLngToContainerPoint([location.lat, location.lon])) <= matchTolerancePx;
  };
  const updateHighlight = () => {
    for (const [location, button] of buttons) button.classList.toggle('active', isAt(location));
  };

  const control = new L.Control({ position: 'topright' });
  control.onAdd = () => {
    const container = L.DomUtil.create('div', 'legend bookmark-control');
    const title = document.createElement('strong');
    title.textContent = 'Jump to';
    const buttonList = document.createElement('div');
    buttonList.className = 'bookmark-buttons';
    container.append(title, document.createElement('br'), buttonList);

    for (const location of locations) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'bookmark-btn';
      button.textContent = location.name;
      button.addEventListener('click', () => map.setView([location.lat, location.lon], location.zoom));
      buttons.set(location, button);
      buttonList.append(button);
    }

    L.DomEvent.disableClickPropagation(container);
    return container;
  };
  control.addTo(map);

  // Clear as soon as the user starts dragging; moveend re-checks once the map settles.
  map.on('dragstart', () => buttons.forEach(button => button.classList.remove('active')));
  map.on('moveend', updateHighlight);
  updateHighlight();
}