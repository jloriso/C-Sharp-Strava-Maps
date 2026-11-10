if (window.self !== window.top) {
    var mapTypeControl = L.control({ position: 'topleft' });
    mapTypeControl.onAdd = function () {
        var div = L.DomUtil.create('div', 'legend map-type-control');
        div.innerHTML = '<div class="map-type-buttons">{{MAP_TYPE_BUTTONS_HTML}}</div>';
        L.DomEvent.disableClickPropagation(div);
        return div;
    };
    mapTypeControl.addTo(map);
    document.querySelectorAll('.map-type-btn').forEach(function (button) {
        button.addEventListener('click', function () {
            var targetMap = button.getAttribute('data-map');
            if (button.getAttribute('aria-pressed') === 'true') return;
            window.parent.postMessage({ type: 'gpxworldmap-switch-map', map: targetMap }, window.location.origin);
        });
    });
}