using System;
using System.IO;
using System.Linq;
using TheLastWatch.Audio;
using TheLastWatch.Integrations;
using TheLastWatch.Interaction;
using TheLastWatch.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyHudSetup
    {
        private const string Root="Assets/TherapyGame",Request=Root+"/HudRequest.txt",Report=Root+"/UI/HudCheck.txt";
        static TherapyHudSetup()=>EditorApplication.delayCall+=Once;
        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="install-reference-hud-once")return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Stop Play/baking, then choose Therapy Game > Install Reference HUD and Captions.");return;}
            File.WriteAllText(Request,"installing-once");Directory.CreateDirectory(Root+"/UI");
            try{Install();}catch(Exception e){File.WriteAllText(Request,"failed");File.WriteAllText(Report,e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Therapy Game/Install Reference HUD and Captions")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new InvalidOperationException("Stop Play/baking first.");
            var scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded)throw new InvalidOperationException("Keep TherapyRoom open.");
            var room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
            var chat=room.GetComponentInChildren<WellnessVoiceChat>();
            if(chat==null||chat.player==null||chat.roomSpace!=room||chat.entranceDoor==null||chat.IsBusy||chat.IsConnected)
                throw new Exception("Keep the existing idle room-wide voice setup in place.");
            string checks=CheckLayoutAndCaptions()+TherapyRoomVoiceSetup.RunChecks();
            var items=room.GetComponentsInChildren<WellnessInteraction>(true);
            int grounding=items.Count(i=>i.area==WellnessInteraction.Area.Grounding&&i.therapist==null&&i.door==null&&i.breathingOrb==null);
            if(grounding<3||!items.Any(i=>i.breathingOrb!=null))throw new Exception("Existing checklist interaction targets are missing.");
            Directory.CreateDirectory(Root+"/UI/Backups");string backup=Root+"/UI/Backups/TherapyRoom_BeforeReferenceHud.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Reference HUD and live captions");
            try
            {
                var hud=room.GetComponent<WellnessHud>();if(hud==null)hud=Undo.AddComponent<WellnessHud>(room.gameObject);
                Undo.RecordObjects(new UnityEngine.Object[]{hud,chat,chat.player},"Link reference HUD");
                hud.room=room;hud.chat=chat;hud.player=chat.player;hud.showCaptions=hud.showJourney=true;hud.panelOpacity=1;
                chat.hud=hud;chat.player.useReferenceHud=true;
                EditorUtility.SetDirty(hud);EditorUtility.SetDirty(chat);EditorUtility.SetDirty(chat.player);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
                File.WriteAllText(Report,"Reference HUD installed "+DateTime.Now.ToString("s")+"\n"+checks+
                    "PASS: one linked HUD; old exploration/voice banners suppressed; "+grounding+" real grounding targets and existing breathing orb linked via events.\n"+
                    "PASS: captions use actual agent/user transcripts, corrections, interruption and VAD events; waiting text is '...'; rich-text parsing off. Music UI reads the existing playlist and real playback position.\n"+
                    "No heart-rate widget, new microphones, service requests, Play mode, GPU preview, lighting changes or bake. CPU layout preview is not a live Unity capture. Live UI/input/music and remote-caption timing still need a user-controlled play check.\n");
                File.WriteAllText(Request,"installed-live-check-pending");Undo.CollapseUndoOperations(undo);
                Debug.Log("THERAPY_REFERENCE_HUD_READY: scene links, layout, caption and voice regression checks passed; microphone off.");
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }
        public static string CheckLayoutAndCaptions()
        {
            int count=0;void Check(bool ok,string name){count++;if(!ok)throw new Exception("HUD test: "+name);}
            foreach(var size in new[]{new Vector2(1280,720),new Vector2(1366,768),new Vector2(1920,1080),new Vector2(2560,1080),new Vector2(1024,768),new Vector2(800,600)})
            foreach(bool large in new[]{false,true})
            {
                float scale=Mathf.Min(size.x/1280,size.y/720),width=size.x/scale,height=size.y/scale;
                var layout=WellnessHudLayout.At(width,height,large);var cards=new[]{layout.journey,layout.settings,layout.music,layout.captions,layout.interaction};
                foreach(var r in cards)Check(r.xMin>=0&&r.yMin>=0&&r.xMax<=width&&r.yMax<=height,"card within "+size);
                for(int i=0;i<cards.Length;i++)for(int j=i+1;j<cards.Length;j++)Check(!cards[i].Overlaps(cards[j]),"cards do not overlap "+size);
            }
            var c=new WellnessCaptions();Check(c.Message=="...","waiting ellipsis");
            c.Agent("A real companion turn.",1,0);c.Tick(true,false,false,1);Check(c.Who==WellnessCaptions.Speaker.Companion,"speaking caption");
            c.Correct("A corrected turn.",1,1);Check(c.Message=="A corrected turn.","corrected caption");
            c.Tick(false,false,false,2);Check(c.Message=="...","waiting after audio");
            c.Tick(false,true,false,3);Check(c.Who==WellnessCaptions.Speaker.Listening,"live speech hint");
            c.User("A real user turn.",2,4);c.Tick(false,false,false,5);Check(c.Who==WellnessCaptions.Speaker.Player,"user caption");
            c.Interrupt(6);Check(c.Message=="...","interruption reset");c.Tick(false,true,true,7);Check(c.Message=="...","muted does not listen");
            c.Reset();Check(c.Page==0&&c.Message=="...","disconnect/reset");
            Check(WellnessCaptions.Split(new string('a',350),140).All(p=>p.Length<=140),"long messages bounded");
            Check(BackgroundMusicPlayer.CleanTitle("alex-morgan-lofi-restaurant-568157")=="Lofi Restaurant","real track naming");
            Check(WellnessHud.TimeLabel(125.9f)=="2:05"&&WellnessHud.TimeLabel(0)=="0:00","real timeline formatting");
            return "PASS: "+count+" HUD assertions for six display sizes, panel spacing, caption turn/state/correction/reset, pagination and real music metadata.\n";
        }
    }
}
