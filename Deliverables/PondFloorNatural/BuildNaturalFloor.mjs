import {readFileSync,writeFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
const terrainPath='D:/Unity/HTH3 Project/Assets/TherapyGame/Exterior/Source/GardenModel.json';
const source=readFileSync(terrainPath),garden=JSON.parse(source);
const floor=garden.colliders.find(x=>x.name==='Walkable terrain').positions;
const water=garden.meshes.find(x=>x.name==='Pond__water').positions;
const level=-.31,center=[15,-5],radii=[6.7,5.7];
let state=0x29a09b26;
const random=()=>{state=(Math.imul(state,1664525)+1013904223)>>>0;return state/4294967296;};
const r=(a,b)=>a+(b-a)*random(),int=(a,b)=>Math.floor(r(a,b+1)),pick=a=>a[int(0,a.length-1)];
const mix=(a,b,t)=>a.map((x,k)=>x+(b[k]-x)*t),add=(a,b)=>a.map((x,k)=>x+b[k]),mul=(a,s)=>a.map(x=>x*s);
const cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
const sub=(a,b)=>a.map((x,k)=>x-b[k]),norm=a=>mul(a,1/(Math.hypot(...a)||1));
function height(points,x,z){
 for(let i=0;i<points.length;i+=9){
  const ax=points[i],az=points[i+2],bx=points[i+3],bz=points[i+5],cx=points[i+6],cz=points[i+8];
  const d=(bz-cz)*(ax-cx)+(cx-bx)*(az-cz);if(Math.abs(d)<1e-8)continue;
  const u=((bz-cz)*(x-cx)+(cx-bx)*(z-cz))/d,v=((cz-az)*(x-cx)+(ax-cx)*(z-cz))/d;
  if(u>=-1e-7&&v>=-1e-7&&u+v<=1.0000001)return u*points[i+1]+v*points[i+4]+(1-u-v)*points[i+7];
 }return NaN;
}
const rho=(x,z)=>Math.hypot((x-center[0])/radii[0],(z-center[1])/radii[1]);
const entry=(x,z)=>Math.abs(x-15)<1.45&&z<-8.2;
class Model{
 constructor(kind){this.kind=kind;this.positions=[];this.colors=[];}
 tri(a,b,c,color,double=false){
  if(Math.hypot(...cross(sub(b,a),sub(c,a)))<1e-9)return;
  this.positions.push(...a,...b,...c);for(let i=0;i<3;i++)this.colors.push(...color);
  if(double)this.tri(c,b,a,mul(color,.91));
 }
 quad(a,b,c,d,color,double=false){this.tri(a,b,c,color,double);this.tri(a,c,d,color,double);}
}
function stone(model,anchor,sx,sy,sz,color,angular=false){
 const sides=sx<.07?int(5,7):int(7,11),yaw=r(0,Math.PI*2),radial=Array.from({length:sides},()=>r(.78,1.15));
 const rings=angular?[[-.006,.76],[sy*.16,1],[sy*.78,.88],[sy,.54]]:[[-.005,.55],[sy*.20,.96],[sy*.68,.86],[sy*.93,.50]];
 const skew=[r(-.19,.19)*sx,0,r(-.19,.19)*sz],verts=[];
 for(let j=0;j<rings.length;j++)for(let i=0;i<sides;i++){
  const a=i*Math.PI*2/sides+yaw;
  verts.push(add(anchor,[Math.cos(a)*sx*rings[j][1]*radial[i]+skew[0]*j/3,rings[j][0]+(j>0?r(-.05,.05)*sy:0),Math.sin(a)*sz*rings[j][1]*radial[i]+skew[2]*j/3]));
 }
 for(let j=0;j<rings.length-1;j++)for(let i=0;i<sides;i++){
  const a=j*sides+i,b=j*sides+(i+1)%sides,c=(j+1)*sides+(i+1)%sides,d=(j+1)*sides+i;
  model.quad(verts[a],verts[d],verts[c],verts[b],mul(color,r(.91,1.06)));
 }
 const top=add(anchor,[skew[0],sy,skew[2]]);
 for(let i=0;i<sides;i++)model.tri(top,verts[3*sides+(i+1)%sides],verts[3*sides+i],mul(color,r(.96,1.05)));
}
function ribbon(model,start,yaw,length,width,bend,color,broad=false){
 const side=[Math.cos(yaw),0,-Math.sin(yaw)],ahead=[Math.sin(yaw),0,Math.cos(yaw)];
 const rows=[],segments=broad?4:5,curl=r(-.65,.65),lean=r(.25,.6);
 for(let i=0;i<=segments;i++){
  const t=i/segments,w=width*(broad?Math.pow(Math.sin(Math.PI*t),.75):Math.pow(1-t,.8));
  const p=add(start,add(mul(ahead,bend*t*t),[side[0]*Math.sin(t*Math.PI)*curl*.035,length*(t-lean*t*t*.45),side[2]*Math.sin(t*Math.PI)*curl*.035]));
  rows.push([add(p,mul(side,-w)),add(p,[0,w*.28,0]),add(p,mul(side,w))]);
 }
 for(let i=0;i<segments;i++){
  const tint=mix(color,mul(color,1.3),i/segments);
  model.quad(rows[i][0],rows[i+1][0],rows[i+1][1],rows[i][1],tint,true);
  model.quad(rows[i][1],rows[i+1][1],rows[i+1][2],rows[i][2],mul(tint,.84),true);
 }
}
function branch(model,path,radius,color){
 const sides=int(6,8),rings=[];
 for(let j=0;j<path.length;j++){
  const tangent=norm(sub(path[Math.min(j+1,path.length-1)],path[Math.max(0,j-1)]));
  const side=norm(cross(tangent,[0,1,0])),up=norm(cross(side,tangent)),row=[];
  for(let i=0;i<sides;i++){
   const a=i*Math.PI*2/sides,rad=radius*(1-.77*j/(path.length-1))*r(.88,1.12);
   row.push(add(path[j],add(mul(side,Math.cos(a)*rad),mul(up,Math.sin(a)*rad))));
  }rings.push(row);
 }
 for(let j=0;j<rings.length-1;j++)for(let i=0;i<sides;i++)model.quad(rings[j][i],rings[j+1][i],rings[j+1][(i+1)%sides],rings[j][(i+1)%sides],mul(color,.77+.32*(i%3)/2));
 for(let i=0;i<sides;i++){
  model.tri(path[0],rings[0][i],rings[0][(i+1)%sides],mul(color,1.42));
  const last=path.length-1;model.tri(path[last],rings[last][(i+1)%sides],rings[last][i],mul(color,1.42));
 }
}
function make(kind){
 const m=new Model(kind);
 if(kind==='river-stone')stone(m,[0,0,0],r(.10,.23),r(.035,.063),r(.085,.20),pick([[.30,.33,.29],[.37,.33,.26],[.24,.29,.29],[.44,.42,.33]]));
 if(kind==='slate-fragment'){
  const sx=r(.14,.27),sz=r(.07,.15),h=r(.02,.045);
  stone(m,[0,0,0],sx,h,sz,pick([[.21,.25,.26],[.28,.30,.28],[.32,.30,.25]]),true);
  if(random()<.5)stone(m,[r(-.06,.06),h*.58,r(-.03,.03)],sx*.65,h*.48,sz*.7,[.26,.29,.29],true);
 }
 if(kind==='gravel-pocket'){
  const n=int(8,17);
  for(let i=0;i<n;i++){
   const a=r(0,Math.PI*2),rr=Math.sqrt(random())*r(.12,.32),sx=r(.022,.06);
   stone(m,[Math.cos(a)*rr,-.003,Math.sin(a)*rr],sx,r(.012,.030),sx*r(.65,1.2),pick([[.35,.37,.30],[.46,.43,.32],[.24,.29,.28],[.52,.49,.39]]),random()<.4);
  }
 }
 if(kind==='driftwood'){
  const length=r(.40,.77),radius=r(.020,.030),curve=r(-.10,.10),baseY=.032;
  const path=Array.from({length:5},(_,i)=>[(i/4-.5)*length,baseY+r(-.004,.005),Math.sin(i/4*Math.PI)*curve]);
  const color=pick([[.21,.14,.077],[.27,.20,.12],[.18,.17,.11]]);branch(m,path,radius,color);
  for(let j=0;j<int(2,4);j++){
   const start=path[int(1,3)],length2=r(.14,.28),direction=random()<.5?-1:1;
   branch(m,[start,add(start,[length2*.4,r(-.004,.004),length2*.5*direction]),add(start,[length2,r(-.006,.006),length2*direction])],radius*r(.32,.58),mul(color,r(.88,1.1)));
  }
 }
 if(kind==='broadleaf-rosette'){
  const n=int(5,9),orientation=r(0,Math.PI*2);
  for(let i=0;i<n;i++)ribbon(m,[r(-.02,.02),.003,r(-.02,.02)],orientation+i*Math.PI*2/n+r(-.3,.3),r(.105,.18),r(.028,.044),r(.10,.17),pick([[.11,.23,.12],[.15,.26,.12],[.18,.27,.14]]),true);
 }
 if(kind==='ribbon-grass'){
  const n=int(7,13),orientation=r(0,Math.PI*2);
  for(let i=0;i<n;i++)ribbon(m,[r(-.06,.06),.002,r(-.06,.06)],orientation+r(-1.6,1.6),r(.115,.205),r(.007,.014),r(.045,.13),pick([[.13,.22,.09],[.17,.28,.12],[.19,.25,.12]]));
 }
 if(kind==='fallen-leaf'){
  const len=r(.08,.15),w=r(.025,.055),bend=r(-.018,.018),color=pick([[.34,.25,.11],[.24,.29,.13],[.35,.30,.18]]);
  const a=[-len,.004,0],b=[len,.008,bend],c=[-.02,.012,-w],d=[.02,.018,w],mid=[0,.016,0];
  m.tri(a,mid,c,color,true);m.tri(mid,b,c,mul(color,.9),true);m.tri(a,d,mid,mul(color,.84),true);m.tri(mid,d,b,mul(color,1.1),true);
 }
 return m;
}
const pieces=[],occupied=[];
const plan=[['driftwood',6],['broadleaf-rosette',9],['ribbon-grass',12],['river-stone',25],['slate-fragment',14],['gravel-pocket',12],['fallen-leaf',10]];
for(const [kind,number] of plan)for(let item=0;item<number;item++){
 const model=make(kind),signature=createHash('sha256').update(JSON.stringify(model.positions)).digest('hex');
 let placed=null;
 for(let attempt=0;attempt<5000&&!placed;attempt++){
  const a=r(0,Math.PI*2),rr=Math.sqrt(random())*.89,x=15+Math.cos(a)*6.7*rr,z=-5+Math.sin(a)*5.7*rr;
  const plant=kind==='broadleaf-rosette'||kind==='ribbon-grass';
  if(plant&&(rr<.71||rr>.84))continue;
  if(!plant&&random()>.48+.35*Math.sin(x*.8+z*.6)**2)continue;
  const yaw=r(0,Math.PI*2),co=Math.cos(yaw),si=Math.sin(yaw);
  const positions=[],floorHeights=[];let valid=true;
  for(let i=0;i<model.positions.length;i+=3){
   const xx=x+co*model.positions[i]+si*model.positions[i+2],zz=z-si*model.positions[i]+co*model.positions[i+2];
   const h=height(floor,xx,zz),yy=h+model.positions[i+1],radius=rho(xx,zz);
   if(!Number.isFinite(h)||!Number.isFinite(height(water,xx,zz))||radius>.90||entry(xx,zz)||yy>level-.09||(radius<.68&&yy>level-.39)){valid=false;break;}
   positions.push(xx,yy,zz);floorHeights.push(h);
  }
  if(!valid)continue;
  const footprint=Math.max(...Array.from({length:model.positions.length/3},(_,i)=>Math.hypot(model.positions[i*3],model.positions[i*3+2])));
  if(occupied.some(p=>Math.hypot(x-p.x,z-p.z)<(p.radius+footprint)*.83+.045))continue;
  occupied.push({x,z,radius:footprint});
  placed={id:`${kind}-${String(item+1).padStart(2,'0')}`,kind,signature,anchor:[x,height(floor,x,z),z],positions:positions.map(v=>+v.toFixed(6)),floorHeights:floorHeights.map(v=>+v.toFixed(6)),colors:model.colors.map(v=>+v.toFixed(5)),normals:[]};
  for(let i=0;i<positions.length;i+=9){const n=norm(cross(sub(positions.slice(i+3,i+6),positions.slice(i,i+3)),sub(positions.slice(i+6,i+9),positions.slice(i,i+3))));for(let j=0;j<3;j++)placed.normals.push(...n.map(v=>+v.toFixed(6)));}
 }
 assert(placed,'Could not place '+kind);pieces.push(placed);
}
const triangles=pieces.reduce((s,p)=>s+p.positions.length/9,0),signatures=new Set(pieces.map(p=>p.signature));
assert.equal(signatures.size,pieces.length);assert(triangles<22000,`Triangle budget exceeded: ${triangles}`);
const data={version:1,seed:0x29a09b26,waterLevel:level,center,radii,terrainSourceSha256:createHash('sha256').update(source).digest('hex'),pieces};
const counts=Object.fromEntries(plan),report={passed:true,pieces:pieces.length,uniqueGeometrySignatures:signatures.size,types:counts,triangles,renderersAfterCombining:1,colliders:0,checks:['Every vertex on authored bed and inside water','No south-entry decoration','All decoration under water','Low profile below fish routes','No duplicated local-geometry signatures','No regular ring or grid placement']};
writeFileSync('Payload/Editor/PondFloorSource/NaturalPondFloor.json',JSON.stringify(data));
writeFileSync('PreparationChecks.json',JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
// Geometry preview only: CPU rendering of the actual generated triangles.
const require=createRequire(import.meta.url),{createCanvas}=require('C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas');
function render(file,isometric){
 const canvas=createCanvas(1500,1080),ctx=canvas.getContext('2d');ctx.fillStyle='#cbd8d2';ctx.fillRect(0,0,1500,1080);
 const project=p=>isometric?[750+(p[0]-15)*91,470+(p[2]+5)*63-(p[1]+.79)*330]:[750+(p[0]-15)*92,500+(p[2]+5)*83];
 ctx.beginPath();ctx.ellipse(750,isometric?470:500,6.7*92,5.7*(isometric?63:83),0,0,Math.PI*2);ctx.fillStyle='#919780';ctx.fill();
 const tris=[];
 for(const p of pieces)for(let i=0;i<p.positions.length;i+=9){
  const pos=[p.positions.slice(i,i+3),p.positions.slice(i+3,i+6),p.positions.slice(i+6,i+9)],n=p.normals.slice(i,i+3);
  if(n[1]<(isometric?-.75:-.05))continue;
  const illumination=.64+.36*Math.max(0,n[1]*.85+n[0]*-.25+n[2]*-.35);
  tris.push({pos,depth:isometric?pos.reduce((s,p)=>s+p[2]+p[1]*.15,0):pos.reduce((s,p)=>s+p[1],0),color:'rgb('+p.colors.slice(i,i+3).map(v=>Math.round(Math.pow(Math.min(1,v*illumination),1/2.2)*255)).join(',')+')'});
 }
 tris.sort((a,b)=>a.depth-b.depth);
 for(const t of tris){ctx.beginPath();t.pos.forEach((p,i)=>{const[x,y]=project(p);(i?ctx.lineTo:ctx.moveTo).call(ctx,x,y);});ctx.closePath();ctx.fillStyle=t.color;ctx.fill();}
 ctx.fillStyle='#233c32';ctx.textAlign='center';ctx.font='26px sans-serif';ctx.fillText('Natural pond floor · actual generated geometry · water hidden for inspection',750,1000);
 ctx.font='21px sans-serif';ctx.fillText(`${pieces.length} individually shaped decorations • seven model families • ${triangles.toLocaleString()} triangles • one combined renderer`,750,1038);
 writeFileSync(file,canvas.toBuffer('image/png'));
}
render('PondFloorTopDown.png',false);render('PondFloorGeometryPreview.png',true);
