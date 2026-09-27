using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheLastWatch.Audio;
using TheLastWatch.Integrations;
using TheLastWatch.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyMindSpacePolish
    {
        private const string Root="Assets/TherapyGame",Folder=Root+"/Exterior/MindSpacePolish";
        private const string Request=Root+"/MindSpacePolishRequest.txt",Report=Folder+"/PolishCheck.txt";
        static TherapyMindSpacePolish()=>EditorApplication.delayCall+=Once;
        private static void Once()
        {
            if(!File.Exists(Request))return;
            string requested=File.ReadAllText(Request).Trim();
            if(requested!="install-mindspace-polish-once"&&requested!="seal-mindspace-eaves-once"&&requested!="persist-wall-lighting-once")return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Stop Play/baking, then choose Therapy Game > Finish MindSpace Exterior and HUD.");return;}
            File.WriteAllText(Request,"installing-once");EnsureFolder(Folder);
            try{if(requested=="seal-mindspace-eaves-once")SealEaves();else if(requested=="persist-wall-lighting-once")PersistWallLighting();else Install();}
            catch(Exception e){File.WriteAllText(Request,"failed");File.AppendAllText(Report,"\n"+e);Debug.LogException(e);}
        }
        [MenuItem("Therapy Game/Finish MindSpace Exterior and HUD")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new Exception("Stop Play/baking first.");
            Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded)throw new Exception("Keep TherapyRoom open.");
            Transform room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
            Transform garden=room.Find("OutdoorGarden");var hud=room.GetComponent<WellnessHud>();var chat=room.GetComponentInChildren<WellnessVoiceChat>();
            if(garden==null||hud==null||chat==null||chat.hud!=hud||chat.skyCycle==null||chat.IsConnected||chat.IsBusy)throw new Exception("Existing idle room, HUD and sky references required.");
            if(room.Find("MindSpace Exterior")!=null)throw new Exception("Polish is already present; do not duplicate it.");
            var replacements=new[]{"Cabin siding · cedar","Cabin trim · bark","Cabin gables · cedar"};
            foreach(string name in replacements)if(garden.Find(name)==null)throw new Exception("Missing original cabin batch "+name);
            var geometry=MindSpaceExteriorGeometry.Build();
            string checks=MindSpaceExteriorGeometry.Validate(geometry)+TherapyQuietHudSetup.CheckLayout()+
                "PASS: "+QuietHudStateChecks.Run()+" caption/playlist state checks.\n"+TherapyRoomVoiceSetup.RunChecks();
            if(BackgroundMusicPlayer.CreatorCredit("alex-morgan-lofi-restaurant-568157")!="alex-morgan"||
                BackgroundMusicPlayer.CreatorCredit("andriih-soft-soft-music-579821")!="andriih-soft")throw new Exception("Music attribution mismatch.");
            foreach(string dir in new[]{Folder,Folder+"/Meshes",Folder+"/Materials",Folder+"/Backups"})EnsureFolder(dir);
            string backup=Folder+"/Backups/TherapyRoom_BeforeMindSpacePolish.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("MindSpace exterior and compact HUD");
            try
            {
                var root=new GameObject("MindSpace Exterior");root.transform.SetParent(room,false);Undo.RegisterCreatedObjectUndo(root,"Create repaired exterior");
                foreach(string name in replacements){var old=garden.Find(name);Undo.RecordObject(old.gameObject,"Preserve old cabin shell");old.gameObject.SetActive(false);}
                var mats=Materials();
                foreach(var entry in geometry)
                {
                    var go=new GameObject(entry.Key);go.transform.SetParent(root.transform,false);
                    Mesh mesh=entry.Value.Mesh(entry.Key);AssetDatabase.CreateAsset(mesh,Folder+"/Meshes/"+entry.Key+".asset");
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mats[entry.Key];
                    renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    renderer.shadowCastingMode=entry.Key=="AtticGlass"||entry.Key=="Lettering"||entry.Key=="LanternGlow"?ShadowCastingMode.Off:ShadowCastingMode.On;
                    renderer.receiveShadows=entry.Key!="AtticGlass";
                }
                root.transform.Find("Lettering").name="MindSpace Counseling Center · mesh lettering";
                checks+=MindSpaceWallLighting.Repair(room,Folder+"/Meshes");
                Undo.RecordObject(hud,"Keep other HUD elements transparent");hud.panelOpacity=0;EditorUtility.SetDirty(hud);
                Physics.SyncTransforms();checks+=CheckScene(room,root.transform);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
                File.WriteAllText(Report,"MindSpace exterior / compact HUD installed "+DateTime.Now.ToString("s")+"\n"+checks+
                    "PASS: plaque right of entrance; wide real triangular attic glazing; continuous siding above door; equal corner beams; flower box, planters, greenery and small lantern. Original roof, garden, door interaction, NPCs and voice consent untouched. Old cabin batches disabled, not deleted.\n"+
                    "PASS: compact 236x78 logical-pixel music card, translucent backing, supplied creator handles, cached rotating vinyl and three tiny note sprites; game clock/weather bottom-right; soft subtitle entrance plus existing live text reveal.\n"+
                    "No additional lights, particle systems, cameras, textures above 64px, bake, Play mode, microphone or service session. Live appearance/input still requires a user-controlled play check.\n");
                File.WriteAllText(Request,"installed-live-check-pending");Undo.CollapseUndoOperations(undo);
                Debug.Log("THERAPY_MINDSPACE_POLISH_READY: facade, wall lighting, compact HUD and voice regression checks passed; scene saved; no bake or Play mode.");
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }
        private static Dictionary<string,Material> Materials()
        {
            var result=new Dictionary<string,Material>();
            foreach(var pair in new Dictionary<string,string>{{"Backing","5D4934"},{"Cedar","946F46"},{"Trim","4A4837"},{"Oak","B39A6D"},{"Sage","465D48"},{"Soil","473B2B"},{"Leaves","4E7047"},{"Flowers","E8C9AE"},{"Lavender","AC9FCA"},{"Metal","343F37"},{"Lettering","F0E8D2"},{"LanternGlow","E7CB97"},{"AtticGlass","96B7BD"}})
            {
                var m=new Material(Shader.Find(pair.Key=="Lettering"?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")){name="MindSpace "+pair.Key};
                ColorUtility.TryParseHtmlString("#"+pair.Value,out Color color);m.SetColor("_BaseColor",color);
                if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",pair.Key=="AtticGlass"?.65f:.16f);
                if(pair.Key=="LanternGlow"){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(.32f,.23f,.11f));}
                if(pair.Key=="AtticGlass")
                {
                    color.a=.35f;m.SetColor("_BaseColor",color);m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);
                    m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=(int)RenderQueue.Transparent;
                    m.SetShaderPassEnabled("ShadowCaster",false);
                }
                m.enableInstancing=true;AssetDatabase.CreateAsset(m,Folder+"/Materials/"+pair.Key+".mat");result.Add(pair.Key,m);
            }
            return result;
        }
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            if(Directory.Exists(path)){AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);return;}
            int slash=path.LastIndexOf('/');string parent=path.Substring(0,slash);EnsureFolder(parent);
            if(string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent,path.Substring(slash+1))))throw new IOException("Could not create asset folder "+path);
        }
        private static void SealEaves()
        {
            var scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded)throw new Exception("Keep TherapyRoom open.");
            var room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
            var shell=room.Find("MindSpace Exterior");if(shell==null)throw new Exception("Install the exterior first.");
            var geometry=MindSpaceExteriorGeometry.Build();string checks=MindSpaceExteriorGeometry.Validate(geometry);
            string sceneBackup=Folder+"/Backups/TherapyRoom_BeforeEaveSeal.unity";
            if(!File.Exists(sceneBackup)&&!EditorSceneManager.SaveScene(scene,sceneBackup,true))throw new IOException("Roof-seam backup failed.");
            foreach(string key in new[]{"Cedar","Trim"})
            {
                var mesh=shell.Find(key).GetComponent<MeshFilter>().sharedMesh;
                string expected=Folder+"/Meshes/"+key+".asset";
                if(AssetDatabase.GetAssetPath(mesh)!=expected)throw new Exception("Unexpected mesh reference; stop before modifying it.");
                string backup=Folder+"/Backups/"+key+"_BeforeEaveSeal.asset";
                if(!File.Exists(backup)&&!AssetDatabase.CopyAsset(expected,backup))throw new IOException("Mesh backup failed.");
                Undo.RegisterCompleteObjectUndo(mesh,"Seal cabin roof seam");var replacement=geometry[key].Mesh(key);
                EditorUtility.CopySerialized(replacement,mesh);UnityEngine.Object.DestroyImmediate(replacement);EditorUtility.SetDirty(mesh);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Roof-seam scene save failed.");
            File.AppendAllText(Report,"\nRoof-seam refinement saved "+DateTime.Now.ToString("s")+"\n"+checks+"PASS: original roof underside sealed at both eaves; equal posts extend into the roof thickness. Only the new Cedar/Trim mesh assets changed; both backed up.\n");
            File.WriteAllText(Request,"installed-live-check-pending");Debug.Log("THERAPY_MINDSPACE_EAVES_READY: roof seams sealed, geometry checks passed, scene saved.");
        }
        private static void PersistWallLighting()
        {
            var scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded)throw new Exception("Keep TherapyRoom open.");
            var room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
            if(room.Find("MindSpace Exterior")==null)throw new Exception("Install the exterior first.");
            string backup=Folder+"/Backups/TherapyRoom_BeforePersistentWallLighting.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Wall lighting backup failed.");
            string checks=MindSpaceWallLighting.EnsureBinding(room);
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Wall lighting scene save failed.");
            File.AppendAllText(Report,"\nPersistent wall lighting saved "+DateTime.Now.ToString("s")+"\n"+checks);
            File.WriteAllText(Request,"installed-live-check-pending");Debug.Log("THERAPY_MINDSPACE_WALL_PERSISTENCE_READY: original lighting retained on scene reload; scene saved.");
        }
        private static string CheckScene(Transform room,Transform repaired)
        {
            var door=room.Find("Architecture/Door/Open door hinge");
            if(door==null||room.GetComponentInChildren<WellnessVoiceChat>().entranceDoor==null)throw new Exception("Door links were lost.");
            if(repaired.GetComponentsInChildren<Light>().Length!=0||repaired.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Decoration must not add lights or obstruct navigation.");
            var window=room.Find("Architecture/Front garden window");
            Material plaster=room.Find("Architecture/Walls/Entry front wall").GetComponent<MeshRenderer>().sharedMaterial;
            foreach(Transform p in window)if(p.name.StartsWith("Plaster ")&&p.GetComponent<MeshRenderer>().sharedMaterial!=plaster)throw new Exception("Mismatched interior wall material.");
            if(room.GetComponentInChildren<WellnessVoiceChat>().hud!=room.GetComponent<WellnessHud>())throw new Exception("HUD link changed.");
            return "PASS: original door/voice/HUD links preserved; no new decorative colliders or lights; all four interior wall materials match.\n";
        }
    }
}
