import {openSync,readSync,closeSync,writeFileSync} from 'node:fs';
import {createRequire} from 'node:module';
const file='D:/Downloads/koi-pond.glb',fd=openSync(file,'r');
const header=Buffer.alloc(20);readSync(fd,header,0,20,0);
if(header.toString('ascii',0,4)!=='glTF')throw Error('Invalid GLB');
const bytes=Buffer.alloc(header.readUInt32LE(12));readSync(fd,bytes,0,bytes.length,20);closeSync(fd);
const gltf=JSON.parse(bytes.toString('utf8').trim());
const nodes=gltf.nodes??[],meshes=gltf.meshes??[],accessors=gltf.accessors??[];
const report={scenes:gltf.scenes,scene:gltf.scene,animations:(gltf.animations??[]).map(a=>({name:a.name,channels:a.channels.length})),materials:(gltf.materials??[]).map(m=>({name:m.name,color:m.pbrMetallicRoughness?.baseColorFactor,alphaMode:m.alphaMode,texture:m.pbrMetallicRoughness?.baseColorTexture})),skins:gltf.skins?.length??0,images:gltf.images?.length??0,nodes:nodes.map((n,i)=>({i,...n})),meshes:meshes.map((m,i)=>({i,name:m.name,primitives:m.primitives.map(p=>({material:p.material,vertices:accessors[p.attributes.POSITION]?.count,triangles:p.indices!==undefined?accessors[p.indices].count/3:accessors[p.attributes.POSITION].count/3,min:accessors[p.attributes.POSITION]?.min,max:accessors[p.attributes.POSITION]?.max,attributes:Object.keys(p.attributes)}))}))};
writeFileSync(new URL('SourceInventory.json',import.meta.url),JSON.stringify(report,null,2));
console.log(JSON.stringify({nodeCount:nodes.length,meshCount:meshes.length,materials:report.materials,animations:report.animations,images:report.images,roots:report.scenes,firstNodes:report.nodes.slice(0,28),triangles:report.meshes.reduce((s,m)=>s+m.primitives.reduce((v,p)=>v+p.triangles,0),0)},null,2));
const require=createRequire(import.meta.url),root='C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/';
for(const name of ['meshoptimizer','@gltf-transform/core','@gltf-transform/functions']){try{console.log(name+': '+require.resolve(root+name));}catch{console.log(name+': unavailable');}}
