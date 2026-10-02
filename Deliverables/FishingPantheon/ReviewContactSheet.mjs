import {readFileSync,writeFileSync} from 'node:fs';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url),{createCanvas,loadImage}=require('C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas');
const stats=JSON.parse(readFileSync('ModelPreparation.json'));
const names=stats.fish.map(f=>f.name),canvas=createCanvas(1536,1080),ctx=canvas.getContext('2d');
ctx.fillStyle='#0c1b23';ctx.fillRect(0,0,1536,1080);ctx.font='bold 32px sans-serif';ctx.fillStyle='#f5efda';ctx.fillText('MINDSPACE / KOI PANTHEON',36,49);
ctx.font='17px sans-serif';ctx.fillStyle='#9ddac6';ctx.fillText('Actual Unity model renders • 16 new catchable koi • animated collection previews',37,79);
for(let i=0;i<names.length;i++){
 const image=await loadImage('Checks/Koi-'+names[i]+'.png');const x=(i%4)*384,y=100+Math.floor(i/4)*240;
 ctx.strokeStyle='#304449';ctx.strokeRect(x+12,y+8,360,221);ctx.drawImage(image,x+17,y+16,350,197);
 ctx.fillStyle='#ebeada';ctx.font='18px sans-serif';ctx.fillText(names[i].toUpperCase(),x+28,y+216);
}
writeFileSync('Checks/PantheonContactSheet.png',canvas.toBuffer('image/png'));
