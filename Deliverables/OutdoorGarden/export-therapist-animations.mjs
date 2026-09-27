// CPU-only conversion of the two supplied animated therapist GLBs into native
// Unity legacy AnimationClip data. Geometry stays on the already-corrected
// scene characters because it matches these sources exactly and retains the
// existing five-vowel live lip-sync meshes.
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { copyFile, mkdir, readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';

globalThis.ProgressEvent ??= class ProgressEvent {};

const output = new URL('../TherapyGame/Characters/Animation/Source/', import.meta.url);
await mkdir(output, { recursive: true });
const expectedClips = ['Idle', 'Listen', 'Talk', 'Nod', 'Write', 'Think', 'Wave', 'Walk', 'Sit'];
const expectedDurations = [6, 6, 4, 1.6, 4, 5, 2.4, 1.1, 6];
const round = (number) => Math.round(number * 1e7) / 1e7;
const array = (value) => value.toArray().map(round);
const reports = [];

for (const person of ['julien', 'camille']) {
  const sourcePath = `C:/Users/obscu/Downloads/therapist_${person}_animated.glb`;
  const currentPath = new URL(`../TherapyGame/Characters/Refinement/Source/${person}.json`, import.meta.url);
  const current = JSON.parse(await readFile(currentPath, 'utf8'));
  const bytes = await readFile(sourcePath);
  const sourceHash = createHash('sha256').update(bytes).digest('hex');
  const loader = new GLTFLoader();
  loader.register(() => ({ name: 'CPU texture stub', loadTexture: async () => new THREE.Texture() }));
  const gltf = await loader.parseAsync(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), '');
  const json = gltf.parser.json;
  const scene = gltf.scene;
  scene.updateMatrixWorld(true);

  assert.deepEqual(gltf.animations.map((clip) => clip.name), expectedClips, `${person}: unexpected clip set`);
  gltf.animations.forEach((clip, index) => assert(Math.abs(clip.duration - expectedDurations[index]) < 1e-4, `${person}: changed ${clip.name} duration`));
  for (const animation of json.animations) {
    for (const sampler of animation.samplers) {
      assert(['LINEAR', 'STEP', undefined].includes(sampler.interpolation), `${person}: unsupported cubic animation interpolation`);
    }
  }

  const nodes = await gltf.parser.getDependencies('node');
  const nodeGroups = Map.groupBy(nodes, (node) => node.name);
  const sourceNode = (name) => {
    const matches = nodeGroups.get(name) ?? [];
    assert.equal(matches.length, 1, `${person}: expected one source node named ${name}`);
    return matches[0];
  };
  const boneNames = new Set(current.bones.map((bone) => bone.name));
  assert.equal(boneNames.size, current.bones.length, `${person}: current bone names must be unique`);
  const pathCache = new Map();
  const bonePath = (index) => {
    if (pathCache.has(index)) return pathCache.get(index);
    const bone = current.bones[index];
    const path = bone.parent < 0 ? bone.name : `${bonePath(bone.parent)}/${bone.name}`;
    pathCache.set(index, path);
    return path;
  };
  const boneIndex = new Map(current.bones.map((bone, index) => [bone.name, index]));

  let maximumBindPositionError = 0;
  let maximumBindRotationError = 0;
  let maximumBindScaleError = 0;
  const bindDifferences = [];
  for (let index = 0; index < current.bones.length; index++) {
    const bone = current.bones[index];
    const source = sourceNode(bone.name);
    const local = source.matrixWorld.clone();
    if (bone.parent >= 0) local.premultiply(sourceNode(current.bones[bone.parent].name).matrixWorld.clone().invert());
    const position = new THREE.Vector3();
    const rotation = new THREE.Quaternion();
    const scale = new THREE.Vector3();
    local.decompose(position, rotation, scale);
    const positionError = position.distanceTo(new THREE.Vector3(...bone.p));
    const rotationError = THREE.MathUtils.radToDeg(rotation.angleTo(new THREE.Quaternion(...bone.q)));
    const scaleError = scale.distanceTo(new THREE.Vector3(...bone.s));
    maximumBindPositionError = Math.max(maximumBindPositionError, positionError);
    maximumBindRotationError = Math.max(maximumBindRotationError, rotationError);
    maximumBindScaleError = Math.max(maximumBindScaleError, scaleError);
    bindDifferences.push({ name: bone.name, positionError, rotationError, scaleError });
  }
  const worstBindDifferences = bindDifferences.toSorted((a, b) =>
    (b.positionError + b.rotationError + b.scaleError) - (a.positionError + a.rotationError + a.scaleError)).slice(0, 12);
  assert(maximumBindPositionError < 1e-5, `${person}: animated source position rig does not match the installed character: ${JSON.stringify(worstBindDifferences)}`);
  // The installed JSON rounds quaternions to seven decimals; the resulting
  // angular comparison can differ by roughly 0.04 degrees while representing
  // the same authored bind pose.
  assert(maximumBindRotationError < .05, `${person}: animated source rotation rig does not match the installed character: ${JSON.stringify(worstBindDifferences)}`);
  assert(maximumBindScaleError < 1e-5, `${person}: animated source scale rig does not match the installed character: ${JSON.stringify(worstBindDifferences)}`);

  const skippedTargets = new Set();
  const clips = gltf.animations.map((clip) => {
    const tracks = [];
    for (const track of clip.tracks) {
      const separator = track.name.lastIndexOf('.');
      const nodeName = track.name.slice(0, separator);
      const property = track.name.slice(separator + 1);
      const index = boneIndex.get(nodeName);
      // Authored facial morph curves are intentionally omitted: the game keeps
      // its existing real-time audio visemes. Small hair-piece-only curves are
      // also omitted because those meshes are consolidated in the Unity model.
      if (property === 'morphTargetInfluences' || index === undefined) {
        skippedTargets.add(`${nodeName}.${property}`);
        continue;
      }
      assert(['position', 'quaternion', 'scale'].includes(property), `${person}: unsupported ${property} track`);
      tracks.push({
        path: bonePath(index),
        property,
        times: Array.from(track.times, round),
        values: Array.from(track.values, round),
      });
    }
    assert.equal(tracks.length, 33, `${person}: ${clip.name} should retain 33 body/eye transform tracks`);
    return { name: clip.name, duration: round(clip.duration), tracks };
  });

  const sourceTriangles = json.meshes.flatMap((mesh) => mesh.primitives)
    .reduce((total, primitive) => total + json.accessors[primitive.indices ?? primitive.attributes.POSITION].count / 3, 0);
  const currentTriangles = current.meshes.flatMap((mesh) => mesh.submeshes)
    .reduce((total, submesh) => total + submesh.indices.length / 3, 0);
  assert.equal(sourceTriangles, person === 'julien' ? 10776 : 13954, `${person}: unexpected animated source geometry`);
  assert.equal(currentTriangles, sourceTriangles, `${person}: installed corrected geometry no longer matches the animated source`);
  const usedCurrentMaterialNames = [...new Set(current.meshes.flatMap((mesh) => mesh.submeshes)
    .map((submesh) => current.materials[submesh.material].name))].sort();
  const sourceMaterialNames = json.materials.map((material) => material.name).sort();
  assert.deepEqual(usedCurrentMaterialNames, sourceMaterialNames, `${person}: used materials changed`);

  const bounds = new THREE.Box3().setFromObject(scene);
  const data = {
    version: 1,
    person,
    sourceHash,
    clips,
  };
  const report = {
    person,
    sourceHash,
    triangles: sourceTriangles,
    materials: json.materials.length,
    currentBones: current.bones.length,
    retainedTracksPerClip: 33,
    skippedAuthoredTargets: [...skippedTargets].sort(),
    clips: clips.map(({ name, duration, tracks }) => ({ name, duration, tracks: tracks.length })),
    maximumBindPositionError,
    maximumBindRotationError,
    maximumBindScaleError,
    bounds: { min: array(bounds.min), max: array(bounds.max) },
  };
  await writeFile(new URL(`${person}_animations.json`, output), JSON.stringify(data));
  await copyFile(sourcePath, new URL(`therapist_${person}_animated.glb`, output));
  reports.push(report);

  scene.traverse((object) => {
    object.geometry?.dispose();
    if (object.material) for (const material of [object.material].flat()) material.dispose();
  });
}

await writeFile(new URL('AnimationConversionCheck.json', output), JSON.stringify({
  ok: true,
  method: 'GLTFLoader CPU clip conversion; installed corrected geometry retained because source geometry/materials/bind rig match',
  deterministicMapping: {
    Idle: 'no active conversation',
    Listen: 'connected and waiting for or receiving the user turn',
    Talk: 'agent conversation mode is Speaking',
    Nod: 'completed user transcript event',
    Write: 'agent Speaking mode just ended',
    Think: 'completed user turn awaiting an agent response',
    Wave: 'companion becomes active',
    Walk: 'imported but intentionally unused while character remains stationary',
    Sit: 'imported but intentionally unused while character remains standing',
  },
  reports,
}, null, 2));
console.log(JSON.stringify(reports, null, 2));
