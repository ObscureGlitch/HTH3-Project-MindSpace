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
        GameObject rod,bobber,held,previewRoot;
        Transform tip;
        LineRenderer line;
        RenderTexture preview;
        Camera previewCamera;
        public Texture Preview=>preview;
        public KoiFishingViews(Transform parent,Transform eye,KoiPondLibrary library){this.parent=parent;this.eye=eye;this.library=library;}
        static void Destroy(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        static GameObject Child(Transform parent,string name)
        {var go=new GameObject(name){hideFlags=HideFlags.DontSave};go.transform.SetParent(parent,false);return go;}
        Material Material(Color color)
        {var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){hideFlags=HideFlags.DontSave};material.SetColor("_BaseColor",color);owned.Add(material);return material;}
        void MeshPart(Transform root,string name,Mesh mesh,Material material)
        {
            var go=Child(root,name);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.lightProbeUsage=LightProbeUsage.Off;
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
            MeshPart(rod.transform,"Cork grip",Tube(new Vector3(0,-.08f,0),new Vector3(0,.21f,0),.024f),cork);
            MeshPart(rod.transform,"Reel",Tube(new Vector3(-.03f,.17f,-.045f),new Vector3(.05f,.17f,-.045f),.05f),graphite);
            tip=Child(rod.transform,"Line tip").transform;tip.localPosition=new Vector3(0,1.45f,0);
            bobber=Child(parent,"Fishing float (runtime)");
            MeshPart(bobber.transform,"Float red",Tube(Vector3.zero,Vector3.up*.075f,.033f),red);
            MeshPart(bobber.transform,"Float white",Tube(Vector3.up*.075f,Vector3.up*.15f,.024f),white);
            line=Child(parent,"Fishing line (runtime)").AddComponent<LineRenderer>();line.sharedMaterial=white;
            line.useWorldSpace=true;line.positionCount=16;line.startWidth=.003f;line.endWidth=.002f;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
        }
        public void PoseRod(KoiFishingRound round,Vector3 water,float seconds)
        {
            EnsureRod();rod.SetActive(true);bobber.SetActive(true);line.gameObject.SetActive(true);
            if(held!=null)held.SetActive(false);
            float cast=round.State==KoiFishingRound.Phase.Casting?round.CastFraction:1;
            rod.transform.localRotation=Quaternion.Euler(Mathf.Lerp(105,48,cast)+(round.State==KoiFishingRound.Phase.Reeling?Mathf.Sin(seconds*3)*3:0),-9,0);
            Vector3 end=Vector3.Lerp(tip.position,water,cast)+Vector3.up*Mathf.Sin(cast*Mathf.PI)*1.4f;
            if(cast>=1)end.y+=Mathf.Sin(seconds*(round.State==KoiFishingRound.Phase.Bite?18:3))*(round.State==KoiFishingRound.Phase.Bite?.045f:.009f);
            bobber.transform.position=end;
            for(int i=0;i<16;i++){float t=i/15f;line.SetPosition(i,Vector3.Lerp(tip.position,end,t)-Vector3.up*Mathf.Sin(t*Mathf.PI)*.12f);}
        }
        public void HideRod(){if(rod!=null)rod.SetActive(false);if(bobber!=null)bobber.SetActive(false);if(line!=null)line.gameObject.SetActive(false);}
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
            if(held!=null){held.SetActive(false);Destroy(held);held=null;}
            if(fish==null)return;
            held=Model(eye,library,ModelIndex(library,fish.variety),.43f*fish.scale);
            if(held!=null){held.transform.localPosition=new Vector3(.33f,-.28f,.74f);held.transform.localRotation=Quaternion.Euler(-12,90,0);}
        }
        public void ShowHeld(bool show){if(held!=null)held.SetActive(show);}
        public void SelectPreview(KoiCatch fish)
        {
            ClosePreview();if(fish==null)return;
            previewRoot=Child(parent,"Koi inventory preview (runtime)");previewRoot.transform.position=new Vector3(10000,10000,10000);
            var model=Model(previewRoot.transform,library,ModelIndex(library,fish.variety),1.4f,30);if(model==null){ClosePreview();return;}
            model.transform.localRotation=Quaternion.Euler(0,55,0);
            var light=Child(previewRoot.transform,"Preview light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.cullingMask=1<<30;light.transform.localRotation=Quaternion.Euler(30,-35,0);
            previewCamera=Child(previewRoot.transform,"Preview camera").AddComponent<Camera>();previewCamera.enabled=false;previewCamera.cullingMask=1<<30;
            previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=new Color(.11f,.17f,.15f);
            previewCamera.orthographic=true;previewCamera.orthographicSize=.57f;previewCamera.nearClipPlane=.01f;previewCamera.farClipPlane=5;
            previewCamera.transform.localPosition=new Vector3(0,.5f,-2);previewCamera.transform.LookAt(previewRoot.transform.position);
            var data=previewCamera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.volumeLayerMask=0;
            preview=new RenderTexture(512,288,16,RenderTextureFormat.ARGB32){hideFlags=HideFlags.DontSave};preview.Create();previewCamera.targetTexture=preview;previewCamera.aspect=512f/288;
            RenderPipeline.SubmitRenderRequest(previewCamera,new RenderPipeline.StandardRequest{destination=preview});
        }
        public void ClosePreview()
        {if(previewRoot!=null){previewRoot.SetActive(false);Destroy(previewRoot);previewRoot=null;}if(preview!=null){preview.Release();Destroy(preview);preview=null;}previewCamera=null;}
        public void Dispose()
        {
            ClosePreview();foreach(var go in new[]{rod,bobber,held,line!=null?line.gameObject:null})if(go!=null){go.SetActive(false);Destroy(go);}
            foreach(var value in owned)Destroy(value);owned.Clear();rod=bobber=held=null;line=null;
        }
    }
}
