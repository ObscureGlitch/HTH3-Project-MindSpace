// Low-memory CPU geometry preview via Three's SVGRenderer + Sharp. No WebGL, GPU or Unity rendering.
// Not a substitute for a URP lighting/performance check; useful for faces, pose and winding.
import * as THREE from 'three';
import {SVGRenderer} from 'three/addons/renderers/SVGRenderer.js';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
class Element{
 constructor(tag){this.tag=tag;this.attributes={};this.childNodes=[];this.style={};}
 setAttribute(k,v){this.attributes[k]=v;} getAttribute(k){return this.attributes[k];}
 appendChild(n){this.childNodes.push(n);return n;} removeChild(n){this.childNodes.splice(this.childNodes.indexOf(n),1);}
 get firstChild(){return this.childNodes[0];}
 get outerHTML(){return `<${this.tag} ${Object.entries(this.attributes).map(([k,v])=>`${k}="${v}"`).join(' ')}>${this.childNodes.map(n=>n.outerHTML).join('')}</${this.tag}>`;}
}
globalThis.document={createElementNS:(_,tag)=>new Element(tag)};
const folder=new URL('../TherapyGame/Characters/Refinement/Source/',import.meta.url);
await mkdir(new URL('./verification/characters/',import.meta.url),{recursive:true});
for(const name of ['julien','camille']){
 const d=JSON.parse(await readFile(new URL(name+'.json',folder)));
 const root=new THREE.Group(),bones=d.bones.map(b=>new THREE.Group());
 d.bones.forEach((b,i)=>{(b.parent<0?root:bones[b.parent]).add(bones[i]);bones[i].position.fromArray(b.p);bones[i].quaternion.fromArray(b.q);bones[i].scale.fromArray(b.s);});
 root.updateMatrixWorld(true);const inv=bones.map(b=>b.matrixWorld.clone().invert());
 const find=n=>bones[d.bones.findIndex(b=>b.name===n)];
 const s=name==='camille'?.95:1;find('hips').position.set(0,.735/s,0);
 for(const side of ['L','R']){find('hip_'+side).rotation.set(-83*Math.PI/180,0,(side==='L'?-3:3)*Math.PI/180,'ZXY');find('knee_'+side).rotation.set(83*Math.PI/180,0,0);find('knee_'+side).scale.y=1.34;find('ankle_'+side).scale.y=1/1.34;}
 root.updateMatrixWorld(true);const skin=bones.map((b,i)=>b.matrixWorld.clone().multiply(inv[i]));
 const leftRest=find('eyelid_L').quaternion.clone(),rightRest=find('eyelid_R').quaternion.clone();
 for(const view of ['face','three-quarter','seated','A','O','blink']){
  find('eyelid_L').quaternion.copy(leftRest);find('eyelid_R').quaternion.copy(rightRest);
  if(view==='blink')for(const side of ['L','R'])find('eyelid_'+side).quaternion.multiply(new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(1,0,0),66*Math.PI/180));
  root.updateMatrixWorld(true);bones.forEach((b,i)=>skin[i].copy(b.matrixWorld).multiply(inv[i]));
  const scene=new THREE.Scene();scene.background=new THREE.Color('#dce2dd');
  scene.add(new THREE.AmbientLight(0xffffff,1.25));const light=new THREE.DirectionalLight(0xfff8e8,1.5);light.position.set(2,4,3);scene.add(light);
  for(const m of d.meshes){
   const p=(m.shapes?.find(s=>s.name===view)?.positions??m.positions).slice();
   for(let i=0;i<m.joints.length;i++){const v=new THREE.Vector3().fromArray(p,i*3).applyMatrix4(skin[m.joints[i]]);v.toArray(p,i*3);}
   const geometry=new THREE.BufferGeometry();geometry.setAttribute('position',new THREE.Float32BufferAttribute(p,3));let indices=[];
   const mats=m.submeshes.map((sub,i)=>{geometry.addGroup(indices.length,sub.indices.length,i);indices.push(...sub.indices);const mat=d.materials[sub.material];return new THREE.MeshLambertMaterial({color:new THREE.Color().fromArray(mat.color),transparent:mat.alphaMode==='BLEND',opacity:mat.color[3],side:mat.doubleSided?THREE.DoubleSide:THREE.FrontSide});});
   geometry.setIndex(indices);geometry.computeVertexNormals();scene.add(new THREE.Mesh(geometry,mats));
  }
  const isFace=view!=='seated',target=new THREE.Vector3(0,isFace?(name==='camille'?1.42:1.44):.83,.04);
  const span=isFace?.43:1.95;const camera=new THREE.OrthographicCamera(-span/2,span/2,span/2,-span/2,.01,10);
  camera.position.copy(target).add(new THREE.Vector3(view==='three-quarter'?.65:view==='seated'?.8:0,.04,2));camera.lookAt(target);
  const renderer=new SVGRenderer();renderer.setSize(560,560);renderer.setPrecision(3);renderer.render(scene,camera);
  const svg=renderer.domElement.outerHTML.replace('<svg ','<svg xmlns="http://www.w3.org/2000/svg" ');
  const file=new URL(`./verification/characters/${name}-${view}.png`,import.meta.url);
  await sharp(Buffer.from(svg)).png().toFile(fileURLToPath(file));
  scene.traverse(o=>{o.geometry?.dispose();if(o.material)for(const m of [o.material].flat())m.dispose();});
 }
}
console.log('CPU previews saved to OutdoorGarden/verification/characters. No GPU or microphone used.');
