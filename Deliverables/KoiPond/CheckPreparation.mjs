import {readFileSync,writeFileSync} from 'node:fs';
import assert from 'node:assert/strict';
const fish=JSON.parse(readFileSync('Payload/Editor/KoiSource/KoiModels.json'));
const garden=JSON.parse(readFileSync('D:/Unity/HTH3 Project/Assets/TherapyGame/Exterior/Source/GardenModel.json'));
const floor=garden.colliders.find(m=>m.name==='Walkable terrain').positions;
const water=garden.meshes.find(m=>m.name==='Pond__water').positions;
function height(points,x,z){
 for(let i=0;i<points.length;i+=9){
  const ax=points[i],az=points[i+2],bx=points[i+3],bz=points[i+5],cx=points[i+6],cz=points[i+8];
  const d=(bz-cz)*(ax-cx)+(cx-bx)*(az-cz);if(Math.abs(d)<1e-9)continue;
  const u=((bz-cz)*(x-cx)+(cx-bx)*(z-cz))/d,v=((cz-az)*(x-cx)+(ax-cx)*(z-cz))/d;
  if(u>=-1e-7&&v>=-1e-7&&u+v<=1.0000001)return u*points[i+1]+v*points[i+4]+(1-u-v)*points[i+7];
 }return NaN;
}
let minClearance=1,samples=0,maxTriangles=0;
const bounds=[];
for(const f of fish.fish){
 assert.equal(f.parts.length,4);let tris=0,min=[Infinity,Infinity,Infinity],max=[-Infinity,-Infinity,-Infinity];
 for(const p of f.parts){
  for(let i=0;i<p.positions.length;i+=3)for(let k=0;k<3;k++){const v=p.positions[i+k]+p.pivot[k];min[k]=Math.min(min[k],v);max[k]=Math.max(max[k],v);}
  assert(p.triangles.every(i=>Number.isInteger(i)&&i>=0&&i<p.positions.length/3));
  assert(p.colors.every(c=>Number.isFinite(c)&&c>=0&&c<=1));tris+=p.triangles.length/3;
 }
 assert(min[1]*1.03>=-.1&&max[1]*1.03<=.12,f.name+' height');
 assert(Math.max(Math.abs(min[0]),Math.abs(max[0]))*1.03<.14,f.name+' width');
 assert(Math.max(Math.abs(min[2]),Math.abs(max[2]))*1.03<.4,f.name+' length');
 assert(tris<10000);maxTriangles=Math.max(maxTriangles,tris);bounds.push({name:f.name,min,max,tris});
}
for(let lane=0;lane<6;lane++)for(let deg=0;deg<360;deg+=2){
 const a=deg*Math.PI/180,r=.26+lane*.06,px=15+6.7*r*Math.cos(a),pz=-5+5.7*r*Math.sin(a);
 let fx=-6.7*Math.sin(a),fz=5.7*Math.cos(a),length=Math.hypot(fx,fz);fx/=length;fz/=length;
 for(const forward of [-.4,0,.4])for(const side of [-.14,0,.14]){
  const x=px+fx*forward+fz*side,z=pz+fz*forward-fx*side;
  assert(Number.isFinite(height(water,x,z)),'Route leaves authored water');
  const clearance=-.31-.24-.1-height(floor,x,z);assert(clearance>=.07,'Bed collision');
  samples++;minClearance=Math.min(minClearance,clearance);
 }
}
const report={passed:true,varieties:fish.fish.length,routeEnvelopeSamples:samples,minFloorClearance:minClearance,maxVisibleFishTriangles:maxTriangles*6,bounds};
writeFileSync('PreparationChecks.json',JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
