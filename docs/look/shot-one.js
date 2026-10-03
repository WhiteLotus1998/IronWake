// Rasterises one SVG with the preinstalled Chromium: NODE_PATH=$(npm root -g) node docs/look/shot-one.js <in.svg> <out.png> <width> <height>
const { chromium } = require('playwright');
const path = require('path');
(async () => {
  const [svg, out, w, h] = process.argv.slice(2);
  const b = await chromium.launch({ executablePath: process.env.CHROMIUM || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome' });
  const p = await b.newPage({ viewport: { width: +w, height: +h }, deviceScaleFactor: 1 });
  await p.goto('file://' + path.resolve(svg));
  await p.screenshot({ path: out, clip: { x: 0, y: 0, width: +w, height: +h } });
  await b.close();
})();
