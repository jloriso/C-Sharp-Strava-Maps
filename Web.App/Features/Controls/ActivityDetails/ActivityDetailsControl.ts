import './ActivityDetailsControl.css';
import { createActivityCard, showActivityMap, type ActivityMap, type FeedActivity } from '../../Activities/Shared/activityCard';

export interface ActivityDetailsControl {
  /** Opens the dialog and loads the activity's details (the same card as the Activities page). */
  open(activityId: string): void;
}

/**
 * A modal dialog showing one activity's full details. urlTemplate is the served URL of an
 * activity's JSON with an "{id}" placeholder (ActivityFeedDataBuilder.ActivityUrlTemplate).
 */
export function createActivityDetailsControl(urlTemplate: string, tileUrlQuery: string): ActivityDetailsControl {
  const dialog = document.createElement('dialog');
  dialog.className = 'activity-details';
  dialog.setAttribute('aria-label', 'Activity details');

  const close = document.createElement('button');
  close.type = 'button';
  close.className = 'activity-details-close';
  close.setAttribute('aria-label', 'Close');
  close.textContent = '×';
  close.addEventListener('click', () => dialog.close());

  const body = document.createElement('div');
  body.className = 'activity-details-body';
  dialog.append(close, body);
  document.body.append(dialog);

  let activityMap: ActivityMap | null = null;
  let request = 0;

  // Clicks on the backdrop land on the <dialog> element itself; clicks on content don't.
  dialog.addEventListener('click', e => {
    if (e.target === dialog) dialog.close();
  });
  dialog.addEventListener('close', () => {
    request++;
    activityMap?.destroy();
    activityMap = null;
    body.replaceChildren();
  });

  function showMessage(text: string, spinner = false): void {
    const message = document.createElement('div');
    message.className = 'activity-details-message';
    if (spinner) {
      const icon = document.createElement('span');
      icon.className = 'activity-details-spinner';
      message.append(icon);
    }
    message.append(text);
    body.replaceChildren(message);
  }

  async function load(activityId: string, current: number): Promise<void> {
    try {
      const response = await fetch(urlTemplate.replace('{id}', encodeURIComponent(activityId)));
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const activity = await response.json() as FeedActivity;
      if (current !== request) return;
      const { card, mapElement } = createActivityCard(activity);
      body.replaceChildren(card);
      activityMap = showActivityMap(mapElement, activity, tileUrlQuery);
    } catch (error) {
      if (current !== request) return;
      console.error('Failed to load activity details', error);
      showMessage("Couldn't load this activity's details.");
    }
  }

  return {
    open(activityId) {
      activityMap?.destroy();
      activityMap = null;
      const current = ++request;
      showMessage('Loading activity…', true);
      if (!dialog.open) dialog.showModal();
      close.focus();
      void load(activityId, current);
    },
  };
}
