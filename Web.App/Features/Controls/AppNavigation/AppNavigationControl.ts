import './AppNavigationControl.css';

export interface NavigationLink {
  key: string;
  label: string;
  href: string;
}

/** Top-level nav bar that loads each page into the shared content iframe. */
export function createAppNavigation(
  navigation: HTMLElement,
  pageFrame: HTMLIFrameElement,
  links: NavigationLink[],
  initialPage: string,
): void {
  const anchors = links.map(link => {
    const anchor = document.createElement('a');
    anchor.className = 'nav-link';
    anchor.href = link.href;
    anchor.textContent = link.label;
    anchor.addEventListener('click', event => {
      event.preventDefault();
      showPage(link);
    });
    return anchor;
  });
  navigation.append(...anchors);

  function showPage(link: NavigationLink): void {
    links.forEach((candidate, i) => {
      const active = candidate.key === link.key;
      anchors[i].classList.toggle('active', active);
      if (active) anchors[i].setAttribute('aria-current', 'page');
      else anchors[i].removeAttribute('aria-current');
    });
    pageFrame.title = link.label;
    pageFrame.src = link.href;
  }

  const initial = links.find(link => link.key === initialPage) ?? links[0];
  if (initial) showPage(initial);
}
