import * as THREE from 'three';
import {readFile,writeFile} from 'node:fs/promises';
import assert from 'node:assert/strict';
const reports=[];
for(const name of ['julien','camille']){
 const d=JSON.parse(await readFile(new URL(`../TherapyGame/Characters/Source/${name}.json`,import.meta.url)));
 const root=new THREE.Group(),bones=d.bones.map(b=>new THREE.Group());
 d.bones.forEach((b,i)=>{(b.parent<0?root:bones[b.parent]).add(bones[i]);bones[i].position.fromArray(b.p);bones[i].quaternion.fromArray(b.q);bones[i].scale.fromArray(b.s);});
 root.updateMatrixWorld(true);const inverse=bones.map(b=>b.matrixWorld.clone().invert());
 const find=n=>bones[d.bones.findIndex(b=>b.name===n)];
 find('hips').position.set(0,.72,0);
 for(const side of ['L','R']){find('hip_'+side).rotation.set(-67*Math.PI/180,0,(side==='L'?-3:3)*Math.PI/180,'ZXY');find('knee_'+side).rotation.set(67*Math.PI/180,0,0);}
 root.updateMatrixWorld(true);const matrices=bones.map((b,i)=>b.matrixWorld.clone().multiply(inverse[i]));
 const box=new THREE.Box3();
 for(const mesh of d.meshes)for(let i=0;i<mesh.joints.length;i++)box.expandByPoint(new THREE.Vector3().fromArray(mesh.positions,i*3).applyMatrix4(matrices[mesh.joints[i]]));
 const positions=Object.fromEntries(['hips','knee_L','ankle_L','head','notebook'].map(n=>[n,find(n).getWorldPosition(new THREE.Vector3()).toArray()]));
 assert(box.min.y>-.03&&box.min.y<.22&&box.max.y<1.8);
 assert(box.max.z<1&&box.getSize(new THREE.Vector3()).x<1.1);
 reports.push({name,posedBounds:{min:box.min.toArray(),max:box.max.toArray()},positions});
}
await writeFile(new URL('../TherapyGame/Characters/Source/PoseCheck.json',import.meta.url),JSON.stringify({ok:true,method:'CPU-only rigid joint pose check, no rendering',reports},null,2));
console.log(JSON.stringify(reports,null,2));
