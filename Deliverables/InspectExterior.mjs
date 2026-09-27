import fs from 'node:fs';
const root='D:/Unity/HTH3 Project/Assets/TherapyGame';
const scene=fs.readFileSync(root+'/Scenes/TherapyRoom.unity','utf8');
const blocks=scene.split(/(?=^--- !u!)/m),objects=new Map(),transforms=new Map(),renderers=new Map();
for(const b of blocks){const h=b.match(/^--- !u!(\d+) &(\d+)/);if(!h)continue;
 const go=b.match(/m_GameObject: \{fileID: (\d+)\}/)?.[1];
 if(h[1]==='1')objects.set(h[2],b.match(/^  m_Name: (.+)$/m)?.[1]);
 if(h[1]==='4')transforms.set(h[2],{go,parent:b.match(/m_Father: \{fileID: (\d+)\}/)?.[1],p:b.match(/m_LocalPosition: (.+)/)?.[1],s:b.match(/m_LocalScale: (.+)/)?.[1]});
 if(h[1]==='23')renderers.set(go,(b.match(/m_Materials:\s*\n\s*- (.+)/)?.[1]||'')+' lightmap '+b.match(/m_LightmapIndex: (.+)/)?.[1]+' scale '+b.match(/m_LightmapTilingOffset: (.+)/)?.[1]);
}
function path(id){const t=transforms.get(id);return t?(t.parent==='0'?'':path(t.parent)+'/')+objects.get(t.go):'';}
for(const [id,t] of transforms){const p=path(id);if(p.includes('Architecture/')&& /wall|plaster|door|frame|lintel|ceiling/i.test(objects.get(t.go)))console.log(p,t.p,t.s,renderers.get(t.go)||'');}
const folder=root+'/Resources/TheLastWatch/BackgroundMusic';
const garden=JSON.parse(fs.readFileSync(root+'/Exterior/Source/GardenModel.json','utf8'));
const siding=garden.meshes.find(m=>m.name==='Cabin siding__cedar');
for(let i=0;i<siding.positions.length;i+=108){const p=siding.positions.slice(i,i+108),lo=[Infinity,Infinity,Infinity],hi=[-Infinity,-Infinity,-Infinity];for(let v=0;v<p.length;v++){const a=v%3;lo[a]=Math.min(lo[a],p[v]);hi[a]=Math.max(hi[a],p[v]);}if(lo[1]<.4||lo[1]>2.3||Math.abs(lo[0]-3.6)<.3)console.log('SIDING',lo,hi);}
for(const name of fs.readdirSync(folder).filter(n=>n.endsWith('.mp3'))){
 const fd=fs.openSync(folder+'/'+name,'r');const data=Buffer.alloc(32768);fs.readSync(fd,data,0,data.length,0);fs.closeSync(fd);
 const tags={};if(data.toString('ascii',0,3)==='ID3'){
  const version=data[3];for(let pos=10;pos+10<data.length;){const frame=data.toString('ascii',pos,pos+4);if(!/^[A-Z0-9]{4}$/.test(frame))break;
   const size=version===4?((data[pos+4]<<21)|(data[pos+5]<<14)|(data[pos+6]<<7)|data[pos+7]):data.readUInt32BE(pos+4);
   if(size<=0||pos+10+size>data.length)break;
   if(['TIT2','TPE1','TPE2','TCOP'].includes(frame)){const start=pos+11,end=pos+10+size;tags[frame]=data.toString(data[pos+10]===1?'utf16le':data[pos+10]===3?'utf8':'latin1',start,end).replace(/\0/g,'').replace(/^\uFEFF/,'');}pos+=10+size;
  }
 }console.log(JSON.stringify({file:name,tags}));
}
