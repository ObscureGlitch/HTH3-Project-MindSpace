const fs=require('fs');
const path=require('path');
const crypto=require('crypto');
const live='D:/Unity/HTH3 Project/Assets/TherapyGame';
const stage='D:/Hack the Hill/Deliverables/TherapyGame';
const pendingRefresh=process.argv.includes('--pending-refresh');
const read=p=>fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n');
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
function check(result,message){if(!result)throw Error(message);}
function documents(file){return new Map(read(file).split(/(?=^--- !u!)/m).slice(1).map(text=>{
  const match=text.match(/^--- !u!(\d+) &(-?\d+)/);return [match[2],{type:+match[1],text}];
}));}
const before=documents(path.join(live,'Characters/Roaming/Backups/TherapyRoom_BeforeRoaming.unity'));
const after=documents(path.join(live,'Scenes/TherapyRoom.unity'));
const additions=[...after].filter(([id])=>!before.has(id));
check(additions.length===7,'Expected one navigation GameObject/Transform/controller, two movement controllers and two agents.');
check(additions.filter(([,d])=>d.type===195).length===2,'Two NavMeshAgents required.');
check(additions.filter(([,d])=>d.type===114).length===3,'Three new navigation/movement scripts required.');
const guid=name=>read(path.join(live,'Runtime',name+'.cs.meta')).match(/^guid: (\w+)/m)[1];
const movementGuid=guid('WellnessCompanionMovement');
const navigationGuid=guid('WellnessCompanionNavigation');
const navigation=additions.find(([,d])=>d.text.includes(navigationGuid));
check(navigation,'Shared navigation component missing.');
const destinationCount=(navigation[1].text.match(/^  - \{x:/gm)||[]).length;
check(destinationCount===12||(pendingRefresh&&destinationCount===11),'Expected twelve exploration destinations including the room.');
for(const [,d]of additions.filter(([,d])=>d.type===195)){
  check(d.text.includes('m_Enabled: 0'),'Agents must wait for navigation registration on entering Play.');
  check(d.text.includes('m_Radius: 0.29')&&d.text.includes('m_Height: 1.9'),'Agent size changed.');
}
check(additions.filter(([,d])=>d.text.includes(movementGuid)).length===2,'Movement scripts missing.');
const allowedChanges=new Set();
const navigationGO=navigation[1].text.match(/m_GameObject: \{fileID: (\d+)\}/)[1];
const navigationTransform=additions.find(([,d])=>d.type===4&&d.text.includes('m_GameObject: {fileID: '+navigationGO+'}'));
const parent=navigationTransform[1].text.match(/m_Father: \{fileID: (\d+)\}/)[1];
allowedChanges.add(parent);
check(after.get(parent).text.replace('  - {fileID: '+navigationTransform[0]+'}\n','')===before.get(parent).text,'Unrelated room transform changes.');
for(const name of ['Julien','Camille']){
  const [goId,go]=[...after].find(([,d])=>d.type===1&&d.text.includes('  m_Name: '+name+'\n'));
  const components=[...go.text.matchAll(/component: \{fileID: (\d+)\}/g)].map(m=>m[1]);
  const newComponents=components.filter(id=>!before.has(id));
  check(newComponents.length===2,name+' requires one agent and one movement controller.');
  let normalized=go.text;
  for(const id of newComponents)normalized=normalized.replace('  - component: {fileID: '+id+'}\n','');
  if(name==='Camille')normalized=normalized.replace('  m_IsActive: 1','  m_IsActive: 0');
  check(normalized===before.get(goId).text,name+' GameObject changed unexpectedly.');allowedChanges.add(goId);
  for(const id of components.filter(id=>before.has(id))){
    const original=before.get(id).text,current=after.get(id).text;
    if(original===current)continue;
    if(after.get(id).type===4){
      check(current.replace(/^  m_LocalPosition:.*$/m,'POSITION')===original.replace(/^  m_LocalPosition:.*$/m,'POSITION'),name+' transform changed beyond spawn position.');
    }else{
      check(current.replace(/movement: \{fileID: \d+\}/,'movement: {fileID: 0}')===original,name+' existing component changed beyond movement link.');
    }
    allowedChanges.add(id);
  }
}
for(const [id,d]of before){
  check(after.has(id),'Pre-existing scene object deleted: '+id);
  if(d.text===after.get(id).text||allowedChanges.has(id))continue;
  let normalized=after.get(id).text;
  if(normalized.includes('  proximityConversations: 1')){
    normalized=normalized.replace('  proximityConversations: 1','  proximityConversations: 0').replace('  showOnlySelected: 0','  showOnlySelected: 1');
  }else if(normalized.includes('  companions:\n')){
    normalized=normalized.replace(/  companions:\n(?:  - \{fileID: \d+\}\n)+/,'  companions: []\n');
  }
  check(normalized===d.text,'Unexpected existing scene change: '+id);
}
const runtime=['WellnessTherapist','WellnessVoiceChat','WellnessDoor','WellnessCompanionMovement','WellnessCompanionNavigation','WellnessCompanionRoomPolicy'];
for(const name of runtime)check(hash(path.join(live,'Runtime',name+'.cs'))===hash(path.join(stage,'Runtime',name+'.cs')),'Source mismatch: '+name);
for(const name of ['TherapyCompanionRoamingUpgrade','CompanionRoamingValidation'])check(hash(path.join(live,'Editor',name+'.cs'))===hash(path.join(stage,'Editor',name+'.cs')),'Editor source mismatch: '+name);
check(read(path.join(live,'CompanionRoamingRequest.txt')).trim()==='installed-live-check-pending','Install gate incomplete.');
const validationState=read(path.join(live,'CompanionRoamingValidationRequest.txt')).trim();
if(pendingRefresh&&validationState==='verify-roaming-once'){
  const report=read(path.join(live,'Characters/Roaming/RoamingCheck.txt'));
  check(report.includes('110 complete directed paths between 11 destinations')&&!report.includes('Exception'),'Initial installed navigation check missing.');
  console.log('PENDING: Unity refresh must load the exclusive companion selection and movement fixes, and verify the indoor return destination; no runtime pass is claimed.');
}else{
  check(validationState==='passed','Validation gate incomplete.');
  const report=read(path.join(live,'Characters/Roaming/RoamingValidation.txt'));
  check(report.includes('132 directed routes between 12 destinations')&&!report.includes('Exception'),'Expected all 132 routes to pass.');
  check(report.includes('only the selected companion is active'),'Single-companion regression check missing.');
  check(report.includes('both choices start indoors, five-second departure gate'),'Indoor start and door-gate regression checks missing.');
  const chat=[...after.values()].find(d=>d.text.includes('  proximityConversations: 1'));
  check(chat&&chat.text.includes('  showOnlySelected: 1'),'Saved selection visibility must be exclusive.');
  const activeCompanions=[...after.values()].filter(d=>d.type===1&&/^  m_Name: (Julien|Camille)$/m.test(d.text)&&d.text.includes('  m_IsActive: 1'));
  check(activeCompanions.length===1,'Only one companion may be active in the saved scene.');
}
console.log('PASS: 2 configured companion options, 2 movement controllers, 2 NavMeshAgents, 1 shared navigation asset, '+destinationCount+' saved destinations.');
console.log('PASS: all unrelated serialized scene content is unchanged, including meshes, animations, colliders, player, wildlife and lighting.');
console.log('PASS: live/staged source hashes agree.');
