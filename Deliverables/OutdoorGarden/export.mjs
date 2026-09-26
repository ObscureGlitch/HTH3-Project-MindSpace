import {writeFile,mkdir} from 'node:fs/promises';
import {buildGarden} from './garden.mjs';
const garden=buildGarden(),rounded=a=>Array.from(a,x=>Math.round(x*1e5)/1e5);
const meshes=garden.root.children.filter(o=>o.isMesh).map(o=>({name:o.name,material:o.material.name,castShadow:o.castShadow,positions:rounded(o.geometry.attributes.position.array),normals:rounded(o.geometry.attributes.normal.array)}));
const wing=garden.wingGeometry.index?garden.wingGeometry.toNonIndexed():garden.wingGeometry;
const data={version:2,units:'metres',seed:260926,materials:Object.entries(garden.palette).map(([name,color])=>({name,hex:color.toString(16).padStart(6,'0')})),meshes,colliders:garden.colliders.map(g=>({...g,positions:rounded(g.positions)})),boxes:garden.boxes,butterflies:garden.butterflies,wing:{positions:rounded(wing.attributes.position.array),normals:rounded(wing.attributes.normal.array)},body:{positions:rounded(garden.bodyGeometry.attributes.position.array),normals:rounded(garden.bodyGeometry.attributes.normal.array)},route:garden.route,pathStones:garden.pathStones,pond:garden.pond,frontWindow:garden.frontWindow};
await mkdir(new URL('./generated/',import.meta.url),{recursive:true});await writeFile(new URL('./generated/GardenModel.json',import.meta.url),JSON.stringify(data));
console.log(JSON.stringify({meshes:meshes.length,triangles:meshes.reduce((n,g)=>n+g.positions.length/9,0),colliders:data.boxes.length,butterflies:data.butterflies.length},null,2));
