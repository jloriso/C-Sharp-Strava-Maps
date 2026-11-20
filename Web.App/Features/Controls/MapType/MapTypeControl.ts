import './MapTypeControl.css';
import '../../Shared/legend.css';
import type { MapKind } from '../../Shared/types';

const mapTypes: { kind: MapKind; label: string }[] = [
  { kind: 'heatmap', label: 'Heatmap' },
  { kind: 'linemap', label: 'Line Map' },
];

export interface MapTypeControl {
  setActive(kind: MapKind): void;
}

/**
 * Heatmap / Line Map toggle for the MapSwitcher page. It floats over the hosted map
 * (beside the map's Home button); the MapSwitcher performs the actual switch.
 */
export function createMapTypeControl(
  parent: HTMLElement,
  activeMap: MapKind,
  onSelect: (kind: MapKind) => void,
): MapTypeControl {
  const container = document.createElement('div');
  container.className = 'legend map-type-control';
  const buttonList = document.createElement('div');
  buttonList.className = 'map-type-buttons';
  container.append(buttonList);

  const buttons = new Map<MapKind, HTMLButtonElement>();
  for (const { kind, label } of mapTypes) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'map-type-btn';
    button.textContent = label;
    button.addEventListener('click', () => onSelect(kind));
    buttons.set(kind, button);
    buttonList.append(button);
  }

  const setActive = (kind: MapKind) => {
    for (const [buttonKind, button] of buttons) {
      button.classList.toggle('active', buttonKind === kind);
      button.setAttribute('aria-pressed', String(buttonKind === kind));
    }
  };

  setActive(activeMap);
  parent.append(container);
  return { setActive };
}