import './BookmarkControl.css';
import '../../Shared/legend.css';
import type { MapLocation } from '../../Shared/types';

/** Leaflet control with one button per named location that jumps the map there. */
export function addBookmarkControl(map: L.Map, locations: MapLocation[], defaultLocation: string): void {
  const control = new L.Control({ position: 'topright' });
  control.onAdd = () => {
    const container = L.DomUtil.create('div', 'legend bookmark-control');
    const title = document.createElement('strong');
    title.textContent = 'Jump to';
    const buttonList = document.createElement('div');
    buttonList.className = 'bookmark-buttons';
    container.append(title, document.createElement('br'), buttonList);

    const buttons = locations.map(location => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'bookmark-btn';
      button.classList.toggle('active', location.name === defaultLocation);
      button.textContent = location.name;
      button.addEventListener('click', () => {
        map.setView([location.lat, location.lon], location.zoom);
        buttons.forEach(b => b.classList.toggle('active', b === button));
      });
      return button;
    });
    buttonList.append(...buttons);

    L.DomEvent.disableClickPropagation(container);
    return container;
  };
  control.addTo(map);
}
