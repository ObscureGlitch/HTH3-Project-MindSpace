// Offline GLB derivative. Keeps supplied markings, appendages and satellites.
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {MeshoptSimplifier} from '../KoiPond/node_modules/meshoptimizer/index.js';
const bytes=readFileSync('D:/Downloads/koi-pantheon.glb'),len=bytes.readUInt32LE(12);
if(bytes.toString('ascii',0,4)!=='glTF')throw Error('Invalid GLB');
const g=JSON.parse(bytes.toString('utf8',20,20+len)),bin=28+len;
const identity=[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1];
function multiply(a,b){const out=Array(16).fill(0);for(let col=0;col<4;col++)for(let row=0;row<4;row++)for(let k=0;k<4;k++)out[col*4+row]+=a[k*4+row]*b[col*4+k];return out;}
function transform(m,v,w=1){return [0,1,2].map(r=>m[r]*v[0]+m[4+r]*v[1]+m[8+r]*v[2]+m[12+r]*w);}
function accessor(id){const a=g.accessors[id],v=g.bufferViews[a.bufferView],n={SCALAR:1,VEC2:2,VEC3:3,VEC4:4}[a.type],size={5121:1,5123:2,5125:4,5126:4}[a.componentType],read={5121:'readUInt8',5123:'readUInt16LE',5125:'readUInt32LE',5126:'readFloatLE'}[a.componentType];if(a.sparse||!read)throw Error('Unsupported accessor');const out=[];for(let i=0;i<a.count;i++)for(let k=0;k<n;k++){let x=bytes[read](bin+(v.byteOffset||0)+(a.byteOffset||0)+i*(v.byteStride||size*n)+k*size);if(a.normalized)x/=a.componentType===5121?255:65535;out.push(x);}return {data:out,n,count:a.count};}
const round=x=>Math.round(x*1e6)/1e6;
await MeshoptSimplifier.ready;
const output={hash:createHash('sha256').update(bytes).digest('hex'),fish:[]},stats=[];
for(const rootId of g.nodes[0].children){
 const root=g.nodes[rootId],fish={name:root.name.slice(4),parts:[]},groups=new Map();let before=0,after=0,omittedPoints=0;
 function visit(id,parentMatrix,effect=null){
  const node=g.nodes[id];if(node.translation||node.rotation||node.scale)throw Error('Unexpected unbaked TRS');
  const m=multiply(parentMatrix,node.matrix||identity);
  if(['orbs','corona','halo','crescent'].includes(node.name))effect=node.name;
  if(node.mesh!==undefined)for(const prim of g.meshes[node.mesh].primitives){
   if((prim.mode??4)!==4){omittedPoints+=g.accessors[prim.attributes.POSITION].count;continue;}
   const p=accessor(prim.attributes.POSITION),n=accessor(prim.attributes.NORMAL),c=prim.attributes.COLOR_0===undefined?null:accessor(prim.attributes.COLOR_0);
   const ix=prim.indices===undefined?Array.from({length:p.count},(_,i)=>i):accessor(prim.indices).data;before+=ix.length/3;
   if(node.name.includes('cornea'))continue;
   const material=g.materials[prim.material],tint=material.pbrMetallicRoughness?.baseColorFactor||[1,1,1,1],emission=material.emissiveFactor||[0,0,0],strength=material.extensions?.KHR_materials_emissive_strength?.emissiveStrength||1;
   const positions=[],attrs=[],indices=[],unique=new Map();
   for(const src of ix){const point=transform(m,p.data.slice(src*3,src*3+3)),normal=transform(m,n.data.slice(src*3,src*3+3),0);const mag=Math.hypot(...normal)||1;const tuple=[...point,...normal.map(x=>x/mag),...(c?c.data.slice(src*c.n,src*c.n+3):[1,1,1])];const key=tuple.map(round).join(',');let dst=unique.get(key);if(dst===undefined){dst=positions.length/3;unique.set(key,dst);positions.push(...tuple.slice(0,3));attrs.push(...tuple.slice(3));}indices.push(dst);}
   const target=Math.min(indices.length,Math.max(node.name.startsWith('body')?750:48,Math.floor(indices.length*.055/3)*3));
   const [reduced,error]=MeshoptSimplifier.simplifyWithAttributes(new Uint32Array(indices),new Float32Array(positions),3,new Float32Array(attrs),6,[.1,.1,.1,.6,.6,.6],null,target,.014,['Permissive','LockBorder']);
   if(!Number.isFinite(error))throw Error('Simplification failed');
   const name=effect|| (node.name==='fin_caudal'?'tail':node.name==='fin_pectoral_L'?'leftFin':node.name==='fin_pectoral_R'?'rightFin':'body');
   let part=groups.get(name);if(!part){let pivot=[0,0,0];if(name==='tail')pivot=[0,0,-.2475];if(name==='leftFin'||name==='rightFin'){const r=node.extras.root;pivot=[r.z,r.y,r.x];}part={name,pivot,positions:[],normals:[],colors:[],emission:[],surface:[],triangles:[]};groups.set(name,part);}
   const remap=new Map();for(const src of reduced)if(!remap.has(src)){remap.set(src,part.positions.length/3);part.positions.push(round(positions[src*3+2]-part.pivot[0]),round(positions[src*3+1]-part.pivot[1]),round(positions[src*3]-part.pivot[2]));part.normals.push(round(attrs[src*6+2]),round(attrs[src*6+1]),round(attrs[src*6]));for(let k=0;k<3;k++){part.colors.push(round(attrs[src*6+3+k]*tint[k]));part.emission.push(round(emission[k]*Math.min(strength,3)));}part.surface.push(material.pbrMetallicRoughness?.metallicFactor??0,material.pbrMetallicRoughness?.roughnessFactor??.5);}
   for(let t=0;t<reduced.length;t+=3)part.triangles.push(remap.get(reduced[t]),remap.get(reduced[t+2]),remap.get(reduced[t+1]));after+=reduced.length/3;
  }
  for(const child of node.children||[])visit(child,m,effect);
 }
 // Strip the gallery root's translation/pose; retain transforms below each fish.
 for(const id of root.children)visit(id,identity);
 fish.parts=[...groups.values()];if(!['body','tail','leftFin','rightFin'].every(n=>groups.has(n)))throw Error('Missing articulated part');
 for(const part of fish.parts){if(part.positions.some(x=>!Number.isFinite(x))||part.triangles.some(i=>i<0||i>=part.positions.length/3)||part.positions.length/3>60000)throw Error('Invalid part');}
 output.fish.push(fish);stats.push({name:fish.name,before,after,vertices:fish.parts.reduce((s,p)=>s+p.positions.length/3,0),parts:fish.parts.length,pointsReplaced:omittedPoints});console.log(fish.name+': '+before+' -> '+after+' triangles');
}
// Compact little-endian source, editor only; not a giant runtime OBJ/GLB import.
const chunks=[];const int=v=>{const b=Buffer.alloc(4);b.writeInt32LE(v);chunks.push(b);};const str=v=>{const b=Buffer.from(v);int(b.length);chunks.push(b);};const floats=values=>{const b=Buffer.alloc(values.length*4);values.forEach((v,i)=>b.writeFloatLE(v,i*4));chunks.push(b);};
str('KPN1');str(output.hash);int(output.fish.length);
for(const f of output.fish){str(f.name);int(f.parts.length);for(const p of f.parts){str(p.name);floats(p.pivot);int(p.positions.length/3);for(const field of ['positions','normals','colors','emission','surface'])floats(p[field]);int(p.triangles.length);const b=Buffer.alloc(p.triangles.length*4);p.triangles.forEach((v,i)=>b.writeInt32LE(v,i*4));chunks.push(b);}}
mkdirSync('Payload/Editor/PantheonSource',{recursive:true});const source=Buffer.concat(chunks);writeFileSync('Payload/Editor/PantheonSource/Pantheon.bytes',source);
writeFileSync('ModelPreparation.json',JSON.stringify({sourceSha256:output.hash,sourceBytes:bytes.length,preparedBytes:source.length,fish:stats,totalTriangles:stats.reduce((s,f)=>s+f.after,0)},null,2));
console.log('Prepared '+source.length+' bytes; '+stats.reduce((s,f)=>s+f.after,0)+' triangles.');
