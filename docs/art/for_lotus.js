// Rasterises docs/art/for-lotus.html with the preinstalled Chromium, full page:
// NODE_PATH=$(npm root -g) node docs/art/for_lotus.js $PWD/docs/art
const { chromium } = require('playwright');
(async () => {
  const dir = process.argv[2];
  const b = await chromium.launch({ executablePath: process.env.CHROMIUM || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome' });
  const p = await b.newPage({ viewport: { width: 1448, height: 800 }, deviceScaleFactor: 1 });
  await p.goto('file://' + dir + '/for-lotus.html');
  await p.screenshot({ path: dir + '/for-lotus.png', fullPage: true });
  await b.close();
})();
