// 把编辑器源码打包成一个 classic script（IIFE），产物是 ../editor.bundle.js。
//
// 为什么必须打包：预览页面跑在 file:// 下，而 Chromium 会以 CORS 拦掉
// <script type="module">（file:// 的模块加载不被允许），所以 ESM 依赖不能直接引进页面，
// 必须先合成一个普通脚本。esbuild 是唯一需要的开发期依赖。
import { build } from 'esbuild';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));

await build({
  entryPoints: [path.join(here, 'src', 'editor.mjs')],
  outfile: path.join(here, '..', 'editor.bundle.js'),
  bundle: true,
  format: 'iife',
  platform: 'browser',
  // WebView2 在这台机器上是 Chromium 154，取一个远低于它的目标即可。
  target: ['chrome110'],
  minify: true,
  legalComments: 'none',
  logLevel: 'info'
});
