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
const listCount = (text, start, end) => {
  const section = text.match(new RegExp(`^  ${start}:\\r?\\n([\\s\\S]*?)^  ${end}:`, 'm'))?.[1] ?? '';
  return (section.match(/^  - \{fileID: /gm) ?? []).length;
};
const parseAnimals = (text) => text.split(/\r?\n  - kind: /).slice(1).map((block) => ({
  kind: +block.match(/^(\d+)/)[1],
  root: block.match(/^    root: \{fileID: (-?\d+)\}/m)?.[1],
  animation: block.match(/^    animation: \{fileID: (-?\d+)\}/m)?.[1],
  routes: [...block.matchAll(/^      - (\{x: .*\})$/gm)].map((match) => match[1]),
  phase: block.match(/^    phase: (.*)$/m)?.[1],
}));

const scene = parse(read('Scenes/TherapyRoom.unity'));
const before = parse(read(`${folder}/Backups/TherapyRoom_BeforeAnimatedBunnies.unity`));
const byId = new Map(scene.map((document) => [document.id, document]));
const beforeById = new Map(before.map((document) => [document.id, document]));
const checks = [];
const check = (name, ok, detail) => checks.push({ name, ok: !!ok, ...(detail === undefined ? {} : { detail }) });

const gameObjects = scene.filter((document) => document.type === 1);
const transforms = scene.filter((document) => document.type === 4);
const scripts = (name) => scene.filter((document) => document.type === 114 && document.text.includes(`guid: ${guid(`Runtime/${name}.cs`)}`));
const life = scripts('WellnessGardenLife')[0];
const beforeLife = before.find((document) => document.id === life?.id);
const afterAnimals = parseAnimals(life?.text ?? '');
const beforeAnimals = parseAnimals(beforeLife?.text ?? '');
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

check('Three bunnies, three squirrels and five birds remain linked', afterAnimals.length === 11 && afterAnimals.filter((animal) => animal.kind === 0).length === 3 && afterAnimals.filter((animal) => animal.kind === 1).length === 3 && afterAnimals.filter((animal) => animal.kind === 2).length === 5);
check('Bunnies and squirrels use native Animation components while birds remain procedural', afterAnimals.filter((animal) => animal.kind < 2 && animal.animation !== '0').length === 6 && afterAnimals.filter((animal) => animal.kind === 2 && animal.animation !== '0').length === 0);
check('All animal roots, routes and phases are unchanged', afterAnimals.length === beforeAnimals.length && afterAnimals.every((animal, index) =>
  animal.root === beforeAnimals[index].root && animal.phase === beforeAnimals[index].phase && JSON.stringify(animal.routes) === JSON.stringify(beforeAnimals[index].routes)));
check('Existing squirrel animation links are unchanged', afterAnimals.every((animal, index) => animal.kind !== 1 || animal.animation === beforeAnimals[index].animation));

const animations = lifeMembers.filter((document) => document.type === 111);
const skins = lifeMembers.filter((document) => document.type === 137);
const bunnyMeshGuid = guid(`${folder}/Meshes/AnimatedBunny.asset`);
const squirrelMeshGuid = guid(`${folder}/Meshes/AnimatedSquirrel.asset`);
const bunnySkins = skins.filter((skin) => skin.text.includes(`guid: ${bunnyMeshGuid}`));
const squirrelSkins = skins.filter((skin) => skin.text.includes(`guid: ${squirrelMeshGuid}`));
const bunnyMaterialGuids = fs.readdirSync(path.join(root, folder, 'Materials/Bunny'))
  .filter((file) => file.endsWith('.mat'))
  .map((file) => guid(`${folder}/Materials/Bunny/${file}`));
const bunnyClipGuids = fs.readdirSync(path.join(root, folder, 'Clips/Bunny'))
  .filter((file) => file.endsWith('.anim'))
  .map((file) => guid(`${folder}/Clips/Bunny/${file}`));
const rabbitAnimationIds = afterAnimals.filter((animal) => animal.kind === 0).map((animal) => animal.animation);
const bunnyAnimations = rabbitAnimationIds.map((id) => byId.get(id)).filter(Boolean);

check('Exactly three shared-mesh skinned bunny renderers were added', bunnySkins.length === 3 && squirrelSkins.length === 3 && skins.length === 6);
check('Each bunny renderer has 22 bones and all six source materials', bunnySkins.every((skin) => listCount(skin.text, 'm_Bones', 'm_BlendShapeWeights') === 22 && listCount(skin.text, 'm_Materials', 'm_StaticBatchInfo') === 6 && bunnyMaterialGuids.every((id) => skin.text.includes(`guid: ${id}`))));
check('Bunny renderers disable shadows, probes and offscreen skinning', bunnySkins.every((skin) => scalar(skin.text, 'm_CastShadows') === '0' && scalar(skin.text, 'm_UpdateWhenOffscreen') === '0' && scalar(skin.text, 'm_LightProbeUsage') === '0' && scalar(skin.text, 'm_ReflectionProbeUsage') === '0'));
check('Each bunny has all seven supplied looping clips', bunnyAnimations.length === 3 && bunnyAnimations.every((animation) => animation.type === 111 && bunnyClipGuids.every((id) => animation.text.includes(`guid: ${id}`)) && scalar(animation.text, 'm_WrapMode') === '2' && scalar(animation.text, 'm_PlayAutomatically') === '1'));
check('Three bunny model roots use the verified 4.2 visual scale', gameObjects.filter((document) => scalar(document.text, 'm_Name') === 'Animated bunny model').length === 3 && transforms.filter((transform) => {
  const object = byId.get(ref(transform.text, 'm_GameObject'));
  return scalar(object?.text, 'm_Name') === 'Animated bunny model' && scalar(transform.text, 'm_LocalScale') === '{x: 4.2, y: 4.2, z: 4.2}';
}).length === 3);

const meshAsset = read(`${folder}/Meshes/AnimatedBunny.asset`);
check('Native bunny mesh retains 46,992 vertices and six submeshes', /m_VertexCount: 46992/.test(meshAsset) && (meshAsset.match(/firstByte:/g) ?? []).length >= 6);
check('Garden layer now has 33 renderers and still only 12 trunk colliders', lifeMembers.filter((document) => document.type === 23 || document.type === 137).length === 33 && lifeMembers.filter((document) => document.type === 65).length === 12);
check('No wildlife physics, cameras, lights or particle systems were added', !lifeMembers.some((document) => [20, 54, 64, 108, 198].includes(document.type)));

const beforeObjects = before.filter((document) => document.type === 1);
const beforeTransforms = before.filter((document) => document.type === 4);
const allowedOldIds = new Set([beforeLife?.id]);
for (const rabbitName of ['Meadow rabbit 1', 'Meadow rabbit 2', 'Meadow rabbit 3']) {
  const object = beforeObjects.find((document) => scalar(document.text, 'm_Name') === rabbitName);
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
check('All unrelated saved-scene records are byte-for-byte unchanged', unexpectedChanges.length === 0, { unexpectedIds: unexpectedChanges.map((document) => document.id) });
const newDocuments = scene.filter((document) => !beforeById.has(document.id));
check('New scene records are only bunny hierarchy, animation and skin renderers', newDocuments.length === 144 && newDocuments.every((document) => [1, 4, 111, 137].includes(document.type)), {
  count: newDocuments.length,
  unexpectedTypes: [...new Set(newDocuments.filter((document) => ![1, 4, 111, 137].includes(document.type)).map((document) => document.type))],
});

const conversion = JSON.parse(read(`${folder}/Source/BunnyConversionCheck.json`));
const garden = JSON.parse(read(`${folder}/Source/GardenLife.json`));
check('CPU conversion preserved source geometry and all seven clips', conversion.ok && conversion.triangles === 86528 && conversion.vertices === 46992 && conversion.bones === 22 && conversion.materials === 6 && conversion.clips.length === 7 && conversion.maximumRestSkinError === 0);
check('All eleven terrain-authored routes remain closed', garden.animals.length === 11 && garden.animals.every((animal) => animal.route.length === 65 && Math.hypot(animal.route[0].x - animal.route[64].x, animal.route[0].y - animal.route[64].y, animal.route[0].z - animal.route[64].z) < .001));
const sourceHash = conversion.sourceHash;
check('Live, staged and supplied animated bunny GLBs are byte-identical', [
  path.join(root, folder, 'Source/tiny-bunny-animated.glb'),
  path.join(stage, 'Exterior/LivingGarden/Source/tiny-bunny-animated.glb'),
  'C:/Users/obscu/Downloads/tiny-bunny-animated.glb',
].every((file) => hash(file) === sourceHash));
check('Live runtime and bunny editor integrations match staged source', ['Runtime/WellnessGardenLife.cs', 'Editor/TherapyBunnyUpgrade.cs'].every((relative) => hash(path.join(root, relative)) === hash(path.join(stage, relative))));
check('Unity import and saved-scene checks passed', read(`${folder}/BunnyUpgradeCheck.txt`).includes('PASS: saved-scene checks retained all routes'));
check('One-shot bunny upgrade gate completed', read('BunnyUpgradeRequest.txt').trim() === 'installed-live-visual-check-pending');

const result = {
  ok: checks.every((entry) => entry.ok),
  method: 'Saved-scene, source and CPU/import checks only; rendered appearance and live frame rate remain unverified because Windows UI automation could not start',
  checks,
};
console.log(JSON.stringify(result, null, 2));
if (!result.ok) process.exitCode = 1;
