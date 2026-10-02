using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using TheLastWatch.UI;
using TheLastWatch.Environment;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace TherapyGame.Editor
{
    public static class TherapyFishingRefinementChecks
    {
        public const string Output="D:/Hack the Hill/Deliverables/FishingRefinement/Checks/";
        const string Snapshot="D:/Hack the Hill/Deliverables/FishingRefinement/SaveSnapshot-20261001-200616";
        public static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        static FieldInfo Field(string name)=>typeof(WellnessFishing).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
        public static void Set(WellnessFishing f,string name,object value)=>Field(name).SetValue(f,value);
        public static T Get<T>(WellnessFishing f,string name)=>(T)Field(name).GetValue(f);
        public static void Call(WellnessFishing f,string name,params object[] args)=>typeof(WellnessFishing).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,args);
        public static List<FishingPaint> Capture(WellnessFishing f,string method)
        {Call(f,"EnsureTheme");var list=new List<FishingPaint>();Set(f,"paintCapture",list);try{Call(f,method);return list;}finally{Set(f,"paintCapture",null);}}
        public static KoiCatchInventory SnapshotInventory()
        {var saved=new KoiCatchInventory(Snapshot);saved.Reload();Require(saved.Fish.Count==9&&saved.ReadIssueCount==0&&saved.PendingCount==0,"Nine original snapshot catches must load without rewriting.");return saved;}
        static string Saves()
        {
            string folder=Path.GetFullPath("Temp/FishingRefinementFixtures/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            var inv=new KoiCatchInventory(folder);var ordinary=KoiCatch.Create("kohaku",1);inv.Save(ordinary);string original=File.ReadAllText(inv.RecordPath(ordinary));
            var interrupted=KoiCatch.Create("yuki",1);File.WriteAllText(inv.RecordPath(interrupted)+".tmp",JsonUtility.ToJson(interrupted));
            var journal=KoiCatch.Create("genesis",1);inv.PreservePending(journal);File.WriteAllText(inv.RecordPath(journal)+".tmp","partial write");
            var fresh=new KoiCatchInventory(folder);fresh.Reload();Require(fresh.Fish.Count==3&&fresh.PendingCount==2,"Interrupted catches invisible on restart.");
            Require(fresh.RecoverPending()==2&&fresh.PendingCount==0,"Interrupted catches did not commit.");fresh.Reload();Require(fresh.Fish.Count==3,"Recovery duplicates or loses catches.");
            Require(Directory.GetFiles(folder,"*.bak").Length==1,"Damaged temporary data was not retained as a backup.");
            var partialJournal=KoiCatch.Create("ryujin",1);File.WriteAllText(fresh.RecordPath(partialJournal)+".pending","partial journal");fresh.RememberPending(partialJournal);fresh.RecoverPending();Require(fresh.IsCommitted(partialJournal),"A partial journal permanently blocks Retry save.");
            Require(Directory.GetFiles(folder,"*.bak").Length==2,"Damaged pending journal not retained.");
            Require(File.ReadAllText(inv.RecordPath(ordinary))==original,"Original record changed.");fresh.Equip(journal.id);fresh.Reload();Require(fresh.Equipped.id==journal.id,"Equip lost after reload.");
            bool duplicate=false;try{fresh.Save(journal);}catch(IOException){duplicate=true;}Require(duplicate&&fresh.Fish.Count==4,"Duplicate award allowed.");
            File.WriteAllText(inv.RecordPath(ordinary),"damaged externally");fresh.Reload();Require(fresh.Fish.Count==4&&fresh.ReadIssueCount>0,"Transient corruption erased an already loaded fish or was hidden.");
            var remembered=KoiCatch.Create("sakura",1);fresh.RememberPending(remembered);fresh.PreservePending(remembered);fresh.RecoverPending();fresh.Reload();Require(fresh.Fish.Count==5&&fresh.IsCommitted(remembered),"Pending in-memory catch retry broken.");
            SnapshotInventory();
            return "PASS: nine existing snapshot catches load unchanged; completed tmp and pending journals survive a fresh loader and recover once; damaged tmp retained in backup; duplicate, equipped choice and in-memory read failure protections. Fixture files are confined to project Temp.\n";
        }
        public static void Prepare(WellnessFishing f,KoiCatchInventory inventory)
        {Set(f,"inventory",inventory);Get<KoiFishingCollection>(f,"collection").Rebuild(inventory.Fish);Call(f,"ResetCollectionView");Set(f,"selected",inventory.Fish.Count>0?0:-1);}
        static string Layout(KoiPondLibrary library)
        {
            var root=new GameObject("Fishing UI checks"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var f=root.AddComponent<WellnessFishing>();f.catchLibrary=library;f.chat=Object.FindObjectsByType<WellnessFishing>().FirstOrDefault(x=>x!=f)?.chat;Prepare(f,SnapshotInventory());
                Call(f,"SetFilter",5);Require(Get<List<int>>(f,"visible").Count==0&&f.CatchCount==9,"Filtering mutates catches.");
                Call(f,"ResetCollectionView");Require(Get<int>(f,"filter")==-1&&Get<List<int>>(f,"visible").Count==9&&Get<int>(f,"page")==0,"Opening retains stale filter/page.");
                var inventory=Capture(f,"DrawInventory");Require(inventory.Any(p=>p.Content=="Showing 1–6 of 9 fish"),"First page count missing.");
                Set(f,"page",1);inventory=Capture(f,"DrawInventory");Require(inventory.Any(p=>p.Content=="Showing 7–9 of 9 fish"),"Second page hides catches.");
                var cases=new List<List<FishingPaint>>{inventory};
                for(int rank=-1;rank<6;rank++){Call(f,"SetFilter",rank);cases.Add(Capture(f,"DrawInventory"));}
                Call(f,"ResetCollectionView");Set(f,"filterMenuOpen",true);cases.Add(Capture(f,"DrawInventory"));Set(f,"filterMenuOpen",false);
                Set(f,"journal",true);Set(f,"page",1);Set(f,"journalSelected",12);cases.Add(Capture(f,"DrawInventory"));
                var round=new KoiFishingRound(1);while(round.State!=KoiFishingRound.Phase.Bite)round.Step(.02f,false,false);round.Step(.02f,true,true);
                Set(f,"round",round);Set(f,"hookedEntry",KoiFishingLoot.ForVariety("genesis"));var reel=Capture(f,"DrawRound");Require(reel.Count(p=>p.Content!=null)==4,"Active HUD text is cluttered.");cases.Add(reel);
                Set(f,"lastCatch",KoiCatch.Create("amaterasu",1));Set(f,"catchLiftTime",2.3f);cases.Add(Capture(f,"DrawCatch"));
                foreach(var ops in cases)foreach(var p in ops)
                {
                    Require(p.Rect.xMin>=0&&p.Rect.yMin>=0&&p.Rect.xMax<=1280&&p.Rect.yMax<=720,"HUD bounds exceed reference viewport.");
                    if(p.Content==null&&p.Texture==null)Require(p.Color.a>=.95f,"Transparent UI surface remained.");
                    if(p.Content!=null&&!p.Content.Contains("\n")&&!p.Style.wordWrap)Require(p.Style.CalcSize(new GUIContent(p.Content)).x<=p.Rect.width+2,"Text clipped: "+p.Content);
                }
                return "PASS: all nine catches reachable on two explicitly counted pages; stale Godly filter resets to All fish; all six rarity filters preserve records; empty/filter dropdown/journal/long catch name layouts fit 1280×720; all UI surfaces opaque and active reeling HUD has only four labels.\n";
            }
            finally{Object.DestroyImmediate(root);}
        }
        static string Motion()
        {
            Vector3 water=Vector3.zero,display=new Vector3(.2f,1.7f,1.2f);
            Require(Vector3.Distance(KoiFishingCatchMotion.Position(water,display,0),water-Vector3.up*.18f)<.001f,"Start not submerged.");
            Require(KoiFishingCatchMotion.Position(water,display,.08f).y>.45f,"Catch launch has no impact.");
            Require(Mathf.Abs(KoiFishingCatchMotion.Position(water,display,KoiFishingCatchMotion.LiftEnd).y-1.08f)<.001f,"Catch lift incorrect.");
            Require(Vector3.Distance(KoiFishingCatchMotion.Position(water,display,1),display)<.001f,"Catch does not settle.");
            Require(KoiFishingCatchMotion.ShowcaseScale(1.8f)==1&&KoiFishingCatchMotion.ShowcaseScale(2.3f)==1&&KoiFishingCatchMotion.ShowcaseScale(1.9f)>1,"Arrival spring accumulates or is missing.");
            var root=new GameObject("Splash fixture"){hideFlags=HideFlags.HideAndDontSave};
            try{using(var splash=new KoiCatchSplash(root.transform,water)){foreach(float t in new[]{0f,.1f,.4f,.8f,1.5f})splash.Pose(t,Quaternion.identity);Require(root.GetComponentsInChildren<Light>(true).Length==0&&root.GetComponentsInChildren<Collider>(true).Length==0,"Catch splash creates gameplay cost.");}Require(root.transform.childCount==0,"Splash not disposed.");}
            finally{Object.DestroyImmediate(root);}
            return "PASS: submerged start, fast 1.08m water exit, settled display endpoint, bounded arrival spring, disposable splash without lights/colliders.\n";
        }
        public static void VerifyBatch()
        {
            try
            {
                Require(!EditorApplication.isPlaying,"Do not enter Play mode.");EditorSceneManager.OpenScene("Assets/TherapyGame/Scenes/TherapyRoom.unity");var library=Object.FindAnyObjectByType<WellnessFishing>().Library;
                string report=Saves()+Layout(library)+Motion()+TherapyPantheonChecks.CheckOdds(library)+TherapyPantheonChecks.CheckRounds()+TherapyPantheonChecks.CheckSaves()+TherapyPantheonChecks.CheckViews(library);
                File.WriteAllText(Output+"RefinementCheck.txt",report+"No Play mode, real save writes, microphone or webcam were used.\n");Debug.Log("KOI_REFINEMENT_CHECKS_PASSED");EditorApplication.Exit(0);
            }
            catch(Exception e){File.WriteAllText(Output+"RefinementCheck.txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
