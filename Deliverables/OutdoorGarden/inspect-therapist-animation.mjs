import { readFile } from 'node:fs/promises';
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

globalThis.ProgressEvent ??= class ProgressEvent {};

const round = (value) => Math.round(value * 1e7) / 1e7;
const vectorDelta = (values, stride) => {
  let maximum = 0;
  for (let key = 1; key < values.length / stride; key++) {
    let distance = 0;
    for (let component = 0; component < stride; component++) {
      const difference = values[key * stride + component] - values[component];
      distance += difference * difference;
    }
    maximum = Math.max(maximum, Math.sqrt(distance));
  }
  return round(maximum);
};

for (const person of ['julien', 'camille']) {
  const sourcePath = `C:/Users/obscu/Downloads/therapist_${person}_animated.glb`;
  const currentPath = new URL(`../TherapyGame/Characters/Refinement/Source/${person}.json`, import.meta.url);
  const bytes = await readFile(sourcePath);
  const current = JSON.parse(await readFile(currentPath, 'utf8'));
  const loader = new GLTFLoader();
  loader.register(() => ({ name: 'CPU texture stub', loadTexture: async () => new THREE.Texture() }));
  const gltf = await loader.parseAsync(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), '');
  const nodes = await gltf.parser.getDependencies('node');
  gltf.scene.updateMatrixWorld(true);

  const nodeByName = new Map(nodes.map((node) => [node.name, node]));
  const boneNames = new Set(current.bones.map((bone) => bone.name));
  const sourceBoneNames = new Set();
  const skeletons = [];
  gltf.scene.traverse((node) => {
    if (!node.isSkinnedMesh) return;
    const names = node.skeleton.bones.map((bone) => bone.name);
    names.forEach((name) => sourceBoneNames.add(name));
    skeletons.push(names);
  });

  const targetSummary = new Map();
  for (const clip of gltf.animations) {
    for (const track of clip.tracks) {
      const separator = track.name.lastIndexOf('.');
      const name = track.name.slice(0, separator);
      const property = track.name.slice(separator + 1);
      const node = nodeByName.get(name);
      const stride = track.values.length / track.times.length;
      if (!targetSummary.has(name)) targetSummary.set(name, {
        name,
        type: node?.type,
        parent: node?.parent?.name,
        inCurrentRig: boneNames.has(name),
        inSourceSkin: sourceBoneNames.has(name),
        properties: new Set(),
        maximumDelta: 0,
      });
      const summary = targetSummary.get(name);
      summary.properties.add(property);
      summary.maximumDelta = Math.max(summary.maximumDelta, vectorDelta(track.values, stride));
    }
  }

  const currentByName = new Map(current.bones.map((bone) => [bone.name, bone]));
  const bindDifferences = [];
  for (const name of sourceBoneNames) {
    const node = nodeByName.get(name);
    const prior = currentByName.get(name);
    if (!node || !prior) continue;
    const parent = node.parent && sourceBoneNames.has(node.parent.name) ? node.parent : null;
    const local = node.matrixWorld.clone();
    if (parent) local.premultiply(parent.matrixWorld.clone().invert());
    const p = new THREE.Vector3();
    const q = new THREE.Quaternion();
    const s = new THREE.Vector3();
    local.decompose(p, q, s);
    const priorP = new THREE.Vector3(...prior.p);
    const priorQ = new THREE.Quaternion(...prior.q);
    const priorS = new THREE.Vector3(...prior.s);
    bindDifferences.push({
      name,
      position: round(p.distanceTo(priorP)),
      rotationDegrees: round(THREE.MathUtils.radToDeg(q.angleTo(priorQ))),
      scale: round(s.distanceTo(priorS)),
    });
  }

  const morphMeshes = [];
  gltf.scene.traverse((node) => {
    if (!node.isMesh || !node.morphTargetInfluences?.length) return;
    morphMeshes.push({
      name: node.name,
      targets: node.morphTargetInfluences.length,
      dictionary: node.morphTargetDictionary,
      skinned: node.isSkinnedMesh,
    });
  });

  console.log(JSON.stringify({
    person,
    jsonSkins: gltf.parser.json.skins?.length ?? 0,
    uniqueSkeletons: new Set(skeletons.map((names) => names.join('|'))).size,
    sourceSkinBones: [...sourceBoneNames],
    currentBones: current.bones.length,
    missingCurrentBones: [...sourceBoneNames].filter((name) => !boneNames.has(name)),
    extraCurrentBones: [...boneNames].filter((name) => !sourceBoneNames.has(name)),
    worstBindPosition: bindDifferences.toSorted((a, b) => b.position - a.position).slice(0, 5),
    worstBindRotation: bindDifferences.toSorted((a, b) => b.rotationDegrees - a.rotationDegrees).slice(0, 5),
    animatedTargets: [...targetSummary.values()].map((entry) => ({
      ...entry,
      properties: [...entry.properties],
      maximumDelta: round(entry.maximumDelta),
    })),
    morphMeshes,
    clips: gltf.animations.map((clip) => ({
      name: clip.name,
      duration: round(clip.duration),
      tracks: clip.tracks.length,
      keys: clip.tracks.reduce((total, track) => total + track.times.length, 0),
    })),
  }, null, 2));

  gltf.scene.traverse((object) => {
    object.geometry?.dispose();
    if (object.material) for (const material of [object.material].flat()) material.dispose();
  });
}
