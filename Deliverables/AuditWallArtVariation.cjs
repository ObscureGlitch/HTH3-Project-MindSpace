const fs=require('fs');
const path=require('path');

const liveRoot='D:/Unity/HTH3 Project/Assets/TherapyGame';
const scene=fs.readFileSync(path.join(liveRoot,'Scenes/TherapyRoom.unity'),'utf8');
const header=/^--- !u!(\d+) &(\d+)\r?$/gm,documents=new Map();
const headers=[...scene.matchAll(header)];
for(let index=0;index<headers.length;index++){
  const start=headers[index].index+headers[index][0].length+1;
  const end=index+1<headers.length?headers[index+1].index:scene.length;
  documents.set(Number(headers[index][2]),{type:Number(headers[index][1]),body:scene.slice(start,end)});
}
const entries=[...documents.entries()];
const value=(body,key)=>body.match(new RegExp(`^  ${key}: (.*)$`,'m'))?.[1];
const ref=(body,key)=>Number(body.match(new RegExp(`^  ${key}: \\{fileID: (-?\\d+)`,'m'))?.[1]||0);
const goByName=name=>entries.filter(([,doc])=>doc.type===1&&value(doc.body,'m_Name')===name);
const transformFor=goId=>entries.find(([,doc])=>doc.type===4&&ref(doc.body,'m_GameObject')===goId);
const rendererFor=goId=>entries.find(([,doc])=>doc.type===23&&ref(doc.body,'m_GameObject')===goId);
const childIds=body=>[...(body.match(/^  m_Children:\r?\n((?:  - \{fileID: -?\d+\}\r?\n)*)/m)?.[1]||'').matchAll(/fileID: (-?\d+)/g)].map(match=>Number(match[1]));
const childObjects=goId=>childIds(transformFor(goId)[1].body).map(id=>documents.get(id)).map(transform=>({
  transform,
  goId:ref(transform.body,'m_GameObject'),
})).map(item=>({...item,gameObject:documents.get(item.goId)}));
const check=(condition,message)=>{if(!condition)throw new Error(message);};

const artwork=entries.filter(([,doc])=>doc.type===1&&/^Botanical artwork /.test(value(doc.body,'m_Name')||''));
check(artwork.length===4,`Expected four framed artworks, found ${artwork.length}`);
const original=artwork.find(([,doc])=>value(doc.body,'m_Name')==='Botanical artwork 0');
check(original,'Original centre botanical is missing');

const generated=[];
for(const [goId,doc] of artwork){
  const children=childObjects(goId),names=children.map(child=>value(child.gameObject.body,'m_Name'));
  check(names.includes('Rounded oak frame')&&names.includes('Warm paper mount'),'Existing frame or mount was not preserved');
  const prints=children.filter(child=>(value(child.gameObject.body,'m_Name')||'').startsWith('Generated print - '));
  if(goId===original[0])check(prints.length===0,'Original centre botanical received a replacement');
  else{
    check(prints.length===1,'Each replaced frame must contain exactly one generated print');
    for(const child of children.filter(item=>['Abstract clay sun','Botanical stem','Paper leaf'].includes(value(item.gameObject.body,'m_Name'))))
      check(value(child.gameObject.body,'m_IsActive')==='0','Superseded clay motif remains active');
    generated.push(prints[0]);
  }
}
check(generated.length===3,'Expected three generated print objects');
const materialGuids=generated.map(print=>rendererFor(print.goId)[1].body.match(/m_Materials:\r?\n  - \{fileID: 2100000, guid: ([0-9a-f]{32}), type: 2\}/)?.[1]);
check(materialGuids.every(Boolean)&&new Set(materialGuids).size===3,'Generated prints do not reference three distinct materials');
const expectedGuids=['QuietOrbit','StillWater','BalancedStones'].map(name=>fs.readFileSync(path.join(liveRoot,`WallArt/Materials/${name}.mat.meta`),'utf8').match(/^guid: ([0-9a-f]{32})$/m)?.[1]);
check(expectedGuids.every(guid=>materialGuids.includes(guid)),'Generated print material assignments are incomplete');

const report=`PASS: 4 framed artworks, 1 unchanged centre botanical, 3 generated prints, 3 distinct materials, original clay motifs preserved inactive.\n`;
const output='D:/Hack the Hill/Deliverables/WallArtVariationCodeCheck/SceneAudit.txt';
fs.mkdirSync(path.dirname(output),{recursive:true});fs.writeFileSync(output,report);process.stdout.write(report);
