using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Reflection;
using TheLastWatch.Environment;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class PondWaterChecks
    {
        const string Root="Assets/TherapyGame/";
        static PondWaterChecks(){EditorApplication.delayCall+=Once;}
        static void Once()
        {
            string request=Root+"PondWaterRequest.txt";
            if(!File.Exists(request)||File.ReadAllText(request).Trim()!="verify-once")return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Once;return;}
            File.WriteAllText(request,"checking");
            try { Run();File.WriteAllText(request,"passed"); }
            catch(Exception e){File.WriteAllText(Root+"Documentation/PondWaterCheck.txt",e.ToString());File.WriteAllText(request,"failed");Debug.LogException(e);}
        }
        static void Require(bool value,string message){if(!value)throw new Exception("Pond water: "+message);}
        [MenuItem("Therapy Game/Validate Pond Water")]
        public static void Run()
        {
            var wave=new PondWaveField(13.4f,11.4f);int n=PondWaveField.Resolution;
            for(int z=1;z<n-1;z++)for(int x=1;x<n-1;x++)wave.Wet[z*n+x]=true;
            wave.Impulse(.5f,.5f,-.6f);
            float peak=0,remote=0,early=0,late=0;
            for(int frame=0;frame<1200;frame++)
            {
                wave.Step();float energy=0;
                for(int i=0;i<wave.Height.Length;i++)
                {
                    float h=wave.Height[i];Require(!float.IsNaN(h)&&!float.IsInfinity(h)&&Math.Abs(h)<=.07001f,"unstable height");
                    energy+=h*h;peak=Mathf.Max(peak,Mathf.Abs(h));
                }
                remote=Mathf.Max(remote,Mathf.Abs(wave.Height[(n/2)*n+n/2+8]));
                if(frame==40)early=energy;if(frame==1199)late=energy;
            }
            Require(peak>.001f&&remote>.00001f,"impact did not propagate");Require(late<early*.1f,"waves did not settle");
            for(int i=0;i<n;i++)Require(wave.Height[i]==0&&wave.Height[i*n]==0,"wave crossed dry boundary");
            var storm=new PondWaveField(.5f,.5f);Array.Fill(storm.Wet,true);
            for(int frame=0;frame<600;frame++){storm.Impulse(.5f,.5f,-.7f);storm.Step();}
            foreach(float h in storm.Height)Require(!float.IsNaN(h)&&Math.Abs(h)<=.07001f,"CFL stability failed");
            var balanced=new PondWaveField(13.4f,11.4f);Array.Fill(balanced.Wet,true);
            for(int frame=0;frame<300;frame++){balanced.Impulse(.5f,.5f,-.5f);balanced.Step();}
            double sum=0;foreach(float h in balanced.Height)sum+=h;
            Require(Math.Abs(sum/balanced.Height.Length)<.0001,"rain changed mean water level");
            Shader shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"Exterior/Shaders/QuietPond.shader");Require(shader!=null,"missing shader");
            var material=new Material(shader);bool async=ShaderUtil.allowAsyncCompilation;
            try {ShaderUtil.allowAsyncCompilation=false;ShaderUtil.CompilePass(material,0,true);Require(!ShaderUtil.ShaderHasError(shader),"shader compile failure");}
            finally {ShaderUtil.allowAsyncCompilation=async;UnityEngine.Object.DestroyImmediate(material);}
            File.WriteAllText(Root+"Documentation/PondWaterCheck.txt","PASS: runtime C# compiled; pond shader pass compiled.\nPASS: impulse propagation, damped settling, dry boundary, bounded heights and small-grid CFL stress.\nPeak displacement: "+peak+" m; remote wave: "+remote+" m; final/early energy: "+late/early+".\nRuntime appearance and reflection performance still require visual validation.\n");
            Capture();Debug.Log("POND_WATER_CHECKS_PASSED");
        }
        static void Call(WellnessPondWater water,string name,params object[] args)=>typeof(WellnessPondWater).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(water,args);
        static void Capture()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var cycle=UnityEngine.Object.FindFirstObjectByType<WellnessSkyCycle>();
            if(cycle==null||cycle.pondRenderer==null)return;
            Renderer original=cycle.pondRenderer;bool hidden=original.forceRenderingOff;
            GameObject clone=null,cameraObject=null;RenderTexture target=null;Texture2D image=null;
            RenderTexture previous=RenderTexture.active;
            try
            {
                clone=new GameObject("Temporary pond validation") {hideFlags=HideFlags.HideAndDontSave};
                clone.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);clone.transform.localScale=original.transform.lossyScale;
                clone.AddComponent<MeshFilter>().sharedMesh=original.GetComponent<MeshFilter>().sharedMesh;
                var renderer=clone.AddComponent<MeshRenderer>();renderer.sharedMaterial=original.sharedMaterial;
                var water=clone.AddComponent<WellnessPondWater>();Call(water,"InitializeSurface");Require(water.Ready,"surface initialization failed");
                Vector3 center=renderer.bounds.center;Require(water.Contains(center),"authored pond center is dry");
                water.Disturb(center,-.65f);
                var field=(PondWaveField)typeof(WellnessPondWater).GetField("field",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(water);
                for(int i=0;i<38;i++)field.Step();
                water.Disturb(center+new Vector3(1.1f,0,.7f),-.55f);for(int i=0;i<15;i++)field.Step();Call(water,"Upload");
                original.forceRenderingOff=true;
                cameraObject=new GameObject("Temporary water view") {hideFlags=HideFlags.HideAndDontSave};
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=58;camera.nearClipPlane=.04f;camera.farClipPlane=1500;
                camera.transform.position=center+new Vector3(-2,1.1f,-3.5f);camera.transform.LookAt(center+new Vector3(0,0,.8f));
                target=new RenderTexture(960,600,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;camera.aspect=1.6f;
                Call(water,"CaptureReflection",camera);
                var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
                Require(properties.GetFloat("_HasPondReflection")>.5f,"scene reflection was not rendered");
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest {destination=target});
                RenderTexture.active=target;image=new Texture2D(960,600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,600),0,0);image.Apply();
                string output="D:/Hack the Hill/Deliverables/PondWaterValidation/PondPreview.png";Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllBytes(output,image.EncodeToPNG());
                File.AppendAllText(Root+"Documentation/PondWaterCheck.txt","PASS: authored mesh initialization, wet mask, two overlapping impacts, scene reflection render request and offscreen camera capture.\nPreview: "+output+"\n");
            }
            finally
            {
                original.forceRenderingOff=hidden;RenderTexture.active=previous;
                if(clone!=null)UnityEngine.Object.DestroyImmediate(clone);
                if(cameraObject!=null)UnityEngine.Object.DestroyImmediate(cameraObject);
                if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
                if(image!=null)UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
