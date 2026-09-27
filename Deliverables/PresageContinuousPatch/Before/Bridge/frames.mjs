// Private local-pipe protocol: 24-byte LE header, then top-down RGBA pixels.
// A reusable payload buffer avoids repeated multi-megabyte concatenation on partial reads.
export class FrameDecoder {
  constructor(onFrame) { this.onFrame=onFrame;this.header=Buffer.alloc(24);this.headerAt=0;this.payload=null;this.at=0;this.lastTimestamp=-1; }
  push(chunk) {
    let offset=0;
    while(offset<chunk.length){
      if(this.headerAt<24){
        const count=Math.min(24-this.headerAt,chunk.length-offset);
        chunk.copy(this.header,this.headerAt,offset,offset+count);this.headerAt+=count;offset+=count;
        if(this.headerAt<24)return;
        this.width=this.header.readUInt32LE(0);this.height=this.header.readUInt32LE(4);
        this.stride=this.header.readUInt32LE(8);this.format=this.header.readUInt32LE(12);this.timestamp=this.header.readDoubleLE(16);
        if(this.width<1||this.height<1||this.width>4096||this.height>4096||this.width*this.height>4096*2160||
          this.stride!==this.width*4||this.format!==2||!Number.isFinite(this.timestamp)||this.timestamp<=this.lastTimestamp)
          throw new Error('Invalid local camera frame.');
        const size=this.stride*this.height;
        if(this.payload?.length!==size)this.payload=Buffer.allocUnsafe(size);
        this.at=0;
      }
      const count=Math.min(this.payload.length-this.at,chunk.length-offset);
      chunk.copy(this.payload,this.at,offset,offset+count);this.at+=count;offset+=count;
      if(this.at===this.payload.length){
        // Advance the decoder before the consumer runs. A rejected SDK frame must
        // never leave the next packet looking like the previous frame's payload.
        this.lastTimestamp=this.timestamp;this.headerAt=0;
        this.onFrame(this.payload,this.width,this.height,this.stride,this.format,this.timestamp);
      }
    }
  }
}

// These are frame-local timestamp errors, not loss of the camera connection.
// The SDK reports them through BOTH onError and sendFrame's thrown exception.
export const isFrameTimingError = code => code === 10 || code === 11;

export class FrameSubmission {
  constructor(send, notify, now = () => performance.now(), restart = null) {
    this.send=send;this.notify=notify;this.now=now;this.recoveringSince=null;this.failed=false;
    this.restart=restart;this.restarting=false;this.restarts=[];
  }
  reject(error) {
    if(this.failed)return;
    if(!isFrameTimingError(error?.code)) {
      this.failed=true;this.notify('fatal',error);return;
    }
    if(this.recoveringSince===null) {
      this.recoveringSince=this.now();this.notify('recovering',error);
    }
    // A long gap is measured against the last accepted frame. Simply dropping
    // it leaves every later timestamp outside the window too: restart processing,
    // not camera capture, and keep the real capture timestamps unchanged.
    if(error.code===11 && this.restart && !this.restarting) {
      this.restarts=this.restarts.filter(at=>this.now()-at<60000);
      if(this.restarts.length>=2) {
        this.failed=true;
        this.notify('fatal',{code:11,message:'Camera frames keep stalling. Close other video apps or lower game graphics, then retry.'});return;
      }
      this.restarts.push(this.now());this.restarting=true;
      // Defer past sendFrame/native callbacks; never stop the SDK reentrantly.
      Promise.resolve().then(()=>this.failed?undefined:this.restart()).then(()=>{
        this.restarting=false;
      },failure=>{
        this.restarting=false;
        if(!this.failed){this.failed=true;this.notify('fatal',failure);}
      });
    }
    this.tick();
  }
  tick() {
    if(!this.failed && this.recoveringSince!==null && this.now()-this.recoveringSince>=(this.restart?15000:8000)) {
      this.failed=true;
      this.notify('fatal',{code:11,message:'Camera frame timing did not recover. Close other video apps, then retry the camera test.'});
    }
  }
  push(...frame) {
    this.tick();if(this.failed||this.restarting)return;
    try { this.send(...frame); }
    catch(error) { this.reject(error);return; }
    if(this.recoveringSince!==null) {
      this.recoveringSince=null;this.notify('recovered');
    }
  }
}
