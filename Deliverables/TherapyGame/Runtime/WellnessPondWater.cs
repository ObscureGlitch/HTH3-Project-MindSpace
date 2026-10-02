using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TheLastWatch.Integrations;

namespace TheLastWatch.Environment
{
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class WellnessPondWater : MonoBehaviour
    {
        public WellnessRain rain;
        public Renderer surface;
        [Range(0, 1)] public float reflectionStrength = .68f;
        public bool sceneReflections = true;
        private PondWaveField field;
        private MeshFilter filter;
        private Mesh original, dense;
        private Texture2D waves;
        private Color[] pixels;
        private MaterialPropertyBlock properties;
        private Bounds bounds;
        private float level, accumulator, reflectionTimer;
        private bool waveTextureDirty, hasReflectionPose;
        private Vector3 reflectionPosition;
        private Quaternion reflectionRotation;
        private readonly Plane[] viewPlanes = new Plane[6];
        private static readonly int WaterTime = Shader.PropertyToID("_WaterTime");
        private static readonly int HasReflection = Shader.PropertyToID("_HasPondReflection");
        private readonly List<Vector4> pending = new List<Vector4>(256);
        private readonly Collider[] bodies = new Collider[32];
        private readonly HashSet<Rigidbody> visited = new HashSet<Rigidbody>();
        private Vector3 lastFoot;
        private bool hasFoot;
        private PondSceneReflection sceneMirror;
        private int cachedBodyCount;
        private float bodyQueryTimer;
        private ParticleSystem splashes;
        public int ImpactCount { get; private set; }
        public int SimulationSteps { get; private set; }
        public bool Ready => field != null;
        public float WaterLevel => level;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            InitializeSurface();
        }
        private void InitializeSurface()
        {
            if (surface == null) surface = GetComponent<Renderer>();
            filter = surface != null ? surface.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null) return;
            original = filter.sharedMesh; bounds = surface.bounds; level = bounds.center.y;
            field = new PondWaveField(bounds.size.x, bounds.size.z);
            int n = PondWaveField.Resolution;
            var vertices = original.vertices; var triangles = original.triangles;
            for (int i=0;i<vertices.Length;i++) vertices[i]=surface.transform.TransformPoint(vertices[i]);
            for (int z=1;z<n-1;z++) for(int x=1;x<n-1;x++)
            {
                var p=new Vector2(bounds.min.x+bounds.size.x*x/(n-1),bounds.min.z+bounds.size.z*z/(n-1));
                for(int t=0;t<triangles.Length;t+=3)
                    if (Inside(p,vertices[triangles[t]],vertices[triangles[t+1]],vertices[triangles[t+2]]))
                    { field.Wet[z*n+x]=true;break; }
            }
            // Retain the authored outline, but tessellate its triangles for real displacement.
            var meshVertices=new List<Vector3>();var indices=new List<int>();
            for(int t=0;t<triangles.Length;t+=3)
                Subdivide(vertices[triangles[t]],vertices[triangles[t+1]],vertices[triangles[t+2]],0,meshVertices,indices);
            dense=new Mesh { name="Pond displaced surface (runtime)",indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.DontSave };
            dense.SetVertices(meshVertices);dense.SetTriangles(indices,0);dense.RecalculateNormals();dense.RecalculateBounds();
            var expanded=dense.bounds;expanded.Expand(.2f);dense.bounds=expanded;filter.sharedMesh=dense;
            waves=new Texture2D(n,n,TextureFormat.RGBAHalf,false,true) {name="Pond wave field",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.DontSave};
            pixels=new Color[n*n];properties=new MaterialPropertyBlock();Upload();
            reflectionTimer=0;hasReflectionPose=false;
            if(rain!=null)rain.pond=this;
        }
        private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        private static bool Inside(Vector2 p,Vector3 a,Vector3 b,Vector3 c)
        {
            Vector2 aa=new Vector2(a.x,a.z),bb=new Vector2(b.x,b.z),cc=new Vector2(c.x,c.z);
            float area=Cross(bb-aa,cc-aa);if(Mathf.Abs(area)<.000001f)return false;
            float u=Cross(bb-p,cc-p)/area,v=Cross(cc-p,aa-p)/area;
            return u>=0&&v>=0&&u+v<=1;
        }
        private void Subdivide(Vector3 a,Vector3 b,Vector3 c,int depth,List<Vector3> vertices,List<int> indices)
        {
            float abLength=(a-b).sqrMagnitude,bcLength=(b-c).sqrMagnitude,caLength=(c-a).sqrMagnitude;
            if(depth<12&&Mathf.Max(abLength,Mathf.Max(bcLength,caLength))>.36f)
            {
                // Longest-edge bisection avoids over-tessellating the narrow radial fan.
                if(abLength>=bcLength&&abLength>=caLength)
                {Vector3 mid=(a+b)*.5f;Subdivide(a,mid,c,depth+1,vertices,indices);Subdivide(mid,b,c,depth+1,vertices,indices);}
                else if(bcLength>=caLength)
                {Vector3 mid=(b+c)*.5f;Subdivide(a,b,mid,depth+1,vertices,indices);Subdivide(a,mid,c,depth+1,vertices,indices);}
                else
                {Vector3 mid=(c+a)*.5f;Subdivide(a,b,mid,depth+1,vertices,indices);Subdivide(mid,b,c,depth+1,vertices,indices);}
                return;
            }
            int i=vertices.Count;
            vertices.Add(surface.transform.InverseTransformPoint(a));vertices.Add(surface.transform.InverseTransformPoint(b));vertices.Add(surface.transform.InverseTransformPoint(c));
            indices.Add(i);indices.Add(i+1);indices.Add(i+2);
        }
        private Vector2 UV(Vector3 p)=>new Vector2((p.x-bounds.min.x)/bounds.size.x,(p.z-bounds.min.z)/bounds.size.z);
        public bool Contains(Vector3 position)
        {
            if(field==null)return false;Vector2 uv=UV(position);
            if(uv.x<=0||uv.x>=1||uv.y<=0||uv.y>=1)return false;
            int n=PondWaveField.Resolution;
            return field.Wet[Mathf.RoundToInt(uv.y*(n-1))*n+Mathf.RoundToInt(uv.x*(n-1))];
        }
        public void QueueDrop(Vector3 position,float flightTime,float strength)
        {
            if(Contains(position)&&pending.Count<256)pending.Add(new Vector4(position.x,position.z,Time.unscaledTime+flightTime,strength));
        }
        public void Disturb(Vector3 position,float strength)
        {
            if(!Contains(position))return;Vector2 uv=UV(position);field.Impulse(uv.x,uv.y,strength);ImpactCount++;
        }
        private void LateUpdate()
        {
            if(field==null)return;
            for(int i=pending.Count-1;i>=0;i--) if(Time.unscaledTime>=pending[i].z)
            {Vector4 p=pending[i];Vector3 point=new Vector3(p.x,level,p.y);Disturb(point,p.w);Splash(point);pending.RemoveAt(i);}
            // Rain particles use unscaled time as well: impact timing stays in sync with them.
            if(field.IsActive)
            {
                accumulator+=Mathf.Min(Time.unscaledDeltaTime,2*PondWaveField.StepSeconds);
                int steps=0;
                while(accumulator>=PondWaveField.StepSeconds&&steps++<2)
                {field.Step();SimulationSteps++;waveTextureDirty=true;accumulator-=PondWaveField.StepSeconds;}
            }
            else accumulator=0;
            if(rain!=null&&rain.player!=null)
            {
                Vector3 foot=rain.player.transform.position;
                float travel=hasFoot?Vector2.Distance(new Vector2(foot.x,foot.z),new Vector2(lastFoot.x,lastFoot.z)):0;
                if(Contains(foot)&&Mathf.Abs(foot.y-level)<1.3f&&travel>.08f&&travel<1)
                    Disturb(foot,-.48f);
                if(!hasFoot||travel>.08f){lastFoot=foot;hasFoot=true;}
            }
            Camera view=rain!=null&&rain.player!=null?rain.player.ViewCamera:Camera.main;
            bool visible=VisibleFrom(view);
            // Physics keeps running off-screen; only visible water needs a GPU upload.
            if(visible)
            {
                if(waveTextureDirty){Upload();waveTextureDirty=false;}
                surface.GetPropertyBlock(properties);
                properties.SetFloat(WaterTime,Time.unscaledTime);
                properties.SetFloat("_ReflectionStrength",reflectionStrength);
                if(!sceneReflections)properties.SetFloat(HasReflection,0);
                surface.SetPropertyBlock(properties);
            }
            reflectionTimer-=Time.unscaledDeltaTime;
            if(sceneReflections&&visible&&reflectionTimer<=0)
            {
                bool moving=!hasReflectionPose||(view.transform.position-reflectionPosition).sqrMagnitude>.0001f||
                    Quaternion.Angle(view.transform.rotation,reflectionRotation)>.1f;
                // Water normals still animate every frame. The reflected scene can refresh less often.
                reflectionTimer=moving?1f/12f:1f/3f;
                CaptureReflection(view);
            }
        }
        private bool VisibleFrom(Camera view)
        {
            if(view==null||surface==null||!surface.enabled||surface.forceRenderingOff||
                (view.cullingMask&(1<<surface.gameObject.layer))==0)return false;
            GeometryUtility.CalculateFrustumPlanes(view,viewPlanes);
            return GeometryUtility.TestPlanesAABB(viewPlanes,surface.bounds);
        }
        private void Splash(Vector3 point)
        {
            if(rain==null||rain.drops==null)return;
            if(splashes==null)
            {
                var go=new GameObject("Pond impact droplets") {hideFlags=HideFlags.DontSave};
                go.transform.SetParent(transform,false);splashes=go.AddComponent<ParticleSystem>();
                WellnessRain.ConfigureParticles(splashes,rain.drops.GetComponent<ParticleSystemRenderer>().sharedMaterial);
                var main=splashes.main;main.maxParticles=128;main.gravityModifier=.65f;
                var renderer=splashes.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Billboard;
                splashes.Play();
            }
            for(int j=0;j<3;j++)
            {
                float angle=(ImpactCount*2.39996f+j*2.0944f);
                var emit=new ParticleSystem.EmitParams {position=point+Vector3.up*.012f,
                    velocity=new Vector3(Mathf.Cos(angle)*.16f,.65f+j*.08f,Mathf.Sin(angle)*.16f),
                    startLifetime=.23f,startSize=.018f+j*.005f,startColor=new Color(.78f,.88f,.94f,.5f)};
                splashes.Emit(emit,1);
            }
        }
        private void Upload()
        {
            int n=PondWaveField.Resolution;float dx=bounds.size.x/(n-1),dz=bounds.size.z/(n-1);
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                int i=z*n+x;float h=field.Height[i];
                float left=x>0&&field.Wet[i-1]?field.Height[i-1]:h,right=x<n-1&&field.Wet[i+1]?field.Height[i+1]:h;
                float down=z>0&&field.Wet[i-n]?field.Height[i-n]:h,up=z<n-1&&field.Wet[i+n]?field.Height[i+n]:h;
                pixels[i]=new Color(h,(right-left)/(2*dx),(up-down)/(2*dz),field.Wet[i]?1:0);
            }
            waves.SetPixels(pixels);waves.Apply(false,false);
            surface.GetPropertyBlock(properties);properties.SetTexture("_WaveField",waves);
            properties.SetVector("_WaveBounds",new Vector4(bounds.min.x,bounds.min.z,1/bounds.size.x,1/bounds.size.z));
            properties.SetFloat("_HasWaveField",1);properties.SetFloat("_WaterTime",Time.unscaledTime);
            properties.SetFloat("_WaterLevel",level);
            properties.SetFloat("_ReflectionStrength",reflectionStrength);surface.SetPropertyBlock(properties);
        }
        private void FixedUpdate()
        {
            if(field==null)return;
            // Approximate displaced volume for dynamic props; the player keeps its walking controller.
            bodyQueryTimer-=Time.fixedDeltaTime;
            if(bodyQueryTimer<=0)
            {
                bodyQueryTimer=.2f;
                cachedBodyCount=Physics.OverlapBoxNonAlloc(new Vector3(bounds.center.x,level-.35f,bounds.center.z),new Vector3(bounds.extents.x,.5f,bounds.extents.z),bodies,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            }
            visited.Clear();
            for(int i=0;i<cachedBodyCount;i++)
            {
                Collider col=bodies[i];if(col==null)continue;Rigidbody body=col.attachedRigidbody;
                if(body==null||body.isKinematic||!visited.Add(body)||!Contains(body.worldCenterOfMass))continue;
                Bounds b=col.bounds;Vector2 uv=UV(body.worldCenterOfMass);int n=PondWaveField.Resolution;
                float water=level+field.Height[Mathf.RoundToInt(uv.y*(n-1))*n+Mathf.RoundToInt(uv.x*(n-1))];
                float fraction=Mathf.Clamp01((water-b.min.y)/Mathf.Max(.01f,b.size.y));
                if(fraction<=0)continue;
                float volume=b.size.x*b.size.y*b.size.z*fraction;
                float acceleration=Mathf.Min(30,1000*volume/body.mass*Physics.gravity.magnitude);
                body.AddForce(Vector3.up*acceleration-body.linearVelocity*(fraction*3),ForceMode.Acceleration);
                body.AddTorque(-body.angularVelocity*fraction*2,ForceMode.Acceleration);
                if(body.linearVelocity.sqrMagnitude>.04f)Disturb(body.worldCenterOfMass,-Mathf.Min(.25f,body.linearVelocity.magnitude*.02f));
            }
        }
        private void CaptureReflection(Camera view)
        {
            if(!VisibleFrom(view)||view.transform.position.y<=level+.05f)return;
            if(sceneMirror==null)sceneMirror=new PondSceneReflection(surface);
            bool captured=sceneMirror.Capture(view,level);
            surface.GetPropertyBlock(properties);
            properties.SetFloat(HasReflection,captured?1:0);
            if(captured)
            {
                properties.SetTexture("_PondReflection",sceneMirror.Texture);
                properties.SetMatrix("_PondReflectionVP",sceneMirror.ViewProjection);
                reflectionPosition=view.transform.position;reflectionRotation=view.transform.rotation;hasReflectionPose=true;
            }
            surface.SetPropertyBlock(properties);
        }
        private void OnDisable()
        {
            if(rain!=null&&rain.pond==this)rain.pond=null;
            pending.Clear();hasFoot=false;accumulator=0;waveTextureDirty=false;hasReflectionPose=false;
            if(filter!=null&&filter.sharedMesh==dense)filter.sharedMesh=original;
            if(surface!=null&&properties!=null)
            {surface.GetPropertyBlock(properties);properties.SetFloat("_HasWaveField",0);properties.SetFloat("_HasPondReflection",0);surface.SetPropertyBlock(properties);}
            sceneMirror?.Dispose();sceneMirror=null;cachedBodyCount=0;bodyQueryTimer=0;
            if(splashes!=null)Release(splashes.gameObject);

            if(waves!=null)Release(waves);if(dense!=null)Release(dense);field=null;
        }
        private static void Release(Object obj){if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
    }
}
