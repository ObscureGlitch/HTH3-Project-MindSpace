import { test } from 'node:test';
import assert from 'node:assert/strict';
import { SignalCache, SignalDelivery } from './signals.mjs';
const pulse = timestamp => ({cardio:{pulseRate:[{value:72,confidence:90,stable:true,timestamp}]}});
const breath = timestamp => ({breathing:{rate:[{value:12,confidence:85,stable:true,timestamp}]}});

test('setup delivers changes immediately and unchanged readings at up to 10 Hz',()=>{
  const delivery=new SignalDelivery(),cache=new SignalCache();
  assert.equal(delivery.shouldEmit(cache.snapshot(0),0,true),true);
  cache.ingest(pulse(1),1);
  assert.equal(delivery.shouldEmit(cache.snapshot(1),1,true),true);
  cache.ingest(breath(2),2);
  assert.equal(delivery.shouldEmit(cache.snapshot(2),2,true),true);
  assert.equal(delivery.shouldEmit(cache.snapshot(50),50,true),false);
  assert.equal(delivery.shouldEmit(cache.snapshot(102),102,true),true);
  cache.ingest({cardio:{pulseRate:[{value:72,confidence:20,stable:true,timestamp:3}]}},103);
  assert.equal(delivery.shouldEmit(cache.snapshot(103),103,true),true,'Quality loss must bypass throttling.');
});
test('gameplay retains low delivery overhead and expiration is reported immediately',()=>{
  const delivery=new SignalDelivery(),cache=new SignalCache();cache.ingest(pulse(1),0);
  assert.equal(delivery.shouldEmit(cache.snapshot(0),0,false),true);
  assert.equal(delivery.shouldEmit(cache.snapshot(100),100,false),false);
  assert.equal(delivery.shouldEmit(cache.snapshot(500),500,false),true);
  assert.equal(delivery.shouldEmit(cache.snapshot(8000),8000,false),true);
  assert.equal(delivery.shouldEmit(cache.snapshot(8001),8001,false),true);
});
test('interleaved pulse, breathing and waveform packets retain both rates before throttling',()=>{
  const cache=new SignalCache();
  cache.ingest(pulse(1),0);cache.ingest(breath(2),100);cache.ingest({cardio:{arterialPressureTrace:[{value:1}]}},200);
  const snapshot=cache.snapshot(500);
  assert.equal(snapshot.hasPulse,true);assert.equal(snapshot.hasBreathing,true);
  assert.equal(snapshot.pulseConfidence,90);assert.equal(snapshot.breathingStable,true);
});
test('unrelated packets and repeated timestamps cannot extend freshness',()=>{
  const cache=new SignalCache();cache.ingest(pulse(1),0);cache.ingest(pulse(1),7900);cache.ingest(breath(2),8000);
  assert.equal(cache.snapshot(8001).hasPulse,false);assert.equal(cache.snapshot(8001).hasBreathing,true);
});
test('quality regressions replace good readings and framing loss clears all values',()=>{
  const cache=new SignalCache();cache.ingest(pulse(1),0);
  cache.ingest({cardio:{pulseRate:[{value:73,confidence:10,stable:false,timestamp:2}]}},100);
  assert.equal(cache.snapshot(200).pulseStable,false);assert.equal(cache.snapshot(200).pulseConfidence,10);
  cache.clear();assert.equal(cache.snapshot(201).hasPulse,false);
});
