var timelineControlRoot = document.getElementById('timeline-controls');
var timelineSlider = document.getElementById('timeline-slider');
var timelineDate = document.getElementById('timeline-date');
var timelineYear = document.getElementById('timeline-year');
var timelinePlayButton = document.getElementById('timeline-play');
var timelineSpeedButton = document.getElementById('timeline-speed');
var timelineTimer = null;
var timelineSpeedOptions = [30000, 15000, 7500];
var timelineSpeedLabels = ['30s', '15s', '7.5s'];
var timelineSpeedIndex = 0;
var timelineStep = Math.max(1, Math.ceil(timelineActivities.length / 10));

function updateTimelineCount(count) {
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
}

function stopTimelinePlayback() {
    if (timelineTimer !== null) window.clearInterval(timelineTimer);
    timelineTimer = null;
    timelinePlayButton.textContent = 'Play';
    timelinePlayButton.setAttribute('aria-label', 'Play timeline');
}

function startTimelinePlayback() {
    if (Number(timelineSlider.value) >= timelineActivities.length) updateTimelineCount(0);
    timelinePlayButton.textContent = 'Pause';
    timelinePlayButton.setAttribute('aria-label', 'Pause timeline');
    timelineTimer = window.setInterval(function () {
        var next = Number(timelineSlider.value) + 1;
        updateTimelineCount(next);
        if (next >= timelineActivities.length) stopTimelinePlayback();
    }, Math.max(1, timelineSpeedOptions[timelineSpeedIndex] / timelineActivities.length));
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
timelinePlayButton.addEventListener('click', function () {
    if (timelineTimer === null) startTimelinePlayback(); else stopTimelinePlayback();
});
timelineSpeedButton.addEventListener('click', function () {
    timelineSpeedIndex = (timelineSpeedIndex + 1) % timelineSpeedOptions.length;
    timelineSpeedButton.textContent = timelineSpeedLabels[timelineSpeedIndex];
    timelineSpeedButton.setAttribute('aria-label', 'Timeline duration ' + timelineSpeedLabels[timelineSpeedIndex]);
    if (timelineTimer !== null) {
        stopTimelinePlayback();
        startTimelinePlayback();
    }
});
updateTimelineCount(0);