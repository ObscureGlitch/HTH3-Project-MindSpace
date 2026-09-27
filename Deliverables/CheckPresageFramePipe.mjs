import { createConnection } from 'node:net';
import { FrameDecoder } from './TherapyGame/Integrations/PresageBridge/frames.mjs';
import assert from 'node:assert/strict';
let count=0;
const decoder=new FrameDecoder((pixels,width,height,stride,format)=>{
  assert.deepEqual([width,height,stride,format],[1280,720,5120,2]);
  assert.equal(pixels.length,1280*720*4);
  for(let i=0;i<pixels.length;i++)if(pixels[i] !== (i+count)%251)assert.fail('Corrupted frame channel at '+i);
  count++;
});
const socket=createConnection('\\\\.\\pipe\\'+process.argv[2]);
socket.on('data',chunk=>decoder.push(chunk));
socket.on('end',()=>{assert.equal(count,90);console.log('PRIVATE_COLOUR_PIPE: PASS')});
socket.on('error',()=>process.exit(1));
