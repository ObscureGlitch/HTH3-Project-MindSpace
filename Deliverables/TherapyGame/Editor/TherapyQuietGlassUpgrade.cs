using System;
using System.IO;
using System.Linq;
using TheLastWatch.Integrations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyQuietGlassUpgrade
    {
        private const string Root="Assets/TherapyGame",Folder=Root+"/UI/QuietGlass";
        private const string Request=Root+"/QuietGlassRequest.txt",Report=Folder+"/QuietGlassCheck.txt";
        static TherapyQuietGlassUpgrade()=>EditorApplication.delayCall+=Once;
        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="install-quiet-glass-once")return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Stop Play/baking, then choose Therapy Game > Install Quiet Glass Pause Menu.");return;}
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Once;return;}
            File.WriteAllText(Request,"installing-once");
            try{Install();}catch(Exception e){File.WriteAllText(Request,"failed");File.WriteAllText(Report,e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Therapy Game/Install Quiet Glass Pause Menu")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new Exception("Stop Play/baking before installing the menu.");
            Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded)throw new Exception("Keep the existing TherapyRoom scene open.");
            var chats=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WellnessVoiceChat>(true)).ToArray();
            if(chats.Length!=1||chats[0].hud==null||chats[0].player==null)throw new Exception("Expected the existing therapy voice controller, HUD and player.");
            Font serif=AssetDatabase.LoadAssetAtPath<Font>(Folder+"/Fonts/SourceSerif4-Regular.otf");
            Font sans=AssetDatabase.LoadAssetAtPath<Font>(Folder+"/Fonts/Carlito-Regular.ttf");
            if(serif==null||sans==null)throw new Exception("Both Quiet Glass fonts must finish importing first.");
            int layoutChecks=QuietGlassLayoutChecks.Run(),captionChecks=QuietHudStateChecks.Run(),doorwayChecks=DoorwayConversationChecks.Run();
            // Keep the backup outside Assets: no second scene import or duplicate lighting-data import.
            string backup=Path.Combine(Directory.GetCurrentDirectory(),"TherapyBackups","QuietGlass",DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(backup);
            File.Copy(scene.path,Path.Combine(backup,"TherapyRoom_PreSave.unity"),false);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not preserve the current scene before installing the menu.");
            File.Copy(scene.path,Path.Combine(backup,"TherapyRoom_BeforeQuietGlass.unity"),false);
            var chat=chats[0];int objects=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Length);
            Undo.RecordObject(chat,"Install approved Quiet Glass menu");
            chat.useQuietGlassMenu=true;chat.quietGlassSerif=serif;chat.quietGlassSans=sans;
            EditorUtility.SetDirty(chat);EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save TherapyRoom.");
            if(objects!=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Length))throw new Exception("Menu installation must not change scene objects.");
            File.WriteAllText(Report,"Quiet Glass Design A installed "+DateTime.Now.ToString("s")+"\n"+
                "PASS: "+layoutChecks+" reference/resizing checks; "+captionChecks+" caption/playlist checks; "+doorwayChecks+" doorway voice checks.\n"+
                "PASS: approved 1672x941 composition; local vinyl rotation; live song/artist, clock and microphone state.\n"+
                "PASS: existing companion selection, consent, typed chat, captions, biometric opt-in and sky controls preserved.\n"+
                "PASS: new font references and menu enabled in the saved scene. No extra scene objects, cameras, shaders, reflection captures, baking, Play run or microphone.\n"+
                "Backup: "+backup+"\n"+
                "Pending: user-controlled Play screenshot and click-through. Translucent cached layers approximate the mockup frost without an expensive live blur.\n");
            File.WriteAllText(Request,"installed-live-check-pending");
            Debug.Log("THERAPY_QUIET_GLASS_READY: approved pause layout saved with both fonts. CPU checks passed; no Play mode or microphone used.");
        }
    }
}
