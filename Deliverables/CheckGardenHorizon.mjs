// Small CPU-only preview of the shader silhouettes, not a Unity render.
import fs from 'node:fs';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
const sharp = createRequire(import.meta.url)('C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const output = 'D:/Hack the Hill/Deliverables/GardenBoundaryCodeCheck';
fs.mkdirSync(output, {recursive:true});
const frac = x => x - Math.floor(x), sat = x => Math.max(0, Math.min(1,x));
const smooth = (a,b,x) => { const t=sat((x-a)/(b-a)); return t*t*(3-2*t); };
const mix = (a,b,t) => a.map((v,i)=>v*(1-t)+b[i]*t);
const ridge = (t,n,p)=>Math.abs(frac(t*n+p)*2-1);
function profiles(t) {
 const a=t*Math.PI*2, cell=Math.floor(t*384), seed=frac(Math.sin(((cell%384)+384)%384*127.1+19.3)*43758.5453);
 return [.16+.04*Math.sin(a*3+.8)+.03*Math.sin(a*7+2.1)+.055*ridge(t,13,.27)+.014*ridge(t,31,.11),
  .105+.022*Math.sin(a*4+1.7)+.028*ridge(t,11,.37)+.011*ridge(t,23,.4),
  .035+.014*Math.sin(a*3+1.4)+.011*Math.sin(a*8+.6)+Math.pow(1-Math.abs(frac(t*384)*2-1),1.6)*(.004+seed*.008)];
}
let checks=0;
for(let i=0;i<=8192;i++) {
 const t=i/8192,p=profiles(t),q=profiles(t+1);
 for(let k=0;k<3;k++) { assert(Number.isFinite(p[k])&&p[k]>0&&p[k]<.34); assert(Math.abs(p[k]-q[k])<1e-6); checks+=2; }
}
const model=JSON.parse(fs.readFileSync('D:/Hack the Hill/Deliverables/TherapyGame/Exterior/Source/GardenModel.json','utf8'));
const vertices=model.colliders.find(c=>c.name==='Walkable terrain').positions, edge=new Map();
for(let i=0;i<vertices.length;i+=3) {
 const [x,y,z]=vertices.slice(i,i+3);
 if(x===-40||x===50||z===-42||z===36) {
  const key=x+','+z; if(edge.has(key))assert.equal(edge.get(key)[1],y);
  edge.set(key,[x,y,z]);
 }
}
assert.equal(edge.size,224);
const perimeter=[...edge.values()].sort((a,b)=>Math.atan2(a[2]+3,a[0]-5)-Math.atan2(b[2]+3,b[0]-5));
perimeter.forEach((p,i)=>{const q=perimeter[(i+1)%perimeter.length];assert(Math.abs(Math.hypot(p[0]-q[0],p[2]-q[2])-1.5)<1e-6);checks++;});
fs.writeFileSync(output+'/ActualPerimeter.json',JSON.stringify(perimeter));
// 360 degrees horizontally; four compact bands: day, sunset, night, rain.
const conditions=[{n:0,s:0,h:[.69,.8,.89],top:[.19,.42,.72]},
 {n:.4,s:0,h:[.62,.49,.39],top:[.11,.22,.39]},
 {n:1,s:0,h:[.035,.053,.085],top:[.008,.014,.032]},
 {n:0,s:1,h:[.61,.68,.72],top:[.34,.45,.55]}];
const width=1280, band=144, height=band*4, buffer=Buffer.alloc(width*height*3);
conditions.forEach(({n,s,h,top},row)=>{
 for(let y=0;y<band;y++)for(let x=0;x<width;x++) {
  const t=x/(width-1),dy=.37-y/(band-1)*.46,dark=smooth(.28,.97,n),p=profiles(t);
  let col=mix(h,top,Math.pow(sat(dy),.4));
  const colors=[mix(mix([.40,.53,.54],[.027,.041,.061],dark),h,.64+s*.21),
   mix(mix([.28,.43,.40],[.018,.031,.042],dark),h,.49+s*.29),
   mix(mix([.22,.36,.27],[.013,.025,.031],dark),h,.33+s*.38)];
  colors[0]=colors[0].map(v=>v*(.965+.035*ridge(t,13,.27)));
  colors[0]=mix(colors[0],mix([.82,.85,.8],h,.78),smooth(.245,.280,p[0])*smooth(p[0]-.015,p[0]-.003,dy)*(1-dark)*.6);
  if(dy<=.34) { for(let k=0;k<3;k++) col=mix(col,colors[k],1-smooth(p[k]-.0015,p[k]+.0015,dy)); col=mix(col,h,1-smooth(-.12,.018,dy)); }
  for(let c=0;c<3;c++)buffer[((row*band+y)*width+x)*3+c]=Math.round(sat(col[c])*255);
 }
});
await sharp(buffer,{raw:{width,height,channels:3}}).png().toFile(output+'/Horizon-CPU-preview.png');
const summary=`PASS: ${checks} CPU skyline/perimeter checks; seamless 360-degree profiles, below .34 elevation, actual 224-point terrain perimeter complete. Preview bands: daylight / sunset / night / rain. Not a Unity render.\n`;
fs.writeFileSync(output+'/SkylineChecks.txt',summary);console.log(summary);
