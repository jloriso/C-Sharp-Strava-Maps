import './index.css';
import { createAppNavigation, type NavigationLink } from '../Features/Controls/AppNavigation/AppNavigationControl';
import { readPageData } from '../Features/Shared/pageData';
import { getElement } from '../Features/Shared/types';

/** Mirrors IndexHtmlBuilder.cs. */
interface IndexPageData {
  links: NavigationLink[];
  initialPage: string;
}

const data = readPageData<IndexPageData>();
createAppNavigation(
  getElement('app-navigation'),
  getElement<HTMLIFrameElement>('page-frame'),
  data.links,
  data.initialPage,
);
