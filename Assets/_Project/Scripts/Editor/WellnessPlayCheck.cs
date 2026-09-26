using System;
using System.IO;
using TheLastWatch.Environment;
using TheLastWatch.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TheLastWatch.Editor
{
    // Batch smoke check uses the real input adapter and CharacterController in Play mode.
    [InitializeOnLoad]
    internal static class WellnessPlayCheck
    {
        private const string Pending = "Wellness.PlayCheck";
        private static int _stage;
        private static double _stageStart;
        private static WellnessExplorer _player;
        private static Vector3 _start, _orbScale;
        private static bool _failed;
        private static Keyboard _keyboard;
        static WellnessPlayCheck()
        {
            EditorApplication.playModeStateChanged += OnState;
            if (SessionState.GetBool(Pending, false)) EditorApplication.update += Tick;
        }
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(TherapyRoomBuilder.ScenePath);
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Pending + ".Failed", false);
            EditorApplication.EnterPlaymode();
        }
        private static void OnState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _stage = 0; _stageStart = EditorApplication.timeSinceStartup;
                EditorApplication.update -= Tick; EditorApplication.update += Tick;
                Application.logMessageReceived += Log;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Pending, false);
                EditorApplication.Exit(SessionState.GetBool(Pending + ".Failed", false) ? 1 : 0);
            }
        }
        private static void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error) _failed = true;
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - _stageStart < .8) return;
            try
            {
                if (_stage == 0)
                {
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    Application.runInBackground = true;
                    _player = UnityEngine.Object.FindAnyObjectByType<WellnessExplorer>();
                    if (_player == null) throw new Exception("Explorer did not enter Play mode.");
                    _keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
                    _start = _player.transform.position;
                    _orbScale = UnityEngine.Object.FindAnyObjectByType<WellnessBreathingOrb>().transform.localScale;
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W));
                }
                else if (_stage == 1)
                {
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                    if (Vector3.Distance(_start, _player.transform.position) < .25f) throw new Exception("WASD input did not move the player.");
                    if (_player.transform.position.y < -.15f) throw new Exception("Player fell through the floor.");
                    CharacterController controller = _player.GetComponent<CharacterController>();
                    controller.enabled = false; _player.transform.position = new Vector3(-2.7f, .04f, -2f); _player.transform.rotation = Quaternion.Euler(0, -90, 0); controller.enabled = true;
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W));
                }
                else if (_stage == 2)
                {
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                    if (_player.transform.position.x < -3.29f) throw new Exception("Player crossed the left wall collider.");
                    var orb = UnityEngine.Object.FindAnyObjectByType<WellnessBreathingOrb>();
                    if (Vector3.Distance(_orbScale, orb.transform.localScale) < .001f) throw new Exception("Breathing orb did not animate.");
                    if (_failed) throw new Exception("Runtime logged errors; inspect WellnessPlay.log.");
                    File.WriteAllText("Documentation/Wellness/PlayCheck.txt", "PASS: Play mode started; real WASD input moved the player; floor supported the controller; wall collision stopped movement; breathing orb animated; no runtime errors during the check.\n");
                    Debug.Log("WELLNESS_PLAY_PASS"); Finish(); return;
                }
                _stage++; _stageStart = EditorApplication.timeSinceStartup;
            }
            catch (Exception e)
            {
                SessionState.SetBool(Pending + ".Failed", true);
                File.WriteAllText("Documentation/Wellness/PlayCheckFailed.txt", e.ToString());
                Debug.LogException(e); Finish();
            }
        }
        private static void Finish()
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            EditorApplication.ExitPlaymode();
        }
    }
}
