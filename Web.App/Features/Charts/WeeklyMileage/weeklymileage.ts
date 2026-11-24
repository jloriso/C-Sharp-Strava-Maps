import type { Chart as ChartJs } from 'chart.js';
import './weeklymileage.css';
import { createActivityDetailsView, type ActivityDetailsView, type FeedActivity } from '../../Activities/Shared/activityCard';
import { readPageData } from '../../Shared/pageData';
import { getElement, toTypeColorLookup, type ActivityTypeColor } from '../../Shared/types';

declare global {
  // Loaded from the Chart.js CDN <script> in weeklymileage.html; undefined if that failed.
  var Chart: typeof ChartJs | undefined;
}

/** Mirrors WeeklyMileageHtmlBuilder.cs / WeeklyMileageDataBuilder.cs. */
interface WeeklyMileagePageData {
  /** ISO week-start (Monday) dates, one per week with no gaps, from earliest to latest activity. */
  weeks: string[];
  /** Miles per week by category, each index-aligned with `weeks`. */
  seriesByType: Record<string, number[]>;
  /** Per week, then per category: that week's activities, sorted by date. */
  activitiesByWeek: Record<string, Record<string, WeekActivity[]>>;
  activityTypes: ActivityTypeColor[];
  activityDetailsUrl: string;
  tileUrlQuery: string;
}

interface WeekActivity extends Omit<FeedActivity, 'date'> {
  date: string;
  hasDetails: boolean;
}

const WINDOW_WEEKS = 26;
const STEP_WEEKS = 12;
interface ChartPanelDefinition {
  category: string;
  title: string;
}

interface ChartSectionDefinition {
  key: string;
  title: string;
  panels: ChartPanelDefinition[];
}

const chartSections: ChartSectionDefinition[] = [
  {
    key: 'weekly-mileage',
    title: 'Weekly Mileage',
    panels: ['Run', 'Bike'].map(category => ({ category, title: category })),
  },
];

const { weeks, seriesByType, activitiesByWeek, activityTypes, activityDetailsUrl, tileUrlQuery } =
  readPageData<WeeklyMileagePageData>();
const typeColors = toTypeColorLookup(activityTypes);
const activityViews: ActivityDetailsView[] = [];
let weekRequest = 0;

function showMessage(text: string): void {
  getElement('chart-sections').style.display = 'none';
  const message = getElement('chart-message');
  message.textContent = text;
  message.style.display = 'block';
}

const formatWeekLabel = (iso: string) =>
  new Date(`${iso}T00:00:00`).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });

function clearWeekActivities(): void {
  activityViews.forEach(view => view.destroy());
  activityViews.length = 0;
  getElement('week-activities').replaceChildren();
}

async function showWeekActivities(week: string, category: string): Promise<void> {
  const dialog = getElement<HTMLDialogElement>('week-dialog');
  const title = getElement('week-dialog-title');
  const list = getElement('week-activities');
  const emptyMessage = getElement('week-activities-empty');
  const currentRequest = ++weekRequest;
  clearWeekActivities();

  const start = new Date(`${week}T00:00:00`);
  const end = new Date(start);
  end.setDate(end.getDate() + 6);
  const dateOptions: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric', year: 'numeric' };
  title.textContent = `${category} · ${start.toLocaleDateString(undefined, dateOptions)} – ${end.toLocaleDateString(undefined, dateOptions)}`;

  const weekActivities = activitiesByWeek[week]?.[category] ?? [];
  emptyMessage.hidden = weekActivities.length > 0;
  if (!dialog.open) dialog.showModal();
  if (weekActivities.length === 0) return;

  list.textContent = 'Loading activities…';
  const results = await Promise.all(weekActivities.map(async activity => {
    if (!activity.hasDetails) return { activity, detail: null, error: null };
    try {
      const response = await fetch(activityDetailsUrl.replace('{id}', encodeURIComponent(activity.id)));
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      return { activity, detail: await response.json() as FeedActivity, error: null };
    } catch (error) {
      return { activity, detail: null, error };
    }
  }));
  if (currentRequest !== weekRequest) return;

  list.replaceChildren();
  for (const result of results) {
    const view = createActivityDetailsView(result.detail ?? result.activity, tileUrlQuery, result.detail !== null);
    activityViews.push(view);
    list.append(view.card);
    if (result.error) {
      console.error(`Failed to load activity ${result.activity.id}`, result.error);
      const warning = document.createElement('p');
      warning.className = 'week-activity-map-warning';
      warning.textContent = "Couldn't load this activity's map.";
      view.card.append(warning);
    } else if (view.mapElement) {
      view.showMap();
    }
  }
}

interface ChartPanelElements {
  panel: HTMLElement;
  canvas: HTMLCanvasElement;
  range: HTMLOutputElement;
  total: HTMLSpanElement;
  year: HTMLOutputElement;
  slider: HTMLInputElement;
  startButton: HTMLButtonElement;
  olderButton: HTMLButtonElement;
  newerButton: HTMLButtonElement;
  latestButton: HTMLButtonElement;
}

function createNavButton(label: string, title?: string): HTMLButtonElement {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = 'nav-btn';
  button.textContent = label;
  if (title) button.title = title;
  return button;
}

function createChartSection(definition: ChartSectionDefinition): { section: HTMLElement; grid: HTMLElement } {
  const section = document.createElement('section');
  section.className = 'chart-section';
  section.setAttribute('aria-labelledby', `chart-section-${definition.key}-heading`);

  const heading = document.createElement('h2');
  heading.id = `chart-section-${definition.key}-heading`;
  heading.textContent = definition.title;
  const grid = document.createElement('div');
  grid.className = 'chart-grid';
  section.append(heading, grid);

  return { section, grid };
}

function createChartPanelElements(definition: { title: string }): ChartPanelElements {
  const panel = document.createElement('section');
  panel.className = 'chart-panel';

  const heading = document.createElement('div');
  heading.className = 'chart-heading';
  const title = document.createElement('h3');
  title.textContent = definition.title;
  const range = document.createElement('output');
  range.className = 'chart-range';
  heading.append(title, range);

  const chartCanvas = document.createElement('div');
  chartCanvas.className = 'chart-canvas';
  const canvas = document.createElement('canvas');
  canvas.title = 'Select a week to view its activities';
  canvas.setAttribute('aria-label', `${definition.title} weekly mileage chart`);
  chartCanvas.append(canvas);

  const controls = document.createElement('div');
  controls.className = 'controls-row';
  const startButton = createNavButton('\u00ab Start', 'Jump to the first week');
  const olderButton = createNavButton('\u2039 Older');
  const slider = document.createElement('input');
  slider.type = 'range';
  slider.className = 'week-slider';
  slider.min = '0';
  slider.max = '0';
  slider.value = '0';
  slider.step = '1';
  slider.setAttribute('aria-label', `${definition.title} date window`);
  const newerButton = createNavButton('Newer \u203a');
  const latestButton = createNavButton('Latest \u00bb', 'Jump to the latest week');
  controls.append(startButton, olderButton, slider, newerButton, latestButton);

  const footer = document.createElement('div');
  footer.className = 'chart-footer';
  const total = document.createElement('span');
  const year = document.createElement('output');
  year.className = 'visible-year';
  footer.append(total, year);
  panel.append(heading, chartCanvas, controls, footer);

  return { panel, canvas, range, total, year, slider, startButton, olderButton, newerButton, latestButton };
}

function createChartPanel(
  ChartCtor: typeof ChartJs,
  definition: { category: string; title: string },
  maxStart: number,
  container: HTMLElement,
): void {
  const { category, title } = definition;
  const elements = createChartPanelElements(definition);
  container.append(elements.panel);
  const series = seriesByType[category] ?? [];
  let windowStart = maxStart;
  const visibleSlice = <T>(values: T[]) => values.slice(windowStart, windowStart + WINDOW_WEEKS);

  const chart = new ChartCtor(elements.canvas, {
    type: 'bar',
    data: {
      labels: visibleSlice(weeks).map(formatWeekLabel),
      datasets: [{
        label: title,
        data: visibleSlice(series),
        backgroundColor: typeColors[category] ?? '#888888',
        borderRadius: 2,
      }],
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      animation: false,
      scales: {
        x: { grid: { display: false } },
        y: { beginAtZero: true, title: { display: true, text: 'Miles' } },
      },
      plugins: {
        legend: { display: false },
        tooltip: { callbacks: { label: item => `${(item.parsed.y ?? 0).toFixed(1)} mi` } },
      },
      onClick: (event, _activeElements, currentChart) => {
        if (!event.native) return;
        const [point] = currentChart.getElementsAtEventForMode(event.native, 'index', { intersect: false }, false);
        const week = point ? visibleSlice(weeks)[point.index] : undefined;
        if (week) void showWeekActivities(week, category);
      },
    },
  });

  const redraw = () => {
    const visibleWeeks = visibleSlice(weeks);
    const endWeek = new Date(`${visibleWeeks[visibleWeeks.length - 1]}T00:00:00`);
    endWeek.setDate(endWeek.getDate() + 6);
    const startLabel = formatWeekLabel(visibleWeeks[0]);
    const endLabel = endWeek.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
    const startYear = visibleWeeks[0].slice(0, 4);
    const endYear = String(endWeek.getFullYear());
    const visibleMiles = visibleSlice(series);

    elements.range.textContent = `${startLabel} – ${endLabel}`;
    elements.year.textContent = startYear === endYear ? endYear : `${startYear}–${endYear}`;
    elements.total.textContent = `${visibleMiles.reduce((sum, miles) => sum + miles, 0).toFixed(1)} mi`;

    chart.data.labels = visibleWeeks.map(formatWeekLabel);
    chart.data.datasets[0].data = visibleMiles;
    chart.update();

    elements.slider.max = String(maxStart);
    elements.slider.value = String(windowStart);
    elements.startButton.disabled = elements.olderButton.disabled = windowStart <= 0;
    elements.newerButton.disabled = elements.latestButton.disabled = windowStart >= maxStart;
  };

  const setWindowStart = (start: number) => {
    windowStart = Math.max(0, Math.min(maxStart, start));
    redraw();
  };

  elements.startButton.addEventListener('click', () => setWindowStart(0));
  elements.latestButton.addEventListener('click', () => setWindowStart(maxStart));
  elements.olderButton.addEventListener('click', () => setWindowStart(windowStart - STEP_WEEKS));
  elements.newerButton.addEventListener('click', () => setWindowStart(windowStart + STEP_WEEKS));
  elements.slider.addEventListener('input', () => setWindowStart(parseInt(elements.slider.value, 10)));
  redraw();
}

const ChartCtor = window.Chart;
if (!ChartCtor) {
  showMessage('Could not load Chart.js. Check your connection and reload.');
} else if (weeks.length === 0) {
  showMessage('No dated activities to chart yet.');
} else {
  const maxStart = Math.max(0, weeks.length - WINDOW_WEEKS);
  const sectionsContainer = getElement('chart-sections');
  for (const definition of chartSections) {
    const section = createChartSection(definition);
    sectionsContainer.append(section.section);
    definition.panels.forEach(panel => createChartPanel(ChartCtor, panel, maxStart, section.grid));
  }

  const dialog = getElement<HTMLDialogElement>('week-dialog');
  dialog.addEventListener('click', event => {
    if (event.target === dialog) dialog.close();
  });
  dialog.addEventListener('close', () => {
    weekRequest++;
    clearWeekActivities();
  });
  getElement<HTMLButtonElement>('week-dialog-close').addEventListener('click', () => dialog.close());
}
