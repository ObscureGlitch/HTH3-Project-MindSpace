import * as THREE from 'three/webgpu';
import {OrbitControls} from 'three/addons/controls/OrbitControls.js';
import {mix,color,screenUV} from 'three/tsl';
import {buildGarden} from '../garden.mjs';
// This laptop lost its WebGPU device during verification; use the verified WebGL2 backend.
const params=new URLSearchParams(location.search),renderer=new THREE.WebGPURenderer({antialias:true,forceWebGL:true});renderer.setSize(innerWidth,innerHeight);renderer.setPixelRatio(Math.min(devicePixelRatio,1.5));renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.shadowMap.enabled=true;renderer.shadowMap.type=THREE.PCFSoftShadowMap;document.body.appendChild(renderer.domElement);await renderer.init();
const scene=new THREE.Scene();scene.backgroundNode=mix(color(0x80b9c7),color(0xdce7ce),screenUV.y);scene.fog=new THREE.FogExp2(0xbdd4cd,.009);
const camera=new THREE.PerspectiveCamera(53,innerWidth/innerHeight,.1,250),controls=new OrbitControls(camera,renderer.domElement);controls.enableDamping=true;
const views={overview:[[42,25,-35],[9,1,-4]],door:[[-2.43,1.7,-5.4],[14,1,-7]],bridge:[[7,1.7,-6],[24,2,-5]],window:[[4,1.7,0],[17,1,-4]]},v=views[params.get('view')]||views.overview;camera.position.set(...v[0]);controls.target.set(...v[1]);controls.update();
scene.add(new THREE.HemisphereLight(0xc9e5ed,0x888662,2));const sun=new THREE.DirectionalLight(0xffe4bd,2.4);sun.position.set(-25,50,-30);sun.target.position.set(8,0,-3);sun.castShadow=true;sun.shadow.mapSize.set(2048,2048);Object.assign(sun.shadow.camera,{left:-48,right:48,top:48,bottom:-48,near:1,far:160});sun.shadow.bias=-.0003;sun.shadow.normalBias=.06;scene.add(sun,sun.target);
const garden=buildGarden();scene.add(garden.root);const floor=new THREE.Mesh(new THREE.BoxGeometry(7.2,.24,6.2),new THREE.MeshStandardNodeMaterial({color:0xe4dcc2}));floor.position.y=-.12;floor.receiveShadow=true;scene.add(floor);
const timer=new THREE.Timer();timer.connect(document);renderer.setAnimationLoop(timestamp=>{timer.update(timestamp);garden.animate(params.has('steps')?Number(params.get('steps'))/60:timer.getElapsed());controls.update();renderer.render(scene,camera);});
addEventListener('resize',()=>{camera.aspect=innerWidth/innerHeight;camera.updateProjectionMatrix();renderer.setSize(innerWidth,innerHeight);});
document.getElementById('hud').innerHTML='QUIET GARDEN<br>Three.js model authoring · 1 unit = 1 metre<br>Drag to orbit · Scroll to zoom';
if(!params.has('noref')){const ref=document.createElement('img');ref.src='./reference.png';ref.style='position:fixed;right:16px;bottom:16px;width:17vw;border:3px solid #edf1df;border-radius:8px';ref.alt='User-supplied alpine garden reference';document.body.appendChild(ref);}
window.__scene={scene,camera,renderer,controls};
