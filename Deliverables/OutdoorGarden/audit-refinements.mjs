// CPU-only geometry and animation checks. No canvas, browser, Unity or GPU.
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import * as THREE from 'three/webgpu';
import {buildGarden} from './garden.mjs';

const garden=buildGarden(), report={method:'CPU-only; not a rendered or Play-mode test'};
const meshData=JSON.parse(await readFile(new URL('./generated/GardenModel.json',import.meta.url),'utf8'));
assert.equal(meshData.version,2);
assert.equal(meshData.meshes.filter(m=>m.name.startsWith('Pond__')).length,1);
assert.equal(meshData.meshes.filter(m=>m.name.startsWith('Quiet ripples__')).length,0);
assert.equal(meshData.boxes.filter(b=>b.name==='Pond shore').length,0);
assert.equal(meshData.butterflies.length,13);
assert.equal(garden.pathStones.length,64);
assert(garden.pathStones.some(s=>s.offset<-.5)&&garden.pathStones.some(s=>s.offset>.5));
assert(garden.pathStones.every(s=>s.sx<=.26&&s.sz<=.22));
report.pathStones={count:garden.pathStones.length,lateralRange:[Math.min(...garden.pathStones.map(s=>s.offset)),Math.max(...garden.pathStones.map(s=>s.offset))]};

const floorGeometry=new THREE.BufferGeometry().setAttribute('position',new THREE.Float32BufferAttribute(garden.colliders[0].positions,3));
const floor=new THREE.Mesh(floorGeometry,new THREE.MeshBasicNodeMaterial());floor.updateMatrixWorld(true);
const ray=new THREE.Raycaster(); let maximumDepth=0,minimumEntryNormalY=1;
const downAt=(x,z)=>{ray.set(new THREE.Vector3(x,3,z),new THREE.Vector3(0,-1,0));return ray.intersectObject(floor)[0];};
for(let x=8.4;x<21.7;x+=.4)for(let z=-10.6;z<.7;z+=.4){if(Math.hypot((x-15)/6.7,(z+5)/5.7)>1)continue;const hit=downAt(x,z);assert(hit,'missing pond floor');maximumDepth=Math.max(maximumDepth,garden.pond.waterY-hit.point.y);}
assert(maximumDepth<=.481,'pond too deep');
for(let z=-12.4;z<=-8.4;z+=.1){const hit=downAt(15,z);assert(hit);minimumEntryNormalY=Math.min(minimumEntryNormalY,hit.face.normal.y);}
assert(minimumEntryNormalY>.72,'wading approach too steep');
report.pond={maximumDepth,minimumEntryNormalY,blockingShoreBoxes:0,singleWaterSurface:true,frog:meshData.meshes.filter(m=>m.name.startsWith('Lily pad frog__')).length>0};
assert(report.pond.frog);

garden.root.updateMatrixWorld(true);
const siding=garden.root.children.find(o=>o.name==='Cabin siding__cedar');
ray.set(new THREE.Vector3(.40,1.7,-4),new THREE.Vector3(0,0,1));
const sidingHits=ray.intersectObject(siding);
assert(!sidingHits.some(h=>h.distance<1),'new window opening blocked by cedar siding');
report.frontWindow={realSidingOpening:true,width:1.8,height:1.23};

let minimumFacingDot=1;
const flies=garden.root.children.filter(o=>o.name==='Butterfly');
for(let t=0;t<20;t+=.5){
 garden.animate(t);const samples=flies.map(f=>({position:f.position.clone(),forward:new THREE.Vector3(0,0,-1).applyQuaternion(f.quaternion)}));
 garden.animate(t+.001);flies.forEach((f,i)=>{const tangent=f.position.clone().sub(samples[i].position);tangent.y=0;minimumFacingDot=Math.min(minimumFacingDot,samples[i].forward.dot(tangent.normalize()));});
}
assert(minimumFacingDot>.999,'butterfly moving backward');
const spans=garden.butterflies.map(b=>.6*b.scale);
assert(Math.max(...spans)<=.091&&Math.min(...spans)>=.071);
report.butterflies={minimumFacingDot,wingspanMetres:[Math.min(...spans),Math.max(...spans)],flapHz:garden.butterflies.map(b=>b.flapHz),colors:new Set(garden.butterflies.map(b=>b.color)).size};

// Verify that editing the four requested areas did not reshuffle the scenery.
if(process.argv[2]){
 const before=JSON.parse(await readFile(process.argv[2],'utf8'));
 const stable=['Tree trunks__','Pines__','Leaf canopies__','Mountains__','Snow peaks__','Flower petals__'];
 for(const mesh of before.meshes.filter(m=>stable.some(p=>m.name.startsWith(p)))){
  const current=meshData.meshes.find(m=>m.name===mesh.name);
  assert(current,'missing preserved scenery '+mesh.name);assert.deepEqual(current.positions,mesh.positions,'unrelated scenery moved: '+mesh.name);
 }
 report.unrelatedSceneryPreserved=true;
}
report.ok=true;console.log(JSON.stringify(report,null,2));
const geometries=new Set(),materials=new Set();garden.root.traverse(o=>{if(o.geometry)geometries.add(o.geometry);if(o.material)materials.add(o.material);});
geometries.forEach(g=>g.dispose());materials.forEach(m=>m.dispose());floorGeometry.dispose();floor.material.dispose();
