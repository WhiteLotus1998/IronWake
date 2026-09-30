// Rasterises docs/look/*.svg with the preinstalled Chromium: NODE_PATH=$(npm root -g) node docs/look/shot.js $PWD/docs/look
const { chromium } = require('playwright');
(async () => {
  const dir = process.argv[2];
  const b = await chromium.launch({ executablePath: process.env.CHROMIUM || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome' });
  async function shot(svg, out, w, h, scale, clip) {
    const p = await b.newPage({ viewport: { width: w, height: h }, deviceScaleFactor: scale });
    await p.goto('file://' + dir + '/' + svg);
    await p.screenshot({ path: dir + '/' + out, clip: clip || { x: 0, y: 0, width: w, height: h } });
    await p.close();
  }
  await shot('the_tollgate-turn1.svg', 'the_tollgate-turn1.png', 1280, 720, 1);
  // 4x crops: the captain and Wren (player), the bandit leader and the warden (enemy), the forecast box
  await shot('the_tollgate-turn1.svg', 'crop-player-4x.png', 1280, 720, 4, { x: 24 + 5 * 48, y: 64 + 10 * 48 + 8, width: 96, height: 88 });
  await shot('the_tollgate-turn1.svg', 'crop-enemy-4x.png', 1280, 720, 4, { x: 24 + 6 * 48, y: 64 + 1 * 48 + 2, width: 96, height: 94 });
  await shot('forecast.svg', 'crop-forecast-4x.png', 580, 300, 4);
  await shot('silhouettes.svg', 'silhouettes.png', 640, 300, 2);
  await b.close();
})();
