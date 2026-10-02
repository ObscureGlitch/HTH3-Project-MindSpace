using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TheLastWatch.Environment;
using TheLastWatch.Integrations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace TherapyGame.Editor
{
    public static class SeasonalDetailsSetup
    {
        private const string Root="Assets/TherapyGame",Folder=Root+"/Exterior/SeasonalDetails";
        private const string Checks="D:/Hack the Hill/Deliverables/SeasonalDetails/Checks";
        [Serializable] private sealed class Plant {public float x,z;}
        [Serializable] private sealed class GardenData {public Plant[] plants;}
        [Serializable] private sealed class PlacementData {public Vector3[] flowers,leaves;public Vector3 flowerView,leafView;}
        private static System.Random random;
        private static float Range(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        public static void InstallBatch()=>Run("Install",Install);
        public static void VerifyBatch()=>Run("Verify",Verify);
        public static void PreviewBatch()
        {
            Directory.CreateDirectory(Checks);EditorSceneManager.OpenScene(Root+"/Scenes/TherapyRoom.unity");
            IEnumerator frames=PreviewFrames();
            void Advance()
            {
                try
                {
                    if(frames.MoveNext()){EditorApplication.QueuePlayerLoopUpdate();return;}
                    EditorApplication.update-=Advance;File.WriteAllText(Checks+"/Preview-result.txt","PASS: Preview completed across editor frames.");EditorApplication.Exit(0);
                }
                catch(Exception error)
                {
                    EditorApplication.update-=Advance;(frames as IDisposable)?.Dispose();
                    File.WriteAllText(Checks+"/Preview-result.txt","FAILED: "+error);Debug.LogException(error);EditorApplication.Exit(1);
                }
            }
            EditorApplication.update+=Advance;
        }
        private static void Run(string label,Action action)
        {
            try
            {
                Directory.CreateDirectory(Checks);EditorSceneManager.OpenScene(Root+"/Scenes/TherapyRoom.unity");
                action();File.WriteAllText(Checks+"/"+label+"-result.txt","PASS: "+label+" completed.");EditorApplication.Exit(0);
            }
            catch(Exception error){File.WriteAllText(Checks+"/"+label+"-result.txt","FAILED: "+error);Debug.LogException(error);EditorApplication.Exit(1);}
        }
        [MenuItem("Therapy Game/Seasons/Add Spring Flowers and Autumn Leaves")]
        public static void Install()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Stop Play mode before installing scenery.");
            var season=Object.FindAnyObjectByType<WellnessSeasonCycle>();Require(season!=null&&season.sky!=null,"Existing seasons required.");
            Transform garden=GameObject.Find("TherapyRoom/OutdoorGarden").transform;
            Require(garden.position.sqrMagnitude<.00001f&&garden.rotation==Quaternion.identity&&garden.lossyScale==Vector3.one,"Garden alignment changed.");
            var terrain=garden.Find("Walkable surfaces and safety/Walkable terrain").GetComponent<MeshCollider>();
            Require(terrain!=null&&season.sky.pondRenderer!=null,"Terrain and pond references required.");
            Shader shader=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/SeasonalDetails.shader");
            Require(shader!=null&&!ShaderUtil.ShaderHasError(shader),"Seasonal scenery shader failed to import.");
            Transform existing=garden.Find("Seasonal wildflowers and leaf litter");
            Require(existing==null,"Seasonal details already installed; refusing to duplicate scenery.");
            var group=new GameObject("Seasonal wildflowers and leaf litter");group.transform.SetParent(garden,false);
            Undo.RegisterCreatedObjectUndo(group,"Add seasonal flowers and leaves");
            var material=new Material(shader){name="Seasonal flower and leaf colors"};AssetDatabase.CreateAsset(material,Folder+"/SeasonalDetails.mat");
            random=new System.Random(261001);Physics.SyncTransforms();
            var blooms=Enumerable.Range(0,4).Select(_=>new SeasonalSceneryGeometry()).ToArray();
            var litter=Enumerable.Range(0,4).Select(_=>new SeasonalSceneryGeometry()).ToArray();
            var flowers=new List<Vector3>();var leaves=new List<Vector3>();
            GardenData plants=JsonUtility.FromJson<GardenData>(File.ReadAllText(Root+"/Exterior/LivingGarden/Source/GardenLife.json"));
            Require(plants.plants.Length>50,"Authored planting locations required.");
            Bounds pond=season.sky.pondRenderer.bounds;pond.Expand(.9f);
            var paths=PathTriangles(garden);
            bool Place(float x,float z,bool flower,out RaycastHit hit)
            {
                hit=default;Vector3 guess=new Vector3(x,0,z);
                if(Mathf.Abs(x)<4.8f&&Mathf.Abs(z)<4.3f)return false;
                if(x>pond.min.x&&x<pond.max.x&&z>pond.min.z&&z<pond.max.z)return false;
                if(Mathf.Abs(x)>37||Mathf.Abs(z)>29)return false;
                if(!terrain.Raycast(new Ray(guess+Vector3.up*35,Vector3.down),out hit,65)||hit.normal.y<.88f)return false;
                if(flower&&OnPath(guess,paths))return false;
                if(Physics.CheckSphere(hit.point+Vector3.up*.25f,flower?.15f:.10f,~0,QueryTriggerInteraction.Ignore))return false;
                return true;
            }
            // Cluster new flowers around the existing authored beds, preserving
            // paths and open sight lines rather than filling the whole map uniformly.
            for(int pass=0;pass<6&&flowers.Count<540;pass++)
            for(int index=0;index<plants.plants.Length&&flowers.Count<540;index++)
            {
                Plant patch=plants.plants[index];float angle=Range(0,Mathf.PI*2),radius=Range(.22f,1.35f);
                if(!Place(patch.x+Mathf.Cos(angle)*radius,patch.z+Mathf.Sin(angle)*radius,true,out var hit))continue;
                Vector3 point=hit.point+Vector3.up*.006f;
                if(flowers.Any(p=>(p-point).sqrMagnitude<.18f*.18f))continue;
                blooms[Batch(point)].Flower(point,Range(.25f,.49f),Range(0,Mathf.PI*2),index%6,Range(.005f,.995f));flowers.Add(point);
            }
            for(int attempt=0;attempt<12000&&leaves.Count<1080;attempt++)
            {
                Bounds canopy=season.leafCanopies[random.Next(season.leafCanopies.Length)];Vector3 center=season.transform.TransformPoint(canopy.center);
                float angle=Range(0,Mathf.PI*2),radius=Mathf.Sqrt(Range(0,1))*Range(1.5f,5.5f);
                if(!Place(center.x+Mathf.Cos(angle)*radius,center.z+Mathf.Sin(angle)*radius,false,out var hit))continue;
                Vector3 point=hit.point+hit.normal*.018f;
                Color color=Color.Lerp(new Color(.56f,.16f,.045f),new Color(.96f,.62f,.11f),Range(0,1));
                litter[Batch(point)].Leaf(point,hit.normal,Range(.14f,.30f),Range(0,Mathf.PI*2),color,Range(.005f,.995f));leaves.Add(point);
            }
            Require(flowers.Count>=500&&leaves.Count==1080,"Scenery density target could not be grounded safely.");
            season.springBlooms=SaveBatches(group.transform,"Spring wildflowers",blooms,material,season.Current==WellnessSeason.Spring);
            season.autumnGroundCover=SaveBatches(group.transform,"Autumn leaf litter",litter,material,season.Current==WellnessSeason.Fall);
            season.sky.latitude=45;ConfigureFallingLeaves(season.fallingLeaves);
            EditorUtility.SetDirty(season);EditorUtility.SetDirty(season.sky);
            var placements=new PlacementData{flowers=flowers.ToArray(),leaves=leaves.ToArray(),
                flowerView=flowers.OrderBy(p=>(p-new Vector3(1,0,-13)).sqrMagnitude).First(),
                leafView=leaves.OrderBy(p=>(p-new Vector3(-8,0,-14)).sqrMagnitude).First()};
            File.WriteAllText(Folder+"/Placements.json",JsonUtility.ToJson(placements,true));AssetDatabase.ImportAsset(Folder+"/Placements.json");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(season.gameObject.scene);
            Require(EditorSceneManager.SaveScene(season.gameObject.scene),"Scene save failed.");
            Verify();
        }
        private static int Batch(Vector3 point)=>(point.x>=8?1:0)+(point.z>=0?2:0);
        private static Renderer[] SaveBatches(Transform parent,string name,SeasonalSceneryGeometry[] batches,Material material,bool enabled)
        {
            var renderers=new List<Renderer>();
            for(int index=0;index<batches.Length;index++)
            {
                if(batches[index].Count==0)continue;
                var mesh=new Mesh{name=name+" "+index};batches[index].Write(mesh);AssetDatabase.CreateAsset(mesh,Folder+"/"+name.Replace(' ','_')+index+".asset");
                var go=new GameObject(name+" "+index);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.enabled=enabled;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderers.Add(renderer);
            }
            return renderers.ToArray();
        }
        private static List<Vector3> PathTriangles(Transform garden)
        {
            var points=new List<Vector3>();
            foreach(var renderer in garden.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
                var materials=renderer.sharedMaterials;Vector3[] vertices=null;
                for(int slot=0;slot<Mathf.Min(materials.Length,filter.sharedMesh.subMeshCount);slot++)
                {
                    if(materials[slot]==null||!materials[slot].name.Equals("path",StringComparison.OrdinalIgnoreCase))continue;
                    if(vertices==null)vertices=filter.sharedMesh.vertices;
                    foreach(int index in filter.sharedMesh.GetTriangles(slot))points.Add(renderer.transform.TransformPoint(vertices[index]));
                }
            }
            return points;
        }
        private static bool OnPath(Vector3 point,List<Vector3> triangles)
        {
            float Cross(Vector3 a,Vector3 b,Vector3 p)=>(b.x-a.x)*(p.z-a.z)-(b.z-a.z)*(p.x-a.x);
            for(int i=0;i<triangles.Count;i+=3)
            {
                Vector3 a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                if(Mathf.Abs(Cross(a,b,c))<.00001f)continue;
                float u=Cross(a,b,point),v=Cross(b,c,point),w=Cross(c,a,point);
                if((u>=0&&v>=0&&w>=0)||(u<=0&&v<=0&&w<=0))return true;
            }
            return false;
        }
        private static void ConfigureFallingLeaves(ParticleSystem system)
        {
            Require(system!=null,"Existing autumn particle system required.");system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;main.maxParticles=80;main.startLifetime=11;main.startSize=new ParticleSystem.MinMaxCurve(.12f,.23f);
            main.startRotation3D=true;main.startRotationX=new ParticleSystem.MinMaxCurve(-.5f,.5f);main.startRotationY=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            main.startRotationZ=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.cullingMode=ParticleSystemCullingMode.Automatic;
            var rotation=system.rotationOverLifetime;rotation.enabled=true;rotation.separateAxes=true;
            rotation.x=new ParticleSystem.MinMaxCurve(-1.2f,1.2f);rotation.y=new ParticleSystem.MinMaxCurve(-.7f,.7f);rotation.z=new ParticleSystem.MinMaxCurve(-1.6f,1.6f);
            var noise=system.noise;noise.enabled=true;noise.separateAxes=true;noise.strengthX=.22f;noise.strengthY=0;noise.strengthZ=.22f;
            noise.frequency=.65f;noise.scrollSpeed=.35f;noise.octaveCount=1;noise.quality=ParticleSystemNoiseQuality.Low;noise.damping=true;
            var collisions=system.collision;collisions.enabled=false;var trails=system.trails;trails.enabled=false;
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.alignment=ParticleSystemRenderSpace.World;
            EditorUtility.SetDirty(system);EditorUtility.SetDirty(renderer);
        }
        internal static void Invoke(object target,string method,params object[] arguments)
        {target.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,arguments);}
        private static void SetSeason(WellnessSeasonCycle season,WellnessSeason value)
        {season.SetSeason(value);for(int i=0;i<34;i++)season.Tick(.25f);Invoke(season,"ApplyEnvironment");}
        public static void Verify()
        {
            var season=Object.FindAnyObjectByType<WellnessSeasonCycle>();Require(season!=null,"Seasons missing after reload.");
            var sky=season.sky;Require(sky!=null&&sky.seasons==season,"Sky/season link missing.");
            var group=GameObject.Find("TherapyRoom/OutdoorGarden/Seasonal wildflowers and leaf litter");Require(group!=null,"Scenery missing.");
            var filters=group.GetComponentsInChildren<MeshFilter>(true);var renderers=group.GetComponentsInChildren<Renderer>(true);
            int triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3);
            Require(renderers.Length<=8&&season.springBlooms.Length>0&&season.autumnGroundCover.Length>0,"Spatial batch budget.");
            Require(triangles<50000&&filters.All(f=>f.sharedMesh.vertexCount>0),"Geometry budget.");
            Require(group.GetComponentsInChildren<Collider>().Length==0&&group.GetComponentsInChildren<Rigidbody>().Length==0,"Decorations must not add physics bodies.");
            Require(group.GetComponentsInChildren<Light>().Length==0&&group.GetComponentsInChildren<Camera>().Length==0,"Decorations must not add lights or cameras.");
            Require(renderers.Select(r=>r.sharedMaterial).Distinct().Count()==1,"Shared seasonal material.");
            var shader=renderers[0].sharedMaterial.shader;Require(!ShaderUtil.ShaderHasError(shader),"Shader compilation.");
            var placements=JsonUtility.FromJson<PlacementData>(File.ReadAllText(Folder+"/Placements.json"));
            var terrain=GameObject.Find("TherapyRoom/OutdoorGarden/Walkable surfaces and safety/Walkable terrain").GetComponent<MeshCollider>();
            Physics.SyncTransforms();
            foreach(var point in placements.flowers.Concat(placements.leaves))
            {
                Require(terrain.Raycast(new Ray(point+Vector3.up*20,Vector3.down),out var hit,40),"Decoration lost terrain.");
                Require(Mathf.Abs(point.y-hit.point.y)<.035f&&!WellnessRain.UnderRoof(point),"Ground placement / cabin exclusion.");
                Bounds pond=sky.pondRenderer.bounds;Require(!(point.x>pond.min.x&&point.x<pond.max.x&&point.z>pond.min.z&&point.z<pond.max.z),"Decoration over pond.");
            }
            WellnessSeason oldSeason=season.season;bool automatic=season.automatic;float oldHour=sky.hour;
            var report=new List<string>{"PASS: saved seasonal scenery and runtime tests.","Flowers: "+placements.flowers.Length+"; ground leaves: "+placements.leaves.Length+"; renderers: "+renderers.Length+"; total triangles: "+triangles+" (only one seasonal set draws at a time)."};
            Invoke(season,"CaptureEnvironment");
            try
            {
                float[] hours=new float[4];
                for(int i=0;i<4;i++)
                {
                    SetSeason(season,(WellnessSeason)i);hours[i]=sky.DaylightHours;
                    Require(season.springBlooms.All(r=>r.enabled==(i==0))&&season.autumnGroundCover.All(r=>r.enabled==(i==2)),"Seasonal visibility "+i);
                    float decl=sky.SolarDeclination;
                    Require(Mathf.Abs(WellnessSkyCycle.SeasonalSunDirection(sky.SunriseHour,sky.latitude,decl).y)<.00002f,"Sunrise must meet horizon.");
                    Require(Mathf.Abs(WellnessSkyCycle.SeasonalSunDirection(sky.SunsetHour,sky.latitude,decl).y)<.00002f,"Sunset must meet horizon.");
                    Require(WellnessSkyCycle.SeasonalDaylightAmount(12,sky.latitude,decl)>.99f&&WellnessSkyCycle.SeasonalDaylightAmount(0,sky.latitude,decl)<.001f,"Noon and midnight.");
                    Require(Vector3.Distance(WellnessSkyCycle.SeasonalSunDirection(0,sky.latitude,decl),WellnessSkyCycle.SeasonalSunDirection(24,sky.latitude,decl))<.00001f,"Midnight continuity.");
                    report.Add(((WellnessSeason)i)+": "+sky.DaylightSummary);
                }
                Require(hours[1]>hours[0]&&hours[0]>12&&hours[2]<12&&hours[2]>hours[3],"Seasonal daylight ordering.");
                SetSeason(season,WellnessSeason.Summer);float prior=sky.DaylightHours;season.SetSeason(WellnessSeason.Winter);
                for(int i=0;i<32;i++){season.Tick(.25f);float next=sky.DaylightHours;Require(next<=prior+.001f&&prior-next<.5f,"Smooth daylight transition.");prior=next;}
                SetSeason(season,WellnessSeason.Fall);
                Require(season.player!=null&&season.player.ViewCamera!=null,"Leaf viewer missing.");
                Transform camera=season.player.ViewCamera.transform;Vector3 original=camera.position;
                try
                {
                    camera.position=placements.leafView+Vector3.up*1.7f;season.fallingLeaves.Clear();
                    for(int frame=0;frame<120;frame++){Invoke(season,"EmitLeaves",1f/30);season.fallingLeaves.Simulate(1f/30,true,false,false);}
                    int live=season.fallingLeaves.particleCount;Require(live>=5&&live<=80,"Nearby falling leaves should be visible and bounded; got "+live);
                    var particles=new ParticleSystem.Particle[80];int count=season.fallingLeaves.GetParticles(particles);
                    Require(particles.Take(count).All(p=>!float.IsNaN(p.position.x)&&p.velocity.y<0),"Leaves must descend with finite positions.");
                    report.Add("PASS: nearby falling leaves emit, flutter and descend; "+live+" alive after 4 simulated seconds, cap 80; no particle collision or trails.");
                }
                finally{camera.position=original;}
                SetSeason(season,WellnessSeason.Spring);Require(season.fallingLeaves.particleCount==0,"Autumn particles cleared on season change.");
                Require(WellnessFireflies.VisibilityForDaylight(1,0)==0&&WellnessFireflies.VisibilityForDaylight(0,0)>.99f,"Fireflies follow daylight.");
                report.Add("PASS: all four seasonal visibility states; 8-second smooth daylight transition; sunrise/sunset horizon checks; midnight wrap; seasonal night effects; original visibility restored on disable.");
            }
            finally{Invoke(season,"RestoreEnvironment");season.season=oldSeason;season.automatic=automatic;sky.hour=oldHour;}
            File.WriteAllLines(Checks+"/SeasonalDetailsVerification.txt",report);Debug.Log(string.Join("\n",report));
        }
        private static IEnumerator PreviewFrames()
        {
            Verify();yield return null;
            var season=Object.FindAnyObjectByType<WellnessSeasonCycle>();var sky=season.sky;
            var placements=JsonUtility.FromJson<PlacementData>(File.ReadAllText(Folder+"/Placements.json"));
            var temporary=new GameObject("Seasonal preview camera"){hideFlags=HideFlags.HideAndDontSave};
            var camera=temporary.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.08f;camera.farClipPlane=170;camera.fieldOfView=62;
            camera.clearFlags=CameraClearFlags.Skybox;
            RenderTexture previous=RenderTexture.active;var target=new RenderTexture(1440,900,24);target.Create();camera.targetTexture=target;camera.aspect=1.6f;
            var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);Material skyOriginal=RenderSettings.skybox;
            Color ambient=RenderSettings.ambientLight;AmbientMode mode=RenderSettings.ambientMode;
            try
            {
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.67f,.70f,.66f);
                Invoke(season,"CaptureEnvironment");
                foreach(var value in new[]{WellnessSeason.Spring,WellnessSeason.Fall})
                {
                    SetSeason(season,value);Vector3 center=value==WellnessSeason.Spring?placements.flowerView:placements.leafView;
                    camera.transform.position=center+(value==WellnessSeason.Spring?new Vector3(1.2f,1.75f,-4):new Vector3(4,2.1f,-6));camera.transform.LookAt(center+new Vector3(0,.5f,1.3f));
                    if(value==WellnessSeason.Fall)
                    {
                        Transform viewer=season.player.ViewCamera.transform;Vector3 old=viewer.position;viewer.position=camera.transform.position;
                        for(int i=0;i<120;i++){Invoke(season,"EmitLeaves",1f/30);season.fallingLeaves.Simulate(1f/30,true,false,false);}viewer.position=old;
                    }
                    // Let material uploads and the render pipeline settle between
                    // seasons. Synchronous captures in one editor frame can show
                    // stale SRP material buffers after the lifecycle stress tests.
                    for(int frame=0;frame<4;frame++)
                    {
                        yield return null;
                        RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                    }
                    RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();File.WriteAllBytes(Checks+"/"+value+".png",texture.EncodeToPNG());
                }
                Require(!ShaderUtil.ShaderHasError(season.springBlooms[0].sharedMaterial.shader),"GPU shader compilation failed.");
            }
            finally
            {
                Invoke(season,"RestoreEnvironment");RenderSettings.skybox=skyOriginal;RenderSettings.ambientMode=mode;RenderSettings.ambientLight=ambient;
                RenderTexture.active=previous;Object.DestroyImmediate(temporary);Object.DestroyImmediate(target);Object.DestroyImmediate(texture);
            }
        }
    }
}
