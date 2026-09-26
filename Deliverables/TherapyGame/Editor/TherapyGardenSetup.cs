using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TheLastWatch.Environment;
using TheLastWatch.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyGardenSetup
    {
        const string Root = "Assets/TherapyGame", Folder = Root + "/Exterior";
        const string Request = Root + "/GardenRequest.txt", Report = Folder + "/GardenCheck.txt", Flag = "Therapy.GardenCheck";
        [Serializable] public class MatData { public string name, hex; }
        [Serializable] public class MeshData { public string name, material; public bool castShadow; public float[] positions, normals; }
        [Serializable] public class BoxData { public string name; public float[] p, s; public float yaw; }
        [Serializable] public class FlyData { public float[] p; public float radius, phase, scale, flapHz, orbitSpeed; public int color; }
        [Serializable] public class GardenData { public int version; public string units; public MatData[] materials; public MeshData[] meshes, colliders; public BoxData[] boxes; public FlyData[] butterflies; public MeshData wing, body; public Vector3[] route; }
        // Only a specifically requested, single-use import is allowed on reload.
        // Never launch previews, baking or Play mode as an import side effect.
        static TherapyGardenSetup() { EditorApplication.playModeStateChanged += PlayState; EditorApplication.delayCall += ImportOnce; }
        static Transform Room()
        {
            Scene scene = SceneManager.GetSceneByPath(Root + "/Scenes/TherapyRoom.unity");
            if (!scene.isLoaded) throw new Exception("Keep the TherapyRoom scene open before importing the garden.");
            return scene.GetRootGameObjects().Single(g => g.name == "TherapyRoom").transform;
        }
        static void ImportOnce()
        {
            if (!File.Exists(Request)) return;
            string request = File.ReadAllText(Request).Trim();
            if (request != "import-low-load-once" && request != "refine-garden-once") return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            { File.WriteAllText(Request, "manual-only"); Debug.LogWarning("Garden import deferred: stop Play/baking, then use Therapy Game > Install Outdoor Garden."); return; }
            // Consume first so a failure or domain reload can never repeat the operation.
            File.WriteAllText(Request, "importing-once");
            try { Install(); }
            catch (Exception e) { File.WriteAllText(Request, "failed"); File.AppendAllText(Report, e + "\n"); Debug.LogException(e); }
        }
        static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);
        static GameObject Child(string name, Transform parent) { var g = new GameObject(name); g.transform.SetParent(parent, false); return g; }
        static Mesh MakeMesh(MeshData data, string name, bool doubleSided = false)
        {
            int count = data.positions.Length / 3;
            var vertices = new Vector3[count]; var normals = new Vector3[count]; var triangles = new int[count * (doubleSided ? 2 : 1)];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(data.positions[i * 3], data.positions[i * 3 + 1], data.positions[i * 3 + 2]);
                if (data.normals != null && data.normals.Length == data.positions.Length) normals[i] = new Vector3(data.normals[i * 3], data.normals[i * 3 + 1], data.normals[i * 3 + 2]);
            }
            // Coordinates are authored directly in the room's space, not transformed from glTF.
            // Retain cross-product normals and winding together so ground faces upward for physics.
            for (int i = 0; i < count; i += 3) { triangles[i] = i; triangles[i + 1] = i + 1; triangles[i + 2] = i + 2; if (doubleSided) { triangles[count + i] = i; triangles[count + i + 1] = i + 2; triangles[count + i + 2] = i + 1; } }
            string path = Folder + "/Meshes/" + name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices; mesh.triangles = triangles;
            if (data.normals != null && data.normals.Length == data.positions.Length) mesh.normals = normals; else mesh.RecalculateNormals();
            mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh); return mesh;
        }
        [MenuItem("Therapy Game/Install Outdoor Garden")]
        public static void Install()
        {
            Transform room = Room();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode before importing.");
            GardenData data = JsonUtility.FromJson<GardenData>(File.ReadAllText(Folder + "/Source/GardenModel.json"));
            if (data.version != 2 || data.units != "metres" || data.body == null) throw new Exception("Expected the refined garden export (version 2).");
            Shader waterShader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/Shaders/QuietPond.shader");
            if (waterShader == null || ShaderUtil.GetShaderMessages(waterShader).Any(m => m.severity.ToString() == "Error")) throw new Exception("The lightweight pond shader must import without errors first.");
            foreach (string d in new[] { "Meshes", "Materials", "Backups", "Previews", "Settings" }) Directory.CreateDirectory(Folder + "/" + d);
            AssetDatabase.Refresh();
            string backup = Folder + "/Backups/TherapyRoom_BeforeGarden.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(room.gameObject.scene, backup, true);
            string refinementBackup = Folder + "/Backups/TherapyRoom_BeforeGardenRefinements.unity";
            if (!File.Exists(refinementBackup)) EditorSceneManager.SaveScene(room.gameObject.scene, refinementBackup, true);
            Undo.RegisterFullObjectHierarchyUndo(room.gameObject, "Add explorable garden");
            Transform old = room.Find("OutdoorGarden"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            Transform garden = Child("OutdoorGarden", room).transform;
            Undo.RegisterCreatedObjectUndo(garden.gameObject, "Add garden");
            var materials = new Dictionary<string, Material>();
            foreach (MatData d in data.materials)
            {
                string path = Folder + "/Materials/" + d.name + ".mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                Shader shader = d.name == "water" ? waterShader : Shader.Find("Universal Render Pipeline/Lit");
                if (m == null) { m = new Material(shader) { name = d.name }; AssetDatabase.CreateAsset(m, path); }
                m.shader = shader;
                ColorUtility.TryParseHtmlString("#" + d.hex, out Color c); m.SetColor("_BaseColor", c);
                if (d.name == "water")
                {
                    m.shaderKeywords = new string[0]; m.renderQueue = (int)RenderQueue.Transparent;
                    m.SetColor("_BaseColor", new Color(.12f,.39f,.42f)); m.SetFloat("_Opacity", .64f); m.SetFloat("_RippleStrength", .055f);
                }
                else { m.SetFloat("_Smoothness", .08f); m.SetFloat("_Cull", (float)CullMode.Back); }
                m.enableInstancing = true; materials[d.name] = m; EditorUtility.SetDirty(m);
            }
            foreach (MeshData d in data.meshes)
            {
                var g = Child(d.name.Replace("__", " · "), garden); Mesh mesh = MakeMesh(d, d.name.Replace(' ', '_'));
                g.AddComponent<MeshFilter>().sharedMesh = mesh; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = materials[d.material];
                r.shadowCastingMode = d.castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            Transform collision = Child("Walkable surfaces and safety", garden).transform;
            foreach (MeshData d in data.colliders) Child(d.name, collision).AddComponent<MeshCollider>().sharedMesh = MakeMesh(d, d.name.Replace(' ', '_'));
            foreach (BoxData d in data.boxes)
            {
                GameObject g = Child(d.name, collision); g.transform.localPosition = V(d.p); g.transform.localRotation = Quaternion.Euler(0, d.yaw, 0); g.AddComponent<BoxCollider>().size = V(d.s);
            }
            Mesh wing = MakeMesh(data.wing, "Butterfly_wing", true);
            Mesh butterflyBody = MakeMesh(data.body, "Butterfly_body");
            Transform flies = Child("Butterflies", garden).transform;
            foreach (FlyData d in data.butterflies)
            {
                GameObject g = Child("Meadow butterfly", flies); var motion = g.AddComponent<WellnessButterfly>(); motion.home = V(d.p); motion.radius = d.radius; motion.phase = d.phase;
                g.transform.localScale = Vector3.one * d.scale; motion.flapHz = d.flapHz; motion.orbitSpeed = d.orbitSpeed;
                Material mat = materials[new[] { "butterflyBlue", "butterflyGold", "butterflyLilac" }[d.color]];
                foreach (bool left in new[] { true, false })
                {
                    GameObject w = Child(left ? "Left wing" : "Right wing", g.transform); w.AddComponent<MeshFilter>().sharedMesh = wing;
                    var renderer = w.AddComponent<MeshRenderer>(); renderer.sharedMaterial = mat; renderer.shadowCastingMode = ShadowCastingMode.Off;
                    if (left) { w.transform.localScale = new Vector3(-1, 1, 1); motion.leftWing = w.transform; } else motion.rightWing = w.transform;
                }
                var body = Child("Body and forward-facing head", g.transform); body.AddComponent<MeshFilter>().sharedMesh = butterflyBody;
                var bodyRenderer = body.AddComponent<MeshRenderer>(); bodyRenderer.sharedMaterial = materials["body"]; bodyRenderer.shadowCastingMode = ShadowCastingMode.Off; motion.Sample(0);
            }
            AddFrontWindow(room);
            Transform backdrop = room.Find("Architecture/Window/Quiet landscape beyond window"); if (backdrop != null) backdrop.gameObject.SetActive(false);
            Transform door = room.Find("Architecture/Door"); Transform pivot = door.Find("Open door hinge");
            if (pivot == null)
            {
                pivot = Child("Open door hinge", door).transform; pivot.position = new Vector3(-3.05f, 0, -3.08f);
                foreach (Transform t in door.Cast<Transform>().ToArray()) if (t.name == "Closed oak entrance door" || t.name == "Door handle")
                { t.SetParent(pivot, true); GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0); var r = t.GetComponent<Renderer>(); if (r != null) r.lightmapIndex = -1; }
            }
            pivot.localRotation = Quaternion.Euler(0, 105, 0);
            WellnessExplorer player = room.GetComponentInChildren<WellnessExplorer>();
            if (player.GetComponent<WellnessGardenSafety>() == null) player.gameObject.AddComponent<WellnessGardenSafety>();
            player.ViewCamera.farClipPlane = 220;
            var atmosphere = player.ViewCamera.GetComponent<WellnessAtmosphere>(); if (atmosphere != null) atmosphere.gardenTransition = true;
            // Clone the room-only profile rather than overwriting its high-quality assets.
            // Existing indoor lightmaps and sun are kept; no baking or reflection renders.
            ConfigureLowLoad(room);
            Physics.SyncTransforms();
            if (!Physics.Raycast(new Vector3(-2.43f, 4, -6), Vector3.down, out RaycastHit ground, 8) || ground.normal.y < .7f)
                throw new Exception("Garden terrain must have an upward-facing collision surface.");
            string refinementChecks = VerifyRefinements(room, garden, player);
            File.WriteAllText(Report, "Refined Three.js garden installed " + DateTime.Now.ToString("s") + "\n" + data.meshes.Length + " mesh batches; " + data.butterflies.Length + " small animated butterflies; " + data.boxes.Length + " box colliders.\nShallow walk-in pond, lily-pad frog, natural trail stones, and real front window.\n" + refinementChecks + "\nOriginal indoor scene backed up; window backdrop disabled, not deleted.\nNo rendering, Play mode, or baking started by this import.\n");
            TherapyGameTools.ValidateImportedRoom(); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(room.gameObject.scene); EditorSceneManager.SaveScene(room.gameObject.scene);
            File.WriteAllText(Request, "refinements-installed-live-check-pending");
            Debug.Log("THERAPY_GARDEN_REFINED: shallow pond and forward-facing butterflies passed edit-mode checks; no previews or Play mode started.");
        }

        static void AddFrontWindow(Transform room)
        {
            Transform architecture = room.Find("Architecture");
            Transform originalWall = architecture.Find("Walls/Entry front wall");
            if (originalWall == null) throw new Exception("Could not find the existing entry wall; leaving it unchanged.");
            Material plaster = originalWall.GetComponent<Renderer>().sharedMaterial;
            Transform existing = architecture.Find("Front garden window");
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
            Undo.RecordObject(originalWall.gameObject, "Preserve entry wall behind new window opening");
            originalWall.gameObject.SetActive(false); // Retained for recovery, not deleted or modified.
            Transform window = Child("Front garden window", architecture).transform;
            Undo.RegisterCreatedObjectUndo(window.gameObject, "Add front garden window");
            CreatePanel("Plaster left of window", window, new Vector3(-.925f,1.5f,-3.1f), new Vector3(1.75f,3,.2f), plaster);
            CreatePanel("Plaster right of window", window, new Vector3(2.625f,1.5f,-3.1f), new Vector3(1.75f,3,.2f), plaster);
            CreatePanel("Plaster below window", window, new Vector3(.85f,.56f,-3.1f), new Vector3(1.8f,1.12f,.2f), plaster);
            CreatePanel("Plaster above window", window, new Vector3(.85f,2.675f,-3.1f), new Vector3(1.8f,.65f,.2f), plaster);
            string path = Folder + "/Materials/FrontWindowGlass.mat";
            Material glass = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (glass == null) { glass = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "FrontWindowGlass" }; AssetDatabase.CreateAsset(glass,path); }
            glass.SetColor("_BaseColor", new Color(.79f,.90f,.88f,.10f)); glass.SetFloat("_Surface",1); glass.SetFloat("_Blend",0);
            glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite",0); glass.SetFloat("_Cull",(float)CullMode.Back); glass.SetFloat("_Smoothness",.72f);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            glass.SetOverrideTag("RenderType","Transparent"); glass.renderQueue=(int)RenderQueue.Transparent;
            glass.SetShaderPassEnabled("ShadowCaster",false); EditorUtility.SetDirty(glass);
            var pane = CreatePanel("Clear front window pane",window,new Vector3(.85f,1.735f,-3.12f),new Vector3(1.67f,1.11f,.018f),glass);
            pane.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        static GameObject CreatePanel(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=name; g.transform.SetParent(parent,false);
            g.transform.localPosition=position; g.transform.localScale=size;
            var renderer=g.GetComponent<Renderer>(); renderer.sharedMaterial=material; renderer.lightmapIndex=-1;
            renderer.lightProbeUsage=LightProbeUsage.Off; renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return g;
        }
        static string VerifyRefinements(Transform room, Transform garden, WellnessExplorer player)
        {
            var terrain = garden.Find("Walkable surfaces and safety/Walkable terrain").GetComponent<MeshCollider>();
            float deepest=0;
            for(float x=8.4f;x<21.7f;x+=.6f) for(float z=-10.6f;z<.7f;z+=.6f)
            {
                if(new Vector2((x-15)/6.7f,(z+5)/5.7f).sqrMagnitude>1)continue;
                if(!terrain.Raycast(new Ray(new Vector3(x,2,z),Vector3.down),out RaycastHit hit,5))throw new Exception("Missing pond floor.");
                deepest=Mathf.Max(deepest,-.31f-hit.point.y);
                if(deepest>.50f)throw new Exception("Pond is deeper than the intended shallow wading limit.");
            }
            // Capsule clearance at short intervals along the south bank, without entering Play mode.
            var controller=player.GetComponent<CharacterController>(); float radius=controller.radius;
            for(float z=-12.4f;z<=-8.4f;z+=.2f)
            {
                if(!terrain.Raycast(new Ray(new Vector3(15,3,z),Vector3.down),out RaycastHit floor,6)||floor.normal.y<.72f)throw new Exception("South pond approach is not gently walkable.");
                Vector3 bottom=floor.point+Vector3.up*(radius+.05f),top=floor.point+Vector3.up*(controller.height-radius+.05f);
                foreach(var obstruction in Physics.OverlapCapsule(bottom,top,radius,~0,QueryTriggerInteraction.Ignore))
                    if(obstruction!=terrain&&!obstruction.transform.IsChildOf(player.transform))throw new Exception("Pond entry is blocked by "+obstruction.name);
            }
            foreach(var butterfly in garden.GetComponentsInChildren<WellnessButterfly>())
            {
                for(int i=0;i<8;i++)
                {
                    float time=i*1.7f;butterfly.Sample(time);Vector3 a=butterfly.transform.localPosition,forward=butterfly.transform.localRotation*Vector3.back;
                    butterfly.Sample(time+.001f);Vector3 tangent=butterfly.transform.localPosition-a;tangent.y=0;
                    if(Vector3.Dot(forward,tangent.normalized)<.995f)throw new Exception("Butterfly facing does not follow flight direction.");
                }
                butterfly.Sample(0);
                if(butterfly.transform.localScale.x<.12f||butterfly.transform.localScale.x>.151f)throw new Exception("Butterfly scale is outside the small size range.");
            }
            if(room.Find("Architecture/Walls/Entry front wall").gameObject.activeSelf)throw new Exception("Original wall still blocks new window.");
            return "PASS edit-mode physics: pond depth <= "+deepest.ToString("F2")+" m; gentle south-bank capsule clearance; 13 butterflies face their flight tangents. Live walking/visual check still pending.";
        }

        static void ConfigureLowLoad(Transform room)
        {
            const string original = Root + "/Realism/Settings";
            string rendererPath = Folder + "/Settings/GardenLowLoadRenderer.asset", pipelinePath = Folder + "/Settings/GardenLowLoadURP.asset";
            if (!File.Exists(rendererPath) && !AssetDatabase.CopyAsset(original + "/RealismRenderer.asset", rendererPath)) throw new Exception("Could not copy the room renderer.");
            if (!File.Exists(pipelinePath) && !AssetDatabase.CopyAsset(original + "/RealismURP.asset", pipelinePath)) throw new Exception("Could not copy the room pipeline.");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            foreach (var feature in renderer.rendererFeatures) { feature.SetActive(false); EditorUtility.SetDirty(feature); }
            var rendererSerialized = new SerializedObject(renderer);
            rendererSerialized.FindProperty("m_RenderingMode").intValue = 0; // Forward: no clustered-light buffers.
            rendererSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            var serialized = new SerializedObject(pipeline); serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            pipeline.renderScale = .85f; pipeline.msaaSampleCount = 2; pipeline.mainLightShadowmapResolution = 1024;
            pipeline.additionalLightsShadowmapResolution = 512; pipeline.shadowCascadeCount = 2; pipeline.shadowDistance = 30; pipeline.maxAdditionalLightsCount = 4;
            EditorUtility.SetDirty(pipeline); room.GetComponent<WellnessScenePipeline>().pipeline = pipeline;
            if (room.GetComponent<WellnessGardenPerformance>() == null) room.gameObject.AddComponent<WellnessGardenPerformance>();
        }

        static readonly Vector3[] WalkRoute = {
            new Vector3(-2.43f,0,-6),new Vector3(1,0,-10),new Vector3(6,0,-12),new Vector3(15,0,-13.5f),new Vector3(23,0,-12),
            new Vector3(26,0,-5),new Vector3(23,0,3),new Vector3(15,0,5),new Vector3(7,0,3),new Vector3(4,0,-3),new Vector3(-2.43f,0,-6),
            new Vector3(1,0,-10),new Vector3(6,0,-12),new Vector3(6,0,-5.9f),new Vector3(24,0,-5.9f),new Vector3(26,0,-5),
            new Vector3(23,0,-12),new Vector3(15,0,-13.5f),new Vector3(6,0,-12),new Vector3(1,0,-10),new Vector3(-2.43f,0,-6),new Vector3(-2.43f,0,-1.75f)
        };
        static WellnessExplorer testPlayer; static CharacterController testController; static Keyboard keyboard; static Mouse mouse;
        static InputActionMap map; static InputSettings settings; static InputSettings.EditorInputBehaviorInPlayMode editorInput;
        static InputSettings.BackgroundBehavior background; static bool runBackground, finishing; static int waypoint; static double startTime, sectionTime;
        static Vector3 originalPosition, originalCameraPosition; static Quaternion originalRotation, originalCameraRotation; static float originalPitch;
        static string runtimeError; static readonly FieldInfo Pitch = typeof(WellnessExplorer).GetField("_pitch", BindingFlags.Instance | BindingFlags.NonPublic);
        [MenuItem("Therapy Game/Verify Garden Walk in Play Mode")]
        public static void RunCheck() { Room(); SessionState.SetBool(Flag, true); File.WriteAllText(Request, "testing"); EditorApplication.EnterPlaymode(); }
        static void PlayState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Flag, false)) return;
            if (state == PlayModeStateChange.ExitingPlayMode && !finishing) Finish(new Exception("Garden test interrupted."), false);
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                try
                {
                    testPlayer = Room().GetComponentInChildren<WellnessExplorer>(); testController = testPlayer.GetComponent<CharacterController>();
                    originalPosition = testPlayer.transform.position; originalRotation = testPlayer.transform.rotation;
                    originalCameraPosition = testPlayer.ViewCamera.transform.localPosition; originalCameraRotation = testPlayer.ViewCamera.transform.localRotation; originalPitch = (float)Pitch.GetValue(testPlayer);
                    settings = InputSystem.settings; editorInput = settings.editorInputBehaviorInPlayMode; background = settings.backgroundBehavior;
                    settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    runBackground = Application.runInBackground; Application.runInBackground = true;
                    keyboard = InputSystem.AddDevice<Keyboard>("GardenTestKeyboard"); mouse = InputSystem.AddDevice<Mouse>("GardenTestMouse");
                    object input = typeof(WellnessExplorer).GetField("_input", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(testPlayer);
                    map = (InputActionMap)input.GetType().GetField("_map", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(input); map.devices = new InputDevice[] { keyboard, mouse };
                    testController.enabled = false; testPlayer.transform.position = new Vector3(-2.43f, .15f, -1.75f); testController.enabled = true; Pitch.SetValue(testPlayer, 0f);
                    waypoint = 0; sectionTime = startTime = EditorApplication.timeSinceStartup; runtimeError = null; Application.logMessageReceived += Log;
                    File.AppendAllText(Report, "Play-mode walking check started: actual WASD input + CharacterController, doorway, entire loop, bridge, return indoors.\n");
                    EditorApplication.update += Tick;
                }
                catch (Exception e) { Finish(e); }
            }
            if (state == PlayModeStateChange.EnteredEditMode) { SessionState.SetBool(Flag, false); finishing = false; File.AppendAllText(Report, "Returned to Edit mode; input settings and original player pose restored.\n"); }
        }
        static void Log(string message, string stack, LogType type) { if ((type == LogType.Error || type == LogType.Exception) && (stack.Contains("TherapyGame") || stack.Contains("TheLastWatch"))) runtimeError = message; }
        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (runtimeError != null) throw new Exception(runtimeError);
                if (EditorApplication.timeSinceStartup - sectionTime > 35) throw new Exception("Walking blocked at waypoint " + waypoint + " position " + testPlayer.transform.position);
                Vector3 direction = WalkRoute[waypoint] - testPlayer.transform.position; direction.y = 0;
                if (direction.magnitude < .22f)
                {
                    File.AppendAllText(Report, "PASS waypoint " + waypoint + ": " + testPlayer.transform.position + "\n");
                    if (++waypoint == WalkRoute.Length) { Finish(null); return; } sectionTime = EditorApplication.timeSinceStartup; direction = WalkRoute[waypoint] - testPlayer.transform.position; direction.y = 0;
                }
                testPlayer.transform.rotation = Quaternion.LookRotation(direction); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            }
            catch (Exception e) { Finish(e); }
        }
        static void Finish(Exception error, bool exit = true)
        {
            finishing = true; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            if (keyboard != null) { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.RemoveDevice(keyboard); keyboard = null; }
            if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; } if (map != null) map.devices = null;
            if (settings != null) { settings.editorInputBehaviorInPlayMode = editorInput; settings.backgroundBehavior = background; } Application.runInBackground = runBackground;
            if (testController != null) { testController.enabled = false; testPlayer.transform.SetPositionAndRotation(originalPosition, originalRotation); testPlayer.ViewCamera.transform.SetLocalPositionAndRotation(originalCameraPosition, originalCameraRotation); Pitch.SetValue(testPlayer, originalPitch); testController.enabled = true; }
            File.WriteAllText(Request, error == null ? "complete" : "failed");
            File.AppendAllText(Report, error == null ? "PASS: doorway exit, full garden loop, bridge crossing and return indoors. Elapsed " + (EditorApplication.timeSinceStartup - startTime).ToString("F1") + " seconds.\n" : error + "\n");
            if (exit) EditorApplication.ExitPlaymode();
        }
        [MenuItem("Therapy Game/Render Garden Previews")]
        public static void RenderPreviews()
        {
            Render("GardenOverview", new Vector3(37,19,-29), new Vector3(9,0,-3));
            Render("FromDoor", new Vector3(-2.43f,1.7f,-5.5f), new Vector3(14,1,-6));
            Render("Bridge", new Vector3(7,1.7f,-5.9f), new Vector3(24,1.5f,-5.9f));
            Render("WindowView", new Vector3(1.5f,1.65f,0), new Vector3(16,1,-3));
        }
        static void Render(string name, Vector3 position, Vector3 target)
        {
            var room = Room(); var previous = QualitySettings.renderPipeline; RenderTexture oldTarget = RenderTexture.active;
            var go = new GameObject("Temporary garden preview") { hideFlags = HideFlags.HideAndDontSave }; var camera = go.AddComponent<Camera>();
            camera.transform.position = position; camera.transform.LookAt(target); camera.fieldOfView = 64; camera.nearClipPlane = .05f; camera.farClipPlane = 220; camera.allowHDR = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true; go.AddComponent<WellnessAtmosphere>().gardenTransition = true;
            var rt = new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB); Texture2D texture = null;
            try
            {
                QualitySettings.renderPipeline = room.GetComponent<WellnessScenePipeline>().pipeline; camera.targetTexture = rt; camera.aspect = 16f / 9;
                camera.Render(); camera.Render(); camera.Render(); RenderTexture.active = rt;
                texture = new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); texture.Apply(); File.WriteAllBytes(Folder + "/Previews/" + name + ".png",texture.EncodeToPNG());
            }
            finally { RenderTexture.active = oldTarget; camera.targetTexture = null; QualitySettings.renderPipeline = previous; if (texture != null) Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go); }
        }
    }
}
