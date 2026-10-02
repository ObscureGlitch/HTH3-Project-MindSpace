import {readFileSync,writeFileSync} from 'node:fs';
import assert from 'node:assert/strict';
const file='Payload/Editor/PondFloorSource/NaturalPondFloor.json',d=JSON.parse(readFileSync(file));
assert.equal(d.pieces.length,88);
assert.equal(new Set(d.pieces.map(p=>p.signature)).size,88);
assert.equal(new Set(d.pieces.map(p=>p.id)).size,88);
assert.equal(new Set(d.pieces.map(p=>p.kind)).size,7);
let vertices=0,triangles=0,minWater=1,minFish=1,minBed=1;
for(const p of d.pieces){
 const n=p.positions.length/3;vertices+=n;triangles+=n/3;
 assert(n%3===0&&p.normals.length===n*3&&p.colors.length===n*3&&p.floorHeights.length===n);
 assert(p.positions.every(Number.isFinite)&&p.normals.every(Number.isFinite)&&p.colors.every(c=>Number.isFinite(c)&&c>=0&&c<=1));
 for(let i=0;i<n;i++){
  const [x,y,z]=p.positions.slice(i*3,i*3+3),radius=Math.hypot((x-15)/6.7,(z+5)/5.7),normal=p.normals.slice(i*3,i*3+3);
  assert(Math.abs(Math.hypot(...normal)-1)<.001,'Nonunit normal '+p.id);
  assert(radius<=.901);assert(!(Math.abs(x-15)<1.449&&z<-8.201));
  minWater=Math.min(minWater,-.31-y);minBed=Math.min(minBed,y-p.floorHeights[i]);
  if(radius<.68){minFish=Math.min(minFish,-.31-.24-.10-y);assert(y<=-.31-.24-.15+.0001);}
 }
 for(let i=0;i<p.positions.length;i+=9){
  const a=p.positions.slice(i,i+3),b=p.positions.slice(i+3,i+6).map((v,k)=>v-a[k]),c=p.positions.slice(i+6,i+9).map((v,k)=>v-a[k]);
  const n=[b[1]*c[2]-b[2]*c[1],b[2]*c[0]-b[0]*c[2],b[0]*c[1]-b[1]*c[0]],length=Math.hypot(...n);
  assert(length>1e-10,'Degenerate triangle '+p.id);
  const stored=p.normals.slice(i,i+3);assert(n.reduce((s,v,k)=>s+v/length*stored[k],0)>.995,'Normal/winding mismatch '+p.id);
 }
}
assert(minWater>=.085&&minFish>=.049&&minBed>=-.015&&triangles<22000);
const report={passed:true,pieces:88,uniqueGeometry:88,modelFamilies:7,vertices,triangles,minWaterSurfaceGap:minWater,minFishEnvelopeGap:minFish,maximumBurial:-minBed,checks:['Serialized payload finite and correctly indexed','Triangle winding agrees with stored normals','All vertices remain below surface','Low-profile scenery clears fish envelope','South shallow entry remains clear','No duplicate geometry signatures']};
writeFileSync('PayloadVerification.json',JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
