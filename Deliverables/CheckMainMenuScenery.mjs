import fs from 'node:fs';
const root='D:/Unity/HTH3 Project/Assets/TherapyGame/Exterior/';
const garden=JSON.parse(fs.readFileSync(root+'Source/GardenModel.json','utf8'));
const life=JSON.parse(fs.readFileSync(root+'LivingGarden/Source/GardenLife.json','utf8'));
const eye=[24,2.7,-5],look=[-1,3.6,-10];
const sub=(a,b)=>a.map((v,i)=>v-b[i]),dot=(a,b)=>a.reduce((n,v,i)=>n+v*b[i],0);
const cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
const norm=a=>a.map(v=>v/Math.hypot(...a));
const forward=norm(sub(look,eye)),right=norm(cross([0,1,0],forward)),up=cross(forward,right);
function project(p){const d=sub(p,eye),z=dot(d,forward),half=Math.tan(48*Math.PI/360);return [.5+dot(d,right)/(2*z*half*(1672/941)),.5+dot(d,up)/(2*z*half)];}
const center=project([0,2,0]);
if(center[0]<.6||center[0]>.92)throw Error('Cabin is not right of title');
let bounds=[1,1,0,0];
for(const x of [-3.6,3.6])for(const y of [0,4.4])for(const z of [-3.2,3.2]){const p=project([x,y,z]);bounds=[Math.min(bounds[0],p[0]),Math.min(bounds[1],p[1]),Math.max(bounds[2],p[0]),Math.max(bounds[3],p[1])];}
if(bounds[0]<.35||bounds[2]>1||bounds[1]<0||bounds[3]>1)throw Error('Cabin framing clips or overlaps title '+bounds);
console.log('PASS: cabin and roof fit within frame, clear of left-hand title:',bounds.map(n=>+n.toFixed(3)));
// Test source-tree triangles along several cabin sightlines without a GPU render.
function hit(o,d,v0,v1,v2,max){
 const e1=sub(v1,v0),e2=sub(v2,v0),p=cross(d,e2),det=dot(e1,p);if(Math.abs(det)<1e-9)return false;
 const inv=1/det,t=sub(o,v0),u=dot(t,p)*inv;if(u<0||u>1)return false;
 const q=cross(t,e1),v=dot(d,q)*inv;if(v<0||u+v>1)return false;
 const distance=dot(e2,q)*inv;return distance>.1&&distance<max-.3;
}
const foliage=garden.meshes.filter(m=>/Tree trunks|Pines|Leaf canopies/.test(m.name));
for(const target of [[0,2,0],[0,4,0],[-2.4,1.5,-3.3],[3.6,1.7,0]]){
 const d=sub(target,eye),length=Math.hypot(...d),direction=norm(d);let blocked=false;
 for(const m of foliage){const p=m.positions;for(let i=0;i<p.length;i+=9)if(hit(eye,direction,p.slice(i,i+3),p.slice(i+3,i+6),p.slice(i+6,i+9),length)){blocked=true;break;}if(blocked)break;}
 if(blocked)throw Error('Original tree blocks cabin sightline '+target);
}
const nearby=(life.trees??[]).filter(t=>Math.hypot(t.x-eye[0],t.z-eye[2])<3);
if(nearby.length)throw Error('Camera is too close to added trees');
console.log('PASS: four cabin sightlines clear original tree triangles; no added tree within 3 m of camera.');
console.log('This is a geometry check, not a Unity visual render; imported/current scene changes need the user Play check.');
