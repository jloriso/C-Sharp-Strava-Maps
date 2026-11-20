import '../../Shared/legend.css';
import { postToParent } from '../../Shared/messages';
import type { ActivityTypeColor } from '../../Shared/types';

export interface ActivityTypeControl {
  getHiddenTypes(): string[];
  /** Applies a selection made on the other map; types not listed are shown. */
  setHiddenTypes(hiddenTypes: string[]): void;
}

/**
 * Leaflet control with one checkbox per activity type. The page decides what toggling a
 * type actually does (heat layers vs. polylines) via `onToggle`.
 */
export function addActivityTypeControl(
  map: L.Map,
  types: ActivityTypeColor[],
  onToggle: (type: string, visible: boolean) => void,
): ActivityTypeControl {
  const checkboxes = new Map<string, HTMLInputElement>();
  const getHiddenTypes = () => [...checkboxes].filter(([, checkbox]) => !checkbox.checked).map(([type]) => type);

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
      checkbox.addEventListener('change', () => {
        onToggle(type, checkbox.checked);
        postToParent({ type: 'gpxworldmap-activity-types', hiddenTypes: getHiddenTypes() });
      });
      checkboxes.set(type, checkbox);

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

  return {
    getHiddenTypes,
    setHiddenTypes(hiddenTypes) {
      const hidden = new Set(hiddenTypes);
      for (const [type, checkbox] of checkboxes) {
        const visible = !hidden.has(type);
        if (checkbox.checked === visible) continue;
        checkbox.checked = visible;
        onToggle(type, visible);
      }
    },
  };
}