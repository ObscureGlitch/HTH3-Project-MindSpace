// CPU approximation of the analytic lunar face, not a Unity/GPU screenshot.
import fs from 'node:fs';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
const {createCanvas,ImageData}=createRequire(import.meta.url)('C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas');
const root='D:/Hack the Hill/Deliverables/CelestialVisibility';
const shader=fs.readFileSync(root+'/Payload/Weather/Shaders/WellnessSkySampling.hlsl','utf8');
assert(shader.includes('const float sunRadius=.0175,moonRadius=.032;'));
const face=fs.readFileSync(root+'/Payload/Weather/Shaders/WellnessMoon.hlsl','utf8');
assert.equal((face.match(/WellnessMoonCrater\(uv/g)||[]).length,6);
assert(face.includes('clamp(.94-maria+craters+grain,.34,1.0)'));
const sat=x=>Math.max(0,Math.min(1,x));
const sm=(a,b,x)=>{const t=sat((x-a)/(b-a));return t*t*(3-2*t);};
const basin=(x,y,cx,cy,sx,sy)=>2**(-(((x-cx)/sx)**2+((y-cy)/sy)**2)*2.4);
function moon(x,y,pixel,updated) {
 const coeff=updated?[.40,.27,.32,.16]:[.29,.21,.25,.12];
 const maria=basin(x,y,-.28,.36,.35,.46)*coeff[0]+basin(x,y,.20,.42,.28,.32)*coeff[1]+basin(x,y,-.47,-.02,.31,.34)*coeff[2]+basin(x,y,.12,-.15,.25,.20)*coeff[3];
 let craters=0;
 const centers=[[.34,-.47,.15],[-.14,-.64,.10],[.55,.18,.13],[-.47,.43,.09]];
 if(updated)centers.push([.08,-.40,.075],[.40,.60,.065]);
 for(const [cx,cy,r]of centers){
  const dx=x-cx,dy=y-cy,d=Math.hypot(dx,dy),aa=Math.max(pixel,updated?.012:.018);
  const rimLight=(dx*(-.6)+dy*.8)/Math.max(d,.001);
  craters+=(1-sm(aa,aa*2.4,Math.abs(d-r)))*((updated?.04:.026)+(updated?.055:.042)*rimLight)-(1-sm(r*.60,r,d))*(updated?.13:.11);
 }
 const grain=Math.sin(x*37+Math.sin(y*19))*Math.sin(y*43-x*11)*.025*sat(1-pixel*18);
 const z=Math.sqrt(sat(1-x*x-y*y)),limb=updated?.74+.26*z:.68+.32*z;
 const albedo=Math.max(updated?.34:.38,Math.min(updated?1:1.08,(updated?.94:.96)-maria+craters+grain));
 return [.97,.965,.92].map(v=>v*albedo*limb);
}
const chordAngle=r=>2*Math.asin(r/2);
const radiusPx=(r,height)=>Math.tan(chordAngle(r))*height/(2*Math.tan(68*Math.PI/360));
const sizeRows=[720,768,1080,1440].map(height=>({height,sunDiameter:2*radiusPx(.0175,height),moonDiameter:2*radiusPx(.032,height),oldMoonDiameter:2*radiusPx(.0085,height)}));
assert(sizeRows[0].moonDiameter>34&&sizeRows[2].moonDiameter>51);
let samples=0,low=1,high=0;
for(const pixel of [.01,.04,.06,.08])for(let y=-1;y<=1;y+=.01)for(let x=-1;x<=1;x+=.01){
 if(x*x+y*y>1)continue;
 const color=moon(x,y,pixel,true);
 for(const channel of color)assert(Number.isFinite(channel)&&channel>=0&&channel<=1);
 if(x*x+y*y<.7**2){low=Math.min(low,color[1]);high=Math.max(high,color[1]);}
 samples++;
}
assert(high-low>.27,'Maria contrast insufficient');
const canvas=createCanvas(1200,530),ctx=canvas.getContext('2d');
ctx.fillStyle='#0c1324';ctx.fillRect(0,0,1200,530);
ctx.fillStyle='#f2ead9';ctx.font='24px Arial';ctx.fillText('Sun & moon visibility study',30,42);
ctx.font='15px Arial';ctx.fillStyle='#aebdca';ctx.fillText('CPU approximation only. Native sizes assume 1080p and the player’s 68° field of view.',30,72);
function disc(cx,cy,r,updated,sun=false){
 const extent=Math.ceil(r*3),w=extent*2+1,pixels=new Uint8ClampedArray(w*w*4);
 for(let y=0;y<w;y++)for(let x=0;x<w;x++){
  const u=(x-extent)/r,v=-(y-extent)/r,d=Math.hypot(u,v),mask=1-sm(1-1/r,1+1/r,d);
  const background=sun?[.20,.42,.72]:[.005,.01,.025];
  let col;
  if(sun)col=background.map((value,i)=>value+[1,.91,.73][i]*mask);
  else{
   const color=moon(u,v,1/r,updated),halo=2**(-d*d/4.15)*(updated?.032:.045);
   col=background.map((value,i)=>(value+[.56,.66,.87][i]*halo)*(1-mask)+color[i]*mask);
  }
  for(let k=0;k<3;k++)pixels[(y*w+x)*4+k]=Math.round(sat(col[k])**(1/2.2)*255);
  pixels[(y*w+x)*4+3]=255;
 }
 const tile=createCanvas(w,w);tile.getContext('2d').putImageData(new ImageData(pixels,w,w),0,0);
 ctx.drawImage(tile,cx-extent,cy-extent);
}
disc(160,220,radiusPx(.0085,1080),false);
disc(430,220,radiusPx(.032,1080),true);
disc(960,258,76,true);
disc(160,378,radiusPx(.00465,1080),false,true);
disc(430,378,radiusPx(.0175,1080),true,true);
ctx.fillStyle='#f2ead9';ctx.font='19px Arial';ctx.fillText('Before',100,126);ctx.fillText('Updated',370,126);ctx.fillText('Moon detail enlarged 3×',828,126);
ctx.font='15px Arial';ctx.fillStyle='#aebdca';
ctx.fillText('Moon ~14 px',108,282);ctx.fillText('Moon ~51 px',378,282);
ctx.fillText('Sun ~7 px',110,443);ctx.fillText('Sun ~28 px',379,443);
ctx.fillText('No new textures, lights or render passes. Clouds, day/night timing and scene lighting are unchanged.',30,501);
fs.writeFileSync(root+'/Celestial-CPU-study.png',canvas.toBuffer('image/png'));
const report={passed:true,type:'CPU formula checks, not Unity rendering',finiteMoonSamples:samples,interiorLinearContrast:high-low,projectedSizes:sizeRows,moonCraterCount:6};
fs.writeFileSync(root+'/VisualMathChecks.json',JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
