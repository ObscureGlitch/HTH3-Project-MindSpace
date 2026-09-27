const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const workspace = 'D:/Hack the Hill';
const project = 'D:/Unity/HTH3 Project/Assets/TherapyGame';
const animation = path.join(project, 'Characters/Animation');
const scenePath = path.join(project, 'Scenes/TherapyRoom.unity');
const backupPath = path.join(animation, 'Backups/TherapyRoom_BeforeAnimatedTherapists.unity');
const names = ['Idle','Listen','Talk','Nod','Write','Think','Wave','Walk','Sit'];
const loops = new Set(['Idle','Listen','Talk','Think','Walk','Sit']);
const durations = {Idle:6,Listen:6,Talk:4,Nod:1.6,Write:4,Think:5,Wave:2.4000001,Walk:1.1,Sit:6};

function fail(message) { throw new Error(message); }
function read(file) { return fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n'); }
function hash(file) { return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex'); }
function guid(meta) {
  const match = read(meta).match(/^guid: ([0-9a-f]{32})$/m);
  if (!match) fail(`Missing GUID in ${meta}`);
  return match[1];
}
function docs(file) {
  const text = read(file);
  const expression = /^--- !u!(\d+) &(-?\d+)\n/gm;
  const headers = [...text.matchAll(expression)];
  const result = new Map();
  headers.forEach((entry, index) => {
    const start = entry.index;
    const end = index + 1 < headers.length ? headers[index + 1].index : text.length;
    result.set(entry[2], { type: Number(entry[1]), body: text.slice(start, end) });
  });
  return result;
}

const before = docs(backupPath);
const after = docs(scenePath);
const added = [...after].filter(([id]) => !before.has(id));
const removed = [...before].filter(([id]) => !after.has(id));
if (removed.length) fail(`Scene objects were removed: ${removed.map(([id]) => id).join(', ')}`);
if (added.length !== 2 || added.some(([, document]) => document.type !== 111))
  fail(`Expected exactly two added Animation components; found ${added.map(([id, document]) => `${id}:u!${document.type}`).join(', ')}`);

const actors = new Map();
for (const [animationId, document] of added) {
  const objectMatch = document.body.match(/m_GameObject: \{fileID: (\d+)\}/);
  if (!objectMatch) fail(`Animation ${animationId} has no GameObject.`);
  const objectId = objectMatch[1];
  const object = after.get(objectId);
  const nameMatch = object && object.body.match(/m_Name: (Julien|Camille)/);
  if (!nameMatch) fail(`Animation ${animationId} is not attached to Julien or Camille.`);
  const actorName = nameMatch[1];
  if (actors.has(actorName)) fail(`Duplicate Animation component on ${actorName}.`);
  actors.set(actorName, {animationId, objectId, animation: document.body});
}
if (actors.size !== 2) fail('Both companions did not receive an Animation component.');

const normalizedAfter = new Map([...after].map(([id, document]) => [id, document.body]));
for (const {animationId, objectId} of actors.values()) {
  normalizedAfter.set(objectId, normalizedAfter.get(objectId).replace(`  - component: {fileID: ${animationId}}\n`, ''));
  let linked = false;
  for (const [id, body] of normalizedAfter) {
    if (body.includes(`  bodyAnimation: {fileID: ${animationId}}`)) {
      normalizedAfter.set(id, body.replace(`  bodyAnimation: {fileID: ${animationId}}`, '  bodyAnimation: {fileID: 0}'));
      linked = true;
    }
  }
  if (!linked) fail(`Animation ${animationId} is not linked by its WellnessTherapist.`);
}

const changed = [];
for (const [id, document] of before) {
  if (normalizedAfter.get(id) !== document.body) changed.push(id);
}
if (changed.length) fail(`Unexpected serialized scene changes remain after normalizing the two intended links: ${changed.join(', ')}`);

for (const person of ['Julien','Camille']) {
  const actor = actors.get(person);
  const lower = person.toLowerCase();
  const expectedGuids = names.map(name => guid(path.join(animation, 'Clips', person, `${name}.anim.meta`)));
  const componentGuids = [...actor.animation.matchAll(/- \{fileID: 7400000, guid: ([0-9a-f]{32}), type: 2\}/g)].map(match => match[1]);
  if (componentGuids.length !== 9 || componentGuids.some((value, index) => value !== expectedGuids[index]))
    fail(`${person} scene clip references do not match its nine generated assets in order.`);
  if (!actor.animation.includes(`m_Animation: {fileID: 7400000, guid: ${expectedGuids[0]}, type: 2}`) ||
      !actor.animation.includes('m_PlayAutomatically: 0') || !actor.animation.includes('m_CullingType: 1'))
    fail(`${person} default clip, autoplay, or culling serialization is incorrect.`);

  const pack = JSON.parse(read(path.join(animation, 'Source', `${lower}_animations.json`)));
  if (pack.version !== 1 || pack.person !== lower || pack.clips.length !== 9 || pack.clips.some((clip, index) => clip.name !== names[index] || clip.tracks.length !== 33))
    fail(`${person} source pack metadata changed.`);
  for (const name of names) {
    const clipPath = path.join(animation, 'Clips', person, `${name}.anim`);
    const clip = read(clipPath);
    const bindings = (clip.match(/^    path: /gm) || []).length;
    const wrap = loops.has(name) ? 2 : 1;
    const stopMatch = clip.match(/m_StopTime: ([0-9.]+)/);
    if (!clip.includes(`m_Name: ${name}`) || !clip.includes('m_Legacy: 1') || !clip.includes('m_SampleRate: 30') ||
        !clip.includes(`m_WrapMode: ${wrap}`) || bindings !== 33 || !stopMatch || Math.abs(Number(stopMatch[1]) - durations[name]) > 0.0001)
      fail(`${person}/${name} clip serialization failed (bindings=${bindings}).`);
  }
}

const expectedHashes = {
  therapist_julien_animated: '1c0f781e31bf70756c8b5b4775ca3d8ee3301efb43d7d1dece78db4eb53c5545',
  therapist_camille_animated: '69130fbf9e5d7e6308b730f495c17f19b48c80a1cf81f7e45a61ca1bfd2e0c85'
};
for (const [stem, expected] of Object.entries(expectedHashes)) {
  const live = path.join(animation, 'Source', `${stem}.glb`);
  const staged = path.join(workspace, 'Deliverables/TherapyGame/Characters/Animation/Source', `${stem}.glb`);
  const supplied = path.join('C:/Users/obscu/Downloads', `${stem}.glb`);
  for (const candidate of [live, staged, supplied]) if (hash(candidate) !== expected) fail(`Source hash mismatch: ${candidate}`);
}

for (const relative of ['Runtime/WellnessTherapist.cs','Runtime/WellnessVoiceChat.cs','Editor/TherapyTherapistAnimationUpgrade.cs']) {
  const live = path.join(project, relative);
  const staged = path.join(workspace, 'Deliverables/TherapyGame', relative);
  if (hash(live) !== hash(staged)) fail(`Live/staged source mismatch: ${relative}`);
}
const gate = read(path.join(project, 'TherapistAnimationRequest.txt')).trim();
const report = read(path.join(animation, 'AnimationUpgradeCheck.txt'));
if (gate !== 'installed-live-visual-check-pending' || (report.match(/^PASS:/gm) || []).length !== 7 || report.includes('Exception'))
  fail('Unity completion gate or report failed.');
const logTail = read('D:/Unity/HTH3 Project/Logs/Editor.log').slice(-120000);
const marker = logTail.lastIndexOf('THERAPY_COMPANION_ANIMATIONS_IMPORTED');
if (marker < 0 || /error CS\d+|NullReferenceException|MissingReferenceException/.test(logTail.slice(marker)))
  fail('Unity did not finish cleanly after the companion animation marker.');

console.log('PASS: exactly two Animation components were added; every pre-existing serialized scene document is otherwise byte-identical.');
console.log('PASS: Julien and Camille each reference nine distinct legacy clips with 33 authored transform bindings, correct durations/wrap modes, 30 fps metadata, autoplay off, and renderer culling.');
console.log('PASS: supplied, staged, and live GLB hashes match; runtime/importer sources match; Unity completion report and post-install log are clean.');
