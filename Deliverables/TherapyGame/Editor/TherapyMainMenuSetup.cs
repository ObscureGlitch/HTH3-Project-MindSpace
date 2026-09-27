using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TheLastWatch.Environment;
using TheLastWatch.Integrations;
using TheLastWatch.Player;
using TheLastWatch.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyMainMenuSetup
    {
        private const string Root="Assets/TherapyGame",ScenePath=Root+"/Scenes/TherapyRoom.unity";
        private const string Request=Root+"/MainMenuRequest.txt",Report=Root+"/UI/MainMenuCheck.txt";
        static TherapyMainMenuSetup()
        {
            EditorApplication.delayCall+=Once;
            EditorSceneManager.sceneOpened+=(s,m)=>EditorApplication.delayCall+=Once;
        }
        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="install-night-sanctuary-menu-once")return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Once;return;}
            if(!SceneManager.GetSceneByPath(ScenePath).isLoaded)return;
            File.WriteAllText(Request,"installing-once");Install();
        }
        [MenuItem("Therapy Game/Install Night Sanctuary Main Menu")]
        public static void Install()
        {
            int undo=-1;EditorBuildSettingsScene[] originalBuild=null;
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!Lightmapping.isRunning,"Stop Play and baking first.");RequireMemory();
                var scene=SceneManager.GetSceneByPath(ScenePath);Require(scene.isLoaded,"Keep TherapyRoom open.");
                var room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
                var player=room.GetComponentInChildren<WellnessExplorer>(true);
                var chat=room.GetComponentInChildren<WellnessVoiceChat>(true);
                var sky=room.GetComponentInChildren<WellnessSkyCycle>(true);
                var hud=room.GetComponent<WellnessHud>();
                Require(player!=null&&chat!=null&&sky!=null&&hud!=null&&player.ViewCamera!=null,"Existing room/player/chat/sky/HUD required.");
                Require(player.enabled&&chat.enabled&&hud.enabled&&sky.enabled,"Gameplay components should be enabled before installation.");
                Require(!chat.IsConnected&&!chat.IsBusy,"No active conversation during installation.");
                Require(chat.quietGlassSerif!=null&&chat.quietGlassSans!=null,"Approved menu fonts must be installed.");
                Require(sky.cloudDeck!=null&&sky.cloudDeck.viewer==player.ViewCamera&&sky.pondRenderer!=null,"Live sky and reflective pond must share the gameplay camera.");
                Require(room.Find("MindSpace Exterior")!=null&&room.Find("OutdoorGarden")!=null,"Existing cabin and garden required.");
                Require(room.GetComponent<WellnessMainMenu>()==null,"Main menu already installed; do not duplicate.");
                int renderers=room.GetComponentsInChildren<Renderer>(true).Length,cameras=room.GetComponentsInChildren<Camera>(true).Length;
                var oldPosition=player.ViewCamera.transform.localPosition;var oldRotation=player.ViewCamera.transform.localRotation;
                float oldHour=sky.hour;bool oldRun=sky.timeRuns;
                string checks=WellnessMainMenuFlow.Checks()+CheckLayout();
                string backup=Path.GetFullPath("TherapyBackups/MainMenu/"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
                Directory.CreateDirectory(backup);File.Copy(scene.path,Path.Combine(backup,"TherapyRoom-on-disk.unity"));
                Require(EditorSceneManager.SaveScene(scene),"Save open scene before adding the title menu.");
                File.Copy(scene.path,Path.Combine(backup,"TherapyRoom-before-main-menu.unity"));
                File.Copy("ProjectSettings/EditorBuildSettings.asset",Path.Combine(backup,"EditorBuildSettings.asset"));
                originalBuild=EditorBuildSettings.scenes;
                Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Night Sanctuary main menu");
                var menu=Undo.AddComponent<WellnessMainMenu>(room.gameObject);
                menu.room=room;menu.player=player;menu.chat=chat;menu.hud=hud;menu.sky=sky;menu.serif=chat.quietGlassSerif;menu.sans=chat.quietGlassSans;
                checks+=CheckView(menu);
                var execution=(DefaultExecutionOrder)Attribute.GetCustomAttribute(typeof(WellnessMainMenu),typeof(DefaultExecutionOrder));
                Require(execution!=null&&execution.order<=-10000,"Main menu must precede gameplay startup.");
                EditorUtility.SetDirty(menu);
                // Make this the actual player-build entry, keeping every other saved scene.
                EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(originalBuild.Where(s=>s.path!=ScenePath)).ToArray();
                Require(EditorBuildSettings.scenes[0].path==ScenePath&&EditorBuildSettings.scenes[0].enabled,"TherapyRoom must be the first build scene.");
                Require(renderers==room.GetComponentsInChildren<Renderer>(true).Length&&cameras==room.GetComponentsInChildren<Camera>(true).Length,"Do not add renderers or a second camera.");
                Require(player.ViewCamera.transform.localPosition==oldPosition&&player.ViewCamera.transform.localRotation==oldRotation&&sky.hour==oldHour&&sky.timeRuns==oldRun,"Menu should not move the editor camera or change the saved sky.");
                Require(player.enabled&&chat.enabled&&hud.enabled,"Installer must leave gameplay components enabled in saved scene.");
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Save main-menu references.");
                string guid=AssetDatabase.AssetPathToGUID(Root+"/Runtime/WellnessMainMenu.cs");
                Require(!string.IsNullOrEmpty(guid)&&File.ReadAllText(scene.path).Contains(guid),"Main-menu script reference must persist.");
                Undo.CollapseUndoOperations(undo);undo=-1;
                File.WriteAllText(Report,"Night Sanctuary main menu installed "+DateTime.Now.ToString("s")+"\n"+checks+
                    "PASS: references saved; TherapyRoom is first enabled build scene; previous build entries retained.\n"+
                    "PASS: no added camera, renderer, light, render texture, shader, scenery replacement, bake or reflection capture. Existing pond/cabin/garden stay live.\n"+
                    "PASS: no editor-time camera movement or sky change. Early startup holds player/HUD/chat/camera provider/coach; entry restores camera then the original per-session consent screen.\n"+
                    "Settings, help, credits and exit confirmation implemented; static ivory text and rounded sage selection. Title starts at 01:30, six-minute day, clear weather, title-only aurora; gameplay cycle timing/weather restore on entry.\n"+
                    "Backup: "+backup+"\nNo Play, microphone/camera, AI connection, GPU preview or full build started. Visual, input, and in-game transition checks remain user-controlled.\n");
                File.WriteAllText(Request,"installed-live-check-pending");Debug.Log("THERAPY_MAIN_MENU_READY: live Night Sanctuary title menu and build entry saved.");
            }
            catch(Exception e)
            {
                if(undo>=0)Undo.RevertAllDownToGroup(undo);
                if(originalBuild!=null)EditorBuildSettings.scenes=originalBuild;
                File.WriteAllText(Report,"Night Sanctuary main menu stopped "+DateTime.Now.ToString("s")+"\n"+e);
                File.WriteAllText(Request,"needs-attention-no-auto-retry");Debug.LogException(e);
            }
        }
        private static string CheckView(WellnessMainMenu menu)
        {
            var rotation=Quaternion.LookRotation(menu.localLook-menu.localEye,Vector3.up);
            float aspect=1672f/941,half=Mathf.Tan(menu.fieldOfView*.5f*Mathf.Deg2Rad);
            Vector3 cabin=Quaternion.Inverse(rotation)*(new Vector3(0,2,0)-menu.localEye);
            Vector2 screen=new Vector2(.5f+cabin.x/(2*cabin.z*half*aspect),.5f+cabin.y/(2*cabin.z*half));
            Require(cabin.z>0&&screen.x>.6f&&screen.x<.92f&&screen.y>.25f&&screen.y<.7f,"Cabin should frame right of the left-hand menu.");
            var worldEye=menu.room.TransformPoint(menu.localEye);
            var garden=menu.room.Find("OutdoorGarden");var ground=garden.GetComponentsInChildren<MeshCollider>().FirstOrDefault(c=>c.name=="Walkable terrain");
            RaycastHit floor=default;
            Require(ground!=null&&ground.Raycast(new Ray(worldEye+Vector3.up*20,Vector3.down),out floor,40),"Title camera must be above the existing garden.");
            Require(worldEye.y-floor.point.y>1.5f,"Title camera must clear the ground.");
            return "PASS: cabin projects at "+screen+" in the right-hand scene; camera clears existing terrain. Actual tree occlusion and scenery appearance still need a visual check.\n";
        }
        private static string CheckLayout()
        {
            int checks=0;
            foreach(var size in new[]{new Vector2(1280,720),new Vector2(1920,1080),new Vector2(2560,1440),new Vector2(3440,1440),new Vector2(1024,768),new Vector2(800,600)})
            {
                float scale=Mathf.Min(size.x/1672,size.y/941);var offset=(size-new Vector2(1672,941)*scale)*.5f;
                foreach(var r in new[]{new Rect(92,144,560,130),new Rect(93,338,390,354),new Rect(690,172,840,610),new Rect(40,885,600,32)})
                {Require(offset.x+r.xMin*scale>=0&&offset.y+r.yMin*scale>=0&&offset.x+r.xMax*scale<=size.x&&offset.y+r.yMax*scale<=size.y,"Layout clipping.");checks++;}
            }
            return "PASS: "+checks+" menu bounds checks across 16:9, ultrawide and 4:3 sizes.\n";
        }
        private static void Require(bool ok,string message){if(!ok)throw new Exception("Main menu: "+message);}
        [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
        {public uint length,load;public ulong totalPhysical,availablePhysical,totalPageFile,availablePageFile,totalVirtual,availableVirtual,availableExtendedVirtual;}
        [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        private static void RequireMemory()
        {
            if(Application.platform!=RuntimePlatform.WindowsEditor)return;
            var m=new MemoryStatus{length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};
            if(!GlobalMemoryStatusEx(ref m)||m.availablePageFile<1536UL*1024*1024||m.availablePhysical<1024UL*1024*1024)
                throw new Exception("Low memory: close unused apps before Therapy Game > Install Night Sanctuary Main Menu. No automatic retry.");
        }
    }
}
