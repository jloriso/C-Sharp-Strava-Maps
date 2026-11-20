import './RoutePlayerControl.css';

const icons = {
  play: '<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M4 2.5v11l9.5-5.5z"/></svg>',
  pause: '<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M3.5 2.5h3v11h-3zM9.5 2.5h3v11h-3z"/></svg>',
  replay: '<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M8 2.5a5.5 5.5 0 1 0 5.4 6.5h-1.8A3.8 3.8 0 1 1 8 4.2v2.3L11.5 3.4 8 .3z"/></svg>',
} as const;

const labels: Record<keyof typeof icons, string> = { play: 'Play route', pause: 'Pause route', replay: 'Replay route' };

export interface RoutePlayer {
  /** Stops playback and removes the player's layers and button. */
  destroy(): void;
}

/**
 * Adds a play button (bottom-left) that draws the route out from start to finish.
 * The full route stays faintly visible while it plays; progress is proportional to
 * distance along the route, since the feed has no per-point timestamps.
 */
export function addRoutePlayerControl(
  map: L.Map,
  segments: L.LatLngTuple[][],
  routeLayers: L.Polyline[],
  color: string,
  durationMs: number,
): RoutePlayer {
  // Cumulative distance (m) at each point; gaps between segments add no distance.
  const cumulative: number[][] = [];
  let total = 0;
  for (const segment of segments) {
    const distances: number[] = [];
    segment.forEach((point, i) => {
      if (i > 0) total += map.distance(segment[i - 1], point);
      distances.push(total);
    });
    cumulative.push(distances);
  }

  const progressLayers = segments.map(() => L.polyline([], { color, weight: 4, opacity: 1, interactive: false }));
  const head = L.circleMarker([0, 0], {
    radius: 6, color: 'white', weight: 2, fillColor: color, fillOpacity: 1, interactive: false,
  });

  let progress = 0;
  let playing = false;
  let frameId = 0;
  let lastTime = 0;

  const button = L.DomUtil.create('button', 'route-player-button');
  button.type = 'button';
  const control = new (L.Control.extend({
    onAdd: () => {
      L.DomEvent.disableClickPropagation(button);
      L.DomEvent.on(button, 'click', () => (playing ? pause() : play()));
      return button;
    },
  }))({ position: 'bottomleft' }).addTo(map);
  setButton('play');

  function setButton(state: keyof typeof icons): void {
    button.innerHTML = icons[state];
    button.title = labels[state];
    button.setAttribute('aria-label', labels[state]);
  }

  function play(): void {
    if (total === 0) return;
    if (progress === 0 || progress >= 1) {
      progress = 0;
      routeLayers.forEach(layer => layer.setStyle({ opacity: 0.25 }));
      progressLayers.forEach(layer => layer.addTo(map));
      head.addTo(map);
    }
    playing = true;
    lastTime = performance.now();
    setButton('pause');
    frameId = requestAnimationFrame(step);
  }

  function pause(): void {
    playing = false;
    cancelAnimationFrame(frameId);
    setButton('play');
  }

  function finish(): void {
    playing = false;
    progress = 1;
    progressLayers.forEach(layer => layer.remove());
    head.remove();
    routeLayers.forEach(layer => layer.setStyle({ opacity: 0.9 }));
    setButton('replay');
  }

  function step(now: number): void {
    progress = Math.min(1, progress + (now - lastTime) / durationMs);
    lastTime = now;
    render();
    if (progress >= 1) finish();
    else frameId = requestAnimationFrame(step);
  }

  function render(): void {
    const target = progress * total;
    let headPoint: L.LatLngTuple | undefined;
    segments.forEach((segment, s) => {
      const distances = cumulative[s];
      if (distances[0] > target) {
        progressLayers[s].setLatLngs([]);
        return;
      }
      if (distances[distances.length - 1] <= target) {
        progressLayers[s].setLatLngs(segment);
        headPoint = segment[segment.length - 1];
        return;
      }
      let i = 0;
      while (distances[i + 1] <= target) i++;
      const span = distances[i + 1] - distances[i];
      const t = span > 0 ? (target - distances[i]) / span : 0;
      const [lat1, lon1] = segment[i];
      const [lat2, lon2] = segment[i + 1];
      headPoint = [lat1 + (lat2 - lat1) * t, lon1 + (lon2 - lon1) * t];
      progressLayers[s].setLatLngs([...segment.slice(0, i + 1), headPoint]);
    });
    if (headPoint) head.setLatLng(headPoint);
  }

  return {
    destroy() {
      cancelAnimationFrame(frameId);
      progressLayers.forEach(layer => layer.remove());
      head.remove();
      control.remove();
    },
  };
}
