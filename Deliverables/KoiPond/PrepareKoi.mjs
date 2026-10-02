// Offline derivative of the user's GLB. No network, Unity, or GPU required.
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {createRequire} from 'node:module';
import {MeshoptSimplifier} from 'meshoptimizer';
const source='D:/Downloads/koi-pond.glb';
const bytes=readFileSync(source), jsonLength=bytes.readUInt32LE(12);
const g=JSON.parse(bytes.toString('utf8',20,20+jsonLength));
const bin=20+jsonLength+8;
const dim={SCALAR:1,VEC2:2,VEC3:3,VEC4:4};
function accessor(id){
  const a=g.accessors[id],v=g.bufferViews[a.bufferView],n=dim[a.type];
  if(a.sparse)throw Error('Sparse accessor unsupported');
  const size={5121:1,5123:2,5125:4,5126:4}[a.componentType];
  const read={5121:'readUInt8',5123:'readUInt16LE',5125:'readUInt32LE',5126:'readFloatLE'}[a.componentType];
  const start=bin+(v.byteOffset||0)+(a.byteOffset||0),stride=v.byteStride||size*n,out=[];
  for(let i=0;i<a.count;i++)for(let k=0;k<n;k++){
    let val=bytes[read](start+i*stride+k*size);
    if(a.normalized)val/=a.componentType===5121?255:65535;
    out.push(val);
  }return {data:out,n,count:a.count};
}
await MeshoptSimplifier.ready;
const result={version:1,sourceSha256:createHash('sha256').update(bytes).digest('hex'),fish:[]};
const stats={sourceTriangles:0,outputTriangles:0,varieties:[]};
const round=x=>Math.round(x*1e7)/1e7;
for(const rootId of g.nodes[0].children){
 const root=g.nodes[rootId],fish={name:root.name.replace('koi_',''),parts:[]};
 const groups=new Map(); let before=0,after=0;
 for(const childId of root.children){
  const node=g.nodes[childId];
  if(node.matrix||node.translation||node.rotation||node.scale)throw Error('Unexpected local transform');
  for(const prim of g.meshes[node.mesh].primitives){
   const p=accessor(prim.attributes.POSITION),n=accessor(prim.attributes.NORMAL),c=prim.attributes.COLOR_0===undefined?null:accessor(prim.attributes.COLOR_0);
   let ix=prim.indices===undefined?Array.from({length:p.count},(_,i)=>i):accessor(prim.indices).data;
   before+=ix.length/3;
   if(node.name.includes('cornea'))continue; // Retain the original iris/pupil, omit clear shells.
   const tint=g.materials[prim.material].pbrMetallicRoughness.baseColorFactor;
   const unique=new Map(),positions=[],attrs=[],indices=[];
   for(const src of ix){
    const tuple=[...p.data.slice(src*3,src*3+3),...n.data.slice(src*3,src*3+3),...(c?c.data.slice(src*c.n,src*c.n+3):[1,1,1])];
    const key=tuple.map(round).join(',');let dst=unique.get(key);
    if(dst===undefined){dst=positions.length/3;unique.set(key,dst);positions.push(...tuple.slice(0,3));attrs.push(...tuple.slice(3));}
    indices.push(dst);
   }
   let target=Math.max(48,Math.floor(indices.length*.045/3)*3);
   if(node.name.startsWith('body'))target=Math.max(600,target);
   const [reduced,error]=MeshoptSimplifier.simplifyWithAttributes(new Uint32Array(indices),new Float32Array(positions),3,new Float32Array(attrs),6,[.1,.1,.1,.6,.6,.6],null,target,.012,['Permissive','LockBorder']);
   if(!Number.isFinite(error))throw Error('Invalid simplification');
   const partName=node.name==='fin_caudal'?'tail':node.name==='fin_pectoral_L'?'leftFin':node.name==='fin_pectoral_R'?'rightFin':'body';
   let part=groups.get(partName);
   if(!part){
    let pivot=[0,0,0];
    if(partName==='tail')pivot=[(g.accessors[prim.attributes.POSITION].min[2]+g.accessors[prim.attributes.POSITION].max[2])*.5,0,-.2475];
    if(partName==='leftFin'||partName==='rightFin'){
      const r=node.extras?.root;
      if(!r)throw Error('Missing pectoral root '+node.name);
      pivot=Array.isArray(r)?[r[2],r[1],r[0]]:[r.z,r.y,r.x];
    }
    part={name:partName,pivot,positions:[],normals:[],colors:[],triangles:[]};groups.set(partName,part);
   }
   const remap=new Map();
   for(const src of reduced)if(!remap.has(src)){
    const dst=part.positions.length/3;remap.set(src,dst);
    part.positions.push(round(positions[src*3+2]-part.pivot[0]),round(positions[src*3+1]-part.pivot[1]),round(positions[src*3]-part.pivot[2]));
    part.normals.push(round(attrs[src*6+2]),round(attrs[src*6+1]),round(attrs[src*6]));
    for(let k=0;k<3;k++)part.colors.push(round(attrs[src*6+3+k]*tint[k]));
   }
   for(let i=0;i<reduced.length;i+=3)part.triangles.push(remap.get(reduced[i]),remap.get(reduced[i+2]),remap.get(reduced[i+1]));
   after+=reduced.length/3;
  }
 }
 fish.parts=[...groups.values()];
 if(fish.parts.length!==4)throw Error('Expected four animatable parts');
 for(const part of fish.parts){
  const count=part.positions.length/3;
  if(part.triangles.some(i=>i<0||i>=count)||part.positions.some(x=>!Number.isFinite(x)))throw Error('Invalid output mesh');
 }
 result.fish.push(fish);stats.sourceTriangles+=before;stats.outputTriangles+=after;
 stats.varieties.push({name:fish.name,before,after,vertices:fish.parts.reduce((s,p)=>s+p.positions.length/3,0)});
 console.log(fish.name+': '+before+' → '+after+' triangles');
}
mkdirSync('Payload/Editor/KoiSource',{recursive:true});
writeFileSync('Payload/Editor/KoiSource/KoiModels.json',JSON.stringify(result));
writeFileSync('ModelPreparation.json',JSON.stringify({...stats,sourceSha256:result.sourceSha256},null,2));
// CPU-only inspection sheet. Flat lighting, no synthetic additions to the meshes.
const require=createRequire(import.meta.url);
const {createCanvas}=require('C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas');
const canvas=createCanvas(1400,900),ctx=canvas.getContext('2d');ctx.fillStyle='#dbe5df';ctx.fillRect(0,0,1400,900);
for(let f=0;f<result.fish.length;f++){
 const fish=result.fish[f],ox=(f%4)*350+175,oy=Math.floor(f/4)*440+110,scale=430;
 ctx.fillStyle='#253d37';ctx.font='24px sans-serif';ctx.textAlign='center';ctx.fillText(fish.name,ox,oy-65);
 for(let view=0;view<2;view++){
  const tris=[];
  for(const p of fish.parts)for(let t=0;t<p.triangles.length;t+=3){
   const ids=p.triangles.slice(t,t+3),pos=ids.map(i=>p.positions.slice(i*3,i*3+3).map((x,k)=>x+p.pivot[k]));
   const normal=ids.reduce((a,i)=>a.map((x,k)=>x+p.normals[i*3+k]/3),[0,0,0]);
   if((view===0?normal[1]:normal[0])<-.03)continue;
   const rgb=ids.reduce((a,i)=>a.map((x,k)=>x+p.colors[i*3+k]/3),[0,0,0]);
   const light=.65+.35*Math.max(0,normal[1]*.8+normal[0]*.5);
   tris.push({pos,depth:pos.reduce((s,v)=>s+v[view===0?1:0],0),color:'rgb('+rgb.map(x=>Math.round(Math.pow(Math.min(1,x*light),1/2.2)*255)).join(',')+')'});
  }
  tris.sort((a,b)=>a.depth-b.depth);
  for(const tri of tris){ctx.beginPath();tri.pos.forEach((p,i)=>{const x=ox+p[2]*scale,y=oy+view*155-p[view===0?0:1]*scale;(i?ctx.lineTo:ctx.moveTo).call(ctx,x,y);});ctx.closePath();ctx.fillStyle=tri.color;ctx.fill();}
 }
}
writeFileSync('KoiModelPreview.png',canvas.toBuffer('image/png'));console.log(JSON.stringify(stats));
