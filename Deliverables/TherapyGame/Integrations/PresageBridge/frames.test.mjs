import { test } from 'node:test';
import assert from 'node:assert/strict';
import { FrameDecoder, FrameSubmission } from './frames.mjs';
function packet(timestamp=1000){
  const header=Buffer.alloc(24);header.writeUInt32LE(2,0);header.writeUInt32LE(1,4);
  header.writeUInt32LE(8,8);header.writeUInt32LE(2,12);header.writeDoubleLE(timestamp,16);
  return Buffer.concat([header,Buffer.from([255,0,0,255,0,0,255,255])]);
}
test('arbitrarily fragmented raw colour frames preserve all channels',()=>{
  const frames=[],decoder=new FrameDecoder((data,...meta)=>frames.push([Buffer.from(data),meta]));
  const input=Buffer.concat([packet(),packet(2000)]);
  for(const byte of input)decoder.push(Buffer.from([byte]));
  assert.equal(frames.length,2);
  assert.deepEqual([...frames[0][0]],[255,0,0,255,0,0,255,255]);
  assert.deepEqual(frames[1][1],[2,1,8,2,2000]);
});
test('truncated frames never reach Presage',()=>{
  let count=0;const decoder=new FrameDecoder(()=>count++);
  decoder.push(packet().subarray(0,30));assert.equal(count,0);
});
test('invalid dimensions, format and timestamps are rejected before allocation/delivery',()=>{
  for(const modify of [p=>p.writeUInt32LE(999999,0),p=>p.writeUInt32LE(1,12),p=>p.writeDoubleLE(NaN,16)]){
    const p=packet();modify(p);assert.throws(()=>new FrameDecoder(()=>assert.fail()).push(p));
  }
  const decoder=new FrameDecoder(()=>{});decoder.push(packet());assert.throws(()=>decoder.push(packet()));
});
test('rejected SDK frame does not corrupt subsequent coalesced packets or disconnect',()=>{
  const events=[],accepted=[];
  const submission=new FrameSubmission((data,w,h,stride,format,ts)=>{
    if(ts===2000)throw Object.assign(new Error('out of order'),{code:10,retryable:false});
    accepted.push(ts);
  },state=>events.push(state));
  const decoder=new FrameDecoder((...frame)=>submission.push(...frame));
  decoder.push(Buffer.concat([packet(1000),packet(2000),packet(3000)]));
  assert.deepEqual(accepted,[1000,3000]);
  assert.deepEqual(events,['recovering','recovered']);
});
test('duplicate error callback and thrown exception report recovery once',()=>{
  const events=[],error={code:11};let clock=0;
  const submission=new FrameSubmission(()=>{submission.reject(error);throw error},state=>events.push(state),()=>clock);
  submission.push();assert.deepEqual(events,['recovering']);
  clock=7999;submission.tick();assert.equal(submission.failed,false);
  clock=8000;submission.tick();submission.tick();submission.push();
  assert.deepEqual(events,['recovering','fatal']);
});
test('authentication, credit, configuration and unknown errors remain fatal',()=>{
  for(const code of [1,2,3,4,5,6,7,8,9,undefined]){
    const events=[];let attempts=0;
    const submission=new FrameSubmission(()=>{attempts++;throw {code,retryable:true}},state=>events.push(state));
    submission.push();submission.push();
    assert.deepEqual(events,['fatal']);assert.equal(attempts,1);
  }
});
test('successful submission clears recovery deadline',()=>{
  let clock=0,drop=true;const events=[];
  const submission=new FrameSubmission(()=>{if(drop)throw {code:11}},state=>events.push(state),()=>clock);
  submission.push();clock=200;drop=false;submission.push();
  clock=9000;submission.tick();assert.equal(submission.failed,false);
  assert.deepEqual(events,['recovering','recovered']);
});
test('long gap restarts processing once, drops frames during restart and then recovers',async()=>{
  let restarted=0,sent=0,release;const events=[];
  const submission=new FrameSubmission(()=>{sent++;if(sent===1)throw {code:11}},state=>events.push(state),()=>0,
    ()=>{restarted++;return new Promise(resolve=>{release=resolve})});
  submission.push();submission.reject({code:11});submission.push();
  assert.equal(sent,1);await Promise.resolve();assert.equal(restarted,1);
  release();await new Promise(resolve=>setImmediate(resolve));
  submission.push();assert.equal(sent,2);assert.equal(submission.failed,false);
  assert.deepEqual(events,['recovering','recovered']);
});
test('persistent gaps have a restart budget, never an endless restart loop',async()=>{
  let restarts=0;
  const events=[],submission=new FrameSubmission(()=>{throw {code:11}},state=>events.push(state),()=>0,async()=>{restarts++});
  for(let i=0;i<5;i++){submission.push();await new Promise(resolve=>setImmediate(resolve));}
  assert.equal(restarts,2);assert.equal(submission.failed,true);
  assert.deepEqual(events,['recovering','fatal']);
});
test('hung or rejected engine restarts produce a single actionable failure',async()=>{
  let clock=0;const events=[];
  const hung=new FrameSubmission(()=>{throw {code:11}},state=>events.push(state),()=>clock,()=>new Promise(()=>{}));
  hung.push();clock=15000;hung.tick();hung.tick();assert.deepEqual(events,['recovering','fatal']);
  const failedEvents=[];
  const failed=new FrameSubmission(()=>{throw {code:11}},state=>failedEvents.push(state),()=>0,async()=>{throw {code:8}});
  failed.push();await new Promise(resolve=>setImmediate(resolve));
  assert.equal(failed.failed,true);assert.deepEqual(failedEvents,['recovering','fatal']);
});
test('processing-status error followed by error callback resets the engine only once',async()=>{
  const events=[];let restarts=0,sent=0;
  const submission=new FrameSubmission(()=>sent++,state=>events.push(state),()=>0,async error=>{
    assert.equal(error.code,8);restarts++;
  });
  submission.reject({code:8});submission.reject({code:8});submission.push();
  assert.equal(sent,0);
  await new Promise(resolve=>setImmediate(resolve));submission.push();
  assert.equal(restarts,1);assert.equal(sent,1);
  assert.deepEqual(events,['recovering','recovered']);
});
test('permanent processing failure retains code 8 and stops after the shared retry budget',async()=>{
  const events=[];
  const submission=new FrameSubmission(()=>{throw {code:8}},(state,error)=>events.push([state,error?.code]),()=>0,async()=>{});
  for(let i=0;i<4;i++){submission.push();await new Promise(resolve=>setImmediate(resolve));}
  assert.equal(submission.failed,true);assert.deepEqual(events,[['recovering',8],['fatal',8]]);
});
