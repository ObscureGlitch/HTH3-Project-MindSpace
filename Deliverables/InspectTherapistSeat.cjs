const fs=require('node:fs');
const root='D:/Unity/HTH3 Project/Assets/TherapyGame';
const raw=fs.readFileSync(root+'/Scenes/TherapyRoom.unity','utf8');
const docs=[...raw.matchAll(/^--- !u!(\d+) &(-?\d+)\r?\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)].map(m=>({type:+m[1],id:m[2],text:m[3]}));
const scalar=(t,k)=>t?.match(new RegExp('^  '+k+': (.*)$','m'))?.[1].trim();
const ref=(t,k)=>scalar(t,k)?.match(/fileID: (-?\d+)/)?.[1];
const byID=new Map(docs.map(d=>[d.id,d]));
const transforms=docs.filter(d=>d.type===4),objects=docs.filter(d=>d.type===1);
const transformOf=g=>transforms.find(t=>ref(t.text,'m_GameObject')===g.id);
const path=t=>{const p=byID.get(ref(t.text,'m_Father'));return (p?path(p)+'/':'')+scalar(byID.get(ref(t.text,'m_GameObject')).text,'m_Name');};
const sofaGO=objects.find(g=>g.id==='2056095165');const sofaTransform=transformOf(sofaGO);
console.log('SOFA:',path(sofaTransform),sofaTransform.text);
const subtree=new Set([sofaTransform.id]);let changed=true;while(changed){changed=false;for(const t of transforms)if(subtree.has(ref(t.text,'m_Father'))&&!subtree.has(t.id)){subtree.add(t.id);changed=true;}}
for(const g of objects.filter(g=>/Sofa|pillow/i.test(scalar(g.text,'m_Name'))||subtree.has(transformOf(g)?.id))){
 const t=transformOf(g);console.log(path(t),t.text.split('  m_Children:')[0]);
}
for(const d of docs.filter(d=>d.type===114&&/Sofa.*cushion/.test(d.text)))console.log(d.text);
