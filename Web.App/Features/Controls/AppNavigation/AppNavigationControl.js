var navigationLinks = {{NAVIGATION_LINKS_JS}};
var initialPage = {{INITIAL_PAGE_JS}};
var navigation = document.getElementById('app-navigation');
var pageFrame = document.getElementById('page-frame');

function showPage(link) {
    navigation.querySelectorAll('.nav-link').forEach(function (item) {
        var active = item.getAttribute('data-page') === link.key;
        item.classList.toggle('active', active);
        if (active) item.setAttribute('aria-current', 'page');
        else item.removeAttribute('aria-current');
    });
    pageFrame.title = link.label;
    pageFrame.src = link.href;
}

navigationLinks.forEach(function (link) {
    var anchor = document.createElement('a');
    anchor.className = 'nav-link';
    anchor.href = link.href;
    anchor.textContent = link.label;
    anchor.setAttribute('data-page', link.key);
    anchor.addEventListener('click', function (event) {
        event.preventDefault();
        showPage(link);
    });
    navigation.appendChild(anchor);
});

showPage(navigationLinks.find(function (link) { return link.key === initialPage; }) || navigationLinks[0]);