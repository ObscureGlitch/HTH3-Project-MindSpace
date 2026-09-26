using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheLastWatch.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TheLastWatch.Editor
{
    [InitializeOnLoad]
    internal static class MilestoneTwoFirstImport
    {
        private const string AttemptedSessionKey = "TheLastWatch.MilestoneTwoSetupAttempted";

        static MilestoneTwoFirstImport()
        {
            if (!File.Exists(MilestoneTwoSetup.ScenePath("Bootstrap")))
            {
                EditorApplication.delayCall += ConfigureWhenReady;
            }
        }

        private static void ConfigureWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ConfigureWhenReady;
                return;
            }

            if (SessionState.GetBool(AttemptedSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(AttemptedSessionKey, true);
            MilestoneTwoSetup.ConfigureProject();
        }
    }

    public static class MilestoneTwoSetup
    {
        public const string InputAssetPath = "Assets/_Project/Config/GameInput.inputactions";
        public const string DevelopmentConfigPath = "Assets/_Project/Config/GameConfiguration.Development.asset";
        public const string ReleaseConfigPath = "Assets/_Project/Config/GameConfiguration.Release.asset";

        private static readonly string[] RequiredFolders =
        {
            "Assets/_Project/Art/Materials",
            "Assets/_Project/Art/Models",
            "Assets/_Project/Art/Textures",
            "Assets/_Project/Audio/Ambience",
            "Assets/_Project/Audio/Dialogue",
            "Assets/_Project/Audio/Music",
            "Assets/_Project/Audio/SFX",
            "Assets/_Project/Config",
            "Assets/_Project/Dialogue",
            "Assets/_Project/Prefabs/Environment",
            "Assets/_Project/Prefabs/Gameplay",
            "Assets/_Project/Prefabs/UI",
            "Assets/_Project/Scenes",
            "Assets/_Project/Scripts/Audio",
            "Assets/_Project/Scripts/Biometrics",
            "Assets/_Project/Scripts/Core",
            "Assets/_Project/Scripts/Dialogue",
            "Assets/_Project/Scripts/Editor",
            "Assets/_Project/Scripts/Environment",
            "Assets/_Project/Scripts/Input",
            "Assets/_Project/Scripts/Interaction",
            "Assets/_Project/Scripts/Player",
            "Assets/_Project/Scripts/UI",
            "Assets/_Project/Scripts/Voice",
            "Assets/_Project/UI"
        };

        private static readonly SceneDefinition[] RequiredScenes =
        {
            new SceneDefinition("Bootstrap", SceneRole.Bootstrap),
            new SceneDefinition("MainMenu", SceneRole.MainMenu),
            new SceneDefinition("RangerLookout", SceneRole.Gameplay),
            new SceneDefinition("DevelopmentSandbox", SceneRole.Development)
        };

        [MenuItem("The Last Watch/Configure Milestone 2")]
        public static void ConfigureProject()
        {
            try
            {
                EnsureFolders();
                ConfigurePlayerSettings();
                CreateInputAssetIfMissing();
                CreateConfigurationAssetsIfMissing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                CreateScenesIfMissing();
                ConfigureBuildScenes();
                AssetDatabase.SaveAssets();
                MilestoneTwoValidator.ValidateOrThrow();
                Debug.Log("[The Last Watch] Milestone 2 project setup completed successfully.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        private static void EnsureFolders()
        {
            foreach (string folder in RequiredFolders)
            {
                Directory.CreateDirectory(folder);
            }

            AssetDatabase.Refresh();
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "The Last Watch Team";
            PlayerSettings.productName = "The Last Watch";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.thelastwatch.game");

            UnityEngine.Object[] settingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settingsAssets.Length > 0)
            {
                var settings = new SerializedObject(settingsAssets[0]);
                SerializedProperty activeInputHandler = settings.FindProperty("activeInputHandler");
                if (activeInputHandler != null)
                {
                    activeInputHandler.intValue = 1;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            }
        }

        private static void CreateInputAssetIfMissing()
        {
            if (File.Exists(InputAssetPath))
            {
                return;
            }

            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "GameInput";

            InputActionMap gameplay = asset.AddActionMap("Gameplay");
            InputAction move = gameplay.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");

            InputAction look = gameplay.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick");

            AddButton(gameplay, "Interact", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            AddButton(gameplay, "Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            AddButton(gameplay, "Flashlight", "<Keyboard>/f", "<Gamepad>/dpad/up");
            AddButton(gameplay, "PushToTalk", "<Keyboard>/v", "<Gamepad>/leftShoulder");

            InputActionMap system = asset.AddActionMap("System");
            AddButton(system, "Pause", "<Keyboard>/escape", "<Gamepad>/start");

            InputActionMap ui = asset.AddActionMap("UI");
            AddButton(ui, "Submit", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
            AddButton(ui, "Cancel", "<Keyboard>/escape", "<Gamepad>/buttonEast");

            File.WriteAllText(InputAssetPath, asset.ToJson());
            UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(InputAssetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void AddButton(InputActionMap map, string name, string keyboardPath, string gamepadPath)
        {
            InputAction action = map.AddAction(name, InputActionType.Button, expectedControlLayout: "Button");
            action.AddBinding(keyboardPath);
            action.AddBinding(gamepadPath);
        }

        private static void CreateConfigurationAssetsIfMissing()
        {
            CreateConfigurationIfMissing(
                DevelopmentConfigPath,
                BuildConfiguration.Development,
                autoLoad: true,
                verbose: true);

            CreateConfigurationIfMissing(
                ReleaseConfigPath,
                BuildConfiguration.Release,
                autoLoad: true,
                verbose: false);
        }

        private static void CreateConfigurationIfMissing(
            string path,
            BuildConfiguration buildConfiguration,
            bool autoLoad,
            bool verbose)
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfiguration>(path) != null)
            {
                return;
            }

            GameConfiguration configuration = ScriptableObject.CreateInstance<GameConfiguration>();
            var serialized = new SerializedObject(configuration);
            serialized.FindProperty("buildConfiguration").enumValueIndex = (int)buildConfiguration;
            serialized.FindProperty("initialSceneName").stringValue = "MainMenu";
            serialized.FindProperty("autoLoadInitialScene").boolValue = autoLoad;
            serialized.FindProperty("verboseLogging").boolValue = verbose;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(configuration, path);
        }

        private static void CreateScenesIfMissing()
        {
            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            GameConfiguration development = AssetDatabase.LoadAssetAtPath<GameConfiguration>(DevelopmentConfigPath);

            foreach (SceneDefinition definition in RequiredScenes)
            {
                string path = ScenePath(definition.Name);
                if (File.Exists(path))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var contextObject = new GameObject("Scene Context");
                SceneContext context = contextObject.AddComponent<SceneContext>();
                var contextSerialized = new SerializedObject(context);
                contextSerialized.FindProperty("role").enumValueIndex = (int)definition.Role;
                contextSerialized.ApplyModifiedPropertiesWithoutUndo();

                if (definition.Role == SceneRole.Bootstrap)
                {
                    var bootstrapObject = new GameObject("Game Bootstrap");
                    GameBootstrap bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
                    var bootstrapSerialized = new SerializedObject(bootstrap);
                    bootstrapSerialized.FindProperty("configuration").objectReferenceValue = development;
                    bootstrapSerialized.FindProperty("inputActions").objectReferenceValue = input;
                    bootstrapSerialized.ApplyModifiedPropertiesWithoutUndo();
                }

                EditorSceneManager.SaveScene(scene, path);
            }
        }

        private static void ConfigureBuildScenes()
        {
            string[] requiredPaths = RequiredScenes.Select(scene => ScenePath(scene.Name)).ToArray();
            IEnumerable<EditorBuildSettingsScene> extras = EditorBuildSettings.scenes
                .Where(scene => !requiredPaths.Contains(scene.path, StringComparer.OrdinalIgnoreCase));

            EditorBuildSettings.scenes = requiredPaths
                .Select(path => new EditorBuildSettingsScene(path, true))
                .Concat(extras)
                .ToArray();
        }

        public static string ScenePath(string sceneName)
        {
            return $"Assets/_Project/Scenes/{sceneName}.unity";
        }

        private readonly struct SceneDefinition
        {
            public SceneDefinition(string name, SceneRole role)
            {
                Name = name;
                Role = role;
            }

            public string Name { get; }
            public SceneRole Role { get; }
        }
    }
}
