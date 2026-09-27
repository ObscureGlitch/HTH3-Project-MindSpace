// CPU-only conversion of the supplied animated bunny GLB into data Unity can
// turn into one native skinned renderer plus legacy AnimationClips. The source
// remains unchanged; Unity needs no runtime glTF package.
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { copyFile, mkdir, readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';

globalThis.ProgressEvent ??= class ProgressEvent {};

const sourcePath = 'C:/Users/obscu/Downloads/tiny-bunny-animated.glb';
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
assert.equal(json.skins?.length ?? 0, 0, 'Expected a rigid transform-rigged bunny');
assert.equal(json.textures?.length ?? 0, 0, 'Unexpected texture payload');
assert.deepEqual(
  gltf.animations.map((clip) => clip.name),
  ['Idle', 'Graze', 'Alert', 'Groom', 'Hop', 'Run', 'Binky'],
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
// Animated rigid mesh nodes must become Unity bones so blinking, grooming,
// muzzle, ear, tail and leg tracks have real transform targets.
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
  name: 'Animated bunny',
  positions: [],
  normals: [],
  uvs: [],
  joints: [],
  weights: [],
  submeshes: [],
};
let triangles = 0;
const meshNodes = [];
scene.traverse((node) => { if (node.isMesh) meshNodes.push(node); });

for (const node of meshNodes) {
  assert(!node.isSkinnedMesh, `Unexpected source skin on ${node.name}`);
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

assert.equal(meshNodes.length, 30, 'Unexpected source mesh node count');
assert.equal(mesh.positions.length / 3, 46992, 'Unexpected source vertex count');
assert.equal(triangles, 86528, 'Unexpected source triangle count');
assert.equal(materials.length, 6, 'Unexpected material count');
assert.equal(mesh.submeshes.length, 6, 'Every supplied material should remain represented');
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
  name: 'Tiny bunny animated',
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
  source: 'tiny-bunny-animated.glb',
  sourceHash,
  triangles,
  vertices: mesh.positions.length / 3,
  sourceMeshNodes: meshNodes.length,
  unityRenderersPerBunny: 1,
  materials: materials.length,
  bones: bones.length,
  clips: clips.map(({ name, duration, tracks }) => ({ name, duration, tracks: tracks.length })),
  bounds: data.bounds,
  maximumRestSkinError: 0,
};

await writeFile(new URL('Bunny.json', output), JSON.stringify(data));
await writeFile(new URL('BunnyConversionCheck.json', output), JSON.stringify(report, null, 2));
await copyFile(sourcePath, new URL('tiny-bunny-animated.glb', output));
console.log(JSON.stringify(report, null, 2));

scene.traverse((object) => {
  object.geometry?.dispose();
  if (object.material) for (const material of [object.material].flat()) material.dispose();
});
