import { getElement } from '../../Shared/types';

/**
 * Fetches a map page's data file (served separately so the page and its basemap render
 * first), showing a status message over the map until it arrives.
 */
export async function loadMapData<T>(url: string): Promise<T> {
  const status = document.createElement('div');
  status.className = 'map-data-status';
  status.setAttribute('role', 'status');
  status.textContent = 'Loading activities…';
  getElement('map').append(status);

  try {
    const response = await fetch(url);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json() as T;
    status.remove();
    return data;
  } catch (error) {
    status.textContent = "Couldn't load activity data. Reload the page to try again.";
    status.classList.add('is-error');
    throw error;
  }
}
