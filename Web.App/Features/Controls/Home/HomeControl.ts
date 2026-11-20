import './HomeControl.css';
import '../../Shared/legend.css';

const homeIconSvg =
  '<svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true" focusable="false">' +
  '<path fill="currentColor" d="M12 3l9 7.5V21a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1V10.5L12 3z"/>' +
  '</svg>';

/** Leaflet "Home" button that navigates the top-level window back to the app index. */
export function addHomeControl(map: L.Map, homeUrl: string): void {
  const control = new L.Control({ position: 'topleft' });
  control.onAdd = () => {
    const container = L.DomUtil.create('div', 'legend home-control');
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'home-btn';
    button.title = 'Home';
    button.setAttribute('aria-label', 'Go to home page');
    button.innerHTML = homeIconSvg;
    button.addEventListener('click', () => {
      const homeWindow = window.top ?? window;
      homeWindow.location.href = homeUrl || '/';
    });
    container.append(button);

    L.DomEvent.disableClickPropagation(container);
    return container;
  };
  control.addTo(map);
}
