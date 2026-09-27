// Read-only post-import audit. Does not render, enter Play mode, or modify Unity.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

const root = 'D:/Unity/HTH3 Project/Assets/TherapyGame';
const stage = 'D:/Hack the Hill/Deliverables/TherapyGame';
const folder = 'Exterior/LivingGarden';
const read = (relative) => fs.readFileSync(path.join(root, relative), 'utf8');
const parse = (text) => [...text.matchAll(/^--- !u!(\d+) &(-?\d+)(?: stripped)?\r?\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)]
  .map((match) => ({ type: +match[1], id: match[2], text: match[3] }));
const scalar = (text, key) => text?.match(new RegExp(`^  ${key}: (.*)$`, 'm'))?.[1].trim();
const ref = (text, key) => scalar(text, key)?.match(/fileID: (-?\d+)/)?.[1];
const guid = (relative) => read(`${relative}.meta`).match(/guid: (\w+)/)[1];
const hash = (file) => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');

const scene = parse(read('Scenes/TherapyRoom.unity'));
const before = parse(read(`${folder}/Backups/TherapyRoom_BeforeAnimatedSquirrels.unity`));
const byId = new Map(scene.map((document) => [document.id, document]));
const beforeById = new Map(before.map((document) => [document.id, document]));
const checks = [];
const check = (name, ok, detail) => checks.push({ name, ok: !!ok, ...(detail === undefined ? {} : { detail }) });

const gameObjects = scene.filter((document) => document.type === 1);
const transforms = scene.filter((document) => document.type === 4);
const scripts = (name) => scene.filter((document) => document.type === 114 && document.text.includes(`guid: ${guid(`Runtime/${name}.cs`)}`));
const life = scripts('WellnessGardenLife')[0];
const lifeObject = byId.get(ref(life?.text, 'm_GameObject'));
const lifeTransform = transforms.find((document) => ref(document.text, 'm_GameObject') === lifeObject?.id);
const lifeTransformIds = new Set([lifeTransform?.id]);
for (let changed = true; changed;) {
  changed = false;
  for (const transform of transforms) {
    if (lifeTransformIds.has(ref(transform.text, 'm_Father')) && !lifeTransformIds.has(transform.id)) {
      lifeTransformIds.add(transform.id);
      changed = true;
    }
  }
}
const lifeGameIds = new Set(transforms.filter((transform) => lifeTransformIds.has(transform.id)).map((transform) => ref(transform.text, 'm_GameObject')));
const lifeMembers = scene.filter((document) => lifeGameIds.has(ref(document.text, 'm_GameObject')));
const kinds = [...life.text.matchAll(/- kind: (\d+)/g)].map((match) => +match[1]);
const animals = [...life.text.matchAll(/- kind: (\d+)[\s\S]*?animation: \{fileID: (-?\d+)\}[\s\S]*?phase:/g)]
  .map((match) => ({ kind: +match[1], animation: match[2] }));

check('Three rabbits, three squirrels and five birds are linked', kinds.length === 11 && kinds.filter((kind) => kind === 0).length === 3 && kinds.filter((kind) => kind === 1).length === 3 && kinds.filter((kind) => kind === 2).length === 5);
check('Only squirrels reference native Animation components', animals.length === 11 && animals.filter((animal) => animal.kind === 1 && animal.animation !== '0').length === 3 && animals.filter((animal) => animal.kind !== 1 && animal.animation !== '0').length === 0);

const animations = lifeMembers.filter((document) => document.type === 111);
const skins = lifeMembers.filter((document) => document.type === 137);
const meshGuid = guid(`${folder}/Meshes/AnimatedSquirrel.asset`);
const materialGuids = fs.readdirSync(path.join(root, folder, 'Materials/Squirrel'))
  .filter((file) => file.endsWith('.mat'))
  .map((file) => guid(`${folder}/Materials/Squirrel/${file}`));
const clipGuids = fs.readdirSync(path.join(root, folder, 'Clips/Squirrel'))
  .filter((file) => file.endsWith('.anim'))
  .map((file) => guid(`${folder}/Clips/Squirrel/${file}`));
check('Exactly three shared-mesh skinned squirrel renderers', skins.length === 3 && skins.every((skin) => skin.text.includes(`guid: ${meshGuid}`)));
check('Each squirrel renderer has 24 bones and all eight source materials', skins.every((skin) => (skin.text.match(/^  - \{fileID: /gm) ?? []).length === 24 + 8 && materialGuids.every((id) => skin.text.includes(`guid: ${id}`))));
check('Squirrel renderers disable shadows, probes and offscreen skinning', skins.every((skin) => scalar(skin.text, 'm_CastShadows') === '0' && scalar(skin.text, 'm_UpdateWhenOffscreen') === '0' && scalar(skin.text, 'm_LightProbeUsage') === '0' && scalar(skin.text, 'm_ReflectionProbeUsage') === '0'));
check('Each squirrel has all seven supplied looping clips', animations.length === 3 && animations.every((animation) => clipGuids.every((id) => animation.text.includes(`guid: ${id}`)) && scalar(animation.text, 'm_WrapMode') === '2' && scalar(animation.text, 'm_PlayAutomatically') === '1'));
check('Three model roots use the verified 4.2 visual scale', gameObjects.filter((document) => scalar(document.text, 'm_Name') === 'Animated squirrel model').length === 3 && transforms.filter((transform) => {
  const object = byId.get(ref(transform.text, 'm_GameObject'));
  return scalar(object?.text, 'm_Name') === 'Animated squirrel model' && scalar(transform.text, 'm_LocalScale') === '{x: 4.2, y: 4.2, z: 4.2}';
}).length === 3);

const meshAsset = read(`${folder}/Meshes/AnimatedSquirrel.asset`);
check('Native mesh retains 57,802 vertices and eight submeshes', /m_VertexCount: 57802/.test(meshAsset) && (meshAsset.match(/firstByte:/g) ?? []).length >= 8);
check('Garden layer now has 42 renderers and still only 12 trunk colliders', lifeMembers.filter((document) => document.type === 23 || document.type === 137).length === 42 && lifeMembers.filter((document) => document.type === 65).length === 12);
check('No squirrel physics, cameras, lights or particle systems were added', !lifeMembers.some((document) => [20, 54, 64, 108, 198].includes(document.type)));

const beforeObjects = before.filter((document) => document.type === 1);
const beforeTransforms = before.filter((document) => document.type === 4);
const beforeLife = before.find((document) => document.id === life.id);
const beforeLifeObject = beforeById.get(ref(beforeLife?.text, 'm_GameObject'));
const beforeLifeTransform = beforeTransforms.find((document) => ref(document.text, 'm_GameObject') === beforeLifeObject?.id);
const allowedOldIds = new Set([beforeLife?.id, beforeLifeTransform?.id]);
for (const squirrelName of ['Red squirrel 4', 'Red squirrel 5']) {
  const object = beforeObjects.find((document) => scalar(document.text, 'm_Name') === squirrelName);
  const rootTransform = beforeTransforms.find((document) => ref(document.text, 'm_GameObject') === object?.id);
  const subtree = new Set([rootTransform?.id]);
  for (let changed = true; changed;) {
    changed = false;
    for (const transform of beforeTransforms) {
      if (subtree.has(ref(transform.text, 'm_Father')) && !subtree.has(transform.id)) {
        subtree.add(transform.id);
        changed = true;
      }
    }
  }
  const objectIds = new Set(beforeTransforms.filter((transform) => subtree.has(transform.id)).map((transform) => ref(transform.text, 'm_GameObject')));
  for (const document of before) if (subtree.has(document.id) || objectIds.has(document.id) || objectIds.has(ref(document.text, 'm_GameObject'))) allowedOldIds.add(document.id);
}
const unexpectedChanges = before.filter((document) => !allowedOldIds.has(document.id) && byId.get(document.id)?.text !== document.text);
const concurrentHudChanges = unexpectedChanges.filter((document) =>
  document.text.includes('m_EditorClassIdentifier: TherapyGame.Runtime::TheLastWatch.UI.WellnessHud') &&
  document.text.includes('  panelOpacity: 1') &&
  byId.get(document.id)?.text.includes('  panelOpacity: 0'));
check('Unrelated records are unchanged aside from the concurrent HUD opacity update', unexpectedChanges.length === concurrentHudChanges.length, {
  concurrentHudIds: concurrentHudChanges.map((document) => document.id),
  unexpectedIds: unexpectedChanges.filter((document) => !concurrentHudChanges.includes(document)).map((document) => document.id),
});
const newDocuments = scene.filter((document) => !beforeById.has(document.id));
check('New scene records are only model hierarchy, animation and skin renderers', newDocuments.every((document) => [1, 4, 111, 137].includes(document.type)), { unexpectedTypes: [...new Set(newDocuments.filter((document) => ![1, 4, 111, 137].includes(document.type)).map((document) => document.type))] });

const conversion = JSON.parse(read(`${folder}/Source/SquirrelConversionCheck.json`));
const garden = JSON.parse(read(`${folder}/Source/GardenLife.json`));
check('CPU conversion preserved source geometry, bind pose and seven clips', conversion.ok && conversion.triangles === 107104 && conversion.vertices === 57802 && conversion.bones === 24 && conversion.clips.length === 7 && conversion.maximumRestSkinError < 1e-6);
check('Third route export is closed and terrain-audited', garden.animals.length === 11 && garden.animals.filter((animal) => animal.kind === 1).length === 3 && garden.animals.every((animal) => animal.route.length === 65));
const sourceHash = conversion.sourceHash;
check('Live, staged and supplied animated GLBs are byte-identical', [
  path.join(root, folder, 'Source/tiny-squirrel-animated.glb'),
  path.join(stage, 'Exterior/LivingGarden/Source/tiny-squirrel-animated.glb'),
  'C:/Users/obscu/Downloads/tiny-squirrel-animated (1).glb',
].every((file) => hash(file) === sourceHash));
check('Live runtime and editor integrations match staged source', ['Runtime/WellnessGardenLife.cs', 'Editor/TherapySquirrelUpgrade.cs'].every((relative) => hash(path.join(root, relative)) === hash(path.join(stage, relative))));
check('Unity import and saved-scene checks passed', read(`${folder}/SquirrelUpgradeCheck.txt`).includes('PASS: saved-scene checks retained terrain grounding'));
check('One-shot upgrade gate completed', read('SquirrelUpgradeRequest.txt').trim() === 'installed-live-visual-check-pending');

const result = {
  ok: checks.every((entry) => entry.ok),
  method: 'Saved-scene, source and CPU/import checks only; rendered appearance and live frame rate unverified because Windows UI automation was unavailable',
  checks,
};
console.log(JSON.stringify(result, null, 2));
if (!result.ok) process.exitCode = 1;
