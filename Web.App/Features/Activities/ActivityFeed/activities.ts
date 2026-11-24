import './activities.css';
import { createActivityDetailsView, type ActivityDetailsView, type FeedActivity } from '../Shared/activityCard';
import { readPageData } from '../../Shared/pageData';
import { getElement } from '../../Shared/types';

/** Mirrors ActivityFeedHtmlBuilder.cs. */
interface ActivityFeedPageData {
  totalActivities: number;
  /** JSON pages of activities, newest first. */
  pageUrls: string[];
  tileUrlQuery: string;
}

/** Mirrors ActivityFeedDataBuilder.cs. */
interface FeedPage {
  activities: FeedActivity[];
}

/** How far beyond the viewport to fetch more activities / keep card maps alive. */
const loadAheadPx = 1200;
const mapMarginPx = 600;

const data = readPageData<ActivityFeedPageData>();
const feed = getElement('feed');
const status = getElement('feed-status');
const sentinel = getElement('feed-sentinel');
getElement('feed-count').textContent =
  `${data.totalActivities.toLocaleString()} ${data.totalActivities === 1 ? 'activity' : 'activities'}`;

let nextPage = 0;
let loading = false;
let lastMonth: string | null | undefined;

// Card maps are only created near the viewport and removed once far away, so scrolling
// through hundreds of activities doesn't keep hundreds of Leaflet maps alive.
const cardViews = new Map<Element, ActivityDetailsView>();
const mapObserver = new IntersectionObserver(entries => {
  for (const entry of entries) {
    if (entry.isIntersecting) showMap(entry.target as HTMLElement);
    else hideMap(entry.target);
  }
}, { rootMargin: `${mapMarginPx}px 0px` });

new IntersectionObserver(entries => {
  if (entries.some(e => e.isIntersecting)) void loadMore();
}, { rootMargin: `0px 0px ${loadAheadPx}px 0px` }).observe(sentinel);

async function loadMore(): Promise<void> {
  if (loading || nextPage >= data.pageUrls.length) return;
  loading = true;
  setStatus('loading');
  try {
    const response = await fetch(data.pageUrls[nextPage]);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const page = await response.json() as FeedPage;
    page.activities.forEach(appendCard);
    nextPage++;
    loading = false;
    setStatus(nextPage >= data.pageUrls.length ? 'done' : 'idle');
    // The sentinel observer only fires on changes, so keep filling while it's still in range.
    if (sentinel.getBoundingClientRect().top < window.innerHeight + loadAheadPx) void loadMore();
  } catch (error) {
    loading = false;
    console.error('Failed to load activities', error);
    setStatus('error');
  }
}

function setStatus(state: 'idle' | 'loading' | 'done' | 'error'): void {
  status.replaceChildren();
  if (state === 'loading') {
    const spinner = document.createElement('span');
    spinner.className = 'feed-spinner';
    status.append(spinner, nextPage === 0 ? 'Loading activities…' : 'Loading older activities…');
  } else if (state === 'done') {
    status.textContent = data.totalActivities === 0 ? 'No activities with a route were found.' : "That's every activity.";
  } else if (state === 'error') {
    const retry = document.createElement('button');
    retry.type = 'button';
    retry.className = 'feed-retry';
    retry.textContent = 'Retry';
    retry.addEventListener('click', () => void loadMore());
    status.append("Couldn't load more activities.", retry);
  }
}

function appendCard(activity: FeedActivity): void {
  const date = activity.date ? new Date(activity.date) : null;
  const month = date ? date.toLocaleDateString('en-US', { month: 'long', year: 'numeric' }) : null;
  if (month !== lastMonth) {
    const heading = document.createElement('h3');
    heading.className = 'month-heading';
    heading.textContent = month ?? 'Undated';
    feed.append(heading);
    lastMonth = month;
  }

  const view = createActivityDetailsView(activity, data.tileUrlQuery);
  feed.append(view.card);
  cardViews.set(view.mapElement, view);
  mapObserver.observe(view.mapElement);
}

function showMap(element: HTMLElement): void {
  cardViews.get(element)?.showMap();
}

function hideMap(element: Element): void {
  cardViews.get(element)?.hideMap();
}

if (data.pageUrls.length === 0) setStatus('done');
else void loadMore();