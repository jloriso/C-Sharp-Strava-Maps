var homeControl = L.control({ position: 'topleft' });
homeControl.onAdd = function () {
    var div = L.DomUtil.create('div', 'legend home-control');
    div.innerHTML = '<button class="home-btn" type="button" aria-label="Go to home page" title="Home">' +
        '<svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true" focusable="false">' +
        '<path fill="currentColor" d="M12 3l9 7.5V21a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1V10.5L12 3z"/>' +
        '</svg>' +
        '</button>';
    L.DomEvent.disableClickPropagation(div);
    return div;
};
homeControl.addTo(map);

var homeBtn = document.querySelector('.home-btn');
if (homeBtn) {
    homeBtn.addEventListener('click', function () {
        window.location.href = {{HOME_URL_JS}};
    });
}