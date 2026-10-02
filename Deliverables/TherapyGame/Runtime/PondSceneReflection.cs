using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastWatch.Environment
{
    /// <summary>Bounded local-geometry mirror, without a second SRP camera or shadow/sky passes.</summary>
    public sealed class PondSceneReflection : IDisposable
    {
        public const int MaxDraws = 96;
        public const int MaxVertices = 120000;
        private sealed class Entry
        {
            public Renderer renderer;
            public Material material;
            public int submesh, vertices;
        }
        private readonly List<Entry> entries = new List<Entry>(MaxDraws);
        private readonly List<Renderer> candidates = new List<Renderer>();
        private readonly Dictionary<Material,Material> materials = new Dictionary<Material,Material>();
        private readonly Plane[] planes = new Plane[6];
        private readonly CommandBuffer commands = new CommandBuffer {name="Pond: bounded local reflections"};
        private readonly Renderer water;
        private readonly Shader shader;
        private readonly System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
        private bool needsRefresh=true;
        private int cachedMask;
        public RenderTexture Texture { get; private set; }
        public Matrix4x4 ViewProjection { get; private set; }
        public int DrawCount { get; private set; }
        public int VertexCount { get; private set; }
        public double CpuMilliseconds { get; private set; }
        public int RefreshCount { get; private set; }

        public PondSceneReflection(Renderer surface)
        {
            water=surface;
            shader=Resources.Load<Shader>("PondSceneReflection");
        }
        public void Invalidate(){needsRefresh=true;}
        private float Priority(Renderer renderer)
        {
            Bounds b=renderer.bounds;
            float distance=(b.ClosestPoint(water.bounds.center)-water.bounds.center).sqrMagnitude;
            return Mathf.Min(8,b.extents.magnitude)/(1+distance*.035f);
        }
        private void Refresh(int cullingMask)
        {
            foreach(var material in materials.Values)Release(material);
            entries.Clear();materials.Clear();candidates.Clear();
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>())
            {
                if(renderer==water||renderer.gameObject.scene!=water.gameObject.scene||
                    !renderer.enabled||renderer.forceRenderingOff||!renderer.gameObject.activeInHierarchy||
                    (cullingMask&(1<<renderer.gameObject.layer))==0||
                    !(renderer is MeshRenderer||renderer is SkinnedMeshRenderer))continue;
                Bounds b=renderer.bounds;
                if(b.max.y<water.bounds.center.y||b.extents.sqrMagnitude<.015f||
                    (b.ClosestPoint(water.bounds.center)-water.bounds.center).sqrMagnitude>55*55)continue;
                candidates.Add(renderer);
            }
            candidates.Sort((a,b)=>Priority(b).CompareTo(Priority(a)));
            int estimatedVertices=0;
            foreach(var renderer in candidates)
            {
                Mesh mesh=renderer is SkinnedMeshRenderer skin?skin.sharedMesh:renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if(mesh==null||mesh.vertexCount>60000)continue;
                Material[] shared=renderer.sharedMaterials;
                int count=Mathf.Min(shared.Length,mesh.subMeshCount);
                int verticesPerDraw=mesh.vertexCount;
                for(int i=0;i<count;i++)
                {
                    Material source=shared[i];
                    if(source==null||source.renderQueue>=3000||entries.Count>=MaxDraws||estimatedVertices+verticesPerDraw>MaxVertices)continue;
                    if(!materials.TryGetValue(source,out Material capture))
                    {
                        capture=new Material(shader) {name="Pond capture: "+source.name,hideFlags=HideFlags.HideAndDontSave};
                        materials.Add(source,capture);
                    }
                    entries.Add(new Entry {renderer=renderer,material=capture,submesh=i,vertices=verticesPerDraw});
                    estimatedVertices+=verticesPerDraw;
                }
                if(entries.Count>=MaxDraws)break;
            }
            needsRefresh=false;cachedMask=cullingMask;RefreshCount++;
        }
        private static void UpdateMaterial(Material source,Material target)
        {
            target.SetColor("_BaseColor",source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):
                source.HasProperty("_Color")?source.GetColor("_Color"):Color.white);
            string textureName=source.HasProperty("_BaseMap")?"_BaseMap":source.HasProperty("_MainTex")?"_MainTex":null;
            if(textureName!=null)
            {
                target.SetTexture("_BaseMap",source.GetTexture(textureName));
                target.SetTextureScale("_BaseMap",source.GetTextureScale(textureName));
                target.SetTextureOffset("_BaseMap",source.GetTextureOffset(textureName));
            }
            target.SetFloat("_Cutoff",source.HasProperty("_AlphaClip")&&source.GetFloat("_AlphaClip")>.5f?
                (source.HasProperty("_Cutoff")?source.GetFloat("_Cutoff"):.5f):0);
        }
        public bool Capture(Camera view,float level)
        {
            if(shader==null||!shader.isSupported||view==null)return false;
            timer.Restart();
            if(Texture==null)
            {
                Texture=new RenderTexture(256,256,16,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear)
                {name="Pond nearby-object reflection",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};
                Texture.Create();
            }
            if(needsRefresh||cachedMask!=view.cullingMask)Refresh(view.cullingMask);
            Matrix4x4 reflect=Matrix4x4.identity;reflect.m11=-1;reflect.m13=2*level;
            Matrix4x4 reflectedView=view.worldToCameraMatrix*reflect;
            // Keep an ordinary frustum: per-fragment height clipping excludes submerged
            // geometry without relying on oblique near/far or per-layer camera culling.
            ViewProjection=view.projectionMatrix*reflectedView;
            GeometryUtility.CalculateFrustumPlanes(ViewProjection,planes);
            commands.Clear();commands.SetRenderTarget(Texture);commands.ClearRenderTarget(true,true,Color.clear);
            commands.SetGlobalMatrix("_PondCaptureVP",GL.GetGPUProjectionMatrix(view.projectionMatrix,true)*reflectedView);
            commands.SetGlobalFloat("_PondCaptureLevel",level);
            Light sun=RenderSettings.sun;
            commands.SetGlobalVector("_PondCaptureSun",sun!=null?-sun.transform.forward:Vector3.up);
            Color light=sun!=null&&sun.enabled?sun.color*sun.intensity:Color.black;
            commands.SetGlobalColor("_PondCaptureLight",light);
            commands.SetGlobalColor("_PondCaptureAmbient",RenderSettings.ambientEquatorColor);
            foreach(var pair in materials)if(pair.Key!=null)UpdateMaterial(pair.Key,pair.Value);
            DrawCount=0;VertexCount=0;
            foreach(var entry in entries)
            {
                Renderer renderer=entry.renderer;
                if(renderer==null||!renderer.enabled||renderer.forceRenderingOff||!renderer.gameObject.activeInHierarchy||
                    (view.cullingMask&(1<<renderer.gameObject.layer))==0||!GeometryUtility.TestPlanesAABB(planes,renderer.bounds))continue;
                commands.DrawRenderer(renderer,entry.material,entry.submesh,0);DrawCount++;VertexCount+=entry.vertices;
            }
            RenderTexture previous=RenderTexture.active;
            try{Graphics.ExecuteCommandBuffer(commands);}
            finally{RenderTexture.active=previous;timer.Stop();CpuMilliseconds=timer.Elapsed.TotalMilliseconds;}
            return true;
        }
        public void Dispose()
        {
            commands.Release();foreach(var material in materials.Values)Release(material);
            materials.Clear();entries.Clear();candidates.Clear();
            if(Texture!=null){Texture.Release();Release(Texture);Texture=null;}
        }
        private static void Release(UnityEngine.Object obj)
        {if(Application.isPlaying)UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj);}
    }
}
