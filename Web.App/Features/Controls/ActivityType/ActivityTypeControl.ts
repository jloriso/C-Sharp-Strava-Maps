import '../../Shared/legend.css';
import type { ActivityTypeColor } from '../../Shared/types';

/**
 * Leaflet control with one checkbox per activity type. The page decides what toggling a
 * type actually does (heat layers vs. polylines) via `onToggle`.
 */
export function addActivityTypeControl(
  map: L.Map,
  types: ActivityTypeColor[],
  onToggle: (type: string, visible: boolean) => void,
): void {
  const control = new L.Control({ position: 'bottomleft' });
  control.onAdd = () => {
    const container = L.DomUtil.create('div', 'legend activity-type-control');
    const title = document.createElement('strong');
    title.textContent = 'Activity Type';
    container.append(title, document.createElement('br'));

    for (const { type, color } of types) {
      const label = document.createElement('label');
      label.style.display = 'block';

      const checkbox = document.createElement('input');
      checkbox.type = 'checkbox';
      checkbox.checked = true;
      checkbox.addEventListener('change', () => onToggle(type, checkbox.checked));

      const swatch = document.createElement('span');
      swatch.className = 'swatch';
      swatch.style.background = color;

      label.append(checkbox, swatch, type);
      container.append(label);
    }

    L.DomEvent.disableClickPropagation(container);
    return container;
  };
  control.addTo(map);
}
