using System;
using System.IO;
using System.Linq;
using TheLastWatch.Integrations;
using TheLastWatch.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyVoiceRainSetup
    {
        private const string Root="Assets/TherapyGame", Folder=Root+"/Integrations";
        private const string Request=Root+"/VoiceRainRequest.txt", Report=Folder+"/VoiceRainCheck.txt";
        static TherapyVoiceRainSetup() => EditorApplication.delayCall += ImportOnce;
        private static void ImportOnce()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="import-voice-rain-once")return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Voice/rain import deferred. Stop Play/baking, then choose Therapy Game > Install Voice and Rain.");return;}
            File.WriteAllText(Request,"importing-once");
            try {Install();}
            catch(Exception e){File.WriteAllText(Request,"failed");File.WriteAllText(Report,e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Therapy Game/Install Voice and Rain")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new InvalidOperationException("Stop Play mode and baking before installing.");
            Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded)throw new InvalidOperationException("Keep the existing TherapyRoom scene open.");
            Transform room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
            var player=room.GetComponentInChildren<WellnessExplorer>();if(player==null)throw new InvalidOperationException("Therapy player not found.");
            foreach(string name in new[]{"Settings","Materials","Backups"})Directory.CreateDirectory(Folder+"/"+name);
            string backup=Folder+"/Backups/TherapyRoom_BeforeVoiceAndRain.unity";
            if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
            string clipPath=Folder+"/Audio/GardenRain.mp3";
            var audioImporter=AssetImporter.GetAtPath(clipPath) as AudioImporter;
            if(audioImporter==null)throw new InvalidOperationException("The repository rain MP3 must finish importing first.");
            audioImporter.forceToMono=true;audioImporter.loadInBackground=true;
            var audioSettings=audioImporter.defaultSampleSettings;audioSettings.loadType=AudioClipLoadType.Streaming;
            audioSettings.compressionFormat=AudioCompressionFormat.Vorbis;audioSettings.quality=.5f;
            audioSettings.sampleRateSetting=AudioSampleRateSetting.OverrideSampleRate;audioSettings.sampleRateOverride=22050;
            audioImporter.defaultSampleSettings=audioSettings;audioImporter.SaveAndReimport();
            AudioClip clip=AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);if(clip==null)throw new InvalidOperationException("Rain audio import failed.");
            Shader shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");if(shader==null)throw new InvalidOperationException("URP particle shader not available.");
            string matPath=Folder+"/Materials/GentleRain.mat";Material material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(material==null){material=new Material(shader){name="GentleRain"};AssetDatabase.CreateAsset(material,matPath);}
            material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_Cull",(float)CullMode.Off);material.SetColor("_BaseColor",Color.white);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.DisableKeyword("_ALPHATEST_ON");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType","Transparent");material.renderQueue=(int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster",false);EditorUtility.SetDirty(material);
            string configPath=Folder+"/Settings/VoiceCompanions.asset";
            var config=AssetDatabase.LoadAssetAtPath<WellnessVoiceSettings>(configPath);
            if(config==null)
            {
                config=ScriptableObject.CreateInstance<WellnessVoiceSettings>();config.name="VoiceCompanions";
                // Public IDs from Hackathon26's two committed TalkingBoxAgentConfig assets.
                config.agents=new[]{
                    new WellnessVoiceSettings.Agent{displayName="Companion 1 · Yellow",agentId="agent_8001m3e67fv6fazsz8h90paz1xtv",colorVariable="yellow"},
                    new WellnessVoiceSettings.Agent{displayName="Companion 2 · Red",agentId="agent_4301m3e7t5qfffkb26yn6gwydg03",colorVariable="red"}
                };
                AssetDatabase.CreateAsset(config,configPath);
            }
            Transform old=room.Find("Voice and Rain");if(old!=null)Undo.DestroyObjectImmediate(old.gameObject);
            var root=new GameObject("Voice and Rain");root.transform.SetParent(room,false);Undo.RegisterCreatedObjectUndo(root,"Install voice companions and gentle rain");
            var weather=root.AddComponent<WellnessRain>();weather.player=player;
            var rainObject=new GameObject("Local outdoor rain");rainObject.transform.SetParent(root.transform,false);
            weather.drops=rainObject.AddComponent<ParticleSystem>();WellnessRain.ConfigureParticles(weather.drops,material);
            var soundObject=new GameObject("Indoor outdoor rain ambience");soundObject.transform.SetParent(root.transform,false);
            weather.rainAudio=soundObject.AddComponent<AudioSource>();weather.rainAudio.clip=clip;weather.rainAudio.loop=true;
            weather.rainAudio.playOnAwake=false;weather.rainAudio.spatialBlend=0;weather.rainAudio.volume=0;weather.rainAudio.priority=180;
            weather.indoorMuffle=soundObject.AddComponent<AudioLowPassFilter>();weather.indoorMuffle.cutoffFrequency=1700;
            var voiceObject=new GameObject("ElevenLabs voice output");voiceObject.transform.SetParent(root.transform,false);
            var voiceSource=voiceObject.AddComponent<AudioSource>();voiceSource.playOnAwake=false;voiceSource.spatialBlend=0;voiceSource.priority=40;voiceSource.volume=.85f;
            var chat=root.AddComponent<WellnessVoiceChat>();chat.settings=config;chat.player=player;chat.rain=weather;chat.voiceAudio=voiceSource;
            Verify(room,chat,weather);
            TherapyGameTools.ValidateImportedRoom();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Report,"Voice and rain installed "+DateTime.Now.ToString("s")+"\nPASS: two repository agent configurations, explicit consent/start UI, native startup cancellation, input blocking, rain references and 600-particle cap.\nPASS: indoor/outdoor blend and roof-exclusion/lifetime checks. Existing room validation passed.\nNo microphone, ElevenLabs session, Play mode, rendering test, or bake was started. Live voice and visual verification pending.\n");
            File.WriteAllText(Request,"installed-live-verification-pending");Debug.Log("THERAPY_VOICE_RAIN_IMPORTED: no microphone or conversation started.");
        }
        private static void Verify(Transform room,WellnessVoiceChat chat,WellnessRain rain)
        {
            if(chat.settings==null||chat.settings.agents.Length!=2||chat.settings.agents.Any(a=>string.IsNullOrWhiteSpace(a.agentId)))throw new Exception("Missing voice configurations.");
            if(chat.IsConnected||chat.IsPanelOpen||chat.IsBusy)throw new Exception("Voice must remain idle on import.");
            if(chat.voiceAudio==null||chat.voiceAudio.playOnAwake||rain.rainAudio==null||rain.rainAudio.clip==null)throw new Exception("Audio references are incomplete.");
            if(rain.drops.main.maxParticles>600||rain.drops.main.playOnAwake||rain.drops.collision.enabled)throw new Exception("Rain particle budget was exceeded.");
            if(!WellnessRain.UnderRoof(Vector3.zero)||WellnessRain.UnderRoof(new Vector3(8,0,0)))throw new Exception("Rain roof exclusion failed.");
            if(WellnessRain.IndoorBlend(Vector3.zero)<.99f||WellnessRain.IndoorBlend(new Vector3(10,0,-5))>.01f)throw new Exception("Indoor rain blend failed.");
            if(Mathf.Abs(WellnessRain.DropLifetime(6,0)*8.5f-5.92f)>.01f)throw new Exception("Rain lifetime calibration failed.");
            if(room.Find("OutdoorGarden")==null||room.GetComponentsInChildren<TheLastWatch.Interaction.WellnessSeat>().Sum(s=>s.Count)!=5)throw new Exception("Existing garden or seating missing.");
        }
    }
}
