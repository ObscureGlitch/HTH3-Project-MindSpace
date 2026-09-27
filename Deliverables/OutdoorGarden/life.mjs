// Additive, deterministic authoring only. No renderer/browser; one unit = one metre.
import * as THREE from 'three/webgpu';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {ConvexGeometry} from 'three/addons/geometries/ConvexGeometry.js';

export function buildGardenLife(source) {
 let seed=926311;const random=()=>((seed=(Math.imul(seed,1664525)+1013904223)>>>0)/4294967296);
 const range=(a,b)=>a+(b-a)*random(),pick=a=>a[Math.floor(random()*a.length)];
 const palette={leaf:0x719451,darkLeaf:0x42674b,fern:0x648f56,bark:0x79634e,birch:0xd4cead,wood:0xbca276,
  cream:0xf0e3b6,gold:0xdec06b,pink:0xd798ad,purple:0x9d8dc1,blue:0x94b9c7,green:0x799b5e,
  fur:0xb6a38e,brown:0x907664,white:0xe5ddca,rust:0xad7850,belly:0xcfb791,dark:0x343d37,beak:0xb59461};
 const base={ico:new THREE.IcosahedronGeometry(1,0),leaf:new THREE.OctahedronGeometry(1,0),soft:new THREE.IcosahedronGeometry(1,1),
  rod:new THREE.CylinderGeometry(1,1,1,5),cone:new THREE.ConeGeometry(1,1,6),box:new THREE.BoxGeometry(1,1,1)};
 const terrain=source.colliders.find(c=>c.name==='Walkable terrain').positions;
 const cells=new Map();
 for(let i=0;i<terrain.length;i+=9){const v=terrain.slice(i,i+9),key=Math.floor((Math.min(v[0],v[3],v[6])+40)/1.5)+','+Math.floor((Math.min(v[2],v[5],v[8])+42)/1.5);if(!cells.has(key))cells.set(key,[]);cells.get(key).push(v);}
 function height(x,z){const key=Math.floor((x+40)/1.5)+','+Math.floor((z+42)/1.5);for(const v of cells.get(key)||[]){const d=(v[5]-v[8])*(v[0]-v[6])+(v[6]-v[3])*(v[2]-v[8]);const a=((v[5]-v[8])*(x-v[6])+(v[6]-v[3])*(z-v[8]))/d,b=((v[8]-v[2])*(x-v[6])+(v[0]-v[6])*(z-v[8]))/d,c=1-a-b;if(Math.min(a,b,c)>-.00001)return a*v[1]+b*v[4]+c*v[7];}throw Error('Missing terrain at '+x+','+z);}
 const curve=new THREE.CatmullRomCurve3(source.route.map(p=>new THREE.Vector3(p.x,0,p.z)),false,'centripetal');
 const path=curve.getPoints(480).map(p=>[p.x,p.z]);
 // Include the lookout spur and the doorway porch, not just the main loop.
 const paths=[path,[[23,3],[27.8,6.2],[27.8,8.5]],[[-2.43,-3.1],[-2.43,-6]],[[5,-5.9],[25,-5.9]]];
 const segment=(x,z,a,b)=>{let vx=b[0]-a[0],vz=b[1]-a[1];let t=Math.max(0,Math.min(1,((x-a[0])*vx+(z-a[1])*vz)/(vx*vx+vz*vz||1)));return Math.hypot(x-a[0]-t*vx,z-a[1]-t*vz);};
 const pathDistance=(x,z)=>Math.min(...paths.flatMap(p=>p.slice(1).map((q,i)=>segment(x,z,p[i],q))));
 const oldObstacles=source.boxes.filter(b=>['Tree trunk','Meadow boulder'].includes(b.name));
 const sites=[],plants=[],animals=[],treeColliders=[],batches=new Map();
 function clear(x,z,r=.2){if(x<-10.5+r||x>30.5-r||z<-20.5+r||z>14.5-r)return false;
  if(Math.abs(x)<4.7+r&&Math.abs(z)<5.1+r)return false;
  if(Math.hypot((x-15)/(8.5+r),(z+5)/(7.3+r))<1)return false;
  if(pathDistance(x,z)<1.05+r)return false;
  if(Math.hypot(x-27.8,z-8.9)<2.8+r)return false;
  if(oldObstacles.some(b=>Math.hypot(x-b.p[0],z-b.p[2])<r+Math.max(b.s[0],b.s[2])*.6+.3))return false;
  return !sites.some(p=>Math.hypot(x-p.x,z-p.z)<r+p.r+.15);
 }
 function findSite(preferred,r){for(let i=0;i<700;i++){const x=i<160?preferred[0]+range(-4,4):range(-9,29),z=i<160?preferred[1]+range(-4,4):range(-19,13);if(clear(x,z,r)&&Math.abs(height(x+.3,z)-height(x-.3,z))<.3&&Math.abs(height(x,z+.3)-height(x,z-.3))<.3){const s={x,z,r};sites.push(s);return s;}}throw Error('No clear site for radius '+r);}
 function geo(g,hex,p=[0,0,0],s=[1,1,1],rot=[0,0,0]){const out=g.index?g.toNonIndexed():g.clone();out.deleteAttribute('uv');out.applyMatrix4(new THREE.Matrix4().compose(new THREE.Vector3(...p),new THREE.Quaternion().setFromEuler(new THREE.Euler(...rot)),new THREE.Vector3(...s)));out.computeVertexNormals();const color=new THREE.Color(typeof hex==='string'?palette[hex]:hex),values=[];for(let i=0;i<out.attributes.position.count;i++)values.push(color.r,color.g,color.b);out.setAttribute('color',new THREE.Float32BufferAttribute(values,3));return out;}
 function add(list,shape,color,p,s,rot){list.push(geo(base[shape],color,p,s,rot));}
 function rod(list,color,a,b,r){const direction=new THREE.Vector3(...b).sub(new THREE.Vector3(...a));const rot=new THREE.Euler().setFromQuaternion(new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0,1,0),direction.clone().normalize()));list.push(geo(base.rod,color,a.map((v,i)=>(v+b[i])/2),[r,direction.length(),r],[rot.x,rot.y,rot.z]));}
 function merged(list){const result=mergeGeometries(list);list.forEach(g=>g.dispose());result.computeBoundingSphere();return result;}
 function staticParts(x,z,parts){const key='Meadow details '+Math.floor((x+12)/12)+'-'+Math.floor((z+22)/12);if(!batches.has(key))batches.set(key,[]);batches.get(key).push(...parts);}
 const rounded=a=>Array.from(a,x=>Math.round(x*1e5)/1e5);
 function exportGeometry(name,g){const o={name,positions:rounded(g.attributes.position.array),normals:rounded(g.attributes.normal.array),colors:rounded(g.attributes.color.array)};g.dispose();return o;}
 function localPart(name,parts,position=[0,0,0]){return {...exportGeometry(name,merged(parts)),pivot:position};}
 const templates=[];
 function rabbit(){let b=[],h=[],l=[],r=[];add(b,'soft','fur',[0,.20,-.05],[.145,.175,.24]);for(const s of [-1,1]){add(b,'soft','brown',[s*.11,.13,-.13],[.09,.115,.12]);add(b,'ico','white',[s*.085,.035,.16],[.055,.035,.095]);add(b,'ico','fur',[s*.11,.035,-.10],[.07,.035,.11]);}add(b,'soft','white',[0,.18,-.28],[.055,.057,.057]);
  add(h,'soft','fur',[0,0,0],[.12,.11,.13]);add(h,'ico','white',[0,-.036,.094],[.086,.046,.057]);add(h,'ico','pink',[0,-.015,.143],[.018,.014,.012]);for(const s of [-1,1])add(h,'ico','dark',[s*.092,.025,.077],[.013,.017,.011]);
  for(const [parts,s]of [[l,-1],[r,1]]){add(parts,'soft','fur',[0,.125,0],[.036,.15,.027],[0,0,s*-.12]);add(parts,'ico','pink',[0,.13,.024],[.019,.108,.007],[0,0,s*-.12]);}
  return {kind:0,name:'Meadow rabbit',parts:[localPart('Body',b),localPart('Head',h,[0,.325,.16]),localPart('Ear L',l,[-.054,.397,.125]),localPart('Ear R',r,[.054,.397,.125])]};}
 function squirrel(){let b=[],h=[],tail=[];add(b,'soft','rust',[0,.19,-.03],[.095,.15,.17]);add(b,'ico','belly',[0,.21,.108],[.068,.10,.02]);for(const s of [-1,1]){add(b,'ico','brown',[s*.084,.10,-.08],[.065,.085,.085]);add(b,'ico','rust',[s*.065,.034,.075],[.038,.032,.083]);rod(b,'rust',[s*.064,.235,.10],[s*.055,.15,.16],.023);}
  add(h,'soft','rust',[0,0,0],[.085,.082,.094]);add(h,'ico','belly',[0,-.031,.086],[.056,.026,.043]);add(h,'ico','dark',[0,-.015,.118],[.014,.011,.011]);for(const s of [-1,1]){add(h,'ico','dark',[s*.066,.019,.055],[.011,.014,.009]);add(h,'cone','rust',[s*.056,.095,-.015],[.025,.075,.023],[0,0,s*.17]);}
  const c=new THREE.CatmullRomCurve3([[0,0,0],[0,.13,-.10],[.018,.33,-.145],[0,.48,-.08],[0,.51,.045]].map(p=>new THREE.Vector3(...p)));const tube=new THREE.TubeGeometry(c,10,.075,6,false);tail.push(geo(tube,'rust'));tube.dispose();add(tail,'ico','belly',[0,.43,-.09],[.055,.085,.06]);add(tail,'ico','rust',[0,.5,.04],[.066,.07,.07]);
  return {kind:1,name:'Red squirrel',parts:[localPart('Body',b),localPart('Head',h,[0,.315,.125]),localPart('Tail',tail,[0,.125,-.17])]};}
 function bird(){let b=[],l=[],r=[];add(b,'soft','blue',[0,.13,0],[.066,.072,.10]);add(b,'ico',0xc69979,[0,.125,.062],[.051,.05,.041]);add(b,'soft','brown',[0,.213,.074],[.05,.047,.053]);for(const s of [-1,1]){add(b,'ico','dark',[s*.041,.22,.098],[.008,.009,.008]);rod(b,'beak',[s*.025,.085,0],[s*.023,.014,.022],.006);rod(b,'beak',[s*.023,.014,.022],[s*.023,.014,.055],.005);}
  const beak=new THREE.ConeGeometry(.015,.052,4);beak.rotateX(Math.PI/2);b.push(geo(beak,'beak',[0,.207,.138]));beak.dispose();add(b,'ico','brown',[0,.12,-.112],[.043,.013,.072],[.20,0,0]);
  for(const [part,s]of [[l,-1],[r,1]]){const g=new ConvexGeometry([[0,0,.025],[s*.15,-.018,-.08],[s*.085,.012,-.125],[0,-.025,-.06],[s*.04,.023,-.025]].map(v=>new THREE.Vector3(...v)));part.push(geo(g,'blue'));g.dispose();}
  return {kind:2,name:'Meadow songbird',parts:[localPart('Body',b),localPart('Wing L',l,[-.047,.155,.01]),localPart('Wing R',r,[.047,.155,.01])]};}
 templates.push(rabbit(),squirrel(),bird());
 // Clear animal territories first; vegetation is then scattered around them.
 const homes=[[0,-8],[7,6],[21,-17],[-7,2],[28,-14],[14,9],[4,-16],[25,10],[-7,-15],[2,9],[29,-1]];
 // Three squirrels is enough to make separate clearings feel inhabited without
 // turning the quiet garden into a crowded wildlife display.
 for(let i=0;i<11;i++){const kind=i<3?0:i<6?1:2,s=findSite(homes[i],kind===2?1.05:.95),radius=kind===2?.75:.55,points=[];
  for(let j=0;j<=64;j++){const a=j/64*Math.PI*2,x=s.x+Math.sin(a)*radius,z=s.z+(1-Math.cos(a))*radius*.48;points.push([x,height(x,z)+.012,z]);}
  animals.push({name:templates[kind].name+' '+(i+1),kind,route:points,phase:range(0,7),scale:range(.91,1.08),tint:i%3===0?[1.06,1.02,.95]:i%3===1?[.94,.98,1.03]:[1,1,1]});
 }
 const trees=[];for(let i=0;i<12;i++){const s=findSite([i%2?26:-6,-17+(i%6)*5],1.05),x=s.x,z=s.z,y=height(x,z),size=range(2.3,3.45),type=i%4,parts=[];const birch=type===0;
  rod(parts,birch?'birch':'bark',[x,y-.03,z],[x+.1,y+size*.80,z],.075);
  if(birch)for(let k=0;k<6;k++)add(parts,'box','bark',[x+.015,y+.22+k*.32,z-.071],[.095,.023,.009],[0,k*.72,0]);
  if(type===3){for(let j=0;j<3;j++)add(parts,'cone',j%2?'leaf':'darkLeaf',[x,y+size*(.48+j*.21),z],[size*(.29-j*.064),size*.6,size*(.29-j*.064)],[0,j*.9,0]);}
  else for(let j=0;j<5;j++){const a=j*2.399,dx=Math.cos(a)*size*.20,dz=Math.sin(a)*size*.18,cy=y+size*(.68+j*.075);rod(parts,birch?'birch':'bark',[x,y+size*.52,z],[x+dx,cy,z+dz],.025);add(parts,'ico',j%2?'leaf':'green',[x+dx,cy,z+dz],[size*.28,size*.24,size*.26],[0,a,.15]);
   if(type===2)for(let k=0;k<4;k++){const b=k*1.57;add(parts,'ico',k%2?'cream':'pink',[x+dx+Math.cos(b)*size*.2,cy+size*.09,z+dz+Math.sin(b)*size*.17],[.095,.065,.09]);}
   if(type===1)for(let k=0;k<3;k++)add(parts,'ico',0xb87e66,[x+dx+(k-1)*.045,cy-.14,z+dz+size*.22],[.025,.03,.025]);}
  staticParts(x,z,parts);trees.push({x,z,type,height:size});treeColliders.push({name:'Living garden trunk '+(i+1),p:[x,y+size*.38,z],s:[.17,size*.8,.17],yaw:0});
 }
 // Organic drifts with gaps: daisy, bluebell, lavender, poppy and golden clover.
 const bedCenters=[[-5,-8],[2,-14],[8,-16],[20,-17],[28,-10],[25,5],[18,8],[9,8],[3,3],[-7,8],[-8,-16],[29,12]];
 const flowerTypes=['daisy','bluebell','lavender','poppy','clover'];
 for(let i=0;i<1700&&plants.length<165;i++){const [cx,cz]=bedCenters[i%bedCenters.length],a=range(0,6.283),r=Math.sqrt(random())*2.8,x=cx+Math.cos(a)*r,z=cz+Math.sin(a)*r;
  if(!clear(x,z,.16)||plants.some(p=>Math.hypot(p.x-x,p.z-z)<.26))continue;const type=Math.floor(i/bedCenters.length)%5,y=height(x,z),h=range(.21,.48),parts=[],color=['cream','blue','purple','pink','gold'][type];
  rod(parts,'darkLeaf',[x,y-.015,z],[x+.015,y+h,z],.007);
  add(parts,'leaf','green',[x+.036,y+h*.36,z],[.06,.012,.025],[0,a,.32]);
  if(type===2){for(let k=0;k<4;k++)add(parts,'ico',color,[x,y+h-k*.053,z],[.038,.045,.032]);}
  else if(type===1){for(let k=0;k<3;k++)add(parts,'cone',color,[x+.038*(k%2?-1:1),y+h-k*.074,z],[.041,.065,.041],[Math.PI,0,.22]);}
  else {const petals=type===4?4:5;for(let k=0;k<petals;k++){const t=k/petals*Math.PI*2;add(parts,'leaf',color,[x+Math.cos(t)*.049,y+h,z+Math.sin(t)*.049],[.055,.018,.031],[0,-t,0]);}add(parts,'ico',type===3?'dark':'gold',[x,y+h+.012,z],[.023,.017,.023]);}
  staticParts(x,z,parts);plants.push({x,z,type:flowerTypes[type]});
 }
 const details=[];
 for(let i=0;i<36;i++){const s=findSite([i%2?-7:27,range(-17,12)],.22),x=s.x,z=s.z,y=height(x,z),parts=[],isFern=i%3!==0;
  if(isFern)for(let frond=0;frond<5;frond++){const a=frond*1.257;for(let k=1;k<5;k++){const reach=k*.063,px=x+Math.cos(a)*reach,pz=z+Math.sin(a)*reach;add(parts,'leaf','fern',[px,y+.10+Math.sin(k/5*Math.PI)*.15,pz],[.018,.014,.068*(1-k*.12)],[0,-a+.7,0]);}}
  else for(let k=0;k<3;k++){const dx=range(-.13,.13),dz=range(-.13,.13),h=range(.07,.14);rod(parts,'cream',[x+dx,y,z+dz],[x+dx,y+h,z+dz],.013);add(parts,'soft',k%2?'wood':'brown',[x+dx,y+h,z+dz],[.065,.028,.06]);}
  staticParts(x,z,parts);details.push({x,z,kind:isFern?'fern':'mushrooms'});
 }
 for(let i=0;i<3;i++){const s=findSite([i*11-5,-16],.55),x=s.x,z=s.z,y=height(x,z),parts=[];
  const log=new THREE.CylinderGeometry(.14,.17,.86,7);log.rotateZ(Math.PI/2);parts.push(geo(log,'bark',[x,y+.13,z]));log.dispose();
  for(const sign of [-1,1]){const ring=new THREE.CylinderGeometry(.13,.13,.008,7);ring.rotateZ(Math.PI/2);parts.push(geo(ring,'wood',[x+sign*.434,y+.13,z]));ring.dispose();}
  for(let k=0;k<4;k++)add(parts,'ico','fern',[x+range(-.25,.25),y+.27,z+range(-.07,.07)],[.09,.022,.05]);staticParts(x,z,parts);details.push({x,z,kind:'mossy log'});
 }
 const meshes=[...batches].map(([name,list])=>exportGeometry(name,merged(list)));
 Object.values(base).forEach(g=>g.dispose());
 const data={version:1,seed:926311,units:'metres',meshes,templates,animals,trees,plants,details,boxes:treeColliders};
 const triangles=meshes.reduce((n,m)=>n+m.positions.length/9,0)+animals.reduce((n,a)=>n+templates[a.kind].parts.reduce((n,p)=>n+p.positions.length/9,0),0);
 if(triangles>33000)throw Error('Added geometry budget exceeded: '+triangles);
 return {data,height,pathDistance,triangles};
}
