// In-memory format comparison. Never saves or prints a camera frame or vital value.
import {createRequire} from 'node:module';
const {SmartSpectraSDK,SmartSpectraLogLevel,breathingMetrics,cardioMetrics}=createRequire(import.meta.url)(process.argv[2]);
const config={apiKey:process.env.TLW_PRESAGE_API_KEY,requestedMetrics:[...breathingMetrics,...cardioMetrics],enableTelemetry:false,logLevel:SmartSpectraLogLevel.kError};
const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const report=data=>console.log(JSON.stringify(data));
let saved;
const capture=new SmartSpectraSDK(config);
capture.on('error',code=>report({phase:'capture',errorCode:code}));
capture.on('videoOutput',(pixels,w,h,stride,format)=>{
 if(format!==0)return;
 saved={pixels:Buffer.from(pixels),w,h,stride};
});
try{capture.useCamera({deviceIndex:0,width:1280,height:720,fps:30});capture.start();await delay(5000)}
finally{await capture.stopAsync();await capture.destroy()}
if(!saved){report({phase:'capture',missing:true});process.exit(1)}
for(const format of [2,0]){
 const channels=format===2?4:3,{w,h}=saved,frame=Buffer.alloc(w*h*channels);
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){
  const a=y*saved.stride+x*3,b=(y*w+x)*channels;
  frame[b]=saved.pixels[a];frame[b+1]=saved.pixels[a+1];frame[b+2]=saved.pixels[a+2];if(channels===4)frame[b+3]=255;
 }
 const sdk=new SmartSpectraSDK(config),validations={};let state=0,failed=false;
 sdk.on('processingStatus',code=>{state=code});
 sdk.on('validationStatus',code=>{validations[code]=(validations[code]||0)+1});
 sdk.on('error',code=>{failed=true;report({format,errorCode:code})});
 try{
  sdk.useCustomInput();sdk.start();const start=process.hrtime.bigint();
  for(let i=0;i<240&&!failed;i++){
   await delay(33);
   try{sdk.sendFrame(frame,w,h,w*channels,format,(process.hrtime.bigint()-start)/1000n)}
   catch(error){failed=true;report({format,errorCode:error.code})}
  }
  report({format,state,failed,validations});
 }finally{await sdk.stopAsync();await sdk.destroy()}
}
saved=null;
