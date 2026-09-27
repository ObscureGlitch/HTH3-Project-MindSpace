// CPU-only directional study of the two analytic aurora curtains. Not a Unity screenshot.
import { createRequire } from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
sharp.cache(false);sharp.concurrency(1);
const width=960,height=460,pixels=Buffer.alloc(width*height*3);
const clamp=x=>Math.max(0,Math.min(1,x));
const smooth=(a,b,x)=>{const t=clamp((x-a)/(b-a));return t*t*(3-2*t)};
const mix=(a,b,t)=>a.map((v,i)=>v+(b[i]-v)*t);
function aurora(az,el,seconds) {
 const y=Math.sin(el),u=az,vista=smooth(.02,.48,Math.cos(u))*smooth(.04,.16,y)*(1-smooth(.75,.91,y));
 const color=[0,0,0];
 for(let ribbon=0;ribbon<2;ribbon++) {
  const t=seconds+ribbon*49,bend=u+.042*Math.sin(u*7+t*.021)+.022*Math.sin(u*13-t*.014);
  const edge=.17+ribbon*.13+.047*Math.sin(u*3+t*.013)+.018*Math.sin(u*9-t*.021);
  const h=(y-edge)/(.29+.045*Math.sin(u*4-t*.009));
  const curtain=smooth(-.025,.085,h)*2**(-Math.max(0,h)*3.6)*(1-smooth(.7,1.15,h));
  const broad=.5+.5*Math.sin(bend*31+t*.037),fine=.5+.5*Math.sin(bend*93-t*.053);
  const rays=.22+.53*broad*broad+.25*fine*fine,fold=.64+.36*Math.sin(bend*5+t*.011);
  let tint=mix([.10,.80,.40],[.12,.50,.67],clamp(h*1.6));tint=mix(tint,[.49,.20,.63],smooth(.34,.92,h)*.64);
  const hem=2**(-Math.abs(h-.035)*30)*.08;
  for(let c=0;c<3;c++)color[c]+=(tint[c]*curtain*rays*fold*.44+[.15,.62,.37][c]*hem)*(ribbon?.6:1)*vista;
 }
 return color;
}
let seed=98123;function random(){seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296}
const stars=Array.from({length:380},()=>({x:random()*width,y:random()*height,b:.04+random()**3*.35}));
for(let y=0;y<height;y++)for(let x=0;x<width;x++) {
 const az=(x/width-.5)*2.8,el=(1-y/height)*.85,up=Math.sin(el);
 let color=mix([.035,.053,.085],[.008,.014,.032],up**.4);
 const light=aurora(az,el,55);color=color.map((v,c)=>v+light[c]*.8);
 // Sparse pinpoints give scale to the shader study; the game keeps its original star map.
 for(const s of stars) {const d=(s.x-x)**2+(s.y-y)**2;if(d<2.2)color=color.map(v=>v+s.b*(1-d/2.2)*smooth(.04,.35,up));}
 const i=(y*width+x)*3;for(let c=0;c<3;c++)pixels[i+c]=Math.round(255*clamp(color[c])**(1/2.2));
}
await sharp(pixels,{raw:{width,height,channels:3}}).png().toFile('D:/Hack the Hill/Deliverables/NightSkyCodeCheck/aurora-cpu-study.png');
console.log('CPU-only aurora study saved.');
