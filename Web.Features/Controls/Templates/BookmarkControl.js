var bookmarkLocations = {{BOOKMARK_LOCATIONS_JS}};
var bookmarkControl = L.control({ position: 'topright' });
bookmarkControl.onAdd = function () {
    var div = L.DomUtil.create('div', 'legend bookmark-control');
    div.innerHTML = '<strong>Jump to</strong><br>' +
        '<div class="bookmark-buttons">{{BOOKMARK_BUTTONS_HTML}}</div>';
    L.DomEvent.disableClickPropagation(div);
    return div;
};
bookmarkControl.addTo(map);
document.querySelectorAll('.bookmark-btn').forEach(function (btn) {
    btn.addEventListener('click', function () {
        var loc = bookmarkLocations[btn.getAttribute('data-location')];
        if (!loc) return;
        map.setView([loc[0], loc[1]], loc[2]);
        document.querySelectorAll('.bookmark-btn').forEach(function (b) { b.classList.remove('active'); });
        btn.classList.add('active');
    });
});
