/**
 * Reads the JSON the C# app injects into each page's
 * `<script id="page-data" type="application/json">` block (see PageDataBuilder.cs).
 * The generic type is the page's data contract; keep it in sync with its C# HtmlBuilder.
 */
export function readPageData<T>(): T {
  const element = document.getElementById('page-data');
  if (!element?.textContent) throw new Error('Missing #page-data element.');
  return JSON.parse(element.textContent) as T;
}
