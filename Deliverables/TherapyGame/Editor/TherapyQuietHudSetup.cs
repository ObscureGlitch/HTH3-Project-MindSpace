using System;
using System.IO;
using System.Linq;
using TheLastWatch.Integrations;
using TheLastWatch.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyQuietHudSetup
    {
        private const string Root="Assets/TherapyGame",Request=Root+"/QuietHudRequest.txt",Report=Root+"/UI/QuietHudCheck.txt";
        static TherapyQuietHudSetup()=>EditorApplication.delayCall+=Once;
        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="install-quiet-hud-once")return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Stop Play/baking, then choose Therapy Game > Install Quiet HUD Refinement.");return;}
            File.WriteAllText(Request,"installing-once");Directory.CreateDirectory(Root+"/UI");
            try{Install();}catch(Exception e){File.WriteAllText(Request,"failed");File.WriteAllText(Report,e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Therapy Game/Install Quiet HUD Refinement")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new InvalidOperationException("Stop Play/baking first.");
            var scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded)throw new InvalidOperationException("Keep TherapyRoom open.");
            var room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
            var chat=room.GetComponentInChildren<WellnessVoiceChat>();var hud=room.GetComponent<WellnessHud>();
            if(chat==null||hud==null||chat.player==null||chat.hud!=hud||hud.chat!=chat||chat.skyCycle==null||chat.IsBusy||chat.IsConnected)
                throw new Exception("Existing HUD, idle voice and sky references must be present.");
            string checks="PASS: "+QuietHudStateChecks.Run()+" pure caption/playlist assertions.\n"+CheckLayout()+TherapyRoomVoiceSetup.RunChecks();
            Directory.CreateDirectory(Root+"/UI/Backups");string backup=Root+"/UI/Backups/TherapyRoom_BeforeQuietHud.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Quiet transparent HUD");
            try
            {
                Undo.RecordObject(hud,"Transparent gameplay HUD");hud.panelOpacity=0;
                EditorUtility.SetDirty(hud);EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
                File.WriteAllText(Report,"Quiet HUD installed "+DateTime.Now.ToString("s")+"\n"+checks+
                    "PASS: linked HUD retained; transparent default saved; existing sky clock and weather wired; consent and mic controls unchanged.\n"+
                    "Music previous/next wraps the shuffled playlist and preserves pause. Captions are zone-gated, continuously animated and consume streamed AI text/audio character timing, falling back to final messages. Player captions remain provider-finalized utterances.\n"+
                    "No Play mode, rendering, microphone, network session, lighting edits or bake used. Live input/audio timing and appearance require a user-controlled play check.\n");
                File.WriteAllText(Request,"installed-live-check-pending");Undo.CollapseUndoOperations(undo);
                Debug.Log("THERAPY_QUIET_HUD_READY: transparent HUD saved; captions, playlist, clock/weather and voice regressions passed; microphone off.");
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }
        public static string CheckLayout()
        {
            int count=0;void Check(bool ok,string name){count++;if(!ok)throw new Exception("Quiet HUD layout: "+name);}
            foreach(var size in new[]{new Vector2(1280,720),new Vector2(1366,768),new Vector2(1920,1080),new Vector2(2560,1080),new Vector2(1024,768),new Vector2(800,600)})
            foreach(bool large in new[]{false,true})
            {
                float scale=Mathf.Min(size.x/1280,size.y/720),width=size.x/scale,height=size.y/scale;
                var l=WellnessHudLayout.At(width,height,large);var rects=new[]{l.journey,l.settings,l.music,l.captions,l.interaction,l.clock,l.microphone};
                foreach(var r in rects)Check(r.xMin>=0&&r.yMin>=0&&r.xMax<=width&&r.yMax<=height,"within screen "+size);
                for(int i=0;i<rects.Length;i++)for(int j=i+1;j<rects.Length;j++)Check(!rects[i].Overlaps(rects[j]),"no overlap "+size);
            }
            foreach(bool pref in new[]{false,true})foreach(bool range in new[]{false,true})foreach(bool active in new[]{false,true})
                Check(WellnessHud.CaptionVisible(pref,range,active)==(pref&&range&&active),"caption zone gate");
            Check(!WellnessHud.IsDay(0)&&!WellnessHud.IsDay(5.99f)&&WellnessHud.IsDay(6),"sunrise icon boundary");
            Check(WellnessHud.IsDay(17.99f)&&!WellnessHud.IsDay(18)&&!WellnessHud.IsDay(24),"sunset/wrap icon boundary");
            Check(WellnessHud.WeatherIconKind(true,"Clear")==3,"actual rain intensity overrides target label");
            Check(WellnessHud.WeatherIconKind(false,"Cloudy")==2&&WellnessHud.WeatherIconKind(false,"Clearing")==2,"cloud transition icons");
            Check(WellnessHud.WeatherIconKind(false,"Clear")==-1,"clear uses sun or moon");
            return "PASS: "+count+" layout/zone/weather assertions over six display sizes, both font sizes and day/night boundaries.\n";
        }
    }
}
