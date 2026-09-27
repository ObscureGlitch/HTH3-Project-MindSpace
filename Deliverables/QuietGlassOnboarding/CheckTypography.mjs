import { createRequire } from 'node:module';
import { readFileSync } from 'node:fs';
const require=createRequire(import.meta.url);
const {createCanvas,GlobalFonts}=require('C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas');
const fontRoot='D:/Unity/HTH3 Project/Assets/TherapyGame/UI/QuietGlass/Fonts/';
if(!GlobalFonts.registerFromPath(fontRoot+'SourceSerif4-Regular.otf','MindSpaceSerif'))throw Error('Serif missing');
if(!GlobalFonts.registerFromPath(fontRoot+'Carlito-Regular.ttf','MindSpaceSans'))throw Error('Sans missing');
const ctx=createCanvas(8,8).getContext('2d');
const styles={Brand:[30,'Serif'],Tagline:[13,'Sans'],Title:[48,'Serif'],Subtitle:[22,'Sans'],RowTitle:[21,'Serif'],Copy:[15,'Sans',true],Fine:[13,'Sans'],Tiny:[12,'Sans'],Status:[16,'Serif'],Guidance:[12,'Sans',true],PreviewText:[14,'Sans'],button:[17,'Serif'],primary:[18,'Serif'],link:[13,'Sans']};
let checks=0;
function test(text,style,width,height){
 const [size,font,wrap]=styles[style];ctx.font=`${size}px MindSpace${font}`;
 let lines=[];
 for(const paragraph of text.split('\n')){
  if(!wrap){lines.push(paragraph);continue;}
  let line='';for(const word of paragraph.split(' ')){const next=line?line+' '+word:word;if(line&&ctx.measureText(next).width>width){lines.push(line);line=word;}else line=next;}lines.push(line);
 }
 for(const line of lines)if(ctx.measureText(line).width>width-.5)throw Error(`Text clips: ${style} ${width}px: ${line}`);
 const m=ctx.measureText('Ag');const lineHeight=Math.max(size*1.15,m.actualBoundingBoxAscent+m.actualBoundingBoxDescent);
 if(lines.length*lineHeight>height+1)throw Error(`Text height clips: ${style} ${height}px (${lines.length} lines): ${text}`);
 checks++;
}
const source=readFileSync(new URL('Runtime/WellnessVoiceOnboarding.cs',import.meta.url),'utf8');
const regex=/GUI\.Label\(new Rect\((\d+),(\d+),(\d+),(\d+)\),"((?:\\.|[^"\\])*)",t\.(\w+)\)/g;
for(const match of source.matchAll(regex))test(JSON.parse('"'+match[5]+'"'),match[6],+match[3],+match[4]);
test('Microphone audio and messages go to ElevenLabs and agent services.','Copy',491,32);
test('Presage estimates pulse, breathing and HRV locally.\nStable readings may be shared during voice chat.','Copy',491,45);
test('Optional. Requires voice and camera.','Copy',491,32);
for(const text of ['Camera','Position','Pulse','Breathing','Acquiring','Opening','Waiting','Paused','Ready'])test(text,'Status',93,31);
for(const text of ['Continue with choices  ›','Play without voice or camera'])test(text,'primary',308,50);
test('Continue without camera','button',222,46);test('Retry / resume','button',176,46);
test('Companion: Camille  ›','link',191,23);
for(const match of source.matchAll(/return "([^"\n]+)";/g))test(match[1],'Guidance',265,44);
test('Camera stopped. Use Retry / resume. See diagnostics for help.','Guidance',265,44);
console.log(`PASS: ${checks} actual-font text-fit checks using installed Source Serif 4 and Carlito; headless approximation, not a Unity screenshot.`);
