using System;
using System.Collections.Generic;
using System.IO;
using TheLastWatch.Biometrics;
using TheLastWatch.Core;
using TheLastWatch.Dialogue;
using TheLastWatch.Input;
using TheLastWatch.Voice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastWatch.Editor
{
    public static class MilestoneTwoValidator
    {
        private static readonly string[] SceneNames =
        {
            "Bootstrap",
            "MainMenu",
            "RangerLookout",
            "DevelopmentSandbox"
        };

        private static readonly string[] ActionNames =
        {
            InputActionNames.Move,
            InputActionNames.Look,
            InputActionNames.Interact,
            InputActionNames.Sprint,
            InputActionNames.Flashlight,
            InputActionNames.Pause,
            InputActionNames.PushToTalk,
            InputActionNames.Submit,
            InputActionNames.Cancel
        };

        [MenuItem("The Last Watch/Validate Milestone 2")]
        public static void ValidateFromMenu()
        {
            ValidateOrThrow();
        }

        public static void ValidateOrThrow()
        {
            var failures = new List<string>();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                failures.Add("Active build target is not Windows 64-bit standalone.");
            }

            for (int index = 0; index < SceneNames.Length; index++)
            {
                string expectedPath = MilestoneTwoSetup.ScenePath(SceneNames[index]);
                if (!File.Exists(expectedPath))
                {
                    failures.Add($"Missing scene: {expectedPath}");
                    continue;
                }

                if (EditorBuildSettings.scenes.Length <= index ||
                    !string.Equals(EditorBuildSettings.scenes[index].path, expectedPath, StringComparison.Ordinal))
                {
                    failures.Add($"Scene '{SceneNames[index]}' is not enabled at build index {index}.");
                }
            }

            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(MilestoneTwoSetup.InputAssetPath);
            if (input == null)
            {
                failures.Add("Input action asset could not be loaded.");
            }
            else
            {
                foreach (string actionName in ActionNames)
                {
                    if (input.FindAction(actionName, false) == null)
                    {
                        failures.Add($"Missing input action: {actionName}");
                    }
                }
            }

            GameConfiguration development =
                AssetDatabase.LoadAssetAtPath<GameConfiguration>(MilestoneTwoSetup.DevelopmentConfigPath);
            GameConfiguration release =
                AssetDatabase.LoadAssetAtPath<GameConfiguration>(MilestoneTwoSetup.ReleaseConfigPath);
            if (development == null || release == null)
            {
                failures.Add("Development or release configuration asset is missing.");
            }

            ValidateBootstrapReferences(input, development, failures);
            ValidatePlaceholderProviders(failures);

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "Milestone 2 validation failed:\n- " + string.Join("\n- ", failures));
            }

            Debug.Log("[The Last Watch] Milestone 2 validation passed.");
        }

        private static void ValidateBootstrapReferences(
            InputActionAsset input,
            GameConfiguration development,
            ICollection<string> failures)
        {
            string path = MilestoneTwoSetup.ScenePath("Bootstrap");
            if (!File.Exists(path))
            {
                return;
            }

            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            GameBootstrap bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null)
            {
                failures.Add("Bootstrap scene does not contain GameBootstrap.");
                return;
            }

            if (bootstrap.InputActions != input)
            {
                failures.Add("GameBootstrap input reference is invalid.");
            }

            if (bootstrap.Configuration != development)
            {
                failures.Add("GameBootstrap configuration reference is invalid.");
            }
        }

        private static void ValidatePlaceholderProviders(ICollection<string> failures)
        {
            if (new NullVoiceProvider().IsAvailable)
            {
                failures.Add("Null voice provider unexpectedly reports availability.");
            }

            if (new NullSpeechRecognitionProvider().IsAvailable)
            {
                failures.Add("Null speech provider unexpectedly reports availability.");
            }

            if (new NullBiometricProvider().IsAvailable)
            {
                failures.Add("Null biometric provider unexpectedly reports availability.");
            }

            if (new NullDialogueService().IsAvailable)
            {
                failures.Add("Null dialogue provider unexpectedly reports availability.");
            }
        }
    }
}
