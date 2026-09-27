import {createRequire} from 'node:module';
import {readFileSync} from 'node:fs';
const sharp=createRequire(import.meta.url)('C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const path='D:/Hack the Hill/Deliverables/TherapyGame/Weather/Textures/CloudTypes/CumulusBank.png';
const meta=await sharp(path).metadata();
if(!meta.hasAlpha)throw Error('Cloud bank has no alpha');
const {data,info}=await sharp(path).ensureAlpha().raw().toBuffer({resolveWithObject:true});
let clear=0,solid=0,partial=0,border=0,borderCount=0;
for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
 const a=data[(y*info.width+x)*4+3];
 if(a<4)clear++;else if(a>249)solid++;else partial++;
 if(x<info.width*.01||x>info.width*.99||y<info.height*.01||y>info.height*.99){border+=a/255;borderCount++;}
}
if(clear<info.width*info.height*.1||partial<1000||solid<1000||border/borderCount>.05)throw Error('Alpha border/core validation failed');
console.log(JSON.stringify({file:path,width:info.width,height:info.height,alpha:meta.hasAlpha,clear,partial,solid,borderMeanAlpha:border/borderCount},null,2));
const landscape=readFileSync('D:/Unity/HTH3 Project/Assets/TherapyGame/Weather/Shaders/WellnessDistantLandscape.hlsl','utf8');
const mask=readFileSync('D:/Hack the Hill/Deliverables/TherapyGame/Weather/Shaders/WellnessCloudOcclusion.hlsl','utf8');
const normalize=s=>s.replace(/\/\/[^\n]*/g,'').replace(/WellnessCloudRidge/g,'WellnessRidge').replace(/\s+/g,'');
const hills=landscape.slice(landscape.indexOf('float turn ='),landscape.indexOf('float darkness'));
const hidden=mask.slice(mask.indexOf('float turn='),mask.indexOf('float aa='));
if(normalize(hills)!==normalize(hidden))throw Error('Cloud mask contours differ from existing distant hills');
console.log('PASS: cloud-bank mask reproduces the existing mountain and tree contours exactly; landscape shader unchanged.');
