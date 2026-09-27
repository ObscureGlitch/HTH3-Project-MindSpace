// Bounded local camera diagnostic: outputs counts only, never images, vitals or credentials.
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const { SmartSpectraSDK, breathingMetrics, cardioMetrics, SmartSpectraLogLevel } = require(process.argv[2]);
const sdk = new SmartSpectraSDK({apiKey:process.env.TLW_PRESAGE_API_KEY,
  requestedMetrics:[...breathingMetrics,...cardioMetrics],enableTelemetry:false,logLevel:SmartSpectraLogLevel.kError});
const groups = new Map();
let status=-1,validation=-1,stopping=false;
sdk.on('processingStatus',value=>{status=value});
sdk.on('validationStatus',value=>{validation=value});
sdk.on('error',code=>{process.stdout.write(JSON.stringify({errorCode:code})+'\n')});
sdk.on('videoOutput',(buffer,width,height,stride,format)=>{
  const channels=format<2?3:4;
  if(format<0||format>3)return;
  let mono=0,count=0;
  for(let y=0;y<height;y+=Math.max(1,Math.floor(height/16)))for(let x=0;x<width;x+=Math.max(1,Math.floor(width/16))){
    const p=y*stride+x*channels,r=buffer[p],g=buffer[p+1],b=buffer[p+2];
    if(Math.max(r,g,b)-Math.min(r,g,b)<=2)mono++;
    count++;
  }
  const key=JSON.stringify({format,status,validation,monochrome:mono/count>.95});
  groups.set(key,(groups.get(key)||0)+1);
});
async function stop(){
  if(stopping)return;stopping=true;
  try{await sdk.stopAsync();await sdk.destroy()}catch{}
  for(const [key,count] of groups)process.stdout.write(JSON.stringify({...JSON.parse(key),frames:count})+'\n');
  process.exit(0);
}
process.on('SIGTERM',stop);
setTimeout(stop,Math.min(55,Math.max(5,Number(process.argv[3])||20))*1000);
try{sdk.useCamera({deviceIndex:0,width:1280,height:720,fps:30});sdk.start()}
catch{process.stdout.write('{"startupFailed":true}\n');await stop()}
