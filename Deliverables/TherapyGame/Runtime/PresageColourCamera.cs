using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace TheLastWatch.Integrations
{
    // One camera owner. Original colour frames feed both the local preview and Presage.
    // Only one frame may be in flight; a slow consumer cannot build a video backlog.
    public sealed class PresageColourCamera : IDisposable
    {
        public readonly string PipeName="MindSpacePresage-"+Guid.NewGuid().ToString("N");
        private WebCamTexture camera;
        private NamedPipeServerStream pipe;
        private Task connection,write;
        private Color32[] colours;
        private byte[] source,frame;
        private readonly byte[] header=new byte[24];
        private readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
        private float lastPreview=-1;
        public byte[] PreviewRgb { get; private set; }
        public int PreviewWidth { get; private set; }
        public int PreviewHeight { get; private set; }
        public bool PreviewChanged { get; private set; }
        public string Error { get; private set; }

        public PresageColourCamera(int index,int width,int height,int fps)
        {
            try
            {
                var devices=WebCamTexture.devices;
                if(index<0||index>=devices.Length)throw new InvalidOperationException("Select an available colour camera.");
                pipe=PresagePrivatePipe.Create(PipeName);
                connection=pipe.WaitForConnectionAsync();
                camera=new WebCamTexture(devices[index].name,width,height,fps);
                camera.Play();
            }
            catch { Dispose();throw; }
        }

        public void Tick(bool showPreview)
        {
            PreviewChanged=false;
            if(camera==null||Error!=null)return;
            if(connection.IsFaulted||connection.IsCanceled||write!=null&&(write.IsFaulted||write.IsCanceled))
            {Error="Camera connection interrupted. Retry the camera test.";return;}
            if(clock.Elapsed.TotalSeconds>15 && camera.width<=16)
            {Error="No camera frames. Check Windows camera access and close other camera apps.";return;}
            if(clock.Elapsed.TotalSeconds>25&&!connection.IsCompleted)
            {Error="Presage did not connect to the camera. Retry the camera test.";return;}
            if(!camera.didUpdateThisFrame||camera.width<=16||camera.height<=16)return;
            if(!connection.IsCompleted||write!=null&&!write.IsCompleted)return;
            int sw=camera.width,sh=camera.height,angle=((camera.videoRotationAngle%360)+360)%360;
            if((long)sw*sh>4096L*2160){Error="Camera resolution is too large. Select a lower-resolution camera.";return;}
            int w=angle%180==0?sw:sh,h=angle%180==0?sh:sw,size=sw*sh*4;
            if(source==null||source.Length!=size){source=new byte[size];frame=new byte[size];colours=new Color32[sw*sh];}
            camera.GetPixels32(colours);
            var pinned=GCHandle.Alloc(colours,GCHandleType.Pinned);
            try{Marshal.Copy(pinned.AddrOfPinnedObject(),source,0,size);}finally{pinned.Free();}
            PresageColourPixels.Normalize(source,frame,sw,sh,angle,camera.videoVerticallyMirrored);
            if(showPreview&&(lastPreview<0||Time.unscaledTime-lastPreview>=1f/12))
            {
                PreviewWidth=Math.Min(320,w);PreviewHeight=Math.Max(1,h*PreviewWidth/w);
                int length=PreviewWidth*PreviewHeight*3;
                if(PreviewRgb==null||PreviewRgb.Length!=length)PreviewRgb=new byte[length];
                PresageColourPixels.Preview(frame,w,h,PreviewRgb,PreviewWidth,PreviewHeight);
                PreviewChanged=true;lastPreview=Time.unscaledTime;
            }
            PutInt(0,w);PutInt(4,h);PutInt(8,w*4);PutInt(12,2); // SDK kRGBA, top-down, unmirrored.
            Buffer.BlockCopy(BitConverter.GetBytes(clock.Elapsed.TotalMilliseconds*1000),0,header,16,8);
            write=WriteFrame();
        }

        private void PutInt(int offset,int value){Buffer.BlockCopy(BitConverter.GetBytes(value),0,header,offset,4);}
        private async Task WriteFrame()
        {
            await pipe.WriteAsync(header,0,header.Length).ConfigureAwait(false);
            await pipe.WriteAsync(frame,0,frame.Length).ConfigureAwait(false);
        }

        public void Dispose()
        {
            pipe?.Dispose();pipe=null;
            if(camera!=null){camera.Stop();UnityEngine.Object.Destroy(camera);camera=null;}
            // Observe asynchronous cancellation on shutdown without blocking Unity's main thread.
            if(write!=null)_=write.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
            if(connection!=null)_=connection.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
            PreviewRgb=null;source=frame=null;colours=null;
        }
    }
}
