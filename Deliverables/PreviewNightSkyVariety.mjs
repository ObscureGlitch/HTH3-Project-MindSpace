// CPU visual approximation and finite-value checks, not a Unity/GPU screenshot.
import fs from 'node:fs';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
const sharp=createRequire(import.meta.url)('C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='D:/Hack the Hill/Deliverables/NightSkyVarietyCodeCheck';fs.mkdirSync(root,{recursive:true});
const sat=x=>Math.max(0,Math.min(1,x)),sm=(a,b,x)=>{const t=sat((x-a)/(b-a));return t*t*(3-2*t);};
const mix=(a,b,t)=>a.map((v,i)=>v*(1-t)+b[i]*t),dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0);
const add=(a,b)=>a.map((v,i)=>v+b[i]),mul=(a,s)=>a.map(v=>v*s),norm=a=>mul(a,1/Math.hypot(...a));
function aurora(d,seconds,az,tilt,spread,palette,phase) {
 const drift=.025*Math.sin(seconds*.003+phase),f=[Math.sin(az+drift),0,Math.cos(az+drift)],r=[f[2],0,-f[0]];
 const tf=add(mul(f,Math.cos(tilt)),[0,Math.sin(tilt),0]),tu=add([0,Math.cos(tilt),0],mul(f,-Math.sin(tilt)));
 const local=[dot(d,r),dot(d,tu),dot(d,tf)],angle=Math.atan2(local[0],local[2]),u=angle/spread;
 const vista=sm(-.12,.36,Math.cos(angle))*sm(.035,.15,d[1])*(1-sm(.87,.98,local[1]));let light=[0,0,0];
 for(let ribbon=0;ribbon<2;ribbon++) {
  const t=seconds+phase+ribbon*49,bend=u+.042*Math.sin(u*7+t*.021)+.022*Math.sin(u*13-t*.014);
  const edge=.13+ribbon*.18+.070*Math.sin(u*3+t*.013)+.022*Math.sin(u*9-t*.021),height=.38+.055*Math.sin(u*4-t*.009),h=(local[1]-edge)/height;
  const curtain=sm(-.025,.085,h)*2**(-Math.max(0,h)*2.5)*(1-sm(.78,1.18,h));
  const broad=.5+.5*Math.sin(bend*31+t*.037),fine=.5+.5*Math.sin(bend*93-t*.053),rays=.22+.53*broad*broad+.25*fine*fine,fold=.64+.36*Math.sin(bend*5+t*.011);
  const green=mix([.16,.88,.43],[.16,.76,.68],palette*.6),pink=mix([.92,.22,.52],[.61,.32,.92],palette);
  let tint=mix(green,[.20,.64,.73],sat(h*1.35)*.55);tint=mix(tint,pink,sm(.28,.82,h)*.92);
  const hem=2**(-Math.abs(h-.035)*30)*.09;
  light=add(light,mul(add(mul(tint,curtain*rays*fold*.67*(1+.8*sm(.25,.90,h))),mul(green,hem)),ribbon===0?1:.65));
 }
 return mul(light,vista*.8);
}
const basin=(uv,c,s)=>2**(-(((uv[0]-c[0])/s[0])**2+((uv[1]-c[1])/s[1])**2)*2.4);
function moon(uv,pixel) {
 const z=Math.sqrt(sat(1-dot(uv,uv))),maria=basin(uv,[-.28,.36],[.35,.46])*.29+basin(uv,[.20,.42],[.28,.32])*.21+basin(uv,[-.47,-.02],[.31,.34])*.25+basin(uv,[.12,-.15],[.25,.20])*.12;
 let craters=0;
 for(const [x,y,r]of [[.34,-.47,.15],[-.14,-.64,.10],[.55,.18,.13],[-.47,.43,.09]]) {
  const d=Math.hypot(uv[0]-x,uv[1]-y),aa=Math.max(pixel,.018),rimLight=((uv[0]-x)*(-.6)+(uv[1]-y)*.8)/Math.max(d,.001);
  craters+=(1-sm(aa,aa*2.4,Math.abs(d-r)))*(.026+.042*rimLight)-(1-sm(r*.60,r,d))*.11;
 }
 const grain=Math.sin(uv[0]*37+Math.sin(uv[1]*19))*Math.sin(uv[1]*43-uv[0]*11)*.025*sat(1-pixel*18);
 return mul([.97,.965,.92],Math.max(.38,Math.min(1.08,.96-maria+craters+grain))*(.68+.32*z));
}
const width=960,band=270,height=band*3,data=Buffer.alloc(width*height*3);let checks=0,pink=0,zenith=0;
for(let row=0;row<3;row++) {
 const tilt=[.32,.90,1.02][row],elev=[.30,.95,1.25][row],palette=[.1,.45,.9][row];
 const forward=[0,Math.sin(elev),Math.cos(elev)],up=[0,Math.cos(elev),-Math.sin(elev)];
 for(let y=0;y<band;y++)for(let x=0;x<width;x++) {
  const xx=(x/width-.5)*2*1.55,yy=(.5-y/band)*2*1.55*band/width;
  const d=norm(add(add(forward,[xx,0,0]),mul(up,yy)));
  let col=mix([.035,.053,.085],[.008,.014,.032],Math.pow(sat(d[1]),.4));
  const glow=aurora(d,90,0,tilt,1.3,palette,80+row*80);
  if(glow[0]>glow[1]*1.05&&glow[0]>.005)pink++;
  if(d[1]>.97&&Math.max(...glow)>.008)zenith++;
  col=add(col,glow);
  if(x>width-130&&y<130) {
   const uv=[(x-(width-65))/49,(y-65)/49];
   if(Math.hypot(...uv)<1)col=moon(uv,.02); // Enlarged face detail swatch, not angular size.
  }
  for(let c=0;c<3;c++){assert(Number.isFinite(col[c])&&col[c]>=0);checks++;data[((row*band+y)*width+x)*3+c]=Math.round(sat(col[c])**(1/1.4)*255);}
 }
}
assert(pink>1000);assert(zenith>1000);
await sharp(data,{raw:{width,height,channels:3}}).png().toFile(root+'/Aurora-and-moon-CPU-preview.png');
const report=`PASS: ${checks} finite nonnegative sky samples, ${pink} pink/violet upper-curtain pixels, ${zenith} illuminated overhead pixels. Moon detail swatches are magnified; image is a CPU approximation, not a Unity render.\n`;
fs.writeFileSync(root+'/VisualMathChecks.txt',report);console.log(report);
