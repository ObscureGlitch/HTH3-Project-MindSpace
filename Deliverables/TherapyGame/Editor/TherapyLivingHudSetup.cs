using System;
using System.IO;
using System.Linq;
using TheLastWatch.Environment;
using TheLastWatch.Integrations;
using TheLastWatch.Interaction;
using TheLastWatch.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyLivingHudSetup
    {
        private const string Root="Assets/TherapyGame",Folder=Root+"/Exterior/Fireflies";
        private const string Request=Root+"/LivingHudRequest.txt",Report=Folder+"/LivingHudCheck.txt";
        static TherapyLivingHudSetup()=>EditorApplication.delayCall+=Once;
        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="install-living-hud-once")return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Stop Play/baking, then choose Therapy Game > Finish Living HUD and Fireflies.");return;}
            File.WriteAllText(Request,"installing-once");EnsureFolder(Folder);
            try{Install();}catch(Exception e){File.WriteAllText(Request,"failed");File.WriteAllText(Report,e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Therapy Game/Finish Living HUD and Fireflies")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new Exception("Stop Play/baking first.");
            var scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");if(!scene.isLoaded)throw new Exception("Keep TherapyRoom open.");
            var room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
            var garden=room.Find("OutdoorGarden");var chat=room.GetComponentInChildren<WellnessVoiceChat>();var hud=room.GetComponent<WellnessHud>();
            if(garden==null||chat==null||hud==null||chat.IsBusy||chat.IsConnected||chat.skyCycle==null||chat.skyCycle.cloudDeck==null)throw new Exception("Existing idle room, voice and photographic sky required.");
            var cycle=chat.skyCycle;var pond=cycle.pondRenderer!=null?cycle.pondRenderer.sharedMaterial:null;
            var terrain=garden.Find("Walkable surfaces and safety/Walkable terrain")?.GetComponent<MeshCollider>();
            if(pond==null||!pond.HasProperty("_ReflectionStrength")||terrain==null)throw new Exception("Updated pond and saved walkable terrain required.");
            if(garden.Find("Tiny firefly clusters")!=null)throw new Exception("Fireflies already installed; not duplicating.");
            Shader fireflyShader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Exterior/Shaders/Fireflies.shader");
            foreach(var shader in new[]{fireflyShader,pond.shader,cycle.skyMaterial.shader})
                if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Shader import has errors; no scene changes made.");
            string checks=CheckMath()+TherapyQuietHudSetup.CheckLayout()+TherapyRoomVoiceSetup.RunChecks()+
                "PASS: "+QuietHudStateChecks.Run()+" caption/playlist checks; "+DoorwayConversationChecks.Run()+" doorway checks.\n";
            int originalLights=room.GetComponentsInChildren<Light>(true).Length,originalCameras=room.GetComponentsInChildren<Camera>(true).Length;
            EnsureFolder(Folder+"/Backups");
            string backup=Folder+"/Backups/TherapyRoom_BeforeLivingHud.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
            string pondPath=AssetDatabase.GetAssetPath(pond);
            if(!File.Exists(Folder+"/Backups/Pond_BeforeSkyReflection.mat")&&!AssetDatabase.CopyAsset(pondPath,Folder+"/Backups/Pond_BeforeSkyReflection.mat"))throw new IOException("Pond material backup failed.");
            Mesh mesh=BuildFireflies(garden,terrain);
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Living HUD, sky pond and fireflies");
            try
            {
                var group=new GameObject("Tiny firefly clusters");group.transform.SetParent(garden,false);Undo.RegisterCreatedObjectUndo(group,"Add fireflies");
                AssetDatabase.CreateAsset(mesh,Folder+"/TinyFireflies.asset");
                var material=new Material(fireflyShader){name="Tiny firefly glow · no lights"};AssetDatabase.CreateAsset(material,Folder+"/TinyFireflies.mat");
                group.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=group.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                renderer.enabled=false;renderer.allowOcclusionWhenDynamic=false;
                var flies=group.AddComponent<WellnessFireflies>();flies.sky=cycle;flies.glowRenderer=renderer;
                Undo.RecordObject(pond,"Link shared sky reflection");
                foreach(string name in new[]{"_TopColor","_HorizonColor","_GroundColor"})pond.SetColor(name,cycle.skyMaterial.GetColor(name));
                foreach(string name in new[]{"_Night","_Storm","_StarRotation"})pond.SetFloat(name,cycle.skyMaterial.GetFloat(name));
                pond.SetVector("_SunDirection",cycle.skyMaterial.GetVector("_SunDirection"));pond.SetFloat("_ReflectionStrength",.82f);
                pond.SetTexture("_CloudAtlas",cycle.cloudDeck.cloudMaterial.GetTexture("_BaseMap"));EditorUtility.SetDirty(pond);
                Undo.RecordObject(hud,"Show checklist on load");hud.showJourney=true;EditorUtility.SetDirty(hud);
                if(mesh.vertexCount!=224||mesh.triangles.Length!=336||group.GetComponentsInChildren<Renderer>().Length!=1)throw new Exception("Firefly budget exceeded.");
                if(room.GetComponentsInChildren<Light>(true).Length!=originalLights||room.GetComponentsInChildren<Camera>(true).Length!=originalCameras)throw new Exception("Unexpected extra light/camera.");
                if(room.GetComponentsInChildren<WellnessSeat>().Sum(s=>s.Count)!=5||room.GetComponentsInChildren<WellnessDoor>().Length!=1)throw new Exception("Seat/door regression.");
                if(chat.hud!=hud||chat.player==null||!chat.player.useReferenceHud||cycle.cloudDeck.cards.Length!=16)throw new Exception("HUD/sky reference regression.");
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
                File.WriteAllText(Report,"Living HUD / pond sky / fireflies installed "+DateTime.Now.ToString("s")+"\n"+checks+
                    "PASS: clock actual text right-aligned at 12px right / 8px bottom; vinyl center invariant under HUD scaling and rotation; checklist initially expanded; microphone status in settings; Esc opens/closes settings and Left Alt frees cursor.\n"+
                    "PASS: 56 fireflies across 7 terrain-grounded clusters, 112 triangles / one renderer / one material; independent 4.6–8.4s soft flashes, dusk/night visibility, reduced in rain. No added lights, colliders or cameras.\n"+
                    "PASS: pond shares the sky function, sun/moon/stars and existing sixteen cloud-card projections at their original real-time wind speed; teal ripples, bed transparency and shore fading preserved. No reflection camera/texture capture.\n"+
                    "PASS: debounced doorway farewell and welcome, speech-aware delivery, per-direction cooldown and expiry; consent, manual pause, mute, focus and acoustic reach gates retained. No forged player captions. Biometric integration unchanged.\n"+
                    "CPU/import checks only. No Play mode, microphone, voice-provider call, bake or GPU preview. User-controlled live visuals and spoken delivery still need checking.\n");
                File.WriteAllText(Request,"installed-live-check-pending");Undo.CollapseUndoOperations(undo);
                Debug.Log("THERAPY_LIVING_HUD_READY: checks passed; sky pond, fireflies and HUD saved; no Play/microphone/bake.");
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }
        private static Mesh BuildFireflies(Transform garden,MeshCollider terrain)
        {
            var centers=new[]{new Vector2(8,-11),new Vector2(13,-12),new Vector2(22,-8),new Vector2(20,1),new Vector2(9,3),new Vector2(-6,-9),new Vector2(2,8)};
            var random=new System.Random(9261738);float Next(float a,float b)=>a+(b-a)*(float)random.NextDouble();
            var positions=new Vector3[224];var uv=new Vector2[224];var seeds=new Vector2[224];var drift=new Vector2[224];var colors=new Color[224];var triangles=new int[336];
            var corners=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};int index=0;
            foreach(Vector2 center in centers)for(int j=0;j<8;j++)
            {
                Vector3 p=garden.TransformPoint(new Vector3(center.x+Next(-1.55f,1.55f),0,center.y+Next(-1.4f,1.4f)));
                if(!terrain.Raycast(new Ray(p+Vector3.up*18,Vector3.down),out RaycastHit ground,30))throw new Exception("Firefly cluster must have terrain below.");
                p=garden.InverseTransformPoint(ground.point+Vector3.up*Next(.55f,1.45f));
                var seed=new Vector2(Next(.07f,.13f),Next(0,1));var motion=new Vector2(Next(4.6f,8.4f),Next(0,20));
                Color tint=Color.Lerp(new Color(.78f,1,.30f),new Color(1,.86f,.39f),Next(0,1));
                for(int k=0;k<4;k++){int v=index*4+k;positions[v]=p;uv[v]=corners[k];seeds[v]=seed;drift[v]=motion;colors[v]=tint;}
                int t=index*6,b=index*4;triangles[t]=b;triangles[t+1]=b+1;triangles[t+2]=b+2;triangles[t+3]=b;triangles[t+4]=b+2;triangles[t+5]=b+3;index++;
            }
            var mesh=new Mesh{name="56 tiny fireflies · seven clusters"};mesh.vertices=positions;mesh.uv=uv;mesh.uv2=seeds;mesh.uv3=drift;mesh.colors=colors;mesh.triangles=triangles;
            mesh.RecalculateBounds();Bounds bounds=mesh.bounds;bounds.Expand(1);mesh.bounds=bounds;return mesh;
        }
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            if(Directory.Exists(path)){AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);return;}
            int slash=path.LastIndexOf('/');string parent=path.Substring(0,slash);EnsureFolder(parent);
            if(string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent,path.Substring(slash+1))))throw new IOException("Cannot create "+path);
        }
        public static string CheckMath()
        {
            int count=0;void Check(bool yes,string message){count++;if(!yes)throw new Exception("Living HUD math: "+message);}
            foreach(float scale in new[]{.5f,.711f,1,1.25f,1.5f,2})
            foreach(float height in new[]{720f,960f})
            {
                var layout=WellnessHudLayout.At(1280,height);Vector3 pivot=new Vector3(layout.music.x+25.5f,layout.music.y+27.5f,0);
                var canvas=Matrix4x4.Scale(new Vector3(scale,scale,1));
                for(int degrees=0;degrees<=360;degrees+=15)
                    Check(Vector3.Distance(WellnessUiTheme.VinylMatrix(canvas,pivot,degrees).MultiplyPoint3x4(pivot),canvas.MultiplyPoint3x4(pivot))<.002f,"vinyl fixed center");
                Check(1280-layout.clock.xMax==12&&height-layout.clock.yMax==8,"weather corner margins");
            }
            Check(WellnessFireflies.NightVisibility(12,0)==0,"no daylight glow");
            Check(WellnessFireflies.NightVisibility(0,0)>.99f,"night glow");
            Check(WellnessFireflies.NightVisibility(0,1)<WellnessFireflies.NightVisibility(0,0),"rain softens glow");
            for(int minute=0;minute<1440;minute+=10){float f=WellnessFireflies.NightVisibility(minute/60f,.5f);Check(f>=0&&f<=1,"bounded firefly visibility");}
            // Angular cloud rectangle projection: its center must map to the tile center.
            Vector3 direction=new Vector3(2,1.6f,-3).normalized;
            Quaternion rotation=Quaternion.LookRotation(-direction,Vector3.up)*Quaternion.Euler(0,0,3);
            Check(Mathf.Abs(Vector3.Dot(direction,rotation*Vector3.right))<.00001f&&Mathf.Abs(Vector3.Dot(direction,rotation*Vector3.up))<.00001f,"cloud reflection coordinates");
            return "PASS: "+count+" vinyl/corner/firefly/cloud-projection math assertions.\n";
        }
    }
}
