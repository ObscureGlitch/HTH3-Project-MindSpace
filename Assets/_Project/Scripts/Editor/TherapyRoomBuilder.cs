using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheLastWatch.Environment;
using TheLastWatch.Interaction;
using TheLastWatch.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TheLastWatch.Editor
{
    public static class TherapyRoomBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/TherapyRoom.unity";
        private const string Art = "Assets/_Project/Art/Materials/Wellness";
        private const string Prefabs = "Assets/_Project/Prefabs/Environment/Wellness";
        private const string Config = "Assets/_Project/Config/Wellness";
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static Transform _root, _architecture, _furniture, _decor, _plants, _interactive, _lighting;
        private static Camera _camera;

        [MenuItem("The Last Watch/Wellness/Create Therapy Room")]
        public static void CreateRoom()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("TherapyRoom already exists. Opened the saved scene; your scene edits are preserved.");
                return;
            }
            Generate();
        }

        // Batch entry point: will never replace an existing authored scene.
        public static void GenerateBatch()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("TherapyRoom already exists; use ValidateBatch or open it in Unity.");
            Generate();
        }

        private static void Generate()
        {
            foreach (string path in new[] { Art, Prefabs, Config, WellnessGeometry.Folder, "Documentation/Wellness" }) Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
            Materials.Clear(); WellnessGeometry.Cache.Clear();
            ConfigurePipeline(); CreateMaterials();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _root = Group("TherapyRoom");
            _architecture = Group("Architecture", _root); _furniture = Group("Furniture", _root);
            _decor = Group("Decorations", _root); _plants = Group("Plants", _root);
            _interactive = Group("InteractiveObjects", _root); _lighting = Group("Lighting", _root);
            BuildArchitecture(); BuildFurniture(); BuildPlants(); BuildInteractiveAreas(); BuildLighting(); BuildPlayer();
            BuildAudioPlaceholders();
            // Static flags support a compact baked scene and automatic batching.
            foreach (MeshRenderer renderer in _root.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.GetComponent<WellnessBreathingOrb>() != null) continue;
                bool outside = renderer.transform.IsChildOf(_architecture.Find("Window/Quiet landscape beyond window"));
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, outside ? StaticEditorFlags.BatchingStatic : StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Validate();
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, 1, .25f), Quaternion.Euler(25, 38, 0), 7f);
            Debug.Log("WELLNESS_GENERATED: " + ScenePath);
        }

        private static void ConfigurePipeline()
        {
            string path = Config + "/WellnessURP.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (pipeline == null)
            {
                var data = ScriptableObject.CreateInstance<UniversalRendererData>();
                data.name = "WellnessRenderer"; data.renderingMode = RenderingMode.ForwardPlus;
                AssetDatabase.CreateAsset(data, Config + "/WellnessRenderer.asset");
                // SSAO is internal in URP 17; serialize the supported renderer feature.
                Type aoType = typeof(UniversalRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                if (aoType != null)
                {
                    var ao = (ScriptableRendererFeature)ScriptableObject.CreateInstance(aoType);
                    ao.name = "Soft contact shadows";
                    AssetDatabase.AddObjectToAsset(ao, data);
                    var serialized = new SerializedObject(ao);
                    serialized.FindProperty("m_Settings.Intensity").floatValue = .45f;
                    serialized.FindProperty("m_Settings.Radius").floatValue = .22f;
                    serialized.FindProperty("m_Settings.DirectLightingStrength").floatValue = .12f;
                    serialized.FindProperty("m_Settings.Downsample").boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    data.rendererFeatures.Add(ao); EditorUtility.SetDirty(data);
                }
                pipeline = UniversalRenderPipelineAsset.Create(data);
                pipeline.name = "WellnessURP";
                pipeline.msaaSampleCount = 4; pipeline.renderScale = 1f; pipeline.supportsHDR = true;
                pipeline.shadowDistance = 18f; pipeline.shadowCascadeCount = 2;
                pipeline.mainLightShadowmapResolution = 2048;
                var pipelineSettings = new SerializedObject(pipeline);
                pipelineSettings.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
                pipelineSettings.FindProperty("m_ReflectionProbeBlending").boolValue = true;
                pipelineSettings.FindProperty("m_ReflectionProbeBoxProjection").boolValue = true;
                pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(pipeline, path);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = pipeline; }
            QualitySettings.SetQualityLevel(current, false);
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out Color c); return c; }
        private static Material Mat(string name, string hex, float smoothness = .2f, string texture = null, bool twoSided = false, float emission = 0)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            Color color = Hex(hex); m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smoothness);
            if (twoSided) m.SetFloat("_Cull", 0);
            if (texture != null) m.SetTexture("_BaseMap", MakeTexture(texture));
            if (emission > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * emission); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive; }
            string path = Art + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { EditorUtility.CopySerialized(m, existing); Object.DestroyImmediate(m); m = existing; }
            else AssetDatabase.CreateAsset(m, path);
            Materials[name] = m; return m;
        }

        private static Texture2D MakeTexture(string kind)
        {
            string path = Art + "/" + kind + ".asset";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            const int n = 256;
            var texture = existing != null ? existing : new Texture2D(n, n, TextureFormat.RGB24, true) { name = kind, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float noise = Mathf.PerlinNoise(x * .23f, y * .23f), value = .94f;
                if (kind == "OakGrain") value = .94f + .03f * Mathf.PerlinNoise(x * .10f, y * .006f) + .012f * Mathf.Sin(x * .28f + Mathf.PerlinNoise(x * .02f, y * .014f) * 7f);
                else if (kind == "LinenWeave") value = .96f + .015f * noise + .016f * Mathf.Sin(x * 1.57f) * Mathf.Sin(y * 1.57f);
                else if (kind == "RugWeave") value = .93f + .035f * noise + .035f * Mathf.Sin(x * .785f) * Mathf.Cos(y * 1.57f);
                else value = .98f + .015f * noise;
                pixels[y * n + x] = new Color(value, value, value);
            }
            texture.SetPixels(pixels); texture.Apply(true, false);
            if (existing == null) AssetDatabase.CreateAsset(texture, path); else EditorUtility.SetDirty(texture);
            return texture;
        }

        private static void CreateMaterials()
        {
            Mat("WarmPlaster", "E6DFCF", .08f, "Plaster"); Mat("SageWall", "919F88", .08f, "Plaster");
            Mat("Ceiling", "E8E2D6", .06f); Mat("Oak", "CBA77C", .25f, "OakGrain");
            Mat("OakLight", "D9BA92", .23f, "OakGrain"); Mat("OakMid", "C0A17F", .23f, "OakGrain");
            Mat("OakDark", "AF916E", .23f, "OakGrain"); Mat("Linen", "DFD9CA", .13f, "LinenWeave");
            Mat("Cream", "EAE2D0", .14f, "LinenWeave"); Mat("SageFabric", "899B81", .11f, "LinenWeave");
            Mat("Rug", "DCD0B5", .08f, "RugWeave"); Mat("RugBorder", "BCB299", .1f, "RugWeave");
            Mat("Throw", "8D9788", .08f, "RugWeave", true); Mat("Curtain", "E5DFCD", .1f, "LinenWeave", true);
            Mat("Leaf", "647C49", .24f, null, true); Mat("LeafLight", "8A9B60", .23f, null, true);
            Mat("LeafDark", "4F6948", .22f, null, true); Mat("Stem", "75805A", .15f);
            Mat("Pot", "C2B49D", .26f, "Plaster"); Mat("Terracotta", "BD8E73", .18f, "Plaster");
            Mat("Soil", "635C4A", .04f); Mat("Basket", "BAA17B", .1f, "RugWeave");
            Mat("Ceramic", "E5DFD1", .4f); Mat("Stone", "AAAFA8", .27f, "Plaster");
            Mat("Metal", "666457", .43f); Mat("Brass", "A18E65", .55f); Mat("Paper", "E6DDC8", .06f);
            Mat("BookSage", "91A088", .14f); Mat("BookClay", "B79783", .14f); Mat("BookSand", "C4BAA3", .14f);
            Mat("LampShade", "EEE0BB", .1f, "LinenWeave", true, .22f);
            Mat("AmberGlow", "FFE1A6", .2f, null, false, 1.5f); Mat("Canvas", "E9DFC9", .05f);
            Mat("ArtClay", "BA9C80", .1f); Mat("DistantHill", "A1B5AD", .02f);
            Mat("NearHill", "819C88", .03f); Mat("Sky", "D3DFDB", .02f, null, false, .35f);
            Mat("Water", "B1C6BD", .3f); Mat("Tree", "718A70", .1f);
        }

        private static Transform Group(string name, Transform parent = null, Vector3 position = default)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = position; return g.transform;
        }
        private static GameObject MeshObject(string name, Transform parent, Vector3 position, Mesh mesh, string material, bool collision = false)
        {
            Transform g = Group(name, parent, position); g.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = g.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = Materials[material];
            if (collision) { var box = g.gameObject.AddComponent<BoxCollider>(); box.center = mesh.bounds.center; box.size = mesh.bounds.size; }
            return g.gameObject;
        }
        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, string material, float radius = .025f, bool collision = false)
            => MeshObject(name, parent, position, WellnessGeometry.RoundBox(size, radius), material, collision);
        private static GameObject Round(string name, Transform parent, Vector3 position, float radius, float height, string material, bool collision = false)
        {
            float b = Mathf.Min(.04f, height * .18f);
            var profile = new[] { new Vector2(0, 0), new Vector2(radius - b, 0), new Vector2(radius, b), new Vector2(radius, height - b), new Vector2(radius - b, height), new Vector2(0, height) };
            return MeshObject(name, parent, position, WellnessGeometry.Lathe($"Round_{radius:F3}_{height:F3}", profile), material, collision);
        }
        private static GameObject Sphere(string name, Transform parent, Vector3 position, Vector3 size, string material)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere); g.name = name; g.transform.SetParent(parent, false);
            g.transform.localPosition = position; g.transform.localScale = size; Object.DestroyImmediate(g.GetComponent<Collider>());
            g.GetComponent<MeshRenderer>().sharedMaterial = Materials[material]; return g;
        }
        private static void Rod(string name, Transform parent, Vector3 start, Vector3 end, float radius, string material)
        {
            GameObject rod = Round(name, parent, (start + end) * .5f, radius, (end - start).magnitude, material);
            rod.transform.rotation = parent.rotation * Quaternion.FromToRotation(Vector3.up, end - start);
            rod.transform.localPosition = start;
        }

        private static void BuildArchitecture()
        {
            Transform floor = Group("Floor", _architecture);
            Box("Floor slab", floor, new Vector3(0, -.12f, 0), new Vector3(7.2f, .24f, 6.2f), "OakMid", .01f, true);
            // Long boards in alternating lengths, with hairline seams.
            for (int row = 0; row < 28; row++)
            {
                float x = -3.5f + (row + .5f) * .25f;
                float start = -3f;
                for (int board = 0; start < 2.999f; board++)
                {
                    float len = Mathf.Min(board == 0 ? .7f + (row % 3) * .49f : 1.47f, 3f - start);
                    string mat = new[] { "Oak", "OakLight", "OakMid", "Oak" }[(row * 3 + board) % 4];
                    Box("Oak board", floor, new Vector3(x, .009f, start + len * .5f), new Vector3(.248f, .018f, len - .003f), mat, .002f);
                    start += len;
                }
            }
            Transform walls = Group("Walls", _architecture);
            Box("Sage accent wall", walls, new Vector3(0, 1.5f, 3.1f), new Vector3(7.2f, 3, .2f), "SageWall", .01f, true);
            Box("Left plaster wall", walls, new Vector3(-3.6f, 1.5f, 0), new Vector3(.2f, 3, 6), "WarmPlaster", .01f, true);
            // Right wall has a real opening, not an opaque plane over the glass.
            Box("Window lower wall", walls, new Vector3(3.6f, .46f, 0), new Vector3(.2f, .92f, 6), "WarmPlaster", .01f, true);
            Box("Window upper wall", walls, new Vector3(3.6f, 2.89f, 0), new Vector3(.2f, .22f, 6), "WarmPlaster", .01f, true);
            Box("Window front pier", walls, new Vector3(3.6f, 1.85f, -2.4f), new Vector3(.2f, 1.86f, 1.2f), "WarmPlaster", .01f, true);
            Box("Window rear pier", walls, new Vector3(3.6f, 1.85f, 2.4f), new Vector3(.2f, 1.86f, 1.2f), "WarmPlaster", .01f, true);
            Box("Entry left pier", walls, new Vector3(-3.28f, 1.5f, -3.1f), new Vector3(.44f, 3, .2f), "WarmPlaster", .01f, true);
            Box("Entry front wall", walls, new Vector3(.85f, 1.5f, -3.1f), new Vector3(5.3f, 3, .2f), "WarmPlaster", .01f, true);
            Box("Door lintel", walls, new Vector3(-2.43f, 2.71f, -3.1f), new Vector3(1.26f, .58f, .2f), "WarmPlaster", .01f, true);
            Box("Rear baseboard", walls, new Vector3(0, .065f, 2.972f), new Vector3(7, .13f, .045f), "Oak", .006f);
            foreach (float side in new[] { -1f, 1f }) Box("Side baseboard", walls, new Vector3(side * 3.475f, .065f, 0), new Vector3(.045f, .13f, 6), "Oak", .006f);
            Box("Front baseboard", walls, new Vector3(.85f, .065f, -2.973f), new Vector3(5.3f, .13f, .045f), "Oak", .006f);
            Transform ceiling = Group("Ceiling", _architecture);
            Box("Warm ceiling", ceiling, new Vector3(0, 3.09f, 0), new Vector3(7.2f, .18f, 6.2f), "Ceiling", .01f, true);
            Transform window = Group("Window", _architecture, new Vector3(3.47f, 0, 0));
            Box("Oak sill", window, new Vector3(-.07f, .93f, 0), new Vector3(.36f, .07f, 3.75f), "OakLight", .025f);
            foreach (float z in new[] { -1.79f, 0f, 1.79f }) Box("Window mullion", window, new Vector3(0, 1.85f, z), new Vector3(.07f, 1.84f, .045f), "Metal", .006f);
            foreach (float y in new[] { .97f, 2.76f }) Box("Window frame", window, new Vector3(0, y, 0), new Vector3(.07f, .055f, 3.64f), "Metal", .006f);
            var pane = Group("Invisible window safety collider", window, new Vector3(.04f, 1.85f, 0)).gameObject.AddComponent<BoxCollider>(); pane.size = new Vector3(.04f, 1.83f, 3.6f);
            for (int i = 0; i < 9; i++)
            {
                GameObject slat = Box("Light oak blind slat", window, new Vector3(-.11f, 2.69f - i * .061f, 0), new Vector3(.06f, .014f, 3.52f), "OakLight", .004f);
                slat.transform.localRotation = Quaternion.Euler(0, 0, -15);
            }
            foreach (float z in new[] { -1.42f, 1.42f }) Rod("Blind cord", window, new Vector3(-.13f, 2.72f, z), new Vector3(-.13f, 2.14f, z), .004f, "Cream");
            foreach (float z in new[] { -1.91f, 1.91f })
            {
                GameObject curtain = MeshObject("Linen curtain", window, new Vector3(-.20f, 2.88f, z), WellnessGeometry.Cloth("CurtainMesh", .49f, 2.72f, false), "Curtain");
                curtain.transform.localRotation = Quaternion.Euler(0, -90, 0);
            }
            Rod("Curtain rod", window, new Vector3(-.20f, 2.91f, -2.2f), new Vector3(-.20f, 2.91f, 2.2f), .016f, "Metal");
            Transform door = Group("Door", _architecture);
            foreach (float x in new[] { -3.09f, -1.77f }) Box("Oak door frame", door, new Vector3(x, 1.22f, -3f), new Vector3(.07f, 2.44f, .22f), "Oak", .01f);
            Box("Oak door header", door, new Vector3(-2.43f, 2.45f, -3f), new Vector3(1.4f, .09f, .22f), "Oak", .01f);
            Box("Closed oak entrance door", door, new Vector3(-2.43f, 1.2f, -3.08f), new Vector3(1.24f, 2.4f, .065f), "OakLight", .025f, true);
            Rod("Door handle", door, new Vector3(-1.94f, 1.05f, -2.99f), new Vector3(-2.09f, 1.05f, -2.99f), .014f, "Metal");
            BuildLandscape(window);
        }

        private static void BuildLandscape(Transform window)
        {
            Transform outside = Group("Quiet landscape beyond window", window);
            Box("Sky backdrop", outside, new Vector3(17, 5, 0), new Vector3(.3f, 22, 45), "Sky", .01f);
            Box("Still lake", outside, new Vector3(9, -.20f, 0), new Vector3(22, .05f, 38), "Water", .01f);
            for (int i = 0; i < 9; i++)
                Sphere("Soft distant hill", outside, new Vector3(13, -.4f + Mathf.Sin(i) * .3f, -18 + i * 4.5f), new Vector3(4, 4 + i % 3, 9), "DistantHill");
            for (int i = 0; i < 7; i++)
            {
                float z = -10 + i * 3.5f, x = 6 + i % 2;
                Rod("Distant tree trunk", outside, new Vector3(x, -.3f, z), new Vector3(x, 2.8f, z), .055f, "OakDark");
                Sphere("Soft tree crown", outside, new Vector3(x, 2.7f, z), new Vector3(1.8f, 2.1f, 2.2f), "Tree");
                Sphere("Lower tree crown", outside, new Vector3(x, 1.9f, z + .35f), new Vector3(1.6f, 1.4f, 1.9f), "NearHill");
            }
        }

        private static void SavePrefab(Transform model, string name)
        {
            string path = Prefabs + "/" + name + ".prefab";
            if (!File.Exists(path)) PrefabUtility.SaveAsPrefabAssetAndConnect(model.gameObject, path, InteractionMode.AutomatedAction);
        }

        private static Transform Chair(Transform parent, string name, Vector3 position, float yaw)
        {
            Transform chair = Group(name, parent, position); chair.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (float x in new[] { -.34f, .34f }) foreach (float z in new[] { -.30f, .30f })
                Round("Light oak leg", chair, new Vector3(x, .025f, z), .045f, .25f, "Oak");
            Box("Upholstered base", chair, new Vector3(0, .33f, 0), new Vector3(.94f, .28f, .85f), "Linen", .12f);
            Box("Seat cushion", chair, new Vector3(0, .51f, .08f), new Vector3(.72f, .17f, .68f), "Cream", .075f);
            GameObject back = Box("Soft back", chair, new Vector3(0, .79f, -.34f), new Vector3(.89f, .70f, .21f), "Linen", .10f);
            back.transform.localRotation = Quaternion.Euler(-8, 0, 0);
            foreach (float x in new[] { -.425f, .425f })
                Box("Rounded arm", chair, new Vector3(x, .66f, .02f), new Vector3(.19f, .46f, .83f), "Linen", .09f);
            Pillow(chair, new Vector3(.02f, .75f, -.13f), new Vector3(.43f, .40f, .16f), "SageFabric", 9);
            var collider = chair.gameObject.AddComponent<BoxCollider>(); collider.center = new Vector3(0, .55f, 0); collider.size = new Vector3(1.04f, 1.08f, .94f);
            return chair;
        }
        private static void Pillow(Transform parent, Vector3 position, Vector3 size, string material, float roll)
        {
            GameObject pillow = Box("Soft pillow", parent, position, size, material, .075f);
            pillow.transform.localRotation = Quaternion.Euler(-12, 0, roll);
            SavePrefab(pillow.transform, material == "SageFabric" ? "SagePillow" : "CreamPillow");
        }
        private static void Book(Transform parent, Vector3 position, float width, float depth, string color, string name = "Closed journal")
        {
            Transform book = Group(name, parent, position);
            Box("Pages", book, new Vector3(0, .019f, 0), new Vector3(width * .96f, .028f, depth * .94f), "Paper", .004f);
            foreach (float y in new[] { 0f, .039f }) Box("Rounded cover", book, new Vector3(0, y, 0), new Vector3(width, .008f, depth), color, .004f);
            Box("Spine", book, new Vector3(-width * .48f, .018f, 0), new Vector3(.015f, .039f, depth), color, .005f);
        }
        private static void BuildFurniture()
        {
            Transform rug = Group("Woven area rug", _furniture, new Vector3(.03f, .025f, -.30f));
            Box("Bound rounded edge", rug, Vector3.zero, new Vector3(4.55f, .025f, 3.70f), "RugBorder", .012f);
            var rugTop = Box("Woven rug", rug, new Vector3(0, .013f, 0), new Vector3(4.38f, .018f, 3.53f), "Rug", .008f);
            rugTop.GetComponent<Renderer>().sharedMaterial.mainTextureScale = new Vector2(8, 7);
            Transform sofa = Group("Sofa", _furniture, new Vector3(-.25f, 0, 2.38f)); sofa.localRotation = Quaternion.Euler(0, 180, 0);
            foreach (float x in new[] { -.93f, .93f }) foreach (float z in new[] { -.30f, .30f }) Round("Oak leg", sofa, new Vector3(x, .02f, z), .046f, .23f, "Oak");
            Box("Sofa base", sofa, new Vector3(0, .33f, 0), new Vector3(2.35f, .29f, .91f), "Linen", .10f);
            Box("Sofa back", sofa, new Vector3(0, .79f, -.36f), new Vector3(2.30f, .68f, .24f), "Linen", .10f);
            foreach (float x in new[] { -1.09f, 1.09f }) Box("Soft sofa arm", sofa, new Vector3(x, .63f, 0), new Vector3(.22f, .50f, .92f), "Linen", .10f);
            foreach (float x in new[] { -.51f, .51f })
            {
                Box("Separate seat cushion", sofa, new Vector3(x, .51f, .10f), new Vector3(.98f, .19f, .67f), "Cream", .07f);
                Box("Separate back cushion", sofa, new Vector3(x, .80f, -.20f), new Vector3(.99f, .51f, .17f), "Cream", .075f);
            }
            Pillow(sofa, new Vector3(-.73f, .79f, .02f), new Vector3(.44f, .43f, .17f), "SageFabric", 12);
            Pillow(sofa, new Vector3(.71f, .77f, .03f), new Vector3(.45f, .40f, .17f), "Cream", -10);
            var sofaCollider = sofa.gameObject.AddComponent<BoxCollider>(); sofaCollider.center = new Vector3(0, .55f, 0); sofaCollider.size = new Vector3(2.38f, 1.1f, .96f);
            SavePrefab(sofa, "LinenSofa");
            Transform leftChair = Chair(_furniture, "Conversation chair A", new Vector3(-1.10f, 0, -.87f), 28);
            SavePrefab(leftChair, "RoundedArmchair");
            Chair(_furniture, "Conversation chair B", new Vector3(1.07f, 0, -.92f), -28);
            Transform blanket = Group("Knitted blanket", leftChair, new Vector3(-.42f, .87f, -.19f));
            blanket.localRotation = Quaternion.Euler(0, -90, 0);
            MeshObject("Draped knit", blanket, Vector3.zero, WellnessGeometry.Cloth("DrapedBlanket", .5f, .47f, true), "Throw");
            for (int i = 0; i < 11; i++) Rod("Blanket fringe", blanket, new Vector3(-.23f + i * .046f, -.72f, .235f), new Vector3(-.23f + i * .046f, -.80f, .24f), .006f, "Throw");
            Transform table = Group("Round conversation table", _furniture, new Vector3(0, .04f, .18f));
            Round("Soft ottoman base", table, Vector3.zero, .48f, .37f, "Linen", true);
            Round("Rounded oak tray", table, new Vector3(0, .37f, 0), .52f, .055f, "OakLight");
            Cup(table, new Vector3(.21f, .43f, -.13f)); Book(table, new Vector3(-.12f, .43f, -.14f), .24f, .19f, "BookSage");
            Candle(table, new Vector3(-.20f, .43f, .18f), .065f, .12f);
            SavePrefab(table, "ConversationOttoman");
            Lamp(_furniture, "Sofa floor lamp", new Vector3(1.34f, 0, 2.48f), true);
            Artwork(_decor, new Vector3(-1.13f, 1.96f, 2.975f), .57f, .85f, 0, 1);
            Artwork(_decor, new Vector3(-.27f, 2.0f, 2.975f), .77f, 1.08f, 0, 0);
            Artwork(_decor, new Vector3(.64f, 1.96f, 2.975f), .57f, .85f, 0, 2);
        }

        private static void Cup(Transform parent, Vector3 p)
        {
            Transform cup = Group("Ceramic cup", parent, p);
            var profile = new[] { new Vector2(0, 0), new Vector2(.046f, 0), new Vector2(.054f, .015f), new Vector2(.055f, .11f), new Vector2(.048f, .11f), new Vector2(.042f, .024f), new Vector2(0, .024f) };
            MeshObject("Glazed cup", cup, Vector3.zero, WellnessGeometry.Lathe("CupMesh", profile, 24), "Ceramic");
            // A small loop handle using a ring rotated into the vertical plane.
            Transform handle = Group("Cup handle", cup, new Vector3(.053f, .057f, 0)); handle.localRotation = Quaternion.Euler(90, 0, 0);
            MeshObject("Handle loop", handle, Vector3.zero, WellnessGeometry.Lathe("CupHandle", new[] { new Vector2(.022f, -.007f), new Vector2(.033f, -.007f), new Vector2(.035f, 0), new Vector2(.033f, .007f), new Vector2(.022f, .007f), new Vector2(.022f, -.007f) }, 20), "Ceramic");
            SavePrefab(cup, "CeramicCup");
        }
        private static void Candle(Transform parent, Vector3 p, float radius, float height)
        {
            Transform candle = Group("Steady LED candle", parent, p);
            Round("Ceramic candle holder", candle, Vector3.zero, radius, height, "Ceramic");
            Round("Wax inset", candle, new Vector3(0, height, 0), radius * .78f, .008f, "Cream");
            Sphere("Steady amber LED", candle, new Vector3(0, height + .014f, 0), new Vector3(.025f, .033f, .025f), "AmberGlow");
            SavePrefab(candle, "SteadyCandle");
        }
        private static void Lamp(Transform parent, string name, Vector3 p, bool floor)
        {
            Transform lamp = Group(name, parent, p); float stem = floor ? 1.39f : .28f;
            Round("Weighted round base", lamp, Vector3.zero, floor ? .23f : .12f, .045f, "Oak");
            Round("Slender stem", lamp, new Vector3(0, .04f, 0), .022f, stem, "Brass");
            float r = floor ? .28f : .20f, h = floor ? .35f : .28f;
            var profile = new[] { new Vector2(r, 0), new Vector2(r * .72f, h), new Vector2(r * .68f, h), new Vector2(r * .95f, 0), new Vector2(r, 0) };
            MeshObject("Soft linen shade", lamp, new Vector3(0, stem, 0), WellnessGeometry.Lathe(floor ? "FloorShade" : "TableShade", profile), "LampShade");
            Sphere("Frosted bulb", lamp, new Vector3(0, stem + h * .4f, 0), Vector3.one * .09f, "AmberGlow");
            AddLight(lamp, "Warm lamp light 3000K", new Vector3(0, stem + .06f, 0), LightType.Point, new Color(1f, .85f, .65f), floor ? 1.05f : .7f, 3.4f);
            SavePrefab(lamp, floor ? "LinenFloorLamp" : "LinenTableLamp");
        }
        private static void Artwork(Transform parent, Vector3 p, float width, float height, float yaw, int variant)
        {
            Transform art = Group("Botanical artwork " + variant, parent, p); art.localRotation = Quaternion.Euler(0, yaw, 0);
            Box("Rounded oak frame", art, Vector3.zero, new Vector3(width, height, .045f), "OakLight", .02f);
            Box("Warm paper mount", art, new Vector3(0, 0, -.027f), new Vector3(width - .045f, height - .045f, .01f), "Canvas", .004f);
            Sphere("Abstract clay sun", art, new Vector3(.09f, height * .19f, -.037f), new Vector3(width * .27f, width * .27f, .004f), "ArtClay");
            Rod("Botanical stem", art, new Vector3(-.07f, -height * .31f, -.04f), new Vector3(.04f, height * .27f, -.04f), .006f, "LeafDark");
            for (int i = 0; i < 7; i++)
            {
                float sign = i % 2 == 0 ? -1 : 1;
                GameObject leaf = MeshObject("Paper leaf", art, new Vector3(-.07f + i * .014f, -height * .24f + i * height * .071f, -.046f), WellnessGeometry.Leaf(), i % 3 == 0 ? "ArtClay" : "LeafDark");
                leaf.transform.localRotation = Quaternion.Euler(0, 0, sign * (38 + variant * 7));
                leaf.transform.localScale = new Vector3(width * .62f, height * .30f, .002f);
            }
        }

        private static Transform Plant(Transform parent, string name, Vector3 p, float scale, bool bush = false)
        {
            Transform plant = Group(name, parent, p);
            float potRadius = .18f;
            MeshObject("Textured planter", plant, Vector3.zero, WellnessGeometry.Lathe("Planter", new[] { new Vector2(0, 0), new Vector2(.135f, 0), new Vector2(.18f, .32f), new Vector2(.16f, .33f), new Vector2(.15f, .29f), new Vector2(0, .29f) }, 24), "Basket");
            Round("Soil", plant, new Vector3(0, .29f, 0), potRadius * .83f, .012f, "Soil");
            int count = bush ? 14 : 9;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 137.5f * Mathf.Deg2Rad;
                float h = bush ? .36f + (i % 4) * .07f : .47f + (i % 4) * .17f;
                Vector3 end = new Vector3(Mathf.Sin(angle) * .14f, .30f + h, Mathf.Cos(angle) * .14f);
                Rod("Living stem", plant, new Vector3(0, .28f, 0), end, .008f, "Stem");
                GameObject leaf = MeshObject("Curved leaf", plant, end, WellnessGeometry.Leaf(), new[] { "Leaf", "LeafLight", "LeafDark" }[i % 3]);
                leaf.transform.localRotation = Quaternion.Euler(35 + i % 3 * 12, angle * Mathf.Rad2Deg, i % 2 == 0 ? 12 : -12);
                leaf.transform.localScale = Vector3.one * (bush ? .43f : .65f);
            }
            var collider = plant.gameObject.AddComponent<CapsuleCollider>(); collider.radius = .22f; collider.height = .65f; collider.center = new Vector3(0, .325f, 0);
            plant.localScale = Vector3.one * scale; return plant;
        }
        private static void BuildPlants()
        {
            Transform plant = Plant(_plants, "Tall window plant", new Vector3(3.03f, .02f, -2.22f), 1.17f); SavePrefab(plant, "TallLeafyPlant");
            plant = Plant(_plants, "Sofa fern", new Vector3(-1.92f, .02f, 2.44f), .68f, true); SavePrefab(plant, "MediumPlant");
            Plant(_plants, "Reflection greenery", new Vector3(3.07f, .02f, 2.37f), .70f);
        }

        private static WellnessInteraction Interactable(GameObject target, WellnessInteraction.Area area, string title, string text)
        {
            var interaction = target.AddComponent<WellnessInteraction>(); interaction.area = area; interaction.displayName = title; interaction.reflection = text; return interaction;
        }
        private static void BuildInteractiveAreas()
        {
            Transform shelf = Group("GroundingShelf", _interactive, new Vector3(-3.22f, .02f, 1.22f)); shelf.localRotation = Quaternion.Euler(0, 90, 0);
            // Local X runs along the wall, local Z faces the room.
            foreach (float x in new[] { -.66f, .66f }) foreach (float z in new[] { -.16f, .16f })
                Box("Oak shelf upright", shelf, new Vector3(x, 1.02f, z), new Vector3(.055f, 2.04f, .055f), "Oak", .01f, true);
            foreach (float y in new[] { .15f, .54f, .97f, 1.43f, 1.99f }) Box("Oak shelf", shelf, new Vector3(0, y, 0), new Vector3(1.40f, .048f, .43f), "OakLight", .018f, true);
            Transform objects = Group("Grounding objects", shelf);
            GameObject stone = Sphere("Smooth grounding stone", objects, new Vector3(-.40f, 1.055f, .08f), new Vector3(.21f, .11f, .15f), "Stone");
            stone.AddComponent<SphereCollider>(); Interactable(stone, WellnessInteraction.Area.Grounding, "Notice the smooth stone", "Notice its rounded shape, soft color, and the light on its surface.");
            Plant(objects, "Small grounding plant", new Vector3(.40f, .998f, .02f), .23f, true);
            Transform smallPlant = objects.GetChild(objects.childCount - 1); Interactable(smallPlant.gameObject, WellnessInteraction.Area.Grounding, "Notice the plant", "Find three different shades of green. Take your time.");
            Candle(objects, new Vector3(-.08f, .998f, .03f), .055f, .11f);
            Transform candle = objects.GetChild(objects.childCount - 1); var cc = candle.gameObject.AddComponent<BoxCollider>(); cc.center = new Vector3(0, .065f, 0); cc.size = new Vector3(.13f, .15f, .13f);
            Interactable(candle.gameObject, WellnessInteraction.Area.Grounding, "Notice the steady light", "A small, steady light. There is no hurry here.");
            Book(objects, new Vector3(.18f, .998f, .10f), .18f, .15f, "BookSand", "Grounding journal");
            Transform journal = objects.GetChild(objects.childCount - 1); var jc = journal.gameObject.AddComponent<BoxCollider>(); jc.center = new Vector3(0, .02f, 0); jc.size = new Vector3(.18f, .06f, .15f);
            Interactable(journal.gameObject, WellnessInteraction.Area.Grounding, "Notice the journal", "Name one thing you can see, one thing you can hear, and one thing you can feel.");
            for (int i = 0; i < 6; i++)
            {
                Transform book = Group("Shelf book", shelf, new Vector3(-.5f + i * .075f, .58f, 0));
                Box("Plain book cover", book, new Vector3(0, .12f, 0), new Vector3(.055f, .24f + i % 3 * .035f, .24f), new[] { "BookSand", "BookSage", "BookClay" }[i % 3], .006f);
            }
            foreach (float x in new[] { -.37f, .36f }) Box("Woven storage basket", shelf, new Vector3(x, .29f, 0), new Vector3(.51f, .22f, .31f), "Basket", .05f);
            Plant(shelf, "Upper shelf plant", new Vector3(-.39f, 1.46f, 0), .30f, true);
            Round("Ceramic vessel", shelf, new Vector3(.38f, 1.46f, 0), .08f, .20f, "Terracotta");
            Plant(shelf, "Trailing top plant", new Vector3(.42f, 2.015f, 0), .40f, true);
            for (int i = 0; i < 11; i++)
            {
                Vector3 p = new Vector3(.65f + Mathf.Sin(i * .7f) * .045f, 2.08f - i * .065f, .20f);
                GameObject leaf = MeshObject("Trailing leaf", shelf, p, WellnessGeometry.Leaf(), i % 2 == 0 ? "Leaf" : "LeafLight");
                leaf.transform.localRotation = Quaternion.Euler(0, 20, i % 2 == 0 ? 130 : -130); leaf.transform.localScale = Vector3.one * .14f;
            }
            for (int i = 0; i <= 12; i++)
            {
                float x = -.63f + i * .105f, y = 1.91f - .07f * Mathf.Sin(i / 12f * Mathf.PI);
                Sphere("Steady string light", shelf, new Vector3(x, y, .20f), Vector3.one * .025f, "AmberGlow");
                if (i > 0) Rod("String light cable", shelf, new Vector3(x - .105f, 1.91f - .07f * Mathf.Sin((i - 1) / 12f * Mathf.PI), .20f), new Vector3(x, y, .20f), .002f, "Metal");
            }
            BuildDiffuser(shelf, new Vector3(.04f, 1.46f, 0));
            Transform breathing = Group("BreathingOrb", _interactive, new Vector3(2.31f, .02f, 2.38f));
            Round("Rounded oak pedestal", breathing, Vector3.zero, .23f, .87f, "Oak", true);
            Round("Orb cradle", breathing, new Vector3(0, .87f, 0), .18f, .025f, "Brass");
            GameObject orb = Sphere("Breathing amber orb", breathing, new Vector3(0, 1.08f, 0), Vector3.one * .35f, "AmberGlow");
            orb.AddComponent<SphereCollider>(); var motion = orb.AddComponent<WellnessBreathingOrb>();
            var interaction = Interactable(orb, WellnessInteraction.Area.Breathing, "Pause / resume the breathing orb", "Let the slow rhythm be a focus, at whatever pace feels comfortable."); interaction.breathingOrb = motion;
            AddLight(breathing, "Soft orb spill", new Vector3(0, 1.12f, 0), LightType.Point, new Color(1, .86f, .68f), .35f, 2.5f);
            Transform reflection = Group("ReflectionSeat", _interactive);
            Transform chair = Chair(reflection, "Window reflection chair", new Vector3(2.84f, 0, .18f), -90);
            Interactable(chair.gameObject, WellnessInteraction.Area.Reflection, "Pause at the reflection seat", "Watch the quiet landscape. What would you like to carry with you from this moment?");
            Transform side = Group("Reflection side table", reflection, new Vector3(2.94f, .02f, 1.28f));
            Round("Round pedestal", side, Vector3.zero, .19f, .57f, "Oak", true);
            Round("Soft table edge", side, new Vector3(0, .57f, 0), .32f, .055f, "OakLight");
            Lamp(side, "Reflection lamp", new Vector3(.06f, .625f, .02f), false);
            Book(side, new Vector3(-.12f, .63f, -.15f), .19f, .14f, "BookClay", "Reflection journal");
            Transform reflectionJournal = side.GetChild(side.childCount - 1); var rjc = reflectionJournal.gameObject.AddComponent<BoxCollider>(); rjc.size = new Vector3(.20f, .06f, .15f); rjc.center = new Vector3(0, .02f, 0);
            Interactable(reflectionJournal.gameObject, WellnessInteraction.Area.Reflection, "Reflect with the journal", "A thought, a feeling, or a small hope. There is no right answer.");
            Artwork(_decor, new Vector3(-3.48f, 1.91f, -.71f), .67f, .92f, -90, 1);
        }

        private static void BuildDiffuser(Transform shelf, Vector3 p)
        {
            Transform diffuser = Group("Essential oil diffuser", shelf, p);
            MeshObject("Ceramic diffuser", diffuser, Vector3.zero, WellnessGeometry.Lathe("Diffuser", new[] { new Vector2(0, 0), new Vector2(.085f, 0), new Vector2(.095f, .06f), new Vector2(.07f, .14f), new Vector2(.022f, .18f), new Vector2(.014f, .18f) }, 24), "Ceramic");
            Transform mist = Group("Subtle mist", diffuser, new Vector3(0, .19f, 0));
            var ps = mist.gameObject.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.duration = 8; main.loop = true; main.startLifetime = 2.6f; main.startSpeed = .025f; main.startSize = new ParticleSystem.MinMaxCurve(.025f, .05f); main.startColor = new Color(.86f, .91f, .87f, .10f); main.maxParticles = 16; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.rateOverTime = 3f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 5; shape.radius = .008f; shape.rotation = new Vector3(-90, 0, 0);
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.y = .065f;
            var color = ps.colorOverLifetime; color.enabled = true; var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.13f, .3f), new GradientAlphaKey(0, 1) }); color.color = gradient;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .5f, 1, 2));
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "SoftMist" };
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); material.SetFloat("_ZWrite", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f; tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - d), 2))); }
            tex.Apply(); tex.name = "MistFalloff"; AssetDatabase.CreateAsset(tex, Art + "/MistFalloff.asset"); material.SetTexture("_BaseMap", tex); AssetDatabase.CreateAsset(material, Art + "/SoftMist.mat");
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = material; ps.Play();
        }

        private static Light AddLight(Transform parent, string name, Vector3 position, LightType type, Color color, float intensity, float range)
        {
            Light light = Group(name, parent, position).gameObject.AddComponent<Light>(); light.type = type; light.color = color; light.intensity = intensity; light.range = range;
            light.shadows = LightShadows.None; light.lightmapBakeType = LightmapBakeType.Mixed; light.bounceIntensity = 1.3f; return light;
        }
        private static void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("C2CFCC"); RenderSettings.ambientEquatorColor = Hex("ACB4A6"); RenderSettings.ambientGroundColor = Hex("918B7D"); RenderSettings.ambientIntensity = .8f;
            RenderSettings.reflectionIntensity = .45f; RenderSettings.fog = false;
            var sky = new Material(Shader.Find("Skybox/Procedural")) { name = "SoftAfternoonSky" };
            sky.SetColor("_SkyTint", Hex("BDCDD0")); sky.SetColor("_GroundColor", Hex("BEB69C")); sky.SetFloat("_Exposure", .85f); sky.SetFloat("_SunSize", .025f); AssetDatabase.CreateAsset(sky, Art + "/SoftAfternoonSky.mat"); RenderSettings.skybox = sky;
            Light sun = AddLight(_lighting, "Late afternoon daylight", new Vector3(5, 5, -2), LightType.Directional, Hex("FFF0D4"), 1.5f, 20);
            sun.transform.rotation = Quaternion.Euler(28, -65, 0); sun.shadows = LightShadows.Soft; sun.shadowStrength = .65f; sun.shadowBias = .035f; sun.shadowNormalBias = .2f; RenderSettings.sun = sun;
            // Fill from window and broad room bounce. Neither casts real-time shadows.
            AddLight(_lighting, "Neutral window bounce", new Vector3(2.9f, 2.0f, 0), LightType.Point, Hex("E5EFEA"), 1.4f, 7f);
            AddLight(_lighting, "Gentle ceiling bounce", new Vector3(-.5f, 1.95f, -.5f), LightType.Point, Hex("F6EEDF"), .45f, 7f);
            AddLight(_lighting, "Entry fill", new Vector3(-2.55f, 2.30f, -1.8f), LightType.Point, Hex("E7E6D6"), .45f, 4.5f);
            var probe = Group("Room reflection probe", _lighting, new Vector3(0, 1.45f, 0)).gameObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked; probe.size = new Vector3(7, 3, 6); probe.boxProjection = true; probe.resolution = 128; probe.intensity = .5f; probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            var lightProbes = Group("Walkway light probes", _lighting).gameObject.AddComponent<LightProbeGroup>();
            var positions = new List<Vector3>();
            foreach (float y in new[] { .5f, 1.65f, 2.5f }) foreach (float x in new[] { -2.7f, -1.2f, 0, 1.4f, 2.7f }) foreach (float z in new[] { -2.3f, -.2f, 1.8f }) positions.Add(new Vector3(x, y, z));
            lightProbes.probePositions = positions.ToArray();
            var volume = Group("Restrained warm grading", _lighting).gameObject.AddComponent<Volume>(); volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "WellnessPostProcessing";
            var wb = profile.Add<WhiteBalance>(true); wb.temperature.Override(3f);
            var grading = profile.Add<ColorAdjustments>(true); grading.postExposure.Override(.15f); grading.saturation.Override(-5f); grading.contrast.Override(-6f);
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(.045f); bloom.threshold.Override(1.25f); bloom.scatter.Override(.35f);
            AssetDatabase.CreateAsset(profile, Config + "/WellnessVolume.asset"); foreach (VolumeComponent component in profile.components) AssetDatabase.AddObjectToAsset(component, profile); volume.sharedProfile = profile;
            var settings = new LightingSettings { name = "WellnessBakedLighting", bakedGI = true, realtimeGI = false, lightmapper = LightingSettings.Lightmapper.ProgressiveCPU, lightmapResolution = 12, lightmapMaxSize = 1024, indirectSampleCount = 64, directSampleCount = 32, environmentSampleCount = 64, maxBounces = 3, ao = true, aoMaxDistance = .4f, aoExponentIndirect = .5f };
            AssetDatabase.CreateAsset(settings, Config + "/WellnessLighting.asset"); Lightmapping.lightingSettings = settings;
        }

        private static void BuildPlayer()
        {
            Transform player = Group("PlayerSpawn", _root, new Vector3(-2.50f, .04f, -2.25f)); player.localRotation = Quaternion.Euler(0, 27, 0);
            var controller = player.gameObject.AddComponent<CharacterController>(); controller.height = 1.75f; controller.radius = .24f; controller.center = new Vector3(0, .9f, 0); controller.stepOffset = .16f; controller.skinWidth = .025f;
            _camera = Group("First person camera", player, new Vector3(0, 1.65f, 0)).gameObject.AddComponent<Camera>(); _camera.tag = "MainCamera"; _camera.fieldOfView = 68; _camera.nearClipPlane = .04f; _camera.farClipPlane = 80;
            _camera.gameObject.AddComponent<AudioListener>(); var data = _camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.None;
            player.gameObject.AddComponent<WellnessExplorer>().Configure(_camera);
        }
        private static void BuildAudioPlaceholders()
        {
            Transform audio = Group("Audio", _root);
            foreach (string name in new[] { "Quiet room tone - assign loop", "Distant wind or rain - assign loop", "Subtle nature sounds - assign loop" })
            {
                AudioSource source = Group(name, audio).gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = true; source.volume = .10f; source.spatialBlend = 0; source.priority = 200;
            }
        }

        public static void ValidateBatch() { EditorSceneManager.OpenScene(ScenePath); Validate(); }
        [MenuItem("The Last Watch/Wellness/Validate Therapy Room")]
        public static void Validate()
        {
            GameObject root = GameObject.Find("TherapyRoom");
            if (root == null) throw new InvalidOperationException("Open TherapyRoom first.");
            var issues = new List<string>();
            foreach (string required in new[] { "Architecture", "Furniture", "Decorations", "Plants", "InteractiveObjects", "Lighting", "Audio", "PlayerSpawn" }) if (root.transform.Find(required) == null) issues.Add("Missing group: " + required);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) foreach (Material m in r.sharedMaterials)
                if (m == null || m.shader == null || (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && !m.shader.isSupported)) issues.Add("Missing/unsupported material on " + r.name);
            if (GraphicsSettings.defaultRenderPipeline is not UniversalRenderPipelineAsset) issues.Add("URP is not assigned.");
            if (root.GetComponentsInChildren<WellnessInteraction>().Length < 7) issues.Add("Interaction targets missing.");
            if (root.GetComponentsInChildren<Light>().Count(l => l.shadows != LightShadows.None) > 1) issues.Add("Too many shadow casting lights.");
            Physics.SyncTransforms();
            // Clear 1.2m route from the entry along the left aisle to the grounding shelf.
            foreach (Vector3 p in new[] { new Vector3(-2.45f, 1, -2.18f), new Vector3(-2.45f, 1, -1f), new Vector3(-2.20f, 1, .42f) })
            {
                Collider[] hits = Physics.OverlapBox(p, new Vector3(.6f, .75f, .25f));
                if (hits.Any(c => c.GetComponent<CharacterController>() == null)) issues.Add("Walking route obstructed at " + p);
            }
            foreach (WellnessInteraction target in root.GetComponentsInChildren<WellnessInteraction>())
            {
                Collider collider = target.GetComponent<Collider>();
                if (collider == null) { issues.Add("Interaction collider missing: " + target.name); continue; }
                Vector3 center = collider.bounds.center;
                Vector3 origin = target.area == WellnessInteraction.Area.Grounding ? new Vector3(-2.2f, 1.65f, center.z)
                    : target.area == WellnessInteraction.Area.Breathing ? new Vector3(center.x, 1.65f, 1.18f)
                    : new Vector3(1.8f, 1.65f, center.z);
                if (!Physics.Raycast(origin, (center - origin).normalized, out RaycastHit hit, 2.6f) || hit.collider.GetComponentInParent<WellnessInteraction>() != target)
                    issues.Add("Interaction line of sight blocked: " + target.name);
            }
            long triangles = root.GetComponentsInChildren<MeshFilter>().Sum(f => (long)(f.sharedMesh != null ? f.sharedMesh.triangles.Length / 3 : 0));
            string report = "TherapyRoom validation\n" + "Meshes: " + root.GetComponentsInChildren<MeshRenderer>().Length + "\nTriangles (including landscape): " + triangles + "\nInteraction targets: " + root.GetComponentsInChildren<WellnessInteraction>().Length + "\nReal-time shadow lights: " + root.GetComponentsInChildren<Light>().Count(l => l.shadows != LightShadows.None) + "\n" + (issues.Count == 0 ? "PASS: structure, materials, interactions, lighting budget, entry and grounding route.\n" : string.Join("\n", issues));
            File.WriteAllText("Documentation/Wellness/Validation.txt", report); Debug.Log(report);
            if (issues.Count > 0) throw new InvalidOperationException(string.Join("; ", issues));
        }

        public static void BakeBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);
            // This editor/GPU crashes inside ReflectionProbeBaker. Retain a quiet custom
            // environment reflection while baking diffuse GI independently.
            foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
            {
                probe.mode = ReflectionProbeMode.Custom;
                const string cubePath = "Assets/_Project/Art/Materials/Wellness/SoftRoomReflection.asset";
                Cubemap cube = AssetDatabase.LoadAssetAtPath<Cubemap>(cubePath);
                if (cube == null)
                {
                    cube = new Cubemap(16, TextureFormat.RGB24, false) { name = "SoftRoomReflection" };
                    for (int face = 0; face < 6; face++)
                    {
                        Color tint = face == 2 ? Hex("D9D9C9") : face == 3 ? Hex("A69A81") : Hex("B7BBA7");
                        Color[] pixels = Enumerable.Repeat(tint, 16 * 16).ToArray(); cube.SetPixels(pixels, (CubemapFace)face);
                    }
                    cube.Apply(); AssetDatabase.CreateAsset(cube, cubePath);
                }
                probe.customBakedTexture = cube;
            }
            bool baked = Lightmapping.Bake();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            if (!baked) throw new InvalidOperationException("Lightmap bake did not complete.");
            File.WriteAllText("Documentation/Wellness/LightingBake.txt", "Bake completed. Lightmaps: " + LightmapSettings.lightmaps.Length + "; light probes: " + (LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.count : 0) + ". Room reflection uses a subtle custom environment cubemap.\n");
            Debug.Log("WELLNESS_BAKED: " + LightmapSettings.lightmaps.Length + " lightmaps");
        }

        public static void FinalizeLightingBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Config + "/WellnessRenderer.asset");
            data.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            foreach (ScriptableRendererFeature feature in data.rendererFeatures)
            {
                var settings = new SerializedObject(feature);
                settings.FindProperty("m_Settings.Downsample").boolValue = false;
                settings.FindProperty("m_Settings.AOMethod").intValue = 1;
                settings.FindProperty("m_Settings.Intensity").floatValue = .32f;
                settings.FindProperty("m_Settings.Radius").floatValue = .16f;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(data);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Config + "/WellnessURP.asset");
            var pipelineSettings = new SerializedObject(pipeline); pipelineSettings.FindProperty("m_SoftShadowsSupported").boolValue = true; pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            GameObject root = GameObject.Find("TherapyRoom");
            WellnessScenePipeline scope = root.GetComponent<WellnessScenePipeline>(); if (scope == null) scope = root.AddComponent<WellnessScenePipeline>(); scope.pipeline = pipeline;
            var lighting = Lightmapping.lightingSettings;
            lighting.lightmapResolution = 8; lighting.lightmapMaxSize = 512; lighting.indirectSampleCount = 32; lighting.directSampleCount = 16; lighting.environmentSampleCount = 32; lighting.lightProbeSampleCountMultiplier = 1; lighting.maxBounces = 2;
            EditorUtility.SetDirty(lighting);
            ShaderUtil.ClearCachedData(Shader.Find("Universal Render Pipeline/Lit"));
            ShaderUtil.ClearCachedData(Shader.Find("Universal Render Pipeline/Unlit"));
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            Validate(); BakeBatch(); RenderBatch();
        }

        // Review pass for this generated draft: reduce texture contrast and specular hotspots.
        public static void PolishBatch()
        {
            EditorSceneManager.OpenScene(ScenePath); CreateMaterials();
            foreach (string name in new[] { "WarmPlaster", "SageWall", "Ceiling", "Rug", "RugBorder", "Linen", "Cream", "SageFabric" })
            {
                Material material = Materials[name]; material.SetFloat("_SpecularHighlights", 0); material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); EditorUtility.SetDirty(material);
            }
            Materials["Rug"].mainTextureScale = new Vector2(8, 7);
            Light bounce = GameObject.Find("TherapyRoom/Lighting/Gentle ceiling bounce").GetComponent<Light>();
            bounce.transform.localPosition = new Vector3(-.5f, 1.95f, -.5f); bounce.intensity = .45f; bounce.color = Hex("F6EEDF");
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.name == "Warm lamp light 3000K") light.intensity = light.transform.parent.name == "Sofa floor lamp" ? .65f : .45f;
                if (light.name == "Soft orb spill") light.intensity = .18f;
            }
            Validate(); EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            BakeBatch();
        }

        public static void RenderBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Camera camera = Object.FindAnyObjectByType<Camera>();
            RenderView(camera, "Documentation/Wellness/Interior.png", new Vector3(-.10f, 1.73f, -2.75f), new Vector3(.10f, 1.13f, .65f), false);
            RenderView(camera, "Documentation/Wellness/WindowCorner.png", new Vector3(-2.12f, 1.59f, 1.62f), new Vector3(1.65f, 1.05f, -.45f), false);
            Transform architecture = GameObject.Find("TherapyRoom/Architecture").transform;
            architecture.Find("Ceiling").gameObject.SetActive(false);
            foreach (Transform child in architecture.Find("Walls")) if (child.name.Contains("Entry") || child.name.Contains("Door lintel") || child.name.Contains("Front baseboard") || child.name.Contains("Left plaster")) child.gameObject.SetActive(false);
            architecture.Find("Door").gameObject.SetActive(false);
            foreach (Transform decoration in GameObject.Find("TherapyRoom/Decorations").transform)
                if (decoration.position.x < -3f) decoration.gameObject.SetActive(false);
            architecture.Find("Window/Quiet landscape beyond window").gameObject.SetActive(false);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Hex("D7D7CE");
            RenderView(camera, "Documentation/Wellness/Overview.png", new Vector3(-8, 8.2f, -9.5f), new Vector3(0, .6f, 0), true);
            Debug.Log("WELLNESS_RENDERED");
        }
        public static void FinishPresentationBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Config + "/WellnessVolume.asset");
            if (profile.TryGet(out ColorAdjustments grading)) { grading.postExposure.Override(.45f); EditorUtility.SetDirty(grading); }
            Transform lights = GameObject.Find("TherapyRoom/Lighting").transform;
            if (lights.Find("Soft entry bounce") == null)
            {
                Light fill = AddLight(lights, "Soft entry bounce", new Vector3(-.1f, 1.6f, -1.95f), LightType.Point, Hex("EEEDE1"), .65f, 7f);
                fill.lightmapBakeType = LightmapBakeType.Realtime;
            }
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            RenderBatch();
        }
        public static void InstallBackdropBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);
            const string imagePath = "Assets/_Project/Art/Textures/Wellness/QuietLake.png";
            AssetDatabase.ImportAsset(imagePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            importer.maxTextureSize = 2048; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "QuietLakeBackdrop" };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath)); material.SetColor("_BaseColor", new Color(.93f, .95f, .94f));
            AssetDatabase.CreateAsset(material, Art + "/QuietLakeBackdrop.mat");
            Transform landscape = GameObject.Find("TherapyRoom/Architecture/Window/Quiet landscape beyond window").transform;
            foreach (Transform child in landscape) child.gameObject.SetActive(false);
            var backdrop = new GameObject("Soft painted lake backdrop"); backdrop.transform.SetParent(landscape, false);
            backdrop.transform.localPosition = new Vector3(4.5f, 2.65f, 0); backdrop.transform.localRotation = Quaternion.Euler(0, 90, 0);
            var mesh = new Mesh { name = "LandscapeBackdropQuad" };
            mesh.vertices = new[] { new Vector3(-8, -4.5f, 0), new Vector3(8, -4.5f, 0), new Vector3(8, 4.5f, 0), new Vector3(-8, 4.5f, 0) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, WellnessGeometry.Folder + "/LandscapeBackdropQuad.asset");
            backdrop.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = backdrop.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(backdrop, StaticEditorFlags.BatchingStatic);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            Validate(); RenderBatch();
        }
        private static void RenderView(Camera camera, string path, Vector3 position, Vector3 target, bool ortho)
        {
            camera.transform.position = position; camera.transform.LookAt(target); camera.orthographic = ortho; camera.orthographicSize = 4.6f; camera.fieldOfView = 69;
            camera.aspect = 16f / 10f;
            var rt = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32); camera.targetTexture = rt;
            camera.Render(); camera.Render(); camera.Render(); RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture);
        }
    }
}
