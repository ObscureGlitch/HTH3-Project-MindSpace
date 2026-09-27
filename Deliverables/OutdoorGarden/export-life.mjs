import {readFile,writeFile,mkdir} from 'node:fs/promises';
import assert from 'node:assert/strict';
import {buildGardenLife} from './life.mjs';
const source=JSON.parse(await readFile(new URL('./generated/GardenModel.json',import.meta.url),'utf8'));
const {data,height,pathDistance,triangles}=buildGardenLife(source);
assert.equal(data.trees.length,12);assert.equal(data.plants.length,165);assert.equal(data.animals.length,11);
assert.equal(new Set(data.plants.map(p=>p.type)).size,5);
const all=[...data.meshes,...data.templates.flatMap(t=>t.parts)];
for(const m of all){assert(m.positions.length>0&&m.positions.length%9===0);assert.equal(m.positions.length,m.normals.length);assert.equal(m.positions.length,m.colors.length);assert(m.positions.every(Number.isFinite));assert(m.normals.every(Number.isFinite));assert(m.colors.every(c=>c>=0&&c<=1));}
let groundError=0,minPath=100;
for(const a of data.animals){assert.equal(a.route.length,65);for(const p of a.route){groundError=Math.max(groundError,Math.abs(p[1]-height(p[0],p[2])-.012));minPath=Math.min(minPath,pathDistance(p[0],p[2]));assert(Math.hypot((p[0]-15)/7.4,(p[2]+5)/6.4)>1);assert(Math.abs(p[0])>4.7||Math.abs(p[2])>5.1);}assert(Math.hypot(...a.route[0].map((v,i)=>v-a.route.at(-1)[i]))<.001);}
assert(groundError<.001);assert(minPath>1.1);
const report={ok:true,method:'CPU-only modeling, geometry and terrain clearance; no rendered/performance verification',staticBatches:data.meshes.length,triangles,flowers:data.plants.length,flowerTypes:[...new Set(data.plants.map(p=>p.type))],trees:data.trees.length,ferns:data.details.filter(d=>d.kind==='fern').length,mushroomClusters:12,logs:3,rabbits:3,squirrels:3,birds:5,groundError,minPathDistance:minPath,animalHomes:data.animals.map(a=>({name:a.name,home:a.route[0]}))};
await mkdir(new URL('./generated/',import.meta.url),{recursive:true});
// JsonUtility supports Vector3[] records, not jagged numeric arrays.
const unityData={...data,animals:data.animals.map(a=>({...a,route:a.route.map(p=>({x:p[0],y:p[1],z:p[2]}))}))};
await writeFile(new URL('./generated/GardenLife.json',import.meta.url),JSON.stringify(unityData));
await writeFile(new URL('./verification/garden-life-cpu.json',import.meta.url),JSON.stringify(report,null,2));
console.log(JSON.stringify(report,null,2));
