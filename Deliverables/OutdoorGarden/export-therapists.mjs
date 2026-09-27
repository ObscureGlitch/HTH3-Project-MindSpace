// CPU-only import: preserve the supplied rigid joint hierarchy as one-weight Unity skinning.
// No renderer, network, model simplification, or modification of the source GLBs.
import * as THREE from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {readFile, writeFile, mkdir} from 'node:fs/promises';
import assert from 'node:assert/strict';
const refine=process.argv.includes('--refine');
const out = new URL(refine?'../TherapyGame/Characters/Refinement/Source/':'../TherapyGame/Characters/Source/', import.meta.url);
await mkdir(out, {recursive:true});
const round = n => Math.round(n * 1e7) / 1e7;
const arr = v => v.toArray().map(round);
const reports = [];
for (const person of ['julien','camille']) {
  const bytes = await readFile(`C:/Users/obscu/Downloads/therapist_${person}.glb`);
  const loader = new GLTFLoader();
  // Textures are copied byte-for-byte below. Avoid browser image decoding for this geometry-only pass.
  loader.register(() => ({name:'THERAPY_CPU_TEXTURE',loadTexture:async () => new THREE.Texture()}));
  const gltf = await loader.parseAsync(bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength), '');
  const json = gltf.parser.json;
  assert.equal(json.skins?.length ?? 0, 0, 'Weighted skin requires a different conversion');
  assert.equal(gltf.animations.length,0);
  const scene = gltf.scene; scene.updateMatrixWorld(true);
  const nodes = await gltf.parser.getDependencies('node');
  // Extra mouth pivots permit restrained speech motion without changing the supplied face mesh.
  const bones = nodes.filter(n => !n.isMesh || ['lips','teeth_lower','tongue'].includes(n.name));
  const boneIndex = new Map(bones.map((n,i)=>[n,i]));
  const nearestBone = n => {while(n && !boneIndex.has(n))n=n.parent; return n;};
  const boneData = bones.map(n => {
    const parent = nearestBone(n.parent);
    const local = n.matrixWorld.clone(); if(parent)local.premultiply(parent.matrixWorld.clone().invert());
    const p=new THREE.Vector3(),q=new THREE.Quaternion(),s=new THREE.Vector3();local.decompose(p,q,s);
    return {name:n.name,parent:parent?boneIndex.get(parent):-1,p:arr(p),q:arr(q),s:arr(s)};
  });
  const materials = json.materials.map(m => {
    const p=m.pbrMetallicRoughness??{};
    return {name:m.name,color:p.baseColorFactor??[1,1,1,1],roughness:p.roughnessFactor??1,metallic:p.metallicFactor??1,
      texture:p.baseColorTexture?.index??-1,emission:m.emissiveFactor??[0,0,0],alphaMode:m.alphaMode??'OPAQUE',doubleSided:!!m.doubleSided};
  });
  const batches = new Map(); let triangles=0, vertices=0, maxBindError=0, removedDuplicateFaceTriangles=0;
  const meshNodes=[];scene.traverse(n=>{if(n.isMesh)meshNodes.push(n);});
  for (const node of meshNodes) {
    assert(!Array.isArray(node.material));
    const material = gltf.parser.associations.get(node.material).materials;
    // Camille's source head has a duplicate coplanar beard surface: it causes a brown mask/z-fighting.
    if(refine&&person==='camille'&&node.parent.name==='head_skin'&&materials[material].name==='beard_short'){
      removedDuplicateFaceTriangles+=(node.geometry.index?.count??node.geometry.attributes.position.count)/3;continue;
    }
    const isMouth=['lips','mouth_interior','teeth_upper','teeth_lower','tongue'].includes(node.name);
    const key=refine&&isMouth?'Face':materials[material].alphaMode==='BLEND'?'Glass':'Body';
    if(!batches.has(key))batches.set(key,{name:key,positions:[],normals:[],uvs:[],joints:[],submeshes:[],parts:[]});
    const batch=batches.get(key),offset=batch.positions.length/3;
    let sub=batch.submeshes.find(s=>s.material===material);
    if(!sub){sub={material,indices:[]};batch.submeshes.push(sub);}
    const g=node.geometry,p=g.getAttribute('position'),n=g.getAttribute('normal'),uv=g.getAttribute('uv');
    const joint=nearestBone(node),bi=boneIndex.get(joint); assert(bi!==undefined);
    batch.parts.push({name:node.name,start:offset,count:p.count,bone:bi});
    const normalMatrix=new THREE.Matrix3().getNormalMatrix(node.matrixWorld);
    const inverseBind=joint.matrixWorld.clone().invert();
    for(let i=0;i<p.count;i++) {
      const v=new THREE.Vector3().fromBufferAttribute(p,i).applyMatrix4(node.matrixWorld);
      const normal=new THREE.Vector3().fromBufferAttribute(n,i).applyNormalMatrix(normalMatrix);
      const restored=v.clone().applyMatrix4(inverseBind).applyMatrix4(joint.matrixWorld);
      maxBindError=Math.max(maxBindError,v.distanceTo(restored));
      batch.positions.push(...arr(v));batch.normals.push(...arr(normal));
      // GLTF texture coordinates have a top-left origin; Unity's imported PNG uses bottom-left.
      batch.uvs.push(uv?round(uv.getX(i)):0,uv?round(1-uv.getY(i)):0);batch.joints.push(bi);
    }
    const indices=g.index?.array??Array.from({length:p.count},(_,i)=>i);
    // Coordinates are deliberately kept unchanged. Do NOT reverse only the indices:
    // that would point the triangle cross-products opposite the exported outward normals.
    const reflected=node.matrixWorld.determinant()<0;
    for(let i=0;i<indices.length;i+=3)sub.indices.push(offset+indices[i],offset+indices[i+(reflected?2:1)],offset+indices[i+(reflected?1:2)]);
    triangles+=indices.length/3;vertices+=p.count;
  }
  // Extract embedded textures unchanged; no procedural substitutes or image edits.
  const jsonLength=bytes.readUInt32LE(12),binStart=20+jsonLength+8;
  const textures=[];
  for(let i=0;i<(json.textures?.length??0);i++){
    const source=json.images[json.textures[i].source];assert.equal(source.mimeType,'image/png');
    const view=json.bufferViews[source.bufferView];assert.equal(view.buffer,0);
    const filename=`${person}_texture_${i}.png`;
    await writeFile(new URL(filename,out),bytes.subarray(binStart+(view.byteOffset??0),binStart+(view.byteOffset??0)+view.byteLength));textures.push(filename);
  }
  if(refine){
    const face=batches.get('Face'),faceNode=nodes.find(n=>n.name==='face');
    const toFace=faceNode.matrixWorld.clone().invert(),fromFace=faceNode.matrixWorld;
    const lipPart=face.parts.find(p=>p.name==='lips'),box=new THREE.Box3();
    for(let i=lipPart.start;i<lipPart.start+lipPart.count;i++)box.expandByPoint(new THREE.Vector3().fromArray(face.positions,i*3).applyMatrix4(toFace));
    const center=box.getCenter(new THREE.Vector3()),original=[...face.positions];
    const shape=(width,height)=>{
      const output=[];
      for(let i=0;i<face.joints.length;i++){
        const v=new THREE.Vector3().fromArray(original,i*3).applyMatrix4(toFace);
        v.x=center.x+(v.x-center.x)*width;v.y=center.y+(v.y-center.y)*height;
        // Bring the lower lip forward slightly for rounded vowels, not through the face.
        v.z+=(1-width)*.004;
        output.push(...arr(v.applyMatrix4(fromFace)));
      }return output;
    };
    face.positions=shape(1,.18); // Closed resting mouth, not an always-open talking pose.
    face.shapes=[['A',1,1],['I',1.16,.38],['U',.58,.65],['E',1.08,.60],['O',.69,.93]].map(([name,w,h])=>({name,positions:shape(w,h)}));
  }
  const data={version:refine?2:1,name:person,units:'metres',bones:boneData,materials,textures,meshes:[...batches.values()]};
  const bounds=new THREE.Box3().setFromObject(scene);
  assert(triangles<16000&&bones.length<70&&batches.size===(refine?3:2)&&maxBindError<1e-6);
  for(const m of data.meshes){assert(m.positions.every(Number.isFinite));assert.equal(m.normals.length,m.positions.length);assert.equal(m.uvs.length,m.positions.length/3*2);assert(m.submeshes.every(s=>s.indices.every(i=>i>=0&&i<m.joints.length)));}
  await writeFile(new URL(`${person}.json`,out),JSON.stringify(data));
  const sourceTriangles=json.meshes.flatMap(m=>m.primitives).reduce((n,p)=>n+json.accessors[p.indices??p.attributes.POSITION].count/3,0);
  assert.equal(triangles+removedDuplicateFaceTriangles,sourceTriangles,'Every intended source primitive must survive conversion');
  reports.push({person,sourceMeshes:json.meshes.length,sourceTriangles,removedDuplicateFaceTriangles,bones:bones.length,triangles,vertices,renderers:batches.size,materials:materials.length,maxBindError,standingBounds:{min:arr(bounds.min),max:arr(bounds.max)}});
  scene.traverse(o=>{o.geometry?.dispose();if(o.material){for(const m of [o.material].flat()){m.map?.dispose();m.dispose();}}});
}
await writeFile(new URL('ConversionCheck.json',out),JSON.stringify({ok:true,method:'GLTFLoader CPU conversion; no rendered visual check',reports},null,2));
console.log(JSON.stringify(reports,null,2));
