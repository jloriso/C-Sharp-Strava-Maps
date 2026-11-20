import { rm } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { build } from 'vite';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

// Every HTML page the C# app loads. Each is built on its own so it can be inlined into a
// single file (vite-plugin-singlefile doesn't support multiple inputs per build).
const pages = [
  'Index/index.html',
  'Features/Charts/WeeklyMileage/weeklymileage.html',
  'Features/Maps/Heatmap/heatmap.html',
  'Features/Maps/Linemap/linemap.html',
  'Features/Maps/MapSwitcher/maps.html',
];

await rm(path.join(root, 'dist'), { recursive: true, force: true });

for (const page of pages) {
  await build({
    root,
    configFile: path.join(root, 'vite.config.ts'),
    logLevel: 'warn',
    build: { rollupOptions: { input: path.join(root, page) } },
  });
  console.log(`built ${page}`);
}
