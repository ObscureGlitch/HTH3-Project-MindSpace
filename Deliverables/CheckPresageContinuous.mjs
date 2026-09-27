import {createServer} from 'node:net';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {once} from 'node:events';
import assert from 'node:assert/strict';
import {fileURLToPath} from 'node:url';
const name='MindSpacePresage-'+randomUUID().replaceAll('-','');
const server=createServer();server.listen('\\\\.\\pipe\\'+name);await once(server,'listening');
const connected=once(server,'connection');
const child=spawn(process.execPath,['--experimental-loader',new URL('./PresageMockLoader.mjs',import.meta.url).href,
 fileURLToPath(new URL('./TherapyGame/Integrations/PresageBridge/bridge.mjs',import.meta.url)),'--frame-pipe',name],
 {windowsHide:true,env:{...process.env,TLW_PRESAGE_API_KEY:'test-key-not-a-credential'},stdio:['pipe','pipe','pipe']});
const messages=[];let text='';child.stdout.on('data',data=>{
 text+=data;const lines=text.split('\n');text=lines.pop();for(const line of lines)if(line)messages.push(JSON.parse(line));
});
child.stderr.resume();const exited=once(child,'exit');
const deadline=setTimeout(()=>child.kill(),10000);let socket;
try {
 [socket]=await Promise.race([connected,exited.then(()=>{throw new Error('Bridge exited before connection')})]);
 socket.on('error',()=>{});
 for(let i=0;i<40;i++){
  const packet=Buffer.alloc(32);packet.writeUInt32LE(2,0);packet.writeUInt32LE(1,4);packet.writeUInt32LE(8,8);packet.writeUInt32LE(2,12);packet.writeDoubleLE((i+1)*33333,16);
  await new Promise((resolve,reject)=>socket.write(packet,error=>error?reject(error):resolve()));
  await new Promise(resolve=>setTimeout(resolve,35));
 }
 child.stdin.end('stop\n');const [exitCode]=await exited;assert.equal(exitCode,0);
 assert.equal(messages.filter(m=>m.type==='error').length,0,JSON.stringify(messages));
 assert.equal(messages.filter(m=>m.type==='recovering'&&m.errorCode===8).length,1);
 const recovery=messages.findIndex(m=>m.type==='recovered');assert.ok(recovery>=0);
 assert.ok(messages.slice(recovery+1).some(m=>m.type==='validation'&&m.validationCode===0));
 assert.ok(messages.some(m=>m.type==='validation'&&m.validationCode===7));
 assert.ok(messages.filter(m=>m.type==='frame').length>=2);
 console.log('CONTINUOUS_CAMERA_BRIDGE: PASS (invalid -> valid -> processing failure -> fresh checks, same pipe)');
}finally{clearTimeout(deadline);socket?.destroy();server.close();if(child.exitCode===null)child.kill();}
