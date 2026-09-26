import {readFile,writeFile} from 'node:fs/promises';
const data=JSON.parse(await readFile(new URL('./generated/GardenModel.json',import.meta.url),'utf8'));
const errors=[];
for(const g of [...data.meshes,...data.colliders]){
 if(g.positions.length%9)errors.push(g.name+': incomplete triangles');
 if(g.positions.some(v=>!Number.isFinite(v)))errors.push(g.name+': invalid coordinate');
 if(g.normals&&g.normals.length!==g.positions.length)errors.push(g.name+': mismatched normals');
}
for(const g of data.colliders)for(let i=0;i<g.positions.length;i+=9){const p=g.positions,ax=p[i+3]-p[i],az=p[i+5]-p[i+2],bx=p[i+6]-p[i],bz=p[i+8]-p[i+2];if(az*bx-ax*bz<=0)errors.push(g.name+': inverted ground triangle '+i/9);}
if(data.butterflies.length!==13)errors.push('Expected 13 butterflies');
const report={ok:errors.length===0,errors,meshBatches:data.meshes.length,triangles:data.meshes.reduce((n,g)=>n+g.positions.length/9,0),boxColliders:data.boxes.length,butterflies:data.butterflies.length,units:data.units};
await writeFile(new URL('./verification/geometry.json',import.meta.url),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));if(errors.length)process.exitCode=1;
