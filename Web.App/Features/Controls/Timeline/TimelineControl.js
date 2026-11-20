var timelineControlRoot = document.getElementById('timeline-controls');
var timelineSlider = document.getElementById('timeline-slider');
var timelineDate = document.getElementById('timeline-date');
var timelineYear = document.getElementById('timeline-year');
var timelineStep = Math.max(1, Math.ceil(timelineActivities.length / 10));

function updateTimelineCount(count, notifyParent) {
    count = Math.max(0, Math.min(timelineActivities.length, count));
    timelineSlider.value = String(count);
    timelineDate.textContent = count === 0
        ? 'Before first activity'
        : timelineActivities[count - 1].date + ' · ' + count + ' / ' + timelineActivities.length;
    timelineYear.textContent = count === 0 ? '' : timelineActivities[count - 1].date.slice(0, 4);
    document.getElementById('timeline-start').disabled = count === 0;
    document.getElementById('timeline-older').disabled = count === 0;
    document.getElementById('timeline-newer').disabled = count === timelineActivities.length;
    document.getElementById('timeline-latest').disabled = count === timelineActivities.length;
    if (typeof window.onTimelineChange === 'function') window.onTimelineChange(count);
    if (notifyParent !== false && window.self !== window.top) {
        window.parent.postMessage({ type: 'gpxworldmap-timeline-position', count: count }, window.location.origin);
    }
}

timelineSlider.max = String(timelineActivities.length);
if (timelineActivities.length > 0) timelineControlRoot.hidden = false;
timelineSlider.addEventListener('input', function () { updateTimelineCount(Number(timelineSlider.value)); });
document.getElementById('timeline-start').addEventListener('click', function () { updateTimelineCount(0); });
document.getElementById('timeline-older').addEventListener('click', function () {
    updateTimelineCount(Number(timelineSlider.value) - timelineStep);
});
document.getElementById('timeline-newer').addEventListener('click', function () {
    updateTimelineCount(Number(timelineSlider.value) + timelineStep);
});
document.getElementById('timeline-latest').addEventListener('click', function () {
    updateTimelineCount(timelineActivities.length);
});
window.addEventListener('message', function (event) {
    if (window.self === window.top || event.source !== window.parent || event.origin !== window.location.origin) return;
    if (!event.data || event.data.type !== 'gpxworldmap-set-map-state') return;
    updateTimelineCount(Number(event.data.count) || 0, false);
    var center = event.data.center;
    var zoom = Number(event.data.zoom);
    if (center && Number.isFinite(center.lat) && Number.isFinite(center.lng) && Number.isFinite(zoom)) {
        map.setView([center.lat, center.lng], zoom);
    }
});
updateTimelineCount(0);