import type { Chart as ChartJs } from 'chart.js';
import './weeklymileage.css';
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
  activityTypes: ActivityTypeColor[];
}

const WINDOW_WEEKS = 26;
const STEP_WEEKS = 12;
const chartTypes = ['Run', 'Bike', 'Other'];

const { weeks, seriesByType, activityTypes } = readPageData<WeeklyMileagePageData>();
const typeColors = toTypeColorLookup(activityTypes);

function showMessage(text: string): void {
  getElement('chart-grid').style.display = 'none';
  const message = getElement('chart-message');
  message.textContent = text;
  message.style.display = 'block';
}

const formatWeekLabel = (iso: string) =>
  new Date(`${iso}T00:00:00`).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });

function createChartPanel(ChartCtor: typeof ChartJs, type: string, maxStart: number): void {
  const key = type.toLowerCase();
  const series = seriesByType[type] ?? [];
  let windowStart = maxStart;
  const visibleSlice = <T>(values: T[]) => values.slice(windowStart, windowStart + WINDOW_WEEKS);

  const canvas = getElement<HTMLCanvasElement>(`chart-${key}`);
  const chart = new ChartCtor(canvas, {
    type: 'bar',
    data: {
      labels: visibleSlice(weeks).map(formatWeekLabel),
      datasets: [{
        label: type,
        data: visibleSlice(series),
        backgroundColor: typeColors[type] ?? '#888888',
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
    },
  });

  const slider = getElement<HTMLInputElement>(`week-slider-${key}`);
  const startButton = getElement<HTMLButtonElement>(`btn-start-${key}`);
  const olderButton = getElement<HTMLButtonElement>(`btn-older-${key}`);
  const newerButton = getElement<HTMLButtonElement>(`btn-newer-${key}`);
  const latestButton = getElement<HTMLButtonElement>(`btn-latest-${key}`);

  const redraw = () => {
    const visibleWeeks = visibleSlice(weeks);
    const endWeek = new Date(`${visibleWeeks[visibleWeeks.length - 1]}T00:00:00`);
    endWeek.setDate(endWeek.getDate() + 6);
    const startLabel = formatWeekLabel(visibleWeeks[0]);
    const endLabel = endWeek.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
    const startYear = visibleWeeks[0].slice(0, 4);
    const endYear = String(endWeek.getFullYear());
    const visibleMiles = visibleSlice(series);

    getElement(`range-${key}`).textContent = `${startLabel} – ${endLabel}`;
    getElement(`year-${key}`).textContent = startYear === endYear ? endYear : `${startYear}–${endYear}`;
    getElement(`total-${key}`).textContent = `${visibleMiles.reduce((sum, miles) => sum + miles, 0).toFixed(1)} mi`;

    chart.data.labels = visibleWeeks.map(formatWeekLabel);
    chart.data.datasets[0].data = visibleMiles;
    chart.update();

    slider.max = String(maxStart);
    slider.value = String(windowStart);
    startButton.disabled = olderButton.disabled = windowStart <= 0;
    newerButton.disabled = latestButton.disabled = windowStart >= maxStart;
  };

  const setWindowStart = (start: number) => {
    windowStart = Math.max(0, Math.min(maxStart, start));
    redraw();
  };

  startButton.addEventListener('click', () => setWindowStart(0));
  latestButton.addEventListener('click', () => setWindowStart(maxStart));
  olderButton.addEventListener('click', () => setWindowStart(windowStart - STEP_WEEKS));
  newerButton.addEventListener('click', () => setWindowStart(windowStart + STEP_WEEKS));
  slider.addEventListener('input', () => setWindowStart(parseInt(slider.value, 10)));
  redraw();
}

const ChartCtor = window.Chart;
if (!ChartCtor) {
  showMessage('Could not load Chart.js. Check your connection and reload.');
} else if (weeks.length === 0) {
  showMessage('No dated activities to chart yet.');
} else {
  const maxStart = Math.max(0, weeks.length - WINDOW_WEEKS);
  chartTypes.forEach(type => createChartPanel(ChartCtor, type, maxStart));
}
