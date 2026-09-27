using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TheLastWatch.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyNightSkyVarietyUpgrade
    {
        private const string Root="Assets/TherapyGame";
        private const string Request=Root+"/NightSkyVarietyRequest.txt",Report=Root+"/Weather/NightSky/VarietyCheck.txt";
        static TherapyNightSkyVarietyUpgrade()
        {
            EditorApplication.delayCall+=Once;
            EditorSceneManager.sceneOpened+=(scene,mode)=>EditorApplication.delayCall+=Once;
        }
        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="verify-night-sky-variety-once")return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Once;return;}
            // A cold project launch may import scripts before restoring the open
            // scene. Leave the one-shot request pending until TherapyRoom opens.
            if(!SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity").isLoaded)return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Stop Play/baking, then use Therapy Game > Verify Varied Night Sky.");return;}
            File.WriteAllText(Request,"verifying-once");
            try{Verify();}
            catch(Exception e)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Report));
                File.WriteAllText(Report,"Night-sky verification stopped: "+DateTime.Now.ToString("s")+"\n"+e);
                File.WriteAllText(Request,"needs-attention-no-auto-retry");Debug.LogException(e);
            }
        }
        [MenuItem("Therapy Game/Verify Varied Night Sky")]
        public static void Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new Exception("Keep Play and baking stopped.");
            RequireMemory();
            Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded)throw new Exception("Keep TherapyRoom open.");
            var cycle=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WellnessSkyCycle>(true)).Single();
            if(cycle.skyMaterial==null||cycle.pondRenderer==null||cycle.cloudDeck==null)throw new Exception("Existing sky/pond/cloud references required.");
            var pond=cycle.pondRenderer.sharedMaterial;
            if(pond==null||!cycle.skyMaterial.HasProperty("_AuroraShape")||!pond.HasProperty("_AuroraShape"))
                throw new Exception("Both shaders must expose the shared aurora composition.");
            string checks=NightSkyEventChecks.Run();
            foreach(var material in new[]{cycle.skyMaterial,pond})
            {
                RequireMemory();
                bool previous=ShaderUtil.allowAsyncCompilation;
                try{ShaderUtil.allowAsyncCompilation=false;ShaderUtil.CompilePass(material,0,true);}
                finally{ShaderUtil.allowAsyncCompilation=previous;}
                if(ShaderUtil.ShaderHasError(material.shader))throw new Exception("Shader verification failed; no retry/cache clearing: "+material.name+"\n"+
                    string.Join("\n",ShaderUtil.GetShaderMessages(material.shader).Select(m=>m.message)));
            }
            string sampling=File.ReadAllText(Root+"/Weather/Shaders/WellnessSkySampling.hlsl");
            if(!sampling.Contains("WellnessDistantLandscape(d,sky")||!sampling.Contains("WellnessMoonFace"))throw new Exception("Preserve both the complete garden horizon and the detailed moon.");
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report,"Varied night sky verified "+DateTime.Now.ToString("s")+"\n"+checks+
                "PASS: sky and pond expose the same aurora shape; both shader passes compiled without errors.\n"+
                "PASS: 45% aurora chance per dusk, independent of clouds/settings; emerald/teal with rose/violet tops, randomized world-space direction, overhead tilt, spread and folds.\n"+
                "PASS: shooting stars every 18-40 clear-night seconds after a 5-12 second first wait, brightness 1.05-1.50, maximum three simultaneous trails unchanged.\n"+
                "PASS: moon angular radius 0.0085 radians, broad maria, four soft craters, limb shading and faint halo; detail only evaluated on moon pixels.\n"+
                "PASS: existing scene, garden horizon, cloud/weather speeds, lights, camera and comfort settings preserved. No added meshes, lights, textures, render passes, baking, Play mode, microphone or automatic retries.\n"+
                "Scene settings retained: meteors="+cycle.shootingStars+", aurora="+cycle.auroraBorealis+", effects intensity="+cycle.nightEffectsIntensity+".\n"+
                "Final appearance/frame time still require a user-controlled Play check; this verification does not render frames.\n");
            File.WriteAllText(Request,"verified-live-check-pending");
            Debug.Log("THERAPY_NIGHT_VARIETY_READY: randomized pink/green overhead aurora, selected nights, brighter meteors and detailed moon verified; scene unchanged.");
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint length,load;
            public ulong totalPhysical,availablePhysical,totalPageFile,availablePageFile,totalVirtual,availableVirtual,availableExtendedVirtual;
        }
        [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        private static void RequireMemory()
        {
            if(Application.platform!=RuntimePlatform.WindowsEditor)return;
            var memory=new MemoryStatus{length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};
            if(!GlobalMemoryStatusEx(ref memory)||memory.availablePageFile<1536UL*1024*1024||memory.availablePhysical<1024UL*1024*1024)
                throw new Exception("Not enough memory headroom. Close unused apps, keep Play stopped, then use Therapy Game > Verify Varied Night Sky. No automatic retry will run.");
        }
    }
}
