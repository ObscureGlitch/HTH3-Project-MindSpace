using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheLastWatch.Environment;
using TheLastWatch.Integrations;
using TheLastWatch.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyWeatherSetup
    {
        private const string Root="Assets/TherapyGame",Folder=Root+"/Weather";
        private const string Request=Root+"/WeatherCycleRequest.txt",Report=Folder+"/WeatherCycleCheck.txt";
        static TherapyWeatherSetup() => EditorApplication.delayCall+=ImportOnce;
        private static void ImportOnce()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="install-weather-cycle-once")return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Weather setup deferred. Stop Play/baking, then choose Therapy Game > Install Weather and Sky Cycle.");return;}
            File.WriteAllText(Request,"installing-once");
            try {Install();}
            catch(Exception e){Directory.CreateDirectory(Folder);File.WriteAllText(Report,e.ToString());File.WriteAllText(Request,"failed");Debug.LogException(e);}
        }
        [MenuItem("Therapy Game/Install Weather and Sky Cycle")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new InvalidOperationException("Stop Play mode and baking first.");
            Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded)throw new InvalidOperationException("Keep the existing TherapyRoom scene open.");
            Transform room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
            var player=room.GetComponentInChildren<WellnessExplorer>();var chat=room.GetComponentInChildren<WellnessVoiceChat>();
            var rain=room.GetComponentInChildren<WellnessRain>();var atmosphere=room.GetComponentInChildren<WellnessAtmosphere>();
            Light daylight=room.GetComponentsInChildren<Light>().Single(l=>l.type==LightType.Directional);
            if(player==null||chat==null||rain==null||atmosphere==null)throw new InvalidOperationException("Existing therapy, voice and rain components are required.");
            foreach(string name in new[]{"Materials","Meshes","Backups"})Directory.CreateDirectory(Folder+"/"+name);
            string backup=Folder+"/Backups/TherapyRoom_BeforeWeatherCycle.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not preserve the open scene, including unsaved edits.");
            Shader skyShader=Shader.Find("Therapy Game/Quiet Sky"),cloudShader=Shader.Find("Therapy Game/Soft Low Poly Cloud");
            if(skyShader==null||cloudShader==null)throw new InvalidOperationException("Weather shaders must finish importing first.");
            if(ShaderUtil.ShaderHasError(skyShader)||ShaderUtil.ShaderHasError(cloudShader))throw new InvalidOperationException("A weather shader has an import error.");
            Material sky=GetMaterial("QuietSky",skyShader),cloud=GetMaterial("SoftCloud",cloudShader);
            Mesh mesh=GetCloudMesh();
            Transform previous=room.Find("Sky and Weather");if(previous!=null)Undo.DestroyObjectImmediate(previous.gameObject);
            var group=new GameObject("Sky and Weather");group.transform.SetParent(room,false);Undo.RegisterCreatedObjectUndo(group,"Add clouds and weather cycle");
            var cycle=group.AddComponent<WellnessSkyCycle>();cycle.daylight=daylight;cycle.skyMaterial=sky;cycle.cloudMaterial=cloud;
            cycle.rain=rain;cycle.atmosphere=atmosphere;
            cycle.pondRenderer=room.Find("OutdoorGarden").GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="Therapy Game/Quiet Pond");
            cycle.clouds=new Transform[16];var random=new System.Random(260926);
            for(int i=0;i<cycle.clouds.Length;i++)
            {
                var go=new GameObject("Drifting cloud "+(i+1).ToString("00"));go.transform.SetParent(group.transform,false);
                // Four staggered rows cover the garden and its mountain backdrop.
                float x=-90+(i%4)*53+Next(random,-8,8),z=-85+(i/4)*52+Next(random,-9,9);
                go.transform.localPosition=new Vector3(x,Next(random,42,59),z);
                go.transform.localRotation=Quaternion.Euler(0,Next(random,-24,24),0);
                go.transform.localScale=new Vector3(Next(random,5.5f,8),Next(random,3.3f,4.8f),Next(random,4.5f,6.8f));
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=cloud;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                renderer.allowOcclusionWhenDynamic=false;cycle.clouds[i]=go.transform;
            }
            Undo.RecordObject(chat,"Link sky/weather controls");chat.skyCycle=cycle;EditorUtility.SetDirty(chat);
            CheckCycleMath();VerifyScene(room,cycle,chat,mesh);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save the updated TherapyRoom scene.");
            File.WriteAllText(Report,"Weather cycle installed "+DateTime.Now.ToString("s")+"\nPASS: 16 shared low-poly clouds / 7,680 total triangles, no cloud colliders/shadows; one existing directional light.\nPASS: day/night boundaries, midnight wrap, normalized sun direction, weather loop continuity/ranges, and rain scaling.\nPASS: UI, rain, sky/cloud materials, pond and atmosphere references. Existing voice agents and five seating positions retained.\nSaved before-setup backup includes the scene's unsaved changes. No Play mode, microphone, lighting bake, GI update, reflection capture, or rendering test was started. Live appearance remains unverified.\n");
            File.WriteAllText(Request,"installed-live-visual-check-pending");Debug.Log("THERAPY_WEATHER_CYCLE_IMPORTED: CPU checks passed; no Play mode or bake started.");
        }
        private static float Next(System.Random random,float min,float max) => min+(max-min)*(float)random.NextDouble();
        private static Material GetMaterial(string name,Shader shader)
        {
            string path=Folder+"/Materials/"+name+".mat";Material result=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(result==null){result=new Material(shader){name=name};AssetDatabase.CreateAsset(result,path);}return result;
        }
        private static Mesh GetCloudMesh()
        {
            string path=Folder+"/Meshes/SoftCloudCluster.asset";Mesh existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
            float g=(1+Mathf.Sqrt(5))/2;
            var vertices=new[]{new Vector3(-1,g,0),new Vector3(1,g,0),new Vector3(-1,-g,0),new Vector3(1,-g,0),new Vector3(0,-1,g),new Vector3(0,1,g),new Vector3(0,-1,-g),new Vector3(0,1,-g),new Vector3(g,0,-1),new Vector3(g,0,1),new Vector3(-g,0,-1),new Vector3(-g,0,1)};
            for(int i=0;i<vertices.Length;i++)vertices[i].Normalize();
            int[] faces={0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1};
            Vector3[] centers={new Vector3(-1.6f,-.08f,0),new Vector3(-.65f,.28f,.18f),new Vector3(.30f,.40f,0),new Vector3(1.3f,.02f,.1f),new Vector3(-.1f,-.13f,-.75f),new Vector3(.45f,-.1f,.8f)};
            Vector3[] scales={new Vector3(1.25f,.65f,.95f),new Vector3(1.25f,.9f,1.12f),new Vector3(1.3f,.95f,1.1f),new Vector3(1.2f,.67f,.94f),new Vector3(1.3f,.6f,.84f),new Vector3(1.25f,.63f,.88f)};
            var output=new List<Vector3>();var indices=new List<int>();
            for(int lobe=0;lobe<centers.Length;lobe++)for(int f=0;f<faces.Length;f+=3)
            {
                Vector3 a=vertices[faces[f]],b=vertices[faces[f+1]],c=vertices[faces[f+2]];
                Vector3 ab=(a+b).normalized,bc=(b+c).normalized,ca=(c+a).normalized;
                Triangle(a,ab,ca,centers[lobe],scales[lobe],output,indices);
                Triangle(b,bc,ab,centers[lobe],scales[lobe],output,indices);
                Triangle(c,ca,bc,centers[lobe],scales[lobe],output,indices);
                Triangle(ab,bc,ca,centers[lobe],scales[lobe],output,indices);
            }
            var mesh=new Mesh{name="Soft cloud cluster · 480 triangles"};mesh.SetVertices(output);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        private static void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 center,Vector3 scale,List<Vector3> vertices,List<int> indices)
        {
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),a+b+c)<0){Vector3 temp=b;b=c;c=temp;}
            int start=vertices.Count;vertices.Add(center+Vector3.Scale(a,scale));vertices.Add(center+Vector3.Scale(b,scale));vertices.Add(center+Vector3.Scale(c,scale));
            indices.Add(start);indices.Add(start+1);indices.Add(start+2);
        }
        [MenuItem("Therapy Game/Validate Weather Cycle (CPU Only)")]
        public static void CheckCycleMath()
        {
            void Require(bool value,string message){if(!value)throw new Exception("Weather validation: "+message);}
            Require(Mathf.Abs(WellnessSkyCycle.WrapHour(25.5f)-1.5f)<.0001f,"midnight wrap");
            Require(Mathf.Abs(WellnessSkyCycle.WrapHour(-1)-23)<.0001f,"negative hour wrap");
            Require(WellnessSkyCycle.DaylightAmount(12)>.99f && WellnessSkyCycle.DaylightAmount(0)<.01f,"day/night contrast");
            Require(Mathf.Abs(WellnessSkyCycle.SunDirection(6).y)<.0001f&&Mathf.Abs(WellnessSkyCycle.SunDirection(18).y)<.0001f,"sunrise/sunset horizon");
            for(int i=0;i<=1440;i++)
            {
                Vector3 sun=WellnessSkyCycle.SunDirection(i/60f);Require(Mathf.Abs(sun.magnitude-1)<.0001f,"sun direction normalization");
                Vector2 weather=WellnessSkyCycle.SampleWeather(i/1440f);
                Require(weather.x>=.179f&&weather.x<=1.001f&&weather.y>=0&&weather.y<=1,"weather range");
            }
            foreach(float edge in new[]{0,.3125f,.5f,.75f,1})
                Require(Vector2.Distance(WellnessSkyCycle.SampleWeather(edge-.00001f),WellnessSkyCycle.SampleWeather(edge+.00001f))<.001f,"smooth phase boundary");
            Require(WellnessSkyCycle.SampleWeather(.2f).y==0&&WellnessSkyCycle.SampleWeather(.7f).y>.99f,"clear/rain periods");
            var go=new GameObject("Temporary weather math check"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var rain=go.AddComponent<WellnessRain>();rain.intensity=.6f;rain.SetWeatherAmount(.5f);
                Require(Mathf.Abs(rain.EffectiveIntensity-.3f)<.0001f,"rain scales by weather");
                rain.rainEnabled=false;Require(rain.EffectiveIntensity==0,"rain disable override");
                rain.rainEnabled=true;rain.ClearWeatherOverride();Require(Mathf.Abs(rain.EffectiveIntensity-.6f)<.0001f,"rain cleanup");
            }
            finally {UnityEngine.Object.DestroyImmediate(go);}
        }
        private static void VerifyScene(Transform room,WellnessSkyCycle cycle,WellnessVoiceChat chat,Mesh mesh)
        {
            if(cycle.clouds.Length!=16||mesh.triangles.Length/3!=480)throw new Exception("Cloud geometry exceeds the intended budget.");
            if(cycle.GetComponentsInChildren<Collider>().Length!=0||cycle.GetComponentsInChildren<Light>().Length!=0)throw new Exception("Clouds must not add colliders or extra lights.");
            if(cycle.clouds.Any(c=>c.GetComponent<Renderer>().shadowCastingMode!=ShadowCastingMode.Off))throw new Exception("Cloud shadows must stay disabled.");
            if(chat.skyCycle!=cycle||cycle.pondRenderer==null||cycle.rain!=chat.rain)throw new Exception("Cycle integration references are incomplete.");
            if(chat.IsConnected||chat.IsBusy)throw new Exception("A voice session must not be active during import.");
            if(room.GetComponentsInChildren<TheLastWatch.Interaction.WellnessSeat>().Sum(s=>s.Count)!=5)throw new Exception("Existing seating changed.");
            if(room.GetComponentsInChildren<Light>().Count(l=>l.type==LightType.Directional)!=1)throw new Exception("Keep exactly one directional light.");
        }
    }
}
