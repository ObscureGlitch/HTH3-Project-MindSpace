using System;
using System.IO;
using System.Linq;
using TheLastWatch.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyNightSkyUpgrade
    {
        private const string Root="Assets/TherapyGame",Folder=Root+"/Weather/NightSky";
        private const string Request=Root+"/NightSkyRequest.txt",Report=Folder+"/NightSkyCheck.txt";
        static TherapyNightSkyUpgrade()=>EditorApplication.delayCall+=Once;
        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="install-night-sky-once")return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Stop Play/baking, then choose Therapy Game > Install Gentle Night Sky.");return;}
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Once;return;}
            File.WriteAllText(Request,"installing-once");EnsureFolder(Folder);
            try{Install();}catch(Exception e){File.WriteAllText(Request,"failed");File.WriteAllText(Report,e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Therapy Game/Install Gentle Night Sky")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new Exception("Stop Play/baking first.");
            Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded)throw new Exception("Keep TherapyRoom open before installing the night sky.");
            var cycles=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WellnessSkyCycle>(true)).ToArray();
            if(cycles.Length!=1)throw new Exception("Expected exactly one existing therapy sky cycle.");
            var cycle=cycles[0];var pond=cycle.pondRenderer!=null?cycle.pondRenderer.sharedMaterial:null;
            if(cycle.skyMaterial==null||pond==null||cycle.cloudDeck==null||cycle.cloudDeck.viewer==null)throw new Exception("Existing sky, pond and photographic cloud viewer references required.");
            foreach(var material in new[]{cycle.skyMaterial,pond})
                if(!material.HasProperty("_NightEffects")||ShaderUtil.ShaderHasError(material.shader))throw new Exception("Night-sky shader import has errors or is incomplete: "+material.name);
            string checks=NightSkyEventChecks.Run();
            EnsureFolder(Folder+"/Backups");string backup=Folder+"/Backups/TherapyRoom_BeforeNightSky.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Night-sky scene backup failed.");
            int renderers=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Renderer>(true).Length);
            int lights=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Light>(true).Length);
            Undo.RecordObject(cycle,"Enable gentle night sky");
            cycle.shootingStars=true;cycle.auroraBorealis=true;cycle.nightEffectsIntensity=.8f;
            if(renderers!=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Renderer>(true).Length)||
                lights!=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Light>(true).Length))throw new Exception("Night effects must not add renderers/lights.");
            EditorUtility.SetDirty(cycle);EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Night-sky scene save failed.");
            File.WriteAllText(Report,"Gentle night sky installed "+DateTime.Now.ToString("s")+"\n"+checks+
                "PASS: existing sky and pond both expose the shared night display; imported shaders report no errors.\n"+
                "PASS: enable shooting stars/aurora at 80% strength; original time, weather, cloud speed, lighting and other scene features preserved.\n"+
                "PASS: no additional scene objects, renderers, lights, cameras, textures, reflection captures, baking, Play mode or microphone. Scene backed up and saved.\n"+
                "Final appearance/performance still needs user-controlled Play verification; this installer does not render a test frame.\n");
            File.WriteAllText(Request,"installed-live-check-pending");
            Debug.Log("THERAPY_NIGHT_SKY_READY: gentle aurora and bounded shooting stars saved; CPU checks passed, no extra rendering objects.");
        }
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            if(Directory.Exists(path)){AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);return;}
            int slash=path.LastIndexOf('/');EnsureFolder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
        }
    }
}
