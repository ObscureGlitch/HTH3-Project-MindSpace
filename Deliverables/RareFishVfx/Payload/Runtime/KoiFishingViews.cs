using System;
using System.Collections.Generic;
using TheLastWatch.Environment;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TheLastWatch.UI
{
    public sealed class KoiFishingViews : IDisposable
    {
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Transform parent,eye;
        readonly KoiPondLibrary library;
        GameObject rod,bobber,held,previewRoot,forearm,caughtModel,previewModel;
        Vector3 catchWater;
        float previewClock,previewRenderClock;
        Transform tip;
        LineRenderer line,ripple;
        RenderTexture preview;
        Camera previewCamera;
        KoiRarityVfx catchVfx,heldVfx,previewVfx;
        float heldClock;
        public Texture Preview=>preview;
        public GameObject CaughtModel=>caughtModel;
        public float PreviewYaw=>previewModel!=null?KoiFishingCatchMotion.PreviewYaw(previewClock):0;
        public int PreviewRenderCount {get;private set;}
        public KoiFishingViews(Transform parent,Transform eye,KoiPondLibrary library){this.parent=parent;this.eye=eye;this.library=library;}
        static void Destroy(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        static GameObject Child(Transform parent,string name)
        {var go=new GameObject(name){hideFlags=HideFlags.DontSave};go.transform.SetParent(parent,false);return go;}
        Material Material(Color color)
        {var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){hideFlags=HideFlags.DontSave};material.SetColor("_BaseColor",color);owned.Add(material);return material;}
        GameObject MeshPart(Transform root,string name,Mesh mesh,Material material)
        {
            var go=Child(root,name);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.lightProbeUsage=LightProbeUsage.Off;
            return go;
        }
        Mesh Tube(Vector3 a,Vector3 b,float radius)
        {
            var mesh=new Mesh{name="Fishing view-only tube",hideFlags=HideFlags.DontSave};owned.Add(mesh);
            Vector3 axis=(b-a).normalized,right=Vector3.Cross(axis,Vector3.forward).normalized;if(right.sqrMagnitude<.1f)right=Vector3.right;
            Vector3 across=Vector3.Cross(axis,right);var vertices=new Vector3[18];var triangles=new List<int>();
            for(int ring=0;ring<2;ring++)for(int i=0;i<8;i++){float angle=i*Mathf.PI/4;vertices[ring*8+i]=(ring==0?a:b)+(right*Mathf.Cos(angle)+across*Mathf.Sin(angle))*radius*(ring==0?1:.66f);}
            vertices[16]=a;vertices[17]=b;
            for(int i=0;i<8;i++){int next=(i+1)%8;triangles.AddRange(new[]{i,next,i+8,next,next+8,i+8,16,next,i,17,i+8,next+8});}
            mesh.vertices=vertices;mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        void EnsureRod()
        {
            if(rod!=null)return;
            rod=Child(eye,"Fishing rod (view only)");rod.transform.localPosition=new Vector3(.32f,-.34f,.46f);
            var graphite=Material(new Color(.15f,.20f,.17f));var cork=Material(new Color(.64f,.43f,.22f));var red=Material(new Color(.91f,.31f,.21f));var white=Material(new Color(.96f,.95f,.85f));
            MeshPart(rod.transform,"Tapered rod",Tube(Vector3.zero,new Vector3(0,1.45f,0),.013f),graphite);
            MeshPart(rod.transform,"Cork grip",Tube(new Vector3(0,-.16f,0),new Vector3(0,.29f,0),.024f),cork);
            MeshPart(rod.transform,"Reel seat",Tube(new Vector3(0,.026f,-.018f),new Vector3(0,.026f,-.07f),.012f),graphite);
            MeshPart(rod.transform,"Reel",Tube(new Vector3(-.03f,.026f,-.07f),new Vector3(.025f,.026f,-.07f),.033f),graphite);
            // Lightweight first-person grip: the hand follows the rod while the
            // sleeve stretches from a camera-local shoulder to the moving wrist.
            var sleeve=Material(new Color(.38f,.47f,.35f));var skin=Material(new Color(.76f,.62f,.48f));
            forearm=MeshPart(eye,"Casting sleeve (view only)",Tube(Vector3.zero,Vector3.up,.056f),sleeve);
            MeshPart(rod.transform,"Casting wrist",Tube(new Vector3(.051f,.035f,-.012f),new Vector3(.046f,.092f,-.010f),.027f),skin);
            var palm=MeshPart(rod.transform,"Casting palm",Tube(new Vector3(.043f,.08f,0),new Vector3(.043f,.208f,0),.035f),skin);palm.transform.localScale=new Vector3(1,1,.68f);
            for(int i=0;i<4;i++)
            {
                float y=.108f+i*.026f;
                MeshPart(rod.transform,"Grip finger "+i,Tube(new Vector3(.058f,y,.012f),new Vector3(.030f,y,.040f),.009f),skin);
                MeshPart(rod.transform,"Curled finger "+i,Tube(new Vector3(.030f,y,.040f),new Vector3(-.007f,y,.038f),.009f),skin);
                MeshPart(rod.transform,"Curled fingertip "+i,Tube(new Vector3(-.007f,y,.038f),new Vector3(-.027f,y,.004f),.009f),skin);
            }
            // Right-hand thumb points toward the tip; fingers wrap the far side
            // of the handle. The wrist enters from below, not from the fingertips.
            MeshPart(rod.transform,"Grip thumb",Tube(new Vector3(.045f,.152f,-.027f),new Vector3(.014f,.235f,-.028f),.013f),skin);
            MeshPart(rod.transform,"Thumb tip",Tube(new Vector3(.014f,.235f,-.028f),new Vector3(.004f,.240f,-.009f),.010f),skin);
            tip=Child(rod.transform,"Line tip").transform;tip.localPosition=new Vector3(0,1.45f,0);
            bobber=Child(parent,"Fishing float (runtime)");
            MeshPart(bobber.transform,"Float red",Tube(Vector3.zero,Vector3.up*.075f,.033f),red);
            MeshPart(bobber.transform,"Float white",Tube(Vector3.up*.075f,Vector3.up*.15f,.024f),white);
            line=Child(parent,"Fishing line (runtime)").AddComponent<LineRenderer>();line.sharedMaterial=white;
            line.useWorldSpace=true;line.positionCount=16;line.startWidth=.003f;line.endWidth=.002f;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            ripple=Child(parent,"Float landing ripple (runtime)").AddComponent<LineRenderer>();ripple.sharedMaterial=white;
            ripple.useWorldSpace=true;ripple.positionCount=32;ripple.loop=true;ripple.startWidth=ripple.endWidth=.005f;
            ripple.shadowCastingMode=ShadowCastingMode.Off;ripple.receiveShadows=false;
        }
        public void PoseRod(KoiFishingRound round,Vector3 water,float seconds)
        {
            EnsureRod();rod.SetActive(true);forearm.SetActive(true);bobber.SetActive(true);line.gameObject.SetActive(true);
            if(held!=null)held.SetActive(false);
            float cast=round.State==KoiFishingRound.Phase.Casting?round.CastFraction:1;
            var pose=KoiFishingCastMotion.Sample(cast);rod.transform.localPosition=pose.Position;rod.transform.localRotation=pose.Rotation;
            if(round.State==KoiFishingRound.Phase.Reeling)rod.transform.localRotation*=Quaternion.Euler(Mathf.Sin(seconds*3)*3,0,0);
            var release=KoiFishingCastMotion.Sample(KoiFishingCastMotion.Release);
            Vector3 launch=eye.TransformPoint(release.Position+release.Rotation*(Vector3.up*1.45f));
            Vector3 end=KoiFishingCastMotion.FloatPosition(tip.position,launch,water,pose);
            if(cast>=1)end.y+=Mathf.Sin(seconds*(round.State==KoiFishingRound.Phase.Bite?18:3))*(round.State==KoiFishingRound.Phase.Bite?.045f:.009f);
            bobber.transform.position=end;
            for(int i=0;i<16;i++){float t=i/15f;line.SetPosition(i,Vector3.Lerp(tip.position,end,t)-Vector3.up*Mathf.Sin(t*Mathf.PI)*.12f);}
            Vector3 shoulder=eye.TransformPoint(new Vector3(.43f,-.69f,.12f)),wrist=rod.transform.TransformPoint(new Vector3(.051f,.045f,-.012f));
            Vector3 arm=wrist-shoulder;forearm.transform.position=shoulder;forearm.transform.rotation=Quaternion.FromToRotation(Vector3.up,arm.normalized);forearm.transform.localScale=new Vector3(1,arm.magnitude,1);
            float landingAge=round.ElapsedSeconds-KoiFishingCastMotion.Duration;
            bool showRipple=round.State!=KoiFishingRound.Phase.Casting&&landingAge>=0&&landingAge<.8f;
            ripple.gameObject.SetActive(showRipple);
            if(showRipple)
            {
                float life=landingAge/.8f,radius=Mathf.Lerp(.045f,.30f,life);ripple.startWidth=ripple.endWidth=Mathf.Lerp(.006f,.001f,life);
                for(int i=0;i<32;i++){float angle=i*Mathf.PI*2/32;ripple.SetPosition(i,water+new Vector3(Mathf.Cos(angle)*radius,-.034f,Mathf.Sin(angle)*radius));}
            }
        }
        public void HideRod(){if(rod!=null)rod.SetActive(false);if(forearm!=null)forearm.SetActive(false);if(bobber!=null)bobber.SetActive(false);if(line!=null)line.gameObject.SetActive(false);if(ripple!=null)ripple.gameObject.SetActive(false);}
        public void BeginCatch(KoiCatch fish,Vector3 water)
        {
            HideCatch();EnsureRod();catchWater=water;
            caughtModel=Model(parent,library,ModelIndex(library,fish.variety),.43f*fish.scale);
            if(caughtModel!=null)catchVfx=KoiRarityVfx.Create(parent,fish,.43f*fish.scale,KoiRarityVfx.Presentation.Catch);
            if(caughtModel!=null)PoseCatch(0);
        }
        public void PoseCatch(float seconds)
        {
            if(caughtModel==null)return;
            float progress=Mathf.Clamp01(seconds/KoiFishingCatchMotion.Duration);
            caughtModel.transform.position=KoiFishingCatchMotion.Position(catchWater,eye.TransformPoint(new Vector3(.20f,.07f,1.12f)),progress);
            caughtModel.transform.rotation=eye.rotation*KoiFishingCatchMotion.Rotation(progress)*Quaternion.Euler(0,0,Mathf.Sin(seconds*5)*2*(1-progress));
            catchVfx?.Pose(caughtModel.transform.position,eye.rotation,seconds);
            foreach(Transform part in caughtModel.transform)
            {
                if(part.name=="tail")part.localRotation=Quaternion.Euler(0,Mathf.Sin(seconds*8)*9*(1-progress*.8f),0);
                else if(part.name=="leftFin"||part.name=="rightFin")part.localRotation=Quaternion.Euler(0,0,Mathf.Sin(seconds*6)*5);
            }
            // Lift the line together with the model, then hold the catch in view.
            var pose=KoiFishingCastMotion.Sample(1);rod.SetActive(true);forearm.SetActive(true);line.gameObject.SetActive(true);bobber.SetActive(false);ripple.gameObject.SetActive(false);
            rod.transform.localPosition=pose.Position;rod.transform.localRotation=pose.Rotation*Quaternion.Euler(-12*progress,0,0);
            // Give a rare catch's showcase space beside the rod as it arrives.
            if(catchVfx!=null)rod.transform.localPosition+=Vector3.right*(.21f*KoiFishingCatchMotion.Ease(Mathf.InverseLerp(KoiFishingCatchMotion.LiftEnd,1,progress)));
            Vector3 shoulder=eye.TransformPoint(new Vector3(.43f,-.69f,.12f)),wrist=rod.transform.TransformPoint(new Vector3(.051f,.045f,-.012f));var arm=wrist-shoulder;
            forearm.transform.position=shoulder;forearm.transform.rotation=Quaternion.FromToRotation(Vector3.up,arm.normalized);forearm.transform.localScale=new Vector3(1,arm.magnitude,1);
            Vector3 mouth=caughtModel.transform.TransformPoint(new Vector3(0,0,.18f/caughtModel.transform.localScale.x));
            for(int i=0;i<16;i++){float t=i/15f;line.SetPosition(i,Vector3.Lerp(tip.position,mouth,t)-Vector3.up*Mathf.Sin(t*Mathf.PI)*.045f);}
        }
        public void HideCatch(){catchVfx?.Dispose();catchVfx=null;if(caughtModel!=null){caughtModel.SetActive(false);Destroy(caughtModel);caughtModel=null;}}
        public static int ModelIndex(KoiPondLibrary library,string variety)=>library==null||library.varieties==null?-1:Array.FindIndex(library.varieties,v=>v.name==variety);
        public static GameObject Model(Transform parent,KoiPondLibrary library,int index,float length,int layer=0)
        {
            if(library==null||index<0||index>=library.varieties.Length)return null;
            var root=Child(parent,"Caught "+library.varieties[index].name);root.layer=layer;
            Bounds bounds=default;bool first=true;
            foreach(var part in library.varieties[index].parts)
            {
                var go=Child(root.transform,part.name);go.layer=layer;go.transform.localPosition=part.pivot;
                go.AddComponent<MeshFilter>().sharedMesh=part.mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=library.material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.lightProbeUsage=LightProbeUsage.Off;
                Bounds box=part.mesh.bounds;box.center+=part.pivot;if(first){bounds=box;first=false;}else bounds.Encapsulate(box);
            }
            foreach(Transform part in root.transform)part.localPosition-=bounds.center;
            root.transform.localScale=Vector3.one*(length/Mathf.Max(.001f,Mathf.Max(bounds.size.x,bounds.size.z)));return root;
        }
        public void Equip(KoiCatch fish)
        {
            heldVfx?.Dispose();heldVfx=null;heldClock=0;
            if(held!=null){held.SetActive(false);Destroy(held);held=null;}
            if(fish==null)return;
            held=Model(eye,library,ModelIndex(library,fish.variety),.43f*fish.scale);
            if(held!=null)
            {
                held.transform.localPosition=new Vector3(.33f,-.28f,.74f);held.transform.localRotation=Quaternion.Euler(-12,90,0);
                heldVfx=KoiRarityVfx.Create(eye,fish,.34f*fish.scale,KoiRarityVfx.Presentation.Held);heldVfx?.Pose(held.transform.position,eye.rotation,0);
            }
        }
        public void ShowHeld(bool show){if(held!=null)held.SetActive(show);heldVfx?.SetVisible(show);}
        public void TickHeld(float seconds)
        {
            if(heldVfx==null||held==null||!held.activeInHierarchy||!float.IsFinite(seconds)||seconds<=0)return;
            heldClock+=Mathf.Min(seconds,.1f);heldVfx.Pose(held.transform.position,eye.rotation,heldClock);
        }
        public void SelectPreview(KoiCatch fish)
        {
            ClosePreview();if(fish==null)return;
            previewRoot=Child(parent,"Koi inventory preview (runtime)");previewRoot.transform.position=new Vector3(10000,10000,10000);
            previewModel=Model(previewRoot.transform,library,ModelIndex(library,fish.variety),1.4f,30);if(previewModel==null){ClosePreview();return;}
            previewClock=previewRenderClock=0;previewModel.transform.localRotation=Quaternion.Euler(0,55,0);
            var light=Child(previewRoot.transform,"Preview light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.cullingMask=1<<30;light.transform.localRotation=Quaternion.Euler(30,-35,0);
            previewCamera=Child(previewRoot.transform,"Preview camera").AddComponent<Camera>();previewCamera.enabled=false;previewCamera.cullingMask=1<<30;
            previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=Color.clear;
            previewCamera.orthographic=true;previewCamera.orthographicSize=.57f;previewCamera.nearClipPlane=.01f;previewCamera.farClipPlane=5;
            previewCamera.transform.localPosition=new Vector3(0,.5f,-2);previewCamera.transform.LookAt(previewRoot.transform.position);
            var data=previewCamera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.volumeLayerMask=0;
            previewVfx=KoiRarityVfx.Create(previewRoot.transform,fish,1.4f,KoiRarityVfx.Presentation.Collection,30);
            if(previewVfx!=null){previewCamera.orthographicSize=previewVfx.Rank==5?.98f:.88f;previewVfx.Pose(previewModel.transform.position,previewCamera.transform.rotation,0);}
            preview=new RenderTexture(512,288,16,RenderTextureFormat.ARGB32){hideFlags=HideFlags.DontSave};preview.Create();previewCamera.targetTexture=preview;previewCamera.aspect=512f/288;
            RenderPreview();
        }
        void RenderPreview(){RenderPipeline.SubmitRenderRequest(previewCamera,new RenderPipeline.StandardRequest{destination=preview});PreviewRenderCount++;}
        public void TickPreview(float seconds)
        {
            if(previewModel==null||previewCamera==null||!float.IsFinite(seconds)||seconds<=0)return;
            float dt=Mathf.Min(seconds,.1f);previewClock+=dt;previewRenderClock+=dt;
            previewModel.transform.localRotation=Quaternion.Euler(0,KoiFishingCatchMotion.PreviewYaw(previewClock),0);
            previewVfx?.Pose(previewModel.transform.position,previewCamera.transform.rotation,previewClock);
            // One small selected-model render, capped at 30 Hz, only while open.
            if(previewRenderClock>=1f/30){previewRenderClock%=1f/30;RenderPreview();}
        }
        public void ClosePreview()
        {previewVfx?.Dispose();previewVfx=null;if(previewRoot!=null){previewRoot.SetActive(false);Destroy(previewRoot);previewRoot=null;}if(preview!=null){preview.Release();Destroy(preview);preview=null;}previewCamera=null;previewModel=null;previewClock=previewRenderClock=0;PreviewRenderCount=0;}
        public void Dispose()
        {
            ClosePreview();HideCatch();heldVfx?.Dispose();heldVfx=null;foreach(var go in new[]{rod,bobber,held,forearm,line!=null?line.gameObject:null,ripple!=null?ripple.gameObject:null})if(go!=null){go.SetActive(false);Destroy(go);}
            foreach(var value in owned)Destroy(value);owned.Clear();rod=bobber=held=forearm=null;line=ripple=null;
        }
    }
}
