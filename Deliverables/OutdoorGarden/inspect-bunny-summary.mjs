import { readFile } from 'node:fs/promises';
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

globalThis.ProgressEvent ??= class ProgressEvent {};

const path = process.argv[2];
const bytes = await readFile(path);
const loader = new GLTFLoader();
const gltf = await loader.parseAsync(
  bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength),
  '',
);

gltf.scene.updateMatrixWorld(true);
const bounds = new THREE.Box3().setFromObject(gltf.scene);
let vertices = 0;
let triangles = 0;
let meshes = 0;
let skinnedMeshes = 0;
const materials = new Set();

gltf.scene.traverse((node) => {
  if (!node.isMesh) return;
  meshes++;
  if (node.isSkinnedMesh) skinnedMeshes++;
  vertices += node.geometry.attributes.position?.count ?? 0;
  triangles += node.geometry.index
    ? node.geometry.index.count / 3
    : (node.geometry.attributes.position?.count ?? 0) / 3;
  const nodeMaterials = Array.isArray(node.material) ? node.material : [node.material];
  nodeMaterials.filter(Boolean).forEach((material) => materials.add(material.name));
});

const animations = gltf.animations.map((clip) => {
  const rootTracks = clip.tracks.filter((track) => track.name.startsWith('root.'));
  return {
    name: clip.name,
    duration: clip.duration,
    tracks: clip.tracks.length,
    keys: clip.tracks.reduce((sum, track) => sum + track.times.length, 0),
    animatedNodes: [...new Set(clip.tracks.map((track) => track.name.split('.')[0]))],
    rootMotion: rootTracks.map((track) => {
      const stride = track.values.length / track.times.length;
      const first = Array.from(track.values.slice(0, stride));
      const last = Array.from(track.values.slice(-stride));
      let maxDelta = 0;
      for (let index = 0; index < track.values.length; index++) {
        maxDelta = Math.max(maxDelta, Math.abs(track.values[index] - first[index % stride]));
      }
      return { track: track.name, first, last, maxDelta };
    }),
  };
});

console.log(JSON.stringify({
  path,
  bounds: {
    min: bounds.min.toArray(),
    max: bounds.max.toArray(),
    size: bounds.getSize(new THREE.Vector3()).toArray(),
  },
  meshes,
  skinnedMeshes,
  vertices,
  triangles,
  materials: [...materials],
  animations,
}, null, 2));
