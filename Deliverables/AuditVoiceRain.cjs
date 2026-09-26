// Read-only saved-scene audit. No Unity process, graphics or network access.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const project = 'D:/Unity/HTH3 Project';
const root = path.join(project, 'Assets/TherapyGame');
const read = p => fs.readFileSync(path.join(root, p), 'utf8');
const parse = text => [...text.matchAll(/^--- !u!(\d+) &(-?\d+)\r?\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)]
  .map(m => ({ type: +m[1], id: m[2], text: m[3] }));
const scalar = (text, key) => text?.match(new RegExp('^  ' + key + ': (.*)$', 'm'))?.[1].trim();
const ref = (text, key) => scalar(text, key)?.match(/fileID: (\d+)/)?.[1];
const guid = p => read(p + '.meta').match(/guid: (\w+)/)[1];
const docs = parse(read('Scenes/TherapyRoom.unity'));
const before = parse(read('Integrations/Backups/TherapyRoom_BeforeVoiceAndRain.unity'));
const byID = new Map(docs.map(d => [d.id, d]));
const components = name => docs.filter(d => d.type === 114 && d.text.includes('guid: ' + guid('Runtime/' + name + '.cs')));
const checks = [];
const check = (name, ok, detail) => checks.push({name, ok: !!ok, ...(detail === undefined ? {} : {detail})});
const chat = components('WellnessVoiceChat')[0];
const rain = components('WellnessRain')[0];
const explorer = components('WellnessExplorer')[0];
check('Exactly one voice and one rain controller', components('WellnessVoiceChat').length === 1 && components('WellnessRain').length === 1);
check('Both controllers use existing player', ref(chat?.text, 'player') === explorer?.id && ref(rain?.text, 'player') === explorer?.id);
check('Voice references weather and settings', ref(chat?.text, 'rain') === rain?.id && chat?.text.includes('guid: ' + guid('Integrations/Settings/VoiceCompanions.asset')));
const voiceOutput = byID.get(ref(chat?.text, 'voiceAudio'));
const rainOutput = byID.get(ref(rain?.text, 'rainAudio'));
check('Voice output is idle on scene load', voiceOutput?.type === 82 && scalar(voiceOutput.text, 'm_PlayOnAwake') === '0');
check('Rain audio references the imported recording', rainOutput?.type === 82 && rainOutput.text.includes('guid: ' + guid('Integrations/Audio/GardenRain.mp3')) && scalar(rainOutput.text, 'm_PlayOnAwake') === '0');
check('Rain has indoor low-pass filter', byID.get(ref(rain?.text, 'indoorMuffle'))?.type === 169);
const particles = byID.get(ref(rain?.text, 'drops'));
check('Particle reference and 600-particle ceiling saved', particles?.type === 198 && /maxNumParticles: 600/.test(particles.text) && /playOnAwake: 0/.test(particles.text));
const settings = read('Integrations/Settings/VoiceCompanions.asset');
const configs = [
  'D:/Hack the Hill/Deliverables/IntegrationSources/Hackathon26/Assets/Resources/TalkingBoxAgentConfig.asset',
  'D:/Hack the Hill/Deliverables/IntegrationSources/Hackathon26/Assets/Resources/TalkingBoxAgentConfig 1.asset'
];
const ids = configs.map(p => fs.readFileSync(p, 'utf8').match(/agentId: (\S+)/)[1]);
check('Both agent IDs match the source repository', ids.every(id => settings.includes('agentId: ' + id)) && (settings.match(/agentId:/g) || []).length === 2);
check('15-second startup and ten-minute session limits saved', /connectionTimeoutSeconds: 15/.test(settings) && /maximumSessionSeconds: 600/.test(settings));
const audio = read('Integrations/Audio/GardenRain.mp3.meta');
check('Rain audio is mono, streamed and 22.05 kHz', /loadType: 2/.test(audio) && /sampleRateOverride: 22050/.test(audio) && /forceToMono: 1/.test(audio) && /preloadAudioData: 0/.test(audio));
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
check('Rain recording matches Git LFS content', hash(path.join(root, 'Integrations/Audio/GardenRain.mp3')) === 'ca25c3c85f4fd56abf29ec7a59fc75d243bfbe12160f4f0870fc4f18c5e2c63e');
const lock = JSON.parse(fs.readFileSync(path.join(project, 'Packages/packages-lock.json'), 'utf8'));
check('ElevenLabs SDK resolved as local embedded package', lock.dependencies['io.elevenlabs.agents']?.source === 'embedded');
check('Import marker consumed', read('VoiceRainRequest.txt').trim() === 'installed-live-verification-pending');
const oldWithoutTransforms = before.filter(d => d.type !== 4);
const changed = oldWithoutTransforms.filter(d => byID.get(d.id)?.text !== d.text);
check('All pre-existing non-transform scene objects unchanged', changed.length === 0, {checked: oldWithoutTransforms.length, changedIDs: changed.map(d => d.id)});
const newIDs = docs.filter(d => !before.some(b => b.id === d.id));
check('Only four new scene GameObjects', newIDs.filter(d => d.type === 1).length === 4, newIDs.filter(d => d.type === 1).map(d => scalar(d.text, 'm_Name')));
const result = {ok: checks.every(c => c.ok), method: 'CPU-only saved scene/package audit; live voice, microphone and rendering not exercised', checks};
console.log(JSON.stringify(result, null, 2));
if (!result.ok) process.exitCode = 1;
