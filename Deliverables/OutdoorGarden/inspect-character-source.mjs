import * as THREE from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {readFile} from 'node:fs/promises';
for(const name of ['julien','camille']){
 const b=await readFile(`C:/Users/obscu/Downloads/therapist_${name}.glb`),l=new GLTFLoader();l.register(()=>({name:'CPU',loadTexture:async()=>new THREE.Texture()}));
 const g=await l.parseAsync(b.buffer.slice(b.byteOffset,b.byteOffset+b.byteLength),'');g.scene.updateMatrixWorld(true);
 console.log(name);
 const face=g.scene.getObjectByName('face'),origin=new THREE.Vector3(0,-.02,.5).applyMatrix4(face.matrixWorld),direction=new THREE.Vector3(0,0,-1).transformDirection(face.matrixWorld);
 const hits=new THREE.Raycaster(origin,direction).intersectObject(g.scene,true);
 console.log('FACE RAY',hits.slice(0,14).map(h=>({name:h.object.name,material:h.object.material.name,side:h.object.material.side,distance:h.distance,normal:h.face.normal.toArray()})));
 g.scene.traverse(n=>{if(!/lips|mouth|teeth|tongue|head_base|head_skin|eyelid|nose|cardigan|coat|pelvis|trouser|thigh|shin|skull/.test(n.name))return;
  const b=new THREE.Box3().setFromObject(n);console.log(JSON.stringify({name:n.name,parent:n.parent.name,mesh:n.isMesh,p:n.position.toArray(),s:n.scale.toArray(),box:{min:b.min.toArray(),max:b.max.toArray()}}));});
}
