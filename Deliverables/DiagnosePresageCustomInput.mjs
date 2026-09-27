import { createRequire } from 'node:module';
import { FrameSubmission, isFrameTimingError } from './TherapyGame/Integrations/PresageBridge/frames.mjs';
const require=createRequire(import.meta.url);
const {SmartSpectraSDK,SmartSpectraLogLevel,breathingMetrics,cardioMetrics}=require(process.argv[2]);
const key=process.env.TLW_PRESAGE_API_KEY;
const report=data=>console.log('PRESAGE_CHECK:'+JSON.stringify(data));
const sdk=new SmartSpectraSDK({apiKey:key,requestedMetrics:[...breathingMetrics,...cardioMetrics],enableTelemetry:false,logLevel:SmartSpectraLogLevel.kNone});
let status=0,accepted=0,rejected=0;
const validationCounts={};
sdk.on('validationStatus',code=>{validationCounts[code]=(validationCounts[code]||0)+1});
sdk.on('processingStatus',code=>{status=code;report({status:code})});
const submission=new FrameSubmission((...args)=>sdk.sendFrame(...args),(state,error)=>report({recovery:state,code:error?.code}),()=>performance.now(),async(error)=>{
 await sdk.stopAsync();if(error.code===8){sdk.reset();sdk.useCustomInput();}sdk.start();
});
sdk.on('error',(code,message,retryable)=>{
 report({eventError:code,retryable,message:String(message).replaceAll(key,'[redacted]').slice(0,240)});
 if(isFrameTimingError(code))submission.reject({code,message});
});
sdk.on('frameSentThrough',sent=>{if(sent)accepted++;else rejected++});
const frame=Buffer.alloc(1280*720*4,128);
for(let i=3;i<frame.length;i+=4)frame[i]=255;
try{
 sdk.useCustomInput();sdk.start();
 const start=process.hrtime.bigint();let previous=0;
 for(let i=0;i<240;i++){
  await new Promise(resolve=>setTimeout(resolve,33));
  let timestamp=Number((process.hrtime.bigint()-start)/1000n)+(i>=150?6000000:0);
  // Exercise an isolated ordering error, then return to genuine monotonic timestamps.
  if(i===90)timestamp=previous;
  if(i===200)submission.reject({code:8,message:'Injected processing failure for lifecycle test'});
  submission.push(frame,1280,720,5120,2,timestamp);
  previous=timestamp;
 }
 report({complete:true,status,accepted,rejected,failed:submission.failed,validationCounts});
 if(submission.failed||status!==3||accepted<200)process.exitCode=1;
}finally{await sdk.stopAsync();await sdk.destroy()}
