// Test-only SDK: invalid position -> valid -> native status/error pair -> recovery.
export const SmartSpectraLogLevel={kError:3},breathingMetrics=[],cardioMetrics=[];
export const decodeMetrics=()=>null;
export class SmartSpectraSDK {
 static version='test-fixture';
 constructor(){this.events={};this.count=0;this.running=false;this.needsReset=false;}
 on(name,callback){this.events[name]=callback;}
 useCustomInput(){this.custom=true;}
 start(){if(this.needsReset||!this.custom)throw new Error('Reset and reselect custom input first');this.running=true;this.events.processingStatus?.(3);}
 async stopAsync(){this.running=false;this.events.processingStatus?.(1);}
 reset(){if(this.running)throw new Error('Stop before reset');this.needsReset=false;this.custom=false;}
 async destroy(){}
 sendFrame(){
  if(!this.running)throw new Error('Input while stopped');
  this.count++;
  if(this.count===5){this.needsReset=true;this.running=false;this.events.processingStatus?.(5);this.events.error?.(8,'SmartSpectra processing failed.',false);return;}
  this.events.frameSentThrough?.(true,this.count*33333);
  this.events.validationStatus?.(this.count<3?7:0,this.count*33333,'Test positioning');
 }
}
