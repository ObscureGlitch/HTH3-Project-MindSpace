using System;
using System.IO;
using System.Linq;
using System.Text;
using TheLastWatch.Integrations;
using TheLastWatch.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyRoomVoiceSetup
    {
        private const string Root="Assets/TherapyGame";
        private const string Request=Root+"/RoomVoiceRequest.txt";
        private const string Report=Root+"/VoiceAccess/RoomVoiceCheck.txt";
        static TherapyRoomVoiceSetup()=>EditorApplication.delayCall+=Once;
        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="install-room-voice-once")return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Stop Play/baking, then use Therapy Game > Configure Room Voice and Consent.");return;}
            File.WriteAllText(Request,"installing-once");Directory.CreateDirectory(Root+"/VoiceAccess");
            try{Install();}catch(Exception e){File.WriteAllText(Request,"failed");File.WriteAllText(Report,e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Therapy Game/Configure Room Voice and Consent")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new InvalidOperationException("Stop Play/baking first.");
            var scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded)throw new InvalidOperationException("Keep TherapyRoom open.");
            var room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
            var chat=room.GetComponentInChildren<WellnessVoiceChat>();
            var door=room.Find("Architecture/Door/Open door hinge")?.GetComponent<WellnessDoor>();
            if(chat==null||door==null||door.leaf==null||chat.player==null||chat.voiceAudio==null||chat.IsBusy||chat.IsConnected)
                throw new InvalidOperationException("Existing idle room chat, player, audio and hinged door are required.");
            if(Vector3.Distance(room.lossyScale,Vector3.one)>.001f)throw new Exception("Room voice distances require metre-scale room coordinates.");
            if(chat.therapists.Length!=2||chat.therapists.Any(a=>a==null||a.faceRenderer==null||a.speechAnalysis==null))
                throw new Exception("Keep the repaired character and lip-sync links in place.");
            string check=RunChecks();
            Directory.CreateDirectory(Root+"/VoiceAccess/Backups");
            string backup=Root+"/VoiceAccess/Backups/TherapyRoom_BeforeRoomVoice.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Room voice range and startup consent");
            try
            {
                Undo.RecordObjects(new UnityEngine.Object[]{chat,chat.voiceAudio},"Configure room-wide voice");
                chat.roomSpace=room;chat.entranceDoor=door;
                chat.localRoom=new Bounds(new Vector3(0,1.5f,0),new Vector3(7.2f,3.4f,6.2f));
                chat.localDoorway=new Vector3(-2.43f,1.45f,-3.25f);chat.outdoorHearingDistance=6;
                chat.ConfigureVoiceAudio();
                // Validate the actual scene portal against outdoor geometry, without moving the player/door.
                Vector3 portal=room.TransformPoint(chat.localDoorway),outside=room.TransformPoint(new Vector3(-2.43f,1.7f,-5.5f));
                Physics.SyncTransforms();
                var hits=Physics.RaycastAll(portal,(outside-portal).normalized,Vector3.Distance(portal,outside),~0,QueryTriggerInteraction.Ignore);
                bool clear=!hits.Any(h=>!h.transform.IsChildOf(chat.player.transform)&&!h.transform.IsChildOf(door.transform));
                if(!clear)throw new Exception("The configured outdoor doorway path is blocked by scene geometry.");
                if(room.GetComponentsInChildren<WellnessSeat>().Sum(s=>s.Count)!=5)throw new Exception("Existing five seats must remain unchanged.");
                EditorUtility.SetDirty(chat);EditorUtility.SetDirty(chat.voiceAudio);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
                File.WriteAllText(Report,"Room voice update installed "+DateTime.Now.ToString("s")+"\n"+check+
                    "PASS: saved room, door, six-metre exterior reach and directional audio links; clear exterior portal path; repaired models, lip sync and five seats preserved.\n"+
                    "Consent is memory-only, explicit per play run; declining never opens a session. Automatic range return requires enabled, unmuted permission. Pause, focus loss, connection error and time limit require Resume, not fresh consent.\n"+
                    "No Play mode, microphone, remote voice request, rendering or bake started. Live input, network lifecycle, audio balance and UI appearance need a user-controlled play check.\n");
                File.WriteAllText(Request,"installed-live-check-pending");Undo.CollapseUndoOperations(undo);
                Debug.Log("THERAPY_ROOM_VOICE_READY: CPU range/consent tests passed and scene saved; microphone off.");
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }
        public static string RunChecks()
        {
            int count=0;
            void Check(bool condition,string name){count++;if(!condition)throw new Exception("Room voice test: "+name);}
            var b=new Bounds(new Vector3(0,1.5f,0),new Vector3(7.2f,3.4f,6.2f));
            var speaker=new Vector3(1.55f,1.65f,1.12f);var portal=new Vector3(-2.43f,1.45f,-3.25f);
            bool Reach(Vector3 p,float opening,bool clear)=>WellnessVoiceAcoustics.Evaluate(b,p,speaker,portal,opening,6,clear,out _,out _);
            for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++)
            {Check(Reach(new Vector3(x,1.7f,z),0,false),"whole room with closed door / furniture");Check(Reach(new Vector3(x,1.21f,z),0,false),"seated room reach");}
            Check(Reach(new Vector3(-3.59f,1.7f,3.09f),0,false),"far indoor corner");
            Check(Reach(new Vector3(-2.43f,1.7f,-5.5f),1,true),"outside open door");
            Check(!Reach(new Vector3(-2.43f,1.7f,-5.5f),0,true),"outside closed door");
            Check(!Reach(new Vector3(-2.43f,1.7f,-5.5f),1,false),"outside obstructed path");
            Check(!Reach(new Vector3(-2.43f,1.7f,-10),1,true),"outside beyond reach");
            Check(!Reach(new Vector3(-2.43f,1.7f,3.5f),1,true),"behind house");
            Check(!Reach(new Vector3(-4,1.7f,0),1,true),"through side wall");
            Check(!Reach(new Vector3(0,5,0),1,true),"above roof");
            Check(WellnessDoor.OpeningForAngle(0)==0&&WellnessDoor.OpeningForAngle(105)==1,"actual door opening endpoints");
            Check(WellnessDoor.OpeningForAngle(20)>0&&WellnessDoor.OpeningForAngle(20)<WellnessDoor.OpeningForAngle(50),"part-open door gain");
            float last=1;
            for(int i=1;i<=15;i++){float gain=WellnessVoiceAcoustics.Gain(i,true,1,0,6);Check(gain>0&&gain<=last,"indoor distance fades without cutting out");last=gain;}
            Check(WellnessVoiceAcoustics.Gain(8,false,0,2,6)==0,"closed doorway silence");
            Check(WellnessVoiceAcoustics.Gain(12,false,1,6,6)==0,"outdoor edge silence");
            Check(WellnessVoiceAcoustics.Gain(10,false,1,5,6)>WellnessVoiceAcoustics.Gain(10,false,1,5.9f,6),"soft exterior fade");
            var p=new WellnessVoiceConsent();
            Check(!p.CanConnect(true,true,false)&&!p.Decided,"no microphone before choice");
            p.Choose(false);Check(p.Decided&&!p.Granted&&!p.CanConnect(true,true,false),"decline without recording");
            p.Resume();Check(!p.CanConnect(true,true,false),"Resume cannot bypass consent");
            p.Choose(true);Check(p.CanConnect(true,true,false),"affirmative per-run consent");
            Check(!p.CanConnect(false,true,false)&&p.Granted,"range exit stops eligibility, retains consent");
            Check(p.CanConnect(true,true,false),"return resumes without repeat consent");
            p.SetMuted(true);Check(!p.CanConnect(true,true,false),"muted range return cannot restart mic");
            p.SetMuted(false);Check(p.CanConnect(true,true,false),"explicit unmute");
            p.Pause();Check(p.Granted&&!p.CanConnect(true,true,false),"manual/error/focus pause retains consent but blocks reconnect");
            p.Resume();Check(p.CanConnect(true,true,false),"Resume without reconsent");
            Check(!p.CanConnect(true,false,false)&&!p.CanConnect(true,true,true),"background and duplicate connection blocked");
            p.Choose(false);Check(!p.CanConnect(true,true,false),"revocation");
            p=new WellnessVoiceConsent();Check(!p.Granted&&!p.Decided,"new play run resets permission");
            return "PASS: "+count+" CPU assertions for room/seated/corner reach, open/closed/blocked/far exterior, attenuation, door opening and consent state transitions.\n";
        }
    }
}
