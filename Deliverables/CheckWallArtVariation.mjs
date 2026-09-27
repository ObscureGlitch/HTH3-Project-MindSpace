import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';

const sharp=createRequire(import.meta.url)('C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const deliverables=path.dirname(fileURLToPath(import.meta.url));
const textureDirectory=path.join(deliverables,'TherapyGame/WallArt/Textures');
const names=['QuietOrbit','StillWater','BalancedStones'];
const hashes=[],guids=[];

for(const name of names)
{
  const imagePath=path.join(textureDirectory,`${name}.png`);
  const metadata=await sharp(imagePath).metadata();
  assert.equal(metadata.width,1024,`${name} width`);
  assert.equal(metadata.height,1536,`${name} height`);
  assert.equal(metadata.hasAlpha,false,`${name} should be an opaque print`);
  hashes.push(crypto.createHash('sha256').update(fs.readFileSync(imagePath)).digest('hex'));
  const meta=fs.readFileSync(`${imagePath}.meta`,'utf8');
  guids.push(meta.match(/^guid: ([0-9a-f]{32})$/m)?.[1]);
}
assert.equal(new Set(hashes).size,3,'Generated images must be distinct');
assert.equal(new Set(guids).size,3,'Unity texture GUIDs must be distinct');

const upgrade=fs.readFileSync(path.join(deliverables,'TherapyGame/Editor/TherapyWallArtVariationUpgrade.cs'),'utf8');
for(const expected of ['Quiet Orbit','Still Water','Balanced Stones','Botanical artwork 0'])assert(upgrade.includes(expected),`Missing installer invariant: ${expected}`);

const panelWidth=320,panelHeight=480,gap=20,width=panelWidth*3+gap*4,height=panelHeight+gap*2;
const inputs=[];
for(let index=0;index<names.length;index++)inputs.push({
  input:await sharp(path.join(textureDirectory,`${names[index]}.png`)).resize(panelWidth,panelHeight,{fit:'cover'}).png().toBuffer(),
  left:gap+index*(panelWidth+gap),top:gap
});
const outputDirectory=path.join(deliverables,'WallArtVariationCodeCheck');
fs.mkdirSync(outputDirectory,{recursive:true});
await sharp({create:{width,height,channels:3,background:{r:230,g:219,b:196}}}).composite(inputs).png().toFile(path.join(outputDirectory,'Wall-art-contact-sheet.png'));
console.log(`PASS: three distinct opaque 1024x1536 generated prints, three stable Unity GUIDs, installer invariants present.`);
