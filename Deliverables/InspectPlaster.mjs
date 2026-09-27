import fs from 'node:fs';
const root='D:/Unity/HTH3 Project/Assets/TherapyGame/';
function mesh(path){
 const s=fs.readFileSync(root+path,'utf8');
 const count=+s.match(/m_VertexCount: (\d+)/)[1];
 const bytes=Buffer.from(s.match(/_typelessdata: ([0-9a-f]+)/)[1],'hex');
 const stride=bytes.length/count;
 if(stride!==56)throw Error('Unexpected mesh stride '+stride);
 const vertices=Array.from({length:count},(_,i)=>Array.from({length:14},(_,j)=>bytes.readFloatLE(i*stride+j*4)));
 return vertices;
}
const source=mesh('Models/Box_5.300_3.000_0.200_0.010.asset');
const coords=(p,f)=>f<2?[p[2],p[1]]:f<4?[p[0],p[2]]:[p[0],p[1]];
function fit(rows){
 const mean=[0,0,0,0];for(const row of rows)for(let j=0;j<4;j++)mean[j]+=row[j]/rows.length;
 let xx=0,xy=0,yy=0,xu=0,yu=0,xv=0,yv=0;
 for(const row of rows){const [x,y,u,v]=row.map((q,j)=>q-mean[j]);xx+=x*x;xy+=x*y;yy+=y*y;xu+=x*u;yu+=y*u;xv+=x*v;yv+=y*v;}
 const d=xx*yy-xy*xy;if(!(d>1e-12))throw Error('Degenerate');
 const c=[(xu*yy-yu*xy)/d,(yu*xx-xu*xy)/d,(xv*yy-yv*xy)/d,(yv*xx-xv*xy)/d];
 const at=([x,y])=>[mean[2]+c[0]*(x-mean[0])+c[1]*(y-mean[1]),mean[3]+c[2]*(x-mean[0])+c[3]*(y-mean[1])];
 const error=Math.max(...rows.flatMap(r=>at(r).map((q,j)=>Math.abs(q-r[j+2]))));
 if(error>.002)throw Error('Multiple chart islands '+error);
 return {at,error};
}
const maps=Array.from({length:6},(_,f)=>{
 const axis=Math.floor(f/2),sign=f%2?-1:1;
 const face=source.filter(v=>v[3+axis]*sign>.9999);
 return [10,12].map(offset=>fit(face.map(v=>[...coords(v,f),...v.slice(offset,offset+2)])));
});
console.log('PASS: 12 source face/channel fits. Maximum residual',Math.max(...maps.flat().map(m=>m.error)));
const panels=[['left_of_window',[-.925,1.5,-3.1],[1.75,3,.2]],['right_of_window',[2.625,1.5,-3.1],[1.75,3,.2]],['below_window',[.85,.56,-3.1],[1.8,1.12,.2]],['above_window',[.85,2.675,-3.1],[1.8,.65,.2]]];
let changed=0,checked=0;
for(const [name,position,size] of panels){
 for(const v of mesh('Exterior/MindSpacePolish/Meshes/Plaster_'+name+'.asset')){
  const f=Array.from({length:6},(_,f)=>f).find(f=>v[3+Math.floor(f/2)]*(f%2?-1:1)>.9999);
  if(f===undefined)throw Error('Missing face');
  const p=v.slice(0,3).map((q,i)=>q*size[i]+position[i]-[.85,1.5,-3.1][i]);
  const uv=maps[f][1].at(coords(p,f));
  if(uv.some(q=>q<0||q>1||!Number.isFinite(q)))throw Error('Out of atlas '+name+' '+f+' '+uv);
  if(Math.hypot(uv[0]-v[12],uv[1]-v[13])>.005)changed++;
  checked++;
 }
}
console.log('PASS:',checked,'corrected vertex projections stay within atlas;',changed,'old UV coordinates were from different islands.');
for(const y of [-1.5,-.94,0,.85,1.5])for(const x of [-.9,.9]){
 const uv=maps[4][1].at([x,y]);if(uv[1]<.43||uv[1]>.87)throw Error('Front face escaped its chart');
}
console.log('PASS: window surround shares one continuous front-face lightmap chart; old bevel charts are excluded.');
