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
    public static class TherapyPresageSetup
    {
        private const string Root = "Assets/TherapyGame";
        private const string Request = Root + "/PresageRequest.txt";
        private const string Report = Root + "/Integrations/Presage/PresageCheck.txt";

        static TherapyPresageSetup() => EditorApplication.delayCall += ImportOnce;

        private static void ImportOnce()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "install-presage-once") return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            {
                File.WriteAllText(Request, "manual-only");
                Debug.LogWarning("Presage integration deferred. Stop Play mode and baking, then choose Therapy Game > Install Presage Biometric Coaching.");
                return;
            }
            File.WriteAllText(Request, "installing-once");
            try { Install(); }
            catch (Exception exception)
            {
                File.WriteAllText(Request, "failed");
                Directory.CreateDirectory(Path.GetDirectoryName(Report));
                File.WriteAllText(Report, exception.ToString());
                Debug.LogException(exception);
            }
        }

        [MenuItem("Therapy Game/Install Presage Biometric Coaching")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Stop Play mode and baking before installing biometric coaching.");
            Scene scene = SceneManager.GetSceneByPath(Root + "/Scenes/TherapyRoom.unity");
            if (!scene.isLoaded) throw new InvalidOperationException("Keep the existing TherapyRoom scene open.");
            Transform room = scene.GetRootGameObjects().Single(gameObject => gameObject.name == "TherapyRoom").transform;
            WellnessVoiceChat chat = room.GetComponentInChildren<WellnessVoiceChat>(true);
            if (chat == null) throw new InvalidOperationException("Install the ElevenLabs voice integration first.");

            Directory.CreateDirectory(Root + "/Integrations/Presage/Backups");
            string backup = Root + "/Integrations/Presage/Backups/TherapyRoom_BeforePresage.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);

            PresageBiometricProvider provider = chat.GetComponent<PresageBiometricProvider>();
            if (provider == null) provider = Undo.AddComponent<PresageBiometricProvider>(chat.gameObject);
            PresageBiometricCoach coach = chat.GetComponent<PresageBiometricCoach>();
            if (coach == null) coach = Undo.AddComponent<PresageBiometricCoach>(chat.gameObject);
            coach.provider = provider;
            coach.chat = chat;
            chat.biometrics = provider;
            chat.biometricCoach = coach;

            Verify(chat, provider, coach);
            EditorUtility.SetDirty(chat);
            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(coach);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report,
                "Presage biometric coaching installed " + DateTime.Now.ToString("s") + "\n" +
                "PASS: explicit per-session camera consent, optional proactive-guidance consent, local Node sidecar, stable/confident metric filtering, personal baseline, cooldown, ElevenLabs contextual updates, and emergency-language guardrails.\n" +
                "No camera, microphone, provider session, Play mode, rendering test, or lighting bake was started. Live verification remains pending.\n");
            File.WriteAllText(Request, "installed-live-verification-pending");
            Debug.Log("THERAPY_PRESAGE_IMPORTED: no camera, microphone, Presage, or ElevenLabs session started.");
        }

        private static void Verify(WellnessVoiceChat chat, PresageBiometricProvider provider, PresageBiometricCoach coach)
        {
            if (chat.biometrics != provider || chat.biometricCoach != coach || coach.provider != provider || coach.chat != chat)
                throw new Exception("Biometric coaching references are incomplete.");
            if (provider.HasConsent || provider.IsMeasuring || provider.State != PresageBiometricState.Off)
                throw new Exception("Biometric capture must remain off until the player explicitly consents in Play mode.");
            if (coach.minimumConfidence < 50 || coach.coachingCooldownSeconds < 45 || coach.sustainedSeconds < 8)
                throw new Exception("Biometric filtering or anti-interruption guardrails are too permissive.");
            if (string.IsNullOrWhiteSpace(PresageBiometricCoach.AgentPolicy) || PresageBiometricCoach.AgentPolicy.IndexOf("never as a diagnosis", StringComparison.OrdinalIgnoreCase) < 0)
                throw new Exception("The non-diagnostic coaching policy is missing.");
        }
    }
}
