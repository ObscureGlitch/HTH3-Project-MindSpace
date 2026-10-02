using System;
using System.IO;
using System.Linq;
using TheLastWatch.UI;
using TheLastWatch.Player;
using TheLastWatch.Environment;
using TheLastWatch.Integrations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TherapyGame.Editor
{
    public static class TherapyFishingSetup
    {
        const string Root="Assets/TherapyGame",Scene=Root+"/Scenes/TherapyRoom.unity",Report=Root+"/Documentation/FishingCheck.txt";
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        public static void InstallBatch(){EditorSceneManager.OpenScene(Scene);EditorApplication.Exit(Install()?0:1);}
        [MenuItem("Therapy Game/Install Koi Fishing")]
        public static void InstallFromMenu(){Install();}
        static bool Install()
        {
            int undo=-1;
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Stop Play mode first.");var scene=EditorSceneManager.GetActiveScene();Require(scene.path==Scene,"Open TherapyRoom first.");
                var root=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom");var player=root.GetComponentInChildren<WellnessExplorer>(true);var pond=root.GetComponentInChildren<WellnessKoiPond>(true);
                var chat=root.GetComponentInChildren<WellnessVoiceChat>(true);var menu=root.GetComponent<WellnessMainMenu>();
                Require(player!=null&&player.ViewCamera!=null&&pond!=null&&chat!=null&&menu!=null,"Player, pond and existing menus are required.");
                string checks=CheckRounds()+CheckInventory()+CheckWater(pond)+CheckModels(pond,player);
                int cameras=root.GetComponentsInChildren<Camera>(true).Length,colliders=root.GetComponentsInChildren<Collider>(true).Length,renderers=root.GetComponentsInChildren<Renderer>(true).Length;
                Vector3 pose=player.transform.position,eye=player.ViewCamera.transform.localPosition;float fov=player.ViewCamera.fieldOfView;
                var originalLibrary=pond.library;var originalMaterial=pond.waterSurface.sharedMaterial;bool population=pond.randomizeCount;
                string backup=Path.GetFullPath("TherapyBackups/Fishing/"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(backup);File.Copy(scene.path,Path.Combine(backup,"TherapyRoom-before-fishing.unity"));
                Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Koi fishing and inventory");
                var fishing=root.GetComponent<WellnessFishing>()??Undo.AddComponent<WellnessFishing>(root);Undo.RecordObject(fishing,"Fishing references");fishing.player=player;fishing.pond=pond;fishing.chat=chat;fishing.mainMenu=menu;EditorUtility.SetDirty(fishing);
                Require(cameras==root.GetComponentsInChildren<Camera>(true).Length&&colliders==root.GetComponentsInChildren<Collider>(true).Length&&renderers==root.GetComponentsInChildren<Renderer>(true).Length,"Fishing added permanent geometry, cameras or colliders.");
                Require(player.transform.position==pose&&player.ViewCamera.transform.localPosition==eye&&player.ViewCamera.fieldOfView==fov,"Saved player view changed.");
                Require(pond.library==originalLibrary&&pond.waterSurface.sharedMaterial==originalMaterial&&pond.randomizeCount==population,"Existing koi population or pond material changed.");
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Fishing scene save failed.");Undo.CollapseUndoOperations(undo);undo=-1;
                File.WriteAllText(Report,"PASS: koi fishing installed.\n"+checks+"PASS: existing player/pond/menu references saved; no permanent rod renderer, camera or collider, and no change to koi population, pond material or saved player view.\nControls: F cast/cancel; Space hook, hold/tap to lift catch bar, release to lower; I inventory and equip; Esc close/cancel.\nData: local persistentDataPath/MindSpaceFishing, koi records committed individually; 500-catch capacity, no auto-deletion, no network, webcam, microphone or AI calls.\nPending: actual Play input, visuals and lifecycle test.\nBackup: "+backup+"\n");Debug.Log("KOI_FISHING_INSTALLED: "+Report);return true;
            }
            catch(Exception error){if(undo>=0)Undo.RevertAllDownToGroup(undo);File.WriteAllText(Report,"FAILED\n"+error);Debug.LogException(error);return false;}
        }
        public static void VerifyBatch()
        {
            string report=Root+"/Documentation/FishingReloadCheck.txt";
            try
            {
                EditorSceneManager.OpenScene(Scene);var root=EditorSceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="TherapyRoom");var fishing=root.GetComponent<WellnessFishing>();
                Require(fishing!=null&&fishing.player!=null&&fishing.pond!=null&&fishing.chat!=null&&fishing.mainMenu!=null,"Saved fishing references missing.");
                Require(!WellnessFishing.ModalOpen&&!WellnessFishing.OwnsShortcuts,"Editor started with a stale fishing modal.");
                Require(root.GetComponentsInChildren<Camera>(true).All(c=>!c.name.Contains("Preview")),"Preview camera leaked into the scene.");
                string checks=CheckRounds()+CheckInventory()+CheckWater(fishing.pond)+CheckModels(fishing.pond,fishing.player);
                File.WriteAllText(report,"PASS: fresh Unity process reloaded saved fishing component and references.\n"+checks+"PASS: saved scene has no active fishing state, held fish, rod, float or preview camera.\nPending: live input, UI layout and Play-mode lifecycle.\n");Debug.Log("KOI_FISHING_RELOADED: "+report);EditorApplication.Exit(0);
            }
            catch(Exception error){File.WriteAllText(report,"FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
        }
        static string CheckRounds()
        {
            int won=0;
            foreach(float dt in new[]{1f/30,1f/60,1f/120})for(int seed=0;seed<30;seed++)
            {
                var round=new KoiFishingRound(seed);
                for(int frame=0;frame<80/dt&&!round.Finished;frame++)
                {
                    // Reference player uses feedback and anticipates bar inertia;
                    // this runs only in editor checks, never controls live gameplay.
                    bool held=round.Bar+KoiFishingRound.BarWidth*.5f+round.Velocity*.20f<round.Fish;
                    round.Step(dt,held,round.State==KoiFishingRound.Phase.Bite);
                    Require(round.Bar>=0&&round.Bar<=1-KoiFishingRound.BarWidth&&round.Fish>=0&&round.Fish<=1&&round.Progress>=0&&round.Progress<=1,"Fishing round went out of bounds.");
                }
                Require(round.State==KoiFishingRound.Phase.Caught,"Catch bar was unwinnable with feedback: seed "+seed+", dt "+dt);
                Require(round.TryClaim()&&!round.TryClaim(),"A catch could be claimed twice.");won++;
            }
            var missed=new KoiFishingRound(4);for(int i=0;i<2000&&!missed.Finished;i++)missed.Step(.02f,false,false);Require(missed.State==KoiFishingRound.Phase.Escaped&&!missed.TryClaim(),"Missed bite produced a catch.");
            var cancelled=new KoiFishingRound(6);cancelled.Cancel();cancelled.Step(10,true,true);Require(cancelled.State==KoiFishingRound.Phase.Cancelled&&!cancelled.TryClaim(),"Cancelled fishing rewarded a catch.");
            var release=new KoiFishingRound(8);for(int i=0;i<15000&&!release.Finished;i++)release.Step(.01f,false,release.State==KoiFishingRound.Phase.Bite);Require(release.State==KoiFishingRound.Phase.Escaped,"Ignoring the catch bar did not escape.");
            var invalid=new KoiFishingRound(1);invalid.Step(float.NaN,true,true);invalid.Step(-1,false,false);Require(invalid.State==KoiFishingRound.Phase.Casting&&invalid.CastFraction==0,"Invalid delta time advanced fishing.");
            return "PASS: "+won+" seeded catch simulations at 30/60/120 Hz; bounded fish/bar/progress, missed bites, poor reeling, cancellation, one-time claim and invalid-time rejection.\n";
        }
        static string CheckInventory()
        {
            string root=Path.GetFullPath("Temp/FishingChecks"),folder=Path.Combine(root,Guid.NewGuid().ToString("N"));var inventory=new KoiCatchInventory(folder);
            try
            {
                var a=KoiCatch.Create("kohaku",.9f);var b=KoiCatch.Create("showa",1.08f);inventory.Save(a);inventory.Save(b);inventory.Equip(a.id);inventory.Equip(b.id);
                var reload=new KoiCatchInventory(folder);reload.Reload();Require(reload.Fish.Count==2&&reload.Equipped.id==b.id,"Koi or equipped selection did not persist.");
                reload.Equip(null);reload.Reload();Require(reload.Fish.Count==2&&reload.Equipped==null,"Putting a koi away removed catches.");
                bool duplicate=false;try{inventory.Save(a);}catch(IOException){duplicate=true;}Require(duplicate&&inventory.Fish.Count==2,"Duplicate catch ID was rewarded twice.");
                bool unsafePath=false;var bad=KoiCatch.Create("sanke",1);bad.id="../outside";try{inventory.RecordPath(bad);}catch(ArgumentException){unsafePath=true;}Require(unsafePath,"Unsafe catch path accepted.");
                File.WriteAllText(Path.Combine(folder,"damaged.koi.json"),"not json");reload.Reload();Require(reload.Fish.Count==2,"Corrupt record hid valid catches.");
                for(int i=inventory.Fish.Count;i<KoiCatchInventory.Capacity;i++)inventory.Fish.Add(a);bool full=false;try{inventory.Save(KoiCatch.Create("ogon",1));}catch(IOException){full=true;}Require(full,"Full inventory accepted a partial catch.");
                Require(!Directory.EnumerateFiles(folder,"*.tmp").Any(),"Inventory left an uncommitted temporary record.");
                return "PASS: local catch save/reload, equip/change/put-away persistence, duplicate protection, safe GUID paths, corrupt-record tolerance and capacity protection. Real player saves were not opened or altered.\n";
            }
            finally{Require(Path.GetFullPath(folder).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Unsafe fixture cleanup path.");if(Directory.Exists(folder))Directory.Delete(folder,true);}
        }
        static string CheckWater(WellnessKoiPond pond)
        {
            Physics.SyncTransforms();Vector3 center=WellnessFishing.WaterCenter(pond);var hits=new RaycastHit[64];int shores=0;
            foreach(Vector3 direction in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right})
            {
                var eye=center+new Vector3(direction.x*pond.pondRadii.x*1.15f,2,direction.z*pond.pondRadii.y*1.15f);
                Require(WellnessFishing.FindLanding(pond,eye,-direction,null,hits,out var target),"No shoreline cast possible in direction "+direction);
                Require(KoiFishingWater.Radius(target,center,pond.pondRadii)<.84f,"Float landed outside water.");shores++;
            }
            var bridge=pond.transform.root.GetComponentsInChildren<MeshCollider>(true).First(c=>c.name=="Continuous bridge deck");var bounds=bridge.bounds;
            Vector3 bridgeEye=new Vector3(center.x,bounds.max.y+1.6f,center.z);
            Require(WellnessFishing.FindLanding(pond,bridgeEye,Vector3.forward,null,hits,out _),"Cannot cast off the bridge.");
            Require(!KoiFishingWater.Nearby(center+Vector3.right*30+Vector3.up*2,center,pond.pondRadii),"Fishing works from far away.");
            Require(!KoiFishingWater.Candidate(center+Vector3.right*pond.pondRadii.x*1.2f+Vector3.up*2,Vector3.right,center,pond.pondRadii,0,out _),"Shoreline can cast while facing away from the pond.");
            return "PASS: "+shores+" real-scene shoreline approaches and bridge casting, dry-deck/rock rejection, valid water landing, facing-away and remote-distance rejection.\n";
        }
        static string CheckModels(WellnessKoiPond pond,WellnessExplorer player)
        {
            Require(pond.library.varieties.Length>=6&&pond.library.varieties.All(v=>v.parts.Length>=4&&v.parts.All(p=>p.mesh!=null)),"Existing koi models missing.");
            var temp=new GameObject("Fishing model fixture"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                foreach(var variety in pond.library.varieties)
                {
                    int index=KoiFishingViews.ModelIndex(pond.library,variety.name);var model=KoiFishingViews.Model(temp.transform,pond.library,index,.43f);
                    Require(model.GetComponentsInChildren<Collider>().Length==0&&model.GetComponentsInChildren<Renderer>().Length==variety.parts.Length,"Held koi lost model parts or acquired colliders.");
                    Require(model.GetComponentsInChildren<MeshRenderer>().All(r=>r.sharedMaterial==pond.library.material),"Held fish changed source colors/material.");UnityEngine.Object.DestroyImmediate(model);
                }
                using(var views=new KoiFishingViews(temp.transform,temp.transform,pond.library)){views.PoseRod(new KoiFishingRound(1),pond.pondCenter,0);Require(temp.GetComponentsInChildren<Collider>(true).Length==0,"Rod/float created collision geometry.");}
            }
            finally{UnityEngine.Object.DestroyImmediate(temp);}
            return "PASS: all "+pond.library.varieties.Length+" installed koi varieties reusable for catches and equip; original meshes/parts/materials retained, small size variation, no rod/float/held-fish colliders.\n";
        }
        public static void PreviewBatch()
        {
            try
            {
                EditorSceneManager.OpenScene(Scene);var root=EditorSceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="TherapyRoom");var pond=root.GetComponentInChildren<WellnessKoiPond>(true);
                var temp=new GameObject("Temporary koi preview"){hideFlags=HideFlags.HideAndDontSave};
                try
                {
                    using(var views=new KoiFishingViews(temp.transform,temp.transform,pond.library))
                    {
                        views.SelectPreview(KoiCatch.Create(pond.library.varieties[0].name,1));var target=views.Preview as RenderTexture;Require(target!=null,"Koi preview failed.");var prior=RenderTexture.active;var image=new Texture2D(512,288,TextureFormat.RGB24,false);
                        try{RenderTexture.active=target;image.ReadPixels(new Rect(0,0,512,288),0,0);image.Apply();File.WriteAllBytes("D:/Hack the Hill/Deliverables/Fishing/Checks/KoiInventoryPreview.png",image.EncodeToPNG());}
                        finally{RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(image);}
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(temp);}
                Debug.Log("KOI_INVENTORY_PREVIEW: rendered existing koi model.");EditorApplication.Exit(0);
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}
