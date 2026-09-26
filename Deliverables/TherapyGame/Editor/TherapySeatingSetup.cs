using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TheLastWatch.Interaction;
using TheLastWatch.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapySeatingSetup
    {
        private const string Root = "Assets/TherapyGame";
        private const string Request = Root + "/SeatingRequest.txt";
        private const string Report = Root + "/Documentation/SeatingCheck.txt";
        private const string TestFlag = "Therapy.SeatingTest";
        private static WellnessExplorer player;
        private static CharacterController controller;
        private static (WellnessSeat seat, int spot)[] choices;
        private static int choice, stage;
        private static double stageTime;
        private static Vector3 initialPosition, initialCameraPosition, seatedPosition, walkStart;
        private static Quaternion initialRotation, initialCameraRotation, seatedRotation;
        private static float initialPitch;
        private static Keyboard keyboard;
        private static Mouse mouse;
        private static InputSettings originalSettings;
        private static InputSettings.EditorInputBehaviorInPlayMode originalEditorInput;
        private static InputSettings.BackgroundBehavior originalInputBackground;
        private static InputActionMap testMap;
        private static bool originalBackground;
        private static bool finishing;
        private static string runtimeError;
        private static readonly FieldInfo Pitch = typeof(WellnessExplorer).GetField("_pitch", BindingFlags.NonPublic | BindingFlags.Instance);

        static TherapySeatingSetup()
        {
            EditorApplication.delayCall += Dispatch;
            EditorApplication.playModeStateChanged += PlayState;
        }
        private static Transform Room()
        {
            Scene scene = SceneManager.GetSceneByPath(Root + "/Scenes/TherapyRoom.unity");
            if (!scene.isLoaded) throw new InvalidOperationException("Open the existing TherapyRoom scene first.");
            return scene.GetRootGameObjects().Single(g => g.name == "TherapyRoom").transform;
        }
        private static void Dispatch()
        {
            if (!File.Exists(Request)) return;
            string state = File.ReadAllText(Request).Trim();
            if (state != "pending" && state != "test") return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            { EditorApplication.delayCall += Dispatch; return; }
            File.WriteAllText(Request, "running");
            try { if (state == "pending") Install(); RunCheck(); }
            catch (Exception e) { File.WriteAllText(Request, "failed"); File.WriteAllText(Report, e.ToString()); Debug.LogException(e); }
        }
        [MenuItem("Therapy Game/Install Selectable Seats")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before installing seats.");
            Transform room = Room();
            Directory.CreateDirectory(Root + "/Seating/Backups"); Directory.CreateDirectory(Root + "/Documentation");
            string backup = Root + "/Seating/Backups/TherapyRoom_BeforeSeating.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(room.gameObject.scene, backup, true);
            Undo.RegisterFullObjectHierarchyUndo(room.gameObject, "Selectable therapy seats");
            InstallSeat(room.Find("Furniture/Sofa"), new[] {
                Spot("Sofa · left cushion", .51f), Spot("Sofa · right cushion", -.51f) });
            InstallSeat(room.Find("Furniture/Conversation chair A"), new[] { Spot("Left conversation chair", 0) });
            InstallSeat(room.Find("Furniture/Conversation chair B"), new[] { Spot("Right conversation chair", 0) });
            InstallSeat(room.Find("InteractiveObjects/ReflectionSeat/Window reflection chair"), new[] { Spot("Window reflection chair", 0) });
            EditorSceneManager.MarkSceneDirty(room.gameObject.scene); EditorSceneManager.SaveScene(room.gameObject.scene);
            TherapyGameTools.ValidateImportedRoom();
            WellnessSeat[] seats = room.GetComponentsInChildren<WellnessSeat>();
            if (seats.Length != 4 || seats.Sum(s => s.Count) != 5) throw new Exception("Expected four furniture pieces and five sitting spots.");
            File.WriteAllText(Report, "Installed 5 named seat spots on 4 existing furniture pieces. Existing materials, colliders, lighting and 7 original interactions retained.\nScene snapshot saved before seating components were added.\n");
        }
        private static WellnessSeat.Spot Spot(string label, float x) => new WellnessSeat.Spot {
            label = label, localBodyPosition = new Vector3(x, .04f, .12f), eyeHeight = 1.21f,
            localStandPosition = new Vector3(x, .04f, 1.18f)
        };
        private static void InstallSeat(Transform furniture, WellnessSeat.Spot[] spots)
        {
            if (furniture == null || furniture.GetComponent<Collider>() == null) throw new Exception("The expected seat furniture/collider was not found.");
            WellnessSeat seat = furniture.GetComponent<WellnessSeat>();
            if (seat == null) seat = Undo.AddComponent<WellnessSeat>(furniture.gameObject);
            seat.spots = spots; EditorUtility.SetDirty(seat); PrefabUtility.RecordPrefabInstancePropertyModifications(seat);
        }

        [MenuItem("Therapy Game/Verify Seats in Play Mode")]
        public static void RunCheck()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before running the seat check.");
            Room(); SessionState.SetBool(TestFlag, true);
            File.WriteAllText(Report, "Latest seating smoke check: " + DateTime.Now.ToString("s") + "\n5 sitting spots; 4 furniture pieces; original 7 reflection interactions retained.\n");
            File.WriteAllText(Request, "testing"); EditorApplication.EnterPlaymode();
        }
        private static void PlayState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(TestFlag, false)) return;
            if (state == PlayModeStateChange.ExitingPlayMode && !finishing)
                Finish(new OperationCanceledException("Seat check was stopped before completion."), false);
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                try
                {
                    player = Room().GetComponentInChildren<WellnessExplorer>(); controller = player.GetComponent<CharacterController>();
                    initialPosition = player.transform.position; initialRotation = player.transform.rotation;
                    initialCameraPosition = player.ViewCamera.transform.localPosition; initialCameraRotation = player.ViewCamera.transform.localRotation;
                    initialPitch = (float)Pitch.GetValue(player);
                    choices = Room().GetComponentsInChildren<WellnessSeat>().SelectMany(s => Enumerable.Range(0, s.Count).Select(i => (s, i))).ToArray();
                    if (choices.Length != 5) throw new Exception("Five sitting choices were not present in Play mode.");
                    originalSettings = InputSystem.settings;
                    originalEditorInput = originalSettings.editorInputBehaviorInPlayMode; originalInputBackground = originalSettings.backgroundBehavior;
                    // Transient properties are restored before leaving Play mode; no settings asset is saved.
                    originalSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    originalSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    originalBackground = Application.runInBackground; Application.runInBackground = true;
                    keyboard = InputSystem.AddDevice<Keyboard>("TherapySeatTestKeyboard"); mouse = InputSystem.AddDevice<Mouse>("TherapySeatTestMouse");
                    object input = typeof(WellnessExplorer).GetField("_input", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player);
                    testMap = (InputActionMap)input.GetType().GetField("_map", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(input);
                    testMap.devices = new InputDevice[] { keyboard, mouse };
                    runtimeError = null; Application.logMessageReceived += Log;
                    choice = stage = 0; stageTime = EditorApplication.timeSinceStartup;
                    EditorApplication.update -= Tick; EditorApplication.update += Tick;
                }
                catch (Exception e) { Finish(e); }
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(TestFlag, false);
                finishing = false;
                File.AppendAllText(Report, "Returned to Edit mode; input settings and original player pose restored.\n");
            }
        }
        private static void Log(string message, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) && (stack.Contains("TherapyGame") || stack.Contains("TheLastWatch"))) runtimeError = message;
        }
        private static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
        private static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static void PositionFor(WellnessSeat seat, int index)
        {
            var method = typeof(WellnessExplorer).GetMethod("StandingSpace", BindingFlags.NonPublic | BindingFlags.Instance);
            Vector3 position = default; bool found = false;
            for (int i = 0; i < 4; i++)
            {
                object[] arguments = { seat.ExitCandidate(index, i), Vector3.zero };
                if ((bool)method.Invoke(player, arguments)) { position = (Vector3)arguments[1]; found = true; break; }
            }
            Check(found, "No safe approach for " + seat.Label(index));
            controller.enabled = false; player.transform.position = position; controller.enabled = true;
            Quaternion look = Quaternion.LookRotation(seat.AimPosition(index) - player.ViewCamera.transform.position);
            player.transform.rotation = Quaternion.Euler(0, look.eulerAngles.y, 0);
            float pitch = Mathf.DeltaAngle(0, look.eulerAngles.x); Pitch.SetValue(player, pitch);
            player.ViewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0); Physics.SyncTransforms();
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - stageTime < .55) return;
            try
            {
                Check(runtimeError == null, "Runtime error: " + runtimeError);
                var (seat, index) = choices[choice];
                switch (stage)
                {
                    case 0:
                        PositionFor(seat, index); Keys(); break;
                    case 1:
                        Physics.Raycast(player.ViewCamera.ViewportPointToRay(new Vector3(.5f,.5f)), out RaycastHit aimed, 2.6f, ~0, QueryTriggerInteraction.Ignore);
                        Check(player.FocusedSeat == seat && player.FocusedSpot == index, $"Raycast selected the wrong spot: {seat.Label(index)}; focused={player.FocusedSeat?.name}/{player.FocusedSpot}; hit={aimed.collider?.name}; camera={player.ViewCamera.transform.position}, forward={player.ViewCamera.transform.forward}; released={typeof(WellnessExplorer).GetField("_released", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player)}; enabled={player.enabled}");
                        Check(!player.TrySit(seat, -1), "Invalid spot accepted.");
                        Keys(Key.E); break;
                    case 2:
                        Keys(); Check(player.IsSeated && !player.IsTransitioning && player.CurrentSeat == seat && player.CurrentSpot == index, "E did not sit on " + seat.Label(index));
                        Check(!controller.enabled, "Controller still collides while sitting.");
                        Check(Mathf.Abs(player.ViewCamera.transform.position.y - (seat.BodyPosition(index).y + seat.spots[index].eyeHeight)) < .01f, "Wrong seated eye height.");
                        seatedPosition = player.transform.position; seatedRotation = player.transform.rotation;
                        Keys(Key.W); InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(180, 0)); break;
                    case 3:
                        Keys(); Check(Vector3.Distance(seatedPosition, player.transform.position) < .005f, "WASD moved the seated player.");
                        Check(Quaternion.Angle(seatedRotation, player.transform.rotation) > 1, "Mouse look failed while seated.");
                        Check(!player.TrySit(seat, index), "Repeated sit accepted.");
                        if (choice == 0)
                        {
                            var obstacle = new GameObject("Temporary seating safety test") { hideFlags = HideFlags.HideAndDontSave };
                            try
                            {
                                obstacle.transform.position = new Vector3(0, 1.5f, 0); obstacle.AddComponent<BoxCollider>().size = new Vector3(20, 4, 20);
                                Physics.SyncTransforms(); Check(!player.TryStand() && player.IsSeated, "Blocked exit allowed the player to stand inside geometry.");
                            }
                            finally { Object.DestroyImmediate(obstacle); Physics.SyncTransforms(); }
                        }
                        Keys(Key.Escape); break;
                    case 4:
                        Keys(); Check(player.IsSeated && Cursor.lockState == CursorLockMode.None, "Escape did not release the cursor while seated.");
                        Keys(Key.Enter); break;
                    case 5:
                        Keys(); Check(player.IsSeated, "Resume unexpectedly stood up.");
                        Keys(choice == choices.Length - 1 ? Key.E : Key.Space); break;
                    case 6:
                        Keys(); Check(!player.IsSeated && !player.IsTransitioning && controller.enabled, "Stand-up failed: " + seat.Label(index));
                        Check(Vector3.Distance(player.ViewCamera.transform.localPosition, initialCameraPosition) < .005f, "Standing camera height was not restored.");
                        Check(player.transform.position.y > -.1f, "Player fell through floor.");
                        walkStart = player.transform.position; Keys(Key.W); break;
                    case 7:
                        Keys(); Check(Vector3.Distance(walkStart, player.transform.position) > .04f, "Walking did not resume after standing.");
                        File.AppendAllText(Report, "PASS " + seat.Label(index) + ": aimed E selection, correct eye height, seated mouse look, WASD locked, Esc/Enter cursor, stand-up and walking restored.\n");
                        choice++;
                        if (choice >= choices.Length) { Finish(null); return; }
                        stage = -1; break;
                }
                stage++; stageTime = EditorApplication.timeSinceStartup;
            }
            catch (Exception e) { Finish(e); }
        }
        private static void Finish(Exception error, bool exitPlayMode = true)
        {
            finishing = true;
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            if (testMap != null) { testMap.devices = null; testMap = null; }
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
            if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
            if (originalSettings != null)
            {
                originalSettings.editorInputBehaviorInPlayMode = originalEditorInput;
                originalSettings.backgroundBehavior = originalInputBackground;
            }
            Application.runInBackground = originalBackground;
            if (player != null)
            {
                player.enabled = false; controller.enabled = false;
                player.transform.SetPositionAndRotation(initialPosition, initialRotation);
                player.ViewCamera.transform.localPosition = initialCameraPosition; player.ViewCamera.transform.localRotation = initialCameraRotation;
                Pitch.SetValue(player, initialPitch); controller.enabled = true; player.enabled = true;
            }
            File.AppendAllText(Report, error == null ? "PASS: all five spots; invalid/repeated sit rejected; obstructed exits rejected; no therapy runtime errors.\n" : "FAIL: " + error + "\n");
            File.WriteAllText(Request, error == null ? "complete" : "failed");
            if (error == null) Debug.Log("THERAPY_SEATING_PLAY_PASS"); else Debug.LogException(error);
            if (exitPlayMode) EditorApplication.ExitPlaymode();
        }
    }
}
