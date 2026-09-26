// Read-only, CPU-only audit. Does not start Unity or render anything.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = process.argv[2] || 'D:/Unity/HTH3 Project/Assets/TherapyGame';
const read = p => fs.readFileSync(path.join(root, p), 'utf8');
const parse = text => [...text.matchAll(/^--- !u!(\d+) &(\d+)\r?\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)]
  .map(m => ({ type: +m[1], id: m[2], text: m[3] }));
const scalar = (text, key) => text.match(new RegExp('^  ' + key + ': (.*)$', 'm'))?.[1].trim();
const ref = (text, key) => scalar(text, key)?.match(/fileID: (\d+)/)?.[1];
const guid = p => read(p + '.meta').match(/guid: (\w+)/)[1];
const sceneText = read('Scenes/TherapyRoom.unity');
const source = JSON.parse(read('Exterior/Source/GardenModel.json'));
const docs = parse(sceneText);
const old = parse(read('Exterior/Backups/TherapyRoom_BeforeGarden.unity'));
const objects = docs.filter(d => d.type === 1);
const find = name => objects.find(d => scalar(d.text, 'm_Name') === name);
const transforms = new Map(docs.filter(d => d.type === 4).map(d => [ref(d.text, 'm_GameObject'), d]));
const garden = find('OutdoorGarden');
const childTransforms = new Set([transforms.get(garden?.id)?.id]);
for (let changed = true; changed;) {
  changed = false;
  for (const d of transforms.values()) {
    if (childTransforms.has(ref(d.text, 'm_Father')) && !childTransforms.has(d.id)) {
      childTransforms.add(d.id);
      changed = true;
    }
  }
}
const gardenIDs = new Set([...transforms].filter(([, d]) => childTransforms.has(d.id)).map(([id]) => id));
const components = docs.filter(d => gardenIDs.has(ref(d.text, 'm_GameObject')));
const scripts = (list, name) => {
  const scriptGuid = guid('Runtime/' + name + '.cs');
  return list.filter(d => d.type === 114 && d.text.includes('guid: ' + scriptGuid));
};
const checks = [];
const check = (name, ok, details) => checks.push({ name, ok, details });
check('Garden root saved and enabled', !!garden && scalar(garden.text, 'm_IsActive') === '1');
check('Authored visual batches plus 39 butterfly parts', components.filter(d => d.type === 23).length === source.meshes.length + 39);
const boxes = components.filter(d => d.type === 65);
check('Authored solid box colliders', boxes.length === source.boxes.length && boxes.every(d => scalar(d.text, 'm_IsTrigger') === '0'), boxes.length);
const ground = components.filter(d => d.type === 64);
check('Terrain and bridge mesh colliders', ground.length === 2 && ground.every(d => scalar(d.text, 'm_IsTrigger') === '0'),
  ground.map(d => scalar(objects.find(g => g.id === ref(d.text, 'm_GameObject')).text, 'm_Name')));
check('13 animated butterflies', scripts(components, 'WellnessButterfly').length === 13);
const seats = scripts(docs, 'WellnessSeat');
const oldSeats = scripts(old, 'WellnessSeat');
check('Indoor seat components preserved byte-for-byte', seats.length === oldSeats.length && seats.every(d => oldSeats.find(o => o.id === d.id)?.text === d.text),
  { furniture: seats.length, spots: seats.reduce((n, d) => n + (d.text.match(/- label:/g) || []).length, 0) });
check('Seven interaction targets preserved', scripts(docs, 'WellnessInteraction').length === 7);
check('Fall recovery and frame cap attached', scripts(docs, 'WellnessGardenSafety').length === 1 && scripts(docs, 'WellnessGardenPerformance').length === 1);
check('Old window backdrop disabled', scalar(find('Quiet landscape beyond window').text, 'm_IsActive') === '0');
check('Door hinge opened 105 degrees', scalar(transforms.get(find('Open door hinge').id).text, 'm_LocalRotation') === '{x: 0, y: 0.7933533, z: 0, w: 0.6087614}');
const meshNames = [...source.meshes,...source.colliders].map(m => m.name.replaceAll(' ','_')).concat(['Butterfly_wing','Butterfly_body']);
const meshGuids = meshNames.map(f => guid('Exterior/Meshes/' + f + '.asset'));
check('All current native meshes present and referenced', meshGuids.every(g => sceneText.includes('guid: ' + g)), meshGuids.length);
const materialGuids = new Set(fs.readdirSync(path.join(root, 'Exterior/Materials')).filter(f => f.endsWith('.mat')).map(f => guid('Exterior/Materials/' + f)));
const renderedMaterialGuids = components.filter(d => d.type === 23).flatMap(d => [...d.text.matchAll(/fileID: 2100000, guid: (\w+)/g)].map(m => m[1]));
check('Garden material references resolve', renderedMaterialGuids.length === source.meshes.length + 39 && renderedMaterialGuids.every(g => materialGuids.has(g)));
check('Low-load pipeline assigned', scripts(docs, 'WellnessScenePipeline')[0].text.includes('guid: ' + guid('Exterior/Settings/GardenLowLoadURP.asset')));
const renderer = read('Exterior/Settings/GardenLowLoadRenderer.asset');
check('Renderer uses Forward with AO disabled', renderer.includes('m_RenderingMode: 0') && renderer.includes('m_Active: 0'));
check('Refinement trigger consumed', read('GardenRequest.txt').trim() === 'refinements-installed-live-check-pending');
check('Old front wall retained and disabled', scalar(find('Entry front wall').text, 'm_IsActive') === '0');
check('Actual new front window saved', !!find('Front garden window') && !!find('Clear front window pane'));
check('No invisible pond shoreline barriers', !objects.some(g => gardenIDs.has(g.id) && scalar(g.text, 'm_Name') === 'Pond shore'));
const flyDocs = scripts(components,'WellnessButterfly');
check('Small butterflies and faster wingbeats saved', flyDocs.every(d => {
  const transform = transforms.get(ref(d.text,'m_GameObject'));
  const scale = Number(scalar(transform.text,'m_LocalScale').match(/x: ([\d.]+)/)[1]);
  return scale >= .12 && scale <= .151 && Number(scalar(d.text,'flapHz')) >= 7.2;
}));
check('Water uses lightweight custom shader', read('Exterior/Materials/water.mat').includes('guid: '+guid('Exterior/Shaders/QuietPond.shader')));
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
check('Authored source matches imported data', hash(path.join(root, 'Exterior/Source/GardenModel.json')) === hash(path.join(__dirname, 'generated/GardenModel.json')));
console.log(JSON.stringify({ ok: checks.every(c => c.ok), method: 'Saved scene and file checks only; no Play mode or GPU rendering', checks }, null, 2));
if (checks.some(c => !c.ok)) process.exitCode = 1;
