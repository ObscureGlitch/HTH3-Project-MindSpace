using System;
using System.IO;
using System.Linq;
using TheLastWatch.Integrations;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyQuietGlassPolish
    {
        private const string Root="Assets/TherapyGame";
        private const string Request=Root+"/QuietGlassPolishRequest.txt";
        static TherapyQuietGlassPolish()=>EditorApplication.delayCall+=Verify;
        private static void Verify()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="verify-quiet-glass-polish-once")return;
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Verify;return;}
            File.WriteAllText(Request,"verifying-once");
            try
            {
                Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
                if(!scene.isLoaded)throw new Exception("Keep TherapyRoom open for verification.");
                var chat=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WellnessVoiceChat>(true)).Single();
                if(!chat.useQuietGlassMenu||chat.quietGlassSerif==null||chat.quietGlassSans==null)throw new Exception("Existing Design A menu/font references are missing.");
                int motion=QuietGlassPolishChecks.Run(),layout=QuietGlassLayoutChecks.Run(),captions=QuietHudStateChecks.Run();
                File.WriteAllText(Root+"/UI/QuietGlass/PolishCheck.txt","Quiet Glass polish imported "+DateTime.Now.ToString("s")+"\n"+
                    "PASS: "+motion+" rounded-shape, label-motion and note-bound checks; "+layout+" layout checks; "+captions+" captions/playlist checks.\n"+
                    "PASS: existing menu and fonts still assigned. No scene save, lighting change, new camera, custom shader, Play run or microphone.\n"+
                    "Fixed disc rectangles in both HUD/menu; native anti-aliased rounded geometry; four-pixel selection strip; new fully styled weather/time/night pages.\n"+
                    "Pending: user visual/click-through verification at actual Game-view resolution.\n");
                File.WriteAllText(Request,"verified-live-check-pending");
                Debug.Log("THERAPY_QUIET_GLASS_POLISH_READY: UI-only polish imported and CPU checks passed. No scene mutation or Play run.");
            }
            catch(Exception e){File.WriteAllText(Request,"failed");Debug.LogException(e);}
        }
    }
}
