import assert from 'node:assert/strict';
import fs from 'node:fs';
import {createRequire} from 'node:module';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const sharp = createRequire(import.meta.url)('C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');

const deliverablesDirectory = path.dirname(fileURLToPath(import.meta.url));
const shaderPath = path.join(deliverablesDirectory, 'TherapyGame/Weather/Shaders/WellnessSkySampling.hlsl');
const shader = fs.readFileSync(shaderPath, 'utf8');

assert.match(shader, /rotated\.xz\s*\/\s*sqrt\(max\(1e-4,1\+rotated\.y\)\)/);
assert.doesNotMatch(shader, /atan2\(rotated\.z,rotated\.x\)/);

const frac = value => value - Math.floor(value);
function hash(x, y) {
  return [
    frac(Math.sin(x * 127.1 + y * 311.7) * 43758.5453),
    frac(Math.sin(x * 269.5 + y * 183.3) * 43758.5453),
    frac(Math.sin(x * 419.2 + y * 371.9) * 43758.5453),
  ];
}

// In this projection radius squared is 1 - direction.y, so equal-width
// radius-squared bands cover equal solid angles on the upper hemisphere.
const radialBands = new Array(5).fill(0);
const azimuthSectors = new Array(8).fill(0);
const stars = [];
let starCount = 0;

for (const [scale, threshold, offsetX, offsetY] of [[240, .978, 0, 0], [96, .994, .173, .219]]) {
  for (let y = Math.floor(offsetY * scale); y < Math.ceil((1 + offsetY) * scale); y++) {
    for (let x = Math.floor(offsetX * scale); x < Math.ceil((1 + offsetX) * scale); x++) {
      const random = hash(x, y);
      if (random[2] < threshold) continue;
      const u = (x + .14 + random[0] * .72) / scale - offsetX;
      const v = (y + .14 + random[1] * .72) / scale - offsetY;
      const qx = 2 * (u - .5), qy = 2 * (v - .5);
      const radiusSquared = qx * qx + qy * qy;
      // Match the fully visible portion of the shader's horizon fade.
      if (radiusSquared > .65) continue;
      radialBands[Math.min(4, Math.floor(radiusSquared / .65 * 5))]++;
      const angle = (Math.atan2(qy, qx) + Math.PI * 2) % (Math.PI * 2);
      azimuthSectors[Math.min(7, Math.floor(angle / (Math.PI * 2) * 8))]++;
      stars.push({qx, qy, warm: random[0] > .5, bright: random[2] > .995});
      starCount++;
    }
  }
}

const spread = counts => Math.max(...counts) / Math.min(...counts);
assert(starCount > 500, `Expected a useful statistical sample, got ${starCount}`);
assert(spread(radialBands) < 1.45, `Uneven elevation bands: ${radialBands.join(', ')}`);
assert(spread(azimuthSectors) < 1.65, `Uneven azimuth sectors: ${azimuthSectors.join(', ')}`);

const width = 960, height = 600, pixels = Buffer.alloc(width * height * 3);
for (let y = 0; y < height; y++) {
  const vignette = 1 - .22 * Math.hypot((y - height / 2) / height, 0);
  for (let x = 0; x < width; x++) {
    const index = (y * width + x) * 3;
    pixels[index] = Math.round(3 * vignette);
    pixels[index + 1] = Math.round(6 * vignette);
    pixels[index + 2] = Math.round(14 * vignette);
  }
}
const tanHalfVerticalFov = Math.tan(105 / 2 * Math.PI / 180), aspect = width / height;
let renderedStars = 0;
const renderedPositions = new Set();
for (const star of stars) {
  const radiusSquared = star.qx * star.qx + star.qy * star.qy;
  const up = 1 - radiusSquared;
  const horizontalScale = Math.sqrt(2 - radiusSquared);
  const sx = star.qx * horizontalScale / (up * tanHalfVerticalFov * aspect);
  const sy = star.qy * horizontalScale / (up * tanHalfVerticalFov);
  const px = Math.round((sx * .5 + .5) * width), py = Math.round((.5 - sy * .5) * height);
  if (px < 0 || px >= width || py < 0 || py >= height) continue;
  renderedStars++;
  renderedPositions.add(`${px},${py}`);
  const radius = star.bright ? 2 : 1;
  const tint = star.warm ? [255, 229, 190] : [196, 219, 255];
  for (let oy = -radius; oy <= radius; oy++) for (let ox = -radius; ox <= radius; ox++) {
    if (ox * ox + oy * oy > radius * radius || px + ox < 0 || px + ox >= width || py + oy < 0 || py + oy >= height) continue;
    const index = ((py + oy) * width + px + ox) * 3;
    for (let channel = 0; channel < 3; channel++) pixels[index + channel] = tint[channel];
  }
}
const outputDirectory = path.join(deliverablesDirectory, 'NightSkyVarietyCodeCheck');
fs.mkdirSync(outputDirectory, {recursive: true});
await sharp(pixels, {raw: {width, height, channels: 3}}).png().toFile(`${outputDirectory}/Even-star-overhead-CPU-preview.png`);

assert(renderedStars > 400, `Expected at least 400 stars in the overhead preview, got ${renderedStars}`);
assert(renderedPositions.size > 400, `Expected at least 400 distinct preview positions, got ${renderedPositions.size}`);
console.log(`PASS: ${starCount} deterministic stars; radial bands ${radialBands.join('/')}; azimuth sectors ${azimuthSectors.join('/')}; ${renderedPositions.size} distinct stars in the overhead preview.`);
