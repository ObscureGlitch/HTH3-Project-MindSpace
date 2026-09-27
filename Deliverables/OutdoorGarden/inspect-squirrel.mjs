import { readFile } from 'node:fs/promises';
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

globalThis.ProgressEvent ??= class ProgressEvent {};

async function inspect(path) {
  const bytes = await readFile(path);
  const loader = new GLTFLoader();
  const gltf = await loader.parseAsync(
    bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength),
    '',
  );
  gltf.scene.updateMatrixWorld(true);
  const box = new THREE.Box3().setFromObject(gltf.scene);
  const nodes = [];
  gltf.scene.traverse((node) => {
    nodes.push({
      name: node.name,
      type: node.type,
      parent: node.parent?.name,
      position: node.position.toArray(),
      rotation: node.quaternion.toArray(),
      scale: node.scale.toArray(),
      mesh: node.isMesh ? {
        vertices: node.geometry.attributes.position?.count ?? 0,
        triangles: node.geometry.index ? node.geometry.index.count / 3 : (node.geometry.attributes.position?.count ?? 0) / 3,
        skin: node.isSkinnedMesh,
        morphTargets: node.morphTargetInfluences?.length ?? 0,
        material: Array.isArray(node.material) ? node.material.map((m) => m.name) : node.material?.name,
      } : undefined,
      bones: node.isSkinnedMesh ? node.skeleton.bones.map((bone) => bone.name) : undefined,
    });
  });
  return {
    path,
    bounds: { min: box.min.toArray(), max: box.max.toArray(), size: box.getSize(new THREE.Vector3()).toArray() },
    animations: gltf.animations.map((clip) => ({
      name: clip.name,
      duration: clip.duration,
      tracks: clip.tracks.map((track) => {
        const stride = track.values.length / track.times.length;
        const min = Array(stride).fill(Infinity);
        const max = Array(stride).fill(-Infinity);
        for (let key = 0; key < track.times.length; key++) {
          for (let component = 0; component < stride; component++) {
            const value = track.values[key * stride + component];
            min[component] = Math.min(min[component], value);
            max[component] = Math.max(max[component], value);
          }
        }
        return { name: track.name, type: track.ValueTypeName, keys: track.times.length, min, max };
      }),
    })),
    nodes,
  };
}

const paths = process.argv.slice(2);
console.log(JSON.stringify(await Promise.all(paths.map(inspect)), null, 2));
