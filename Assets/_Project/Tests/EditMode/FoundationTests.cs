using NUnit.Framework;
using TheLastWatch.Biometrics;
using TheLastWatch.Core;
using TheLastWatch.Dialogue;
using TheLastWatch.Input;
using TheLastWatch.Voice;
using UnityEditor;
using UnityEngine.InputSystem;

namespace TheLastWatch.Tests.EditMode
{
    public sealed class FoundationTests
    {
        private const string SceneRoot = "Assets/_Project/Scenes/";
        private const string InputPath = "Assets/_Project/Config/GameInput.inputactions";

        [Test]
        public void BootstrapIsFirstBuildScene()
        {
            Assert.That(EditorBuildSettings.scenes, Is.Not.Empty);
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(SceneRoot + "Bootstrap.unity"));
            Assert.That(EditorBuildSettings.scenes[0].enabled, Is.True);
        }

        [Test]
        public void AllRequiredInputActionsExist()
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            Assert.That(asset, Is.Not.Null);

            string[] names =
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

            foreach (string name in names)
            {
                Assert.That(asset.FindAction(name, false), Is.Not.Null, name);
            }
        }

        [Test]
        public void PlaceholderProvidersAreOfflineAndUnavailable()
        {
            Assert.That(new NullVoiceProvider().IsAvailable, Is.False);
            Assert.That(new NullSpeechRecognitionProvider().IsAvailable, Is.False);
            Assert.That(new NullBiometricProvider().IsAvailable, Is.False);
            Assert.That(new NullDialogueService().IsAvailable, Is.False);
        }

        [Test]
        public void DevelopmentAndReleaseConfigurationsExist()
        {
            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameConfiguration>(
                    "Assets/_Project/Config/GameConfiguration.Development.asset"),
                Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameConfiguration>(
                    "Assets/_Project/Config/GameConfiguration.Release.asset"),
                Is.Not.Null);
        }
    }
}
