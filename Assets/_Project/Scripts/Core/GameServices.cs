using System;
using TheLastWatch.Biometrics;
using TheLastWatch.Dialogue;
using TheLastWatch.Input;
using TheLastWatch.Voice;
using UnityEngine.InputSystem;

namespace TheLastWatch.Core
{
    public static class GameServices
    {
        public static bool IsInitialized { get; private set; }
        public static IGameConfiguration Configuration { get; private set; }
        public static IProjectLogger Logger { get; private set; }
        public static GameStateController State { get; private set; }
        public static ISceneTransitionService Scenes { get; private set; }
        public static PauseStateManager Pause { get; private set; }
        public static IGameInput Input { get; private set; }
        public static IVoiceProvider Voice { get; private set; }
        public static ISpeechRecognitionProvider SpeechRecognition { get; private set; }
        public static IBiometricProvider Biometrics { get; private set; }
        public static IDialogueService Dialogue { get; private set; }

        public static void Initialize(GameConfiguration configuration, InputActionAsset inputActions)
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException("Game services are already initialized.");
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (inputActions == null)
            {
                throw new ArgumentNullException(nameof(inputActions));
            }

            Configuration = configuration;
            Logger = new UnityProjectLogger(configuration.VerboseLogging);
            State = new GameStateController();
            Input = new UnityGameInput(inputActions);
            Scenes = new SceneTransitionService(State, Logger);
            Pause = new PauseStateManager(State);
            Voice = new NullVoiceProvider();
            SpeechRecognition = new NullSpeechRecognitionProvider();
            Biometrics = new NullBiometricProvider();
            Dialogue = new NullDialogueService();

            Input.PausePerformed += Pause.Toggle;
            Input.Enable();
            State.SetState(GameState.Bootstrapping);
            IsInitialized = true;
            Logger.Info($"Services initialized for {configuration.BuildConfiguration}.");
        }

        public static void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            Input.PausePerformed -= Pause.Toggle;
            Input.Dispose();
            Pause.Dispose();

            Configuration = null;
            Logger = null;
            State = null;
            Scenes = null;
            Pause = null;
            Input = null;
            Voice = null;
            SpeechRecognition = null;
            Biometrics = null;
            Dialogue = null;
            IsInitialized = false;
        }
    }
}
