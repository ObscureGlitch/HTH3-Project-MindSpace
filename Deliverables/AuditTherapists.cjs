// Read-only post-import audit; never launches Unity, renders, or starts a voice session.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root='D:/Unity/HTH3 Project/Assets/TherapyGame';
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
if(read('TherapistRequest.txt').trim()!=='installed-live-visual-check-pending'){
 console.log('PENDING: Unity character import has not completed. Keep Play stopped and use Assets > Refresh.');process.exit(2);
}
const parse=t=>[...t.matchAll(/^--- !u!(\d+) &(-?\d+)(?: stripped)?\r?\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)].map(m=>({type:+m[1],id:m[2],text:m[3]}));
const scalar=(t,k)=>t?.match(new RegExp('^  '+k+': (.*)$','m'))?.[1].trim();
const ref=(t,k)=>scalar(t,k)?.match(/fileID: (-?\d+)/)?.[1];
const guid=p=>read(p+'.meta').match(/guid: (\w+)/)[1];
const docs=parse(read('Scenes/TherapyRoom.unity')),before=parse(read('Characters/Backups/TherapyRoom_BeforeTherapists.unity'));
const byID=new Map(docs.map(d=>[d.id,d]));
const script=n=>docs.filter(d=>d.type===114&&d.text.includes('guid: '+guid('Runtime/'+n+'.cs')));
const chat=script('WellnessVoiceChat')[0],actors=script('WellnessTherapist'),checks=[];
const check=(name,ok,detail)=>checks.push({name,ok:!!ok,...(detail===undefined?{}:{detail})});
check('Exactly two linked character components',actors.length===2&&actors.every(a=>ref(a.text,'chat')===chat.id));
check('Julien is Voice 1 and Camille is Voice 2',actors.some(a=>scalar(a.text,'characterName')==='Julien'&&scalar(a.text,'voiceIndex')==='0')&&actors.some(a=>scalar(a.text,'characterName')==='Camille'&&scalar(a.text,'voiceIndex')==='1'));
check('Only Julien initially visible; selected avatar mode enabled',actors.filter(a=>scalar(byID.get(ref(a.text,'m_GameObject')).text,'m_IsActive')==='1').length===1&&scalar(chat.text,'showOnlySelected')==='1');
check('Head and mouth references present for both characters',actors.every(a=>['voiceAnchor','head','spine','lowerLip','lowerTeeth','tongue'].every(k=>byID.get(ref(a.text,k))?.type===4)));
check('Interaction component routes E to each character',actors.every(a=>script('WellnessInteraction').some(i=>ref(i.text,'therapist')===a.id&&ref(i.text,'m_GameObject')===ref(a.text,'m_GameObject'))));
const seats=script('WellnessSeat'),spots=seats.flatMap(s=>[...s.text.matchAll(/reserved: (\d)/g)].map(m=>+m[1]));
check('Five existing seat spots, only one reserved',spots.length===5&&spots.filter(Boolean).length===1);
const audio=byID.get(ref(chat.text,'voiceAudio'));
check('Positional voice with no play-on-awake or Doppler',scalar(audio?.text,'m_PlayOnAwake')==='0'&&scalar(audio?.text,'DopplerLevel')==='0'&&/m_Curve: 0|m_Curve:/.test(audio?.text??''));
check('Hearing distance is 3 metres',scalar(chat.text,'hearingDistance')==='3');
const skins=docs.filter(d=>d.type===137&&!before.some(b=>b.id===d.id));
check('Four skinned renderers, no new shadow casting',skins.length===4&&skins.every(s=>scalar(s.text,'m_CastShadows')==='0'&&scalar(s.text,'m_UpdateWhenOffscreen')==='0'));
const settings=read('Integrations/Settings/VoiceCompanions.asset');
check('Original two public agent IDs and color variables retained',settings.includes('agent_8001m3e67fv6fazsz8h90paz1xtv')&&settings.includes('agent_4301m3e7t5qfffkb26yn6gwydg03')&&settings.includes('colorVariable: yellow')&&settings.includes('colorVariable: red'));
check('Every existing scene record retained',before.every(d=>byID.has(d.id)));
const oldIDs=new Set(before.map(d=>d.id)),newDocs=docs.filter(d=>!oldIDs.has(d.id));
check('No new light camera rigidbody particle or audio sources',!newDocs.some(d=>[20,54,82,108,198].includes(d.type)));
check('Existing garden sky rain door and player components unchanged', ['WellnessGardenLife','WellnessSkyCycle','WellnessRain','WellnessDoor','WellnessExplorer'].every(n=>script(n).every(s=>before.find(b=>b.id===s.id)?.text===s.text)));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
check('Live integration matches compiled staged source',['Runtime/WellnessTherapist.cs','Runtime/WellnessVoiceChat.cs','Runtime/WellnessSeat.cs','Runtime/WellnessInteraction.cs','Editor/TherapyTherapistSetup.cs'].every(p=>hash(path.join(root,p))===hash(path.join('D:/Hack the Hill/Deliverables/TherapyGame',p))));
check('CPU import pose and proximity raycast checks completed',read('Characters/TherapistCheck.txt').includes('PASS: actual-scene raycasts allow nearby clear view'));
check('Conversion preserved all model triangles',JSON.parse(read('Characters/Source/ConversionCheck.json')).reports.every(r=>r.triangles===r.sourceTriangles&&r.maxBindError<1e-6));
const result={ok:checks.every(c=>c.ok),method:'Saved scene, source and CPU/import tests only; rendered appearance and live microphone untested',checks};console.log(JSON.stringify(result,null,2));if(!result.ok)process.exitCode=1;
