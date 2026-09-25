var activityTypeControl = L.control({ position: 'bottomleft' });
activityTypeControl.onAdd = function () {
    var div = L.DomUtil.create('div', 'legend activity-type-control');
    div.innerHTML = '<strong>Activity Type</strong><br>{{ACTIVITY_TYPE_ROWS_HTML}}';
    L.DomEvent.disableClickPropagation(div);
    return div;
};
activityTypeControl.addTo(map);
document.querySelectorAll('.activity-type-toggle').forEach(function (cb) {
    cb.addEventListener('change', function () {
        if (typeof window.onActivityTypeToggle === 'function') {
            window.onActivityTypeToggle(cb.getAttribute('data-type'), cb.checked);
        }
    });
});
