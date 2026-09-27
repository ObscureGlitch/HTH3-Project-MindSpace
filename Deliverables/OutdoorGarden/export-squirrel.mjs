// CPU-only conversion of the supplied animated GLB into data Unity can turn into
// one native skinned renderer plus legacy AnimationClips. The source file remains
// unchanged and no renderer, network access, or runtime glTF package is required.
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { copyFile, mkdir, readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';

globalThis.ProgressEvent ??= class ProgressEvent {};

const sourcePath = 'C:/Users/obscu/Downloads/tiny-squirrel-animated (1).glb';
const output = new URL('../TherapyGame/Exterior/LivingGarden/Source/', import.meta.url);
await mkdir(output, { recursive: true });

const bytes = await readFile(sourcePath);
const sourceHash = createHash('sha256').update(bytes).digest('hex');
const loader = new GLTFLoader();
const gltf = await loader.parseAsync(
  bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength),
  '',
);
const json = gltf.parser.json;
assert.equal(json.skins?.length, 1, 'Expected the animated squirrel tail skin');
assert.equal(json.textures?.length ?? 0, 0, 'Unexpected texture payload');
assert.deepEqual(
  gltf.animations.map((clip) => clip.name),
  ['Idle', 'Nibble', 'LookAround', 'Alert', 'Hop', 'DropAndRun', 'Run'],
  'Unexpected supplied animation set',
);

const round = (number) => Math.round(number * 1e7) / 1e7;
const array = (value) => value.toArray().map(round);
const scene = gltf.scene;
scene.updateMatrixWorld(true);

const nodes = await gltf.parser.getDependencies('node');
const animatedNames = new Set(gltf.animations.flatMap((clip) => clip.tracks.map((track) => {
  const separator = track.name.lastIndexOf('.');
  return track.name.slice(0, separator);
})));
// Animated rigid mesh nodes (eyes, nose and chin) must also become Unity bones;
// otherwise their supplied blink/look/nibble tracks would have nothing to drive.
const bones = nodes.filter((node) => !node.isMesh || animatedNames.has(node.name));
assert.equal(new Set(bones.map((bone) => bone.name)).size, bones.length, 'Bone names must be unique');
const boneIndex = new Map(bones.map((bone, index) => [bone, index]));
const nearestBone = (node) => {
  while (node && !boneIndex.has(node)) node = node.parent;
  return node;
};
const boneData = bones.map((bone) => {
  const parent = nearestBone(bone.parent);
  const local = bone.matrixWorld.clone();
  if (parent) local.premultiply(parent.matrixWorld.clone().invert());
  const position = new THREE.Vector3();
  const rotation = new THREE.Quaternion();
  const scale = new THREE.Vector3();
  local.decompose(position, rotation, scale);
  return {
    name: bone.name,
    parent: parent ? boneIndex.get(parent) : -1,
    p: array(position),
    q: array(rotation),
    s: array(scale),
  };
});

const pathCache = new Map();
const bonePath = (bone) => {
  if (pathCache.has(bone)) return pathCache.get(bone);
  const parent = nearestBone(bone.parent);
  const path = parent ? `${bonePath(parent)}/${bone.name}` : bone.name;
  pathCache.set(bone, path);
  return path;
};

const materials = json.materials.map((material) => {
  const pbr = material.pbrMetallicRoughness ?? {};
  return {
    name: material.name,
    color: (pbr.baseColorFactor ?? [1, 1, 1, 1]).map(round),
    roughness: round(pbr.roughnessFactor ?? 1),
    metallic: round(pbr.metallicFactor ?? 1),
    emission: (material.emissiveFactor ?? [0, 0, 0]).map(round),
    alphaMode: material.alphaMode ?? 'OPAQUE',
    doubleSided: !!material.doubleSided,
  };
});

const mesh = {
  name: 'Animated squirrel',
  positions: [],
  normals: [],
  uvs: [],
  joints: [],
  weights: [],
  submeshes: [],
};
let triangles = 0;
let maximumRestSkinError = 0;
const meshNodes = [];
scene.traverse((node) => { if (node.isMesh) meshNodes.push(node); });

for (const node of meshNodes) {
  assert(!Array.isArray(node.material), `Multiple materials on ${node.name}`);
  const association = gltf.parser.associations.get(node.material);
  const materialIndex = association?.materials;
  assert(Number.isInteger(materialIndex), `Missing material association on ${node.name}`);
  let submesh = mesh.submeshes.find((entry) => entry.material === materialIndex);
  if (!submesh) {
    submesh = { material: materialIndex, indices: [] };
    mesh.submeshes.push(submesh);
  }

  const geometry = node.geometry;
  const position = geometry.getAttribute('position');
  const normal = geometry.getAttribute('normal');
  const uv = geometry.getAttribute('uv');
  assert(position && normal, `Missing positions or normals on ${node.name}`);
  const offset = mesh.positions.length / 3;
  const normalMatrix = new THREE.Matrix3().getNormalMatrix(node.matrixWorld);

  if (node.isSkinnedMesh) {
    node.skeleton.update();
    const skinIndex = geometry.getAttribute('skinIndex');
    const skinWeight = geometry.getAttribute('skinWeight');
    assert(skinIndex && skinWeight, 'Skinned tail is missing weights');
    for (let vertex = 0; vertex < position.count; vertex++) {
      const original = new THREE.Vector3().fromBufferAttribute(position, vertex);
      const skinned = original.clone();
      node.applyBoneTransform(vertex, skinned);
      maximumRestSkinError = Math.max(maximumRestSkinError, original.distanceTo(skinned));
      skinned.applyMatrix4(node.matrixWorld);
      const n = new THREE.Vector3().fromBufferAttribute(normal, vertex).applyNormalMatrix(normalMatrix);
      mesh.positions.push(...array(skinned));
      mesh.normals.push(...array(n));
      mesh.uvs.push(uv ? round(uv.getX(vertex)) : 0, uv ? round(1 - uv.getY(vertex)) : 0);
      let totalWeight = 0;
      for (let component = 0; component < 4; component++) {
        const sourceIndex = skinIndex.getComponent(vertex, component);
        const weight = skinWeight.getComponent(vertex, component);
        const mapped = boneIndex.get(node.skeleton.bones[sourceIndex]);
        assert(mapped !== undefined, 'Tail bone is missing from the exported hierarchy');
        mesh.joints.push(mapped);
        mesh.weights.push(round(weight));
        totalWeight += weight;
      }
      assert(Math.abs(totalWeight - 1) < 1e-4, 'Tail weights are not normalized');
    }
  } else {
    const joint = nearestBone(node);
    const jointIndex = boneIndex.get(joint);
    assert(jointIndex !== undefined, `Rigid part ${node.name} has no parent bone`);
    for (let vertex = 0; vertex < position.count; vertex++) {
      const p = new THREE.Vector3().fromBufferAttribute(position, vertex).applyMatrix4(node.matrixWorld);
      const n = new THREE.Vector3().fromBufferAttribute(normal, vertex).applyNormalMatrix(normalMatrix);
      mesh.positions.push(...array(p));
      mesh.normals.push(...array(n));
      mesh.uvs.push(uv ? round(uv.getX(vertex)) : 0, uv ? round(1 - uv.getY(vertex)) : 0);
      mesh.joints.push(jointIndex, 0, 0, 0);
      mesh.weights.push(1, 0, 0, 0);
    }
  }

  const indices = geometry.index?.array ?? Array.from({ length: position.count }, (_, index) => index);
  const reflected = node.matrixWorld.determinant() < 0;
  for (let index = 0; index < indices.length; index += 3) {
    submesh.indices.push(
      offset + indices[index],
      offset + indices[index + (reflected ? 2 : 1)],
      offset + indices[index + (reflected ? 1 : 2)],
    );
  }
  triangles += indices.length / 3;
}

assert(maximumRestSkinError < 1e-6, 'The loaded pose does not match the skin bind pose');
assert.equal(triangles, 107104, 'Unexpected source triangle count');
assert.equal(materials.length, 8, 'Unexpected material count');
assert.equal(mesh.submeshes.length, 8, 'Every supplied material should remain represented');
assert.equal(mesh.normals.length, mesh.positions.length);
assert.equal(mesh.uvs.length, mesh.positions.length / 3 * 2);
assert.equal(mesh.joints.length, mesh.positions.length / 3 * 4);
assert.equal(mesh.weights.length, mesh.joints.length);

const boneByName = new Map(bones.map((bone) => [bone.name, bone]));
const clips = gltf.animations.map((clip) => ({
  name: clip.name,
  duration: round(clip.duration),
  tracks: clip.tracks.map((track) => {
    const separator = track.name.lastIndexOf('.');
    const nodeName = track.name.slice(0, separator);
    const property = track.name.slice(separator + 1);
    const bone = boneByName.get(nodeName);
    assert(bone, `Animation target ${nodeName} is not an exported bone`);
    assert(['position', 'quaternion', 'scale'].includes(property), `Unsupported animated property ${property}`);
    return {
      path: bonePath(bone),
      property,
      times: Array.from(track.times, round),
      values: Array.from(track.values, round),
    };
  }),
}));

const bounds = new THREE.Box3().setFromObject(scene);
const data = {
  version: 1,
  name: 'Tiny squirrel animated',
  units: 'metres',
  sourceHash,
  bounds: { min: array(bounds.min), max: array(bounds.max) },
  bones: boneData,
  materials,
  mesh,
  clips,
};
const report = {
  ok: true,
  method: 'GLTFLoader CPU conversion; no rendered visual check',
  source: 'tiny-squirrel-animated (1).glb',
  sourceHash,
  triangles,
  vertices: mesh.positions.length / 3,
  sourceMeshNodes: meshNodes.length,
  unityRenderersPerSquirrel: 1,
  materials: materials.length,
  bones: bones.length,
  clips: clips.map(({ name, duration, tracks }) => ({ name, duration, tracks: tracks.length })),
  bounds: data.bounds,
  maximumRestSkinError,
};

await writeFile(new URL('Squirrel.json', output), JSON.stringify(data));
await writeFile(new URL('SquirrelConversionCheck.json', output), JSON.stringify(report, null, 2));
await copyFile(sourcePath, new URL('tiny-squirrel-animated.glb', output));
console.log(JSON.stringify(report, null, 2));

scene.traverse((object) => {
  object.geometry?.dispose();
  if (object.material) for (const material of [object.material].flat()) material.dispose();
});
