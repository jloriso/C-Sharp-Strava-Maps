import { defineConfig } from 'vite';
import { viteSingleFile } from 'vite-plugin-singlefile';

// Each page is built separately by scripts/build-pages.mjs (which supplies the HTML entry),
// and inlined into a single self-contained HTML file. The C# app loads these from the
// build output and fills in their {{PAGE_DATA_JSON}} placeholder at runtime.
export default defineConfig({
  publicDir: false,
  plugins: [viteSingleFile()],
  build: {
    outDir: 'dist',
    emptyOutDir: false,
    modulePreload: { polyfill: false },
  },
});
