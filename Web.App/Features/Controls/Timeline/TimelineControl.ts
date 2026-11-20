import './TimelineControl.css';
import { postToParent, type TimelinePosition } from '../../Shared/messages';

export interface DatedItem {
  date: string;
}

export interface TimelineControl {
  getPosition(): TimelinePosition;
  /**
   * Moves to a position reported by another map. The date takes priority, since the heatmap and
   * line map timelines can contain different activities; the count is used when it agrees with it.
   */
  setPosition(position: TimelinePosition, notifyParent?: boolean): void;
}

/**
 * Start/Older/slider/Newer/Latest bar that reveals chronologically ordered activities.
 * Rendered below the map, and hidden when there are no dated activities.
 */
export function createTimelineControl(
  mapElement: HTMLElement,
  items: DatedItem[],
  onChange: (count: number) => void,
): TimelineControl {
  const root = document.createElement('div');
  root.id = 'timeline-controls';
  root.hidden = items.length === 0;

  const makeButton = (id: string, label: string) => {
    const button = document.createElement('button');
    button.id = id;
    button.type = 'button';
    button.className = 'timeline-button';
    button.textContent = label;
    return button;
  };
  const startButton = makeButton('timeline-start', 'Start');
  const olderButton = makeButton('timeline-older', 'Older');
  const newerButton = makeButton('timeline-newer', 'Newer');
  const latestButton = makeButton('timeline-latest', 'Latest');

  const slider = document.createElement('input');
  slider.type = 'range';
  slider.id = 'timeline-slider';
  slider.min = '0';
  slider.max = String(items.length);
  slider.step = '1';
  slider.value = '0';
  slider.setAttribute('aria-label', 'Activities shown over time');

  const dateOutput = document.createElement('output');
  dateOutput.id = 'timeline-date';
  const yearOutput = document.createElement('output');
  yearOutput.id = 'timeline-year';

  root.append(startButton, olderButton, slider, newerButton, latestButton, dateOutput, yearOutput);
  mapElement.after(root);

  const step = Math.max(1, Math.ceil(items.length / 10));
  let current = 0;

  const setCount = (count: number, notifyParent = true) => {
    current = Math.max(0, Math.min(items.length, count));
    slider.value = String(current);
    const latest = current > 0 ? items[current - 1] : undefined;
    dateOutput.textContent = latest ? `${latest.date} · ${current} / ${items.length}` : 'Before first activity';
    yearOutput.textContent = latest ? latest.date.slice(0, 4) : '';
    startButton.disabled = olderButton.disabled = current === 0;
    newerButton.disabled = latestButton.disabled = current === items.length;
    onChange(current);
    if (notifyParent) postToParent({ type: 'gpxworldmap-timeline-position', ...getPosition() });
  };

  const getPosition = (): TimelinePosition =>
    ({ count: current, date: current > 0 ? items[current - 1].date : null });

  /** Number of items dated on or before `date` (items are in chronological order). */
  const countThroughDate = (date: string) => {
    let low = 0;
    let high = items.length;
    while (low < high) {
      const mid = (low + high) >> 1;
      if (items[mid].date <= date) low = mid + 1;
      else high = mid;
    }
    return low;
  };

  const setPosition = ({ count, date }: TimelinePosition, notifyParent = true) => {
    if (date === null) setCount(0, notifyParent);
    else if (count > 0 && items[count - 1]?.date === date) setCount(count, notifyParent);
    else setCount(countThroughDate(date), notifyParent);
  };

  slider.addEventListener('input', () => setCount(Number(slider.value)));
  startButton.addEventListener('click', () => setCount(0));
  olderButton.addEventListener('click', () => setCount(current - step));
  newerButton.addEventListener('click', () => setCount(current + step));
  latestButton.addEventListener('click', () => setCount(items.length));

  setCount(0);
  return { getPosition, setPosition };
}
