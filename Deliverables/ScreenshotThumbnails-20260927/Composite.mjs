import {createRequire} from 'node:module';
import {readFileSync,writeFileSync,existsSync,copyFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {join,dirname} from 'node:path';
const require=createRequire(import.meta.url);
const modules='C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/';
const sharp=require(modules+'sharp');
const {PNG}=require(modules+'pngjs');
const dir=dirname(fileURLToPath(import.meta.url));
const titleSource='C:/Users/obscu/.codex/generated_images/01a0dc83-2050-7761-b5fd-dd5bd5a639a8/exec-c2bb39d3-8c93-45ef-8b49-efaa2428992d.png';
const titleBytes=readFileSync(titleSource),title=PNG.sync.read(titleBytes);
let left=title.width,top=title.height,right=0,bottom=0,transparent=0;
for(let y=0;y<title.height;y++)for(let x=0;x<title.width;x++){
 const alpha=title.data[(y*title.width+x)*4+3];
 if(alpha===0)transparent++;
 if(alpha>0){left=Math.min(left,x);top=Math.min(top,y);right=Math.max(right,x);bottom=Math.max(bottom,y);}
}
if(transparent<title.width*title.height*.25)throw Error('Title has no usable transparent background.');
const crop={left,top,width:right-left+1,height:bottom-top+1};
function safeWrite(path,bytes){if(existsSync(path))throw Error('Refusing to overwrite '+path);writeFileSync(path,bytes);}
safeWrite(join(dir,'MindSpace-Title-Transparent.png'),titleBytes);
const hash=bytes=>createHash('sha256').update(bytes).digest('hex');
function chunks(bytes){
 const found=[];for(let p=8;p<bytes.length;){const n=bytes.readUInt32BE(p),type=bytes.toString('ascii',p+4,p+8);found.push({type,bytes:bytes.subarray(p,p+12+n)});p+=n+12;}return found;
}
// Preserve screenshot color-management/resolution metadata as well as its pixels.
function preserveColorMetadata(original,encoded){
 const types=new Set(['cHRM','gAMA','iCCP','sRGB','pHYs']);
 const retained=chunks(original).filter(c=>types.has(c.type));
 const output=[encoded.subarray(0,8)];
 for(const c of chunks(encoded)){
  if(types.has(c.type))continue;
  output.push(c.bytes);
  if(c.type==='IHDR')output.push(...retained.map(item=>item.bytes));
 }
 return Buffer.concat(output);
}
const cases=[
 {source:'Demo7.png',output:'A-Sunny-Pond.png',x:190,y:206,width:940},
 {source:'Demo6.png',output:'B-Northern-Lights.png',x:58,y:66,width:740},
 {source:'Demo8.png',output:'C-Companion-Cabin.png',x:44,y:35,width:630}
];
const checks=[];
for(const item of cases){
 const sourcePath=join('C:/Users/obscu/Downloads',item.source),sourceBytes=readFileSync(sourcePath);
 const sourceHash=hash(sourceBytes),source=PNG.sync.read(sourceBytes);
 const titleResized=await sharp(titleBytes).extract(crop).resize({width:item.width}).png().toBuffer();
 const overlay=PNG.sync.read(titleResized);
 if(item.x+overlay.width>source.width||item.y+overlay.height>source.height)throw Error('Overlay clips viewport');
 const output=new PNG({width:source.width,height:source.height});output.data=Buffer.from(source.data);
 let covered=0;
 for(let y=0;y<overlay.height;y++)for(let x=0;x<overlay.width;x++){
  const i=(y*overlay.width+x)*4,a=overlay.data[i+3]/255;
  if(a===0)continue;
  covered++;
  const j=((y+item.y)*source.width+x+item.x)*4,b=source.data[j+3]/255;
  const alpha=a+b*(1-a);
  for(let c=0;c<3;c++)output.data[j+c]=Math.round((overlay.data[i+c]*a+source.data[j+c]*b*(1-a))/alpha);
  output.data[j+3]=Math.round(alpha*255);
 }
 const encoded=preserveColorMetadata(sourceBytes,PNG.sync.write(output));
 const decoded=PNG.sync.read(encoded);
 let unchanged=0,changes=0;
 for(let y=0;y<source.height;y++)for(let x=0;x<source.width;x++){
  const j=(y*source.width+x)*4,ox=x-item.x,oy=y-item.y;
  const hasOverlay=ox>=0&&oy>=0&&ox<overlay.width&&oy<overlay.height&&overlay.data[(oy*overlay.width+ox)*4+3]>0;
  const matches=source.data.subarray(j,j+4).equals(decoded.data.subarray(j,j+4));
  if(!hasOverlay&&!matches)throw Error('Changed a pixel outside title at '+x+','+y);
  if(matches)unchanged++;else changes++;
 }
 if(hash(readFileSync(sourcePath))!==sourceHash)throw Error('Original file changed');
 safeWrite(join(dir,item.output),encoded);
 checks.push({source:sourcePath,output:item.output,width:source.width,height:source.height,sourceSHA256:sourceHash,originalUnchanged:true,changedPixels:changes,unchangedPixels:unchanged,titleAlphaCoveredPixels:covered,changesOutsideTitle:0,titlePlacement:{x:item.x,y:item.y,width:overlay.width,height:overlay.height}});
 console.log('PASS: '+item.output+' — '+source.width+'×'+source.height+', zero pixel changes outside title, original file unchanged.');
}
safeWrite(join(dir,'Verification.json'),JSON.stringify(checks,null,2));
