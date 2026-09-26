// Quiet Garden · Therapy Room
// three.js r185 · WebGPU with automatic WebGL2 fallback.
// Scale: decide it now and write it here. 1 unit = 1 metre unless stated.

import * as THREE from 'three/webgpu';           // Timer ships in core
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { mix, color, screenUV } from 'three/tsl';

const renderer = new THREE.WebGPURenderer({ antialias: true });
renderer.setSize(innerWidth, innerHeight);
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));   // cap: cost is quadratic
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.0;
document.body.appendChild(renderer.domElement);

// Not always required (setAnimationLoop initialises too), but it is what makes
// the whole *Async() family unnecessary.
await renderer.init();

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(50, innerWidth / innerHeight, 0.1, 500);
camera.position.set(0, 2.5, 7);

const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;

// A metal with no environment reflects nothing and renders black.
const pmrem = new THREE.PMREMGenerator(renderer);
scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;

// Background as a node: no geometry, no texture.
scene.backgroundNode = mix(color(0x0a1220), color(0x243b55), screenUV.y);

scene.add(new THREE.HemisphereLight(0xbcd8ff, 0x101820, 1.0));
const key = new THREE.DirectionalLight(0xffffff, 2.2);
key.position.set(4, 6, 3);
scene.add(key);

// --- your scene ---------------------------------------------------------
const material = new THREE.MeshStandardNodeMaterial({ roughness: 0.3, metalness: 0.5 });
const mesh = new THREE.Mesh(new THREE.TorusKnotGeometry(1.2, 0.36, 200, 32), material);
scene.add(mesh);
// ------------------------------------------------------------------------

const timer = new THREE.Timer();     // Clock is deprecated since r183

renderer.setAnimationLoop((timestamp) => {
  timer.update(timestamp);                       // Timer needs this; Clock did not
  const dt = Math.min(timer.getDelta(), 1 / 30); // a hidden tab returns a huge delta

  mesh.rotation.y += dt * 0.4;                   // dt-scaled, never per-frame constant

  controls.update();
  renderer.render(scene, camera);
});

addEventListener('resize', () => {
  camera.aspect = innerWidth / innerHeight;
  camera.updateProjectionMatrix();               // forgetting this stretches the image
  renderer.setSize(innerWidth, innerHeight);
});

document.getElementById('hud').textContent =
  `three.js r185 · ${renderer.backend?.isWebGPUBackend ? 'WebGPU' : 'WebGL2 (fallback)'}`;

// Optional: verify_scene.mjs discovers the scene on its own via the devtools
// hook, but exposing it explicitly is more reliable and costs one line.
window.__scene = { scene, camera, renderer, controls };
