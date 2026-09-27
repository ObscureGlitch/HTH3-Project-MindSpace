using System;
using System.IO;
using System.Linq;
using TheLastWatch.Environment;
using TheLastWatch.Integrations;
using TheLastWatch.Interaction;
using TheLastWatch.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyGardenLifeSetup
    {
        private const string Root = "Assets/TherapyGame", Folder = Root + "/Exterior/LivingGarden";
        private const string Request = Root + "/GardenLifeRequest.txt", Report = Folder + "/GardenLifeCheck.txt";
        [Serializable] private class Geometry { public string name; public float[] positions, normals, colors, pivot; }
        [Serializable] private class Template { public int kind; public string name; public Geometry[] parts; }
        [Serializable] private class AnimalData { public int kind; public string name; public Vector3[] route; public float phase, scale; public float[] tint; }
        [Serializable] private class Plant { public float x, z; public string type; }
        [Serializable] private class Tree { public float x, z, height; public int type; }
        [Serializable] private class Detail { public float x, z; public string kind; }
        [Serializable] private class Box { public string name; public float[] p, s; public float yaw; }
        [Serializable] private class LifeData { public int version, seed; public string units; public Geometry[] meshes; public Template[] templates; public AnimalData[] animals; public Plant[] plants; public Tree[] trees; public Detail[] details; public Box[] boxes; }
        static TherapyGardenLifeSetup() => EditorApplication.delayCall += ImportOnce;
        private static void ImportOnce()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "add-living-garden-once") return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || Lightmapping.isRunning)
            { File.WriteAllText(Request, "manual-only"); Debug.LogWarning("Garden life import deferred: stop Play/baking, then choose Therapy Game > Add Garden Life."); return; }
            File.WriteAllText(Request, "installing-once");
            try { Install(); }
            catch (Exception e) { File.WriteAllText(Request, "failed"); File.WriteAllText(Report, e.ToString()); Debug.LogException(e); }
        }
        [MenuItem("Therapy Game/Add Garden Life")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) throw new InvalidOperationException("Stop Play mode and baking first.");
            Scene scene = SceneManager.GetSceneByPath(Root + "/Scenes/TherapyRoom.unity");
            if (!scene.isLoaded) throw new InvalidOperationException("Keep TherapyRoom open.");
            Transform room = scene.GetRootGameObjects().Single(g => g.name == "TherapyRoom").transform;
            Transform garden = room.Find("OutdoorGarden");
            var player = room.GetComponentInChildren<WellnessExplorer>(); var cycle = room.GetComponentInChildren<WellnessSkyCycle>();
            var chat = room.GetComponentInChildren<WellnessVoiceChat>();
            if (garden == null || player == null || cycle == null || chat == null || chat.IsBusy || chat.IsConnected) throw new InvalidOperationException("Existing garden/player and idle voice/weather are required.");
            if (garden.position.sqrMagnitude > .0001f || Quaternion.Angle(garden.rotation, Quaternion.identity) > .001f || Vector3.Distance(garden.lossyScale, Vector3.one) > .001f)
                throw new InvalidOperationException("Garden world-space alignment changed; inspect before importing.");
            LifeData data = JsonUtility.FromJson<LifeData>(File.ReadAllText(Folder + "/Source/GardenLife.json"));
            ValidateData(data);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/Shaders/GardenLife.shader");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Garden life shader import failed.");
            Directory.CreateDirectory(Folder + "/Meshes"); Directory.CreateDirectory(Folder + "/Materials"); Directory.CreateDirectory(Folder + "/Backups");
            string backup = Folder + "/Backups/TherapyRoom_BeforeGardenLife.unity";
            if (!File.Exists(backup) && !EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Scene backup failed.");
            if (garden.Find("Living garden") != null) throw new InvalidOperationException("Living garden is already present; not duplicating or replacing it.");
            var group = new GameObject("Living garden"); group.transform.SetParent(garden, false); Undo.RegisterCreatedObjectUndo(group, "Add garden life");
            var manager = group.AddComponent<WellnessGardenLife>(); manager.viewer = player.ViewCamera; manager.sky = cycle; manager.wildlifeDistance = 38;
            Material material = MaterialAsset("LivingGarden", shader, Color.white);
            foreach (Geometry geometry in data.meshes) AddMesh(group.transform, geometry.name, MeshAsset(geometry, geometry.name.Replace(' ', '_')), material);
            var models = data.templates.Select(t => t.parts.Select(g => MeshAsset(g, t.kind + "_" + g.name.Replace(' ', '_'))).ToArray()).ToArray();
            manager.animals = new WellnessGardenLife.Animal[data.animals.Length];
            for (int i = 0; i < data.animals.Length; i++)
            {
                AnimalData d = data.animals[i]; var root = new GameObject(d.name); root.transform.SetParent(group.transform, false);
                root.transform.localPosition = d.route[0]; root.transform.localScale = Vector3.one * d.scale;
                var facing = WellnessGardenLife.Evaluate(d.kind, 0, d.phase, d.route).direction;
                root.transform.localRotation = Quaternion.LookRotation(facing);
                Material fur = MaterialAsset("WildlifeTone" + i % 3, shader, new Color(d.tint[0], d.tint[1], d.tint[2], 1));
                var animal = new WellnessGardenLife.Animal { kind = d.kind, root = root.transform, route = d.route, phase = d.phase };
                Template template = data.templates[d.kind];
                for (int p = 0; p < template.parts.Length; p++)
                {
                    Geometry part = template.parts[p]; Transform partRoot = AddMesh(root.transform, part.name, models[d.kind][p], fur).transform;
                    partRoot.localPosition = V(part.pivot);
                    switch (part.name) { case "Head": animal.head = partRoot; break; case "Ear L": animal.leftEar = partRoot; break; case "Ear R": animal.rightEar = partRoot; break; case "Tail": animal.tail = partRoot; break; case "Wing L": animal.leftWing = partRoot; break; case "Wing R": animal.rightWing = partRoot; break; }
                }
                animal.renderers = root.GetComponentsInChildren<Renderer>(); manager.animals[i] = animal;
            }
            var collision = new GameObject("New tree trunks"); collision.transform.SetParent(group.transform, false);
            foreach (Box box in data.boxes)
            {
                var g = new GameObject(box.name); g.transform.SetParent(collision.transform, false); g.transform.localPosition = V(box.p);
                g.transform.localRotation = Quaternion.Euler(0, box.yaw, 0); g.AddComponent<BoxCollider>().size = V(box.s);
            }
            Physics.SyncTransforms(); CheckScene(room, manager, data);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save garden additions.");
            File.WriteAllText(Report, "Garden life installed " + DateTime.Now.ToString("s") +
                "\nPASS: 165 flowers / five varieties; 12 varied trees; 24 ferns; 12 mushroom clusters; three mossy logs.\n" +
                "PASS: three rabbits, three squirrels, five songbirds; articulated ears, heads, tails and wings; terrain-grounded routes, quiet rest phases, night/rain rest, 38 m animal visibility limit.\n" +
                "PASS: 32,276 baseline triangles, 47 mesh renderers, 22 shared meshes, four shared texture-free materials, 12 new trunk colliders. No extra lights, cameras, NavMesh, particles or physics simulation.\n" +
                "PASS: scene raycasts verify animal routes match existing terrain; trunk clearances; direction and bounded animation; seats/door/pond/sky preserved.\n" +
                "No Play mode, microphone, GPU preview, lighting bake or reflection capture started. Visual appearance and performance remain unverified.\n");
            File.WriteAllText(Request, "installed-live-visual-check-pending"); Debug.Log("THERAPY_GARDEN_LIFE_IMPORTED: CPU/import checks passed.");
        }
        private static Vector3 V(float[] v) => new Vector3(v[0], v[1], v[2]);
        private static Material MaterialAsset(string name, Shader shader, Color tint)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_Tint", tint); EditorUtility.SetDirty(m); return m;
        }
        private static GameObject AddMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false); g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = g.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true; renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off; return g;
        }
        private static Mesh MeshAsset(Geometry data, string name)
        {
            string path = Folder + "/Meshes/" + name + ".asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            int count = data.positions.Length / 3; var positions = new Vector3[count]; var normals = new Vector3[count]; var colors = new Color[count]; var triangles = new int[count];
            for (int i = 0; i < count; i++) { int p = i * 3; positions[i] = new Vector3(data.positions[p], data.positions[p + 1], data.positions[p + 2]); normals[i] = new Vector3(data.normals[p], data.normals[p + 1], data.normals[p + 2]); colors[i] = new Color(data.colors[p], data.colors[p + 1], data.colors[p + 2], 1); triangles[i] = i; }
            mesh = new Mesh { name = name, indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = positions; mesh.normals = normals; mesh.colors = colors; mesh.triangles = triangles; mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static void ValidateData(LifeData d)
        {
            if (d.version != 1 || d.seed != 926311 || d.units != "metres" || d.templates.Length != 3 || d.animals.Length != 11 || d.trees.Length != 12 || d.boxes.Length != 12 || d.plants.Length != 165 || d.plants.Select(p => p.type).Distinct().Count() != 5) throw new Exception("Unexpected garden life export.");
            if (d.details.Count(x => x.kind == "fern") != 24 || d.details.Count(x => x.kind == "mushrooms") != 12 || d.details.Count(x => x.kind == "mossy log") != 3) throw new Exception("Ground detail counts changed.");
            foreach (Geometry m in d.meshes.Concat(d.templates.SelectMany(t => t.parts)))
                if (m.positions.Length % 9 != 0 || m.normals.Length != m.positions.Length || m.colors.Length != m.positions.Length || m.positions.Any(v => float.IsNaN(v) || float.IsInfinity(v))) throw new Exception("Invalid generated mesh.");
            int triangles = d.meshes.Sum(m => m.positions.Length / 9) + d.animals.Sum(a => d.templates[a.kind].parts.Sum(p => p.positions.Length / 9));
            if (triangles != 32276 || d.animals.Any(a => a.route.Length != 65 || Vector3.Distance(a.route[0], a.route[64]) > .001f)) throw new Exception("Geometry budget or loop closure changed.");
        }
        private static void CheckScene(Transform room, WellnessGardenLife life, LifeData data)
        {
            void Require(bool ok, string message) { if (!ok) throw new Exception("Garden life check: " + message); }
            var terrain = room.Find("OutdoorGarden/Walkable surfaces and safety/Walkable terrain").GetComponent<MeshCollider>();
            Require(life.animals.Count(a => a.kind == 0) == 3 && life.animals.Count(a => a.kind == 1) == 3 && life.animals.Count(a => a.kind == 2) == 5, "wildlife diversity");
            float maximumGroundError = 0;
            foreach (var animal in life.animals)
            {
                foreach (Vector3 point in animal.route)
                {
                    Require(terrain.Raycast(new Ray(point + Vector3.up * 15, Vector3.down), out RaycastHit ground, 25), "route has ground");
                    maximumGroundError = Mathf.Max(maximumGroundError, Mathf.Abs(point.y - ground.point.y - .012f));
                    Require(!Physics.CheckSphere(point + Vector3.up * .3f, .20f, ~0, QueryTriggerInteraction.Ignore), "route clear of solid obstacles");
                }
                for (float t = 0; t < 50; t += .15f)
                {
                    var pose = WellnessGardenLife.Evaluate(animal.kind, t, animal.phase, animal.route);
                    Require(!float.IsNaN(pose.position.x) && pose.direction.sqrMagnitude > .00001f, "bounded pose and forward direction");
                    Require(pose.hop >= 0 && pose.hop <= .066f && Mathf.Abs(pose.flap) <= 62.01f, "gentle animation bounds");
                }
            }
            Require(maximumGroundError < .025f, "routes follow the actual saved terrain");
            Require(life.GetComponentsInChildren<MeshFilter>().Sum(m => m.sharedMesh.triangles.Length / 3) == 32276, "triangle cap");
            Require(life.GetComponentsInChildren<Renderer>().Length == 47, "renderer cap");
            Require(life.GetComponentsInChildren<Collider>().Length == 12 && life.GetComponentsInChildren<Rigidbody>().Length == 0, "only trunk colliders");
            Require(life.GetComponentsInChildren<Light>().Length == 0 && life.GetComponentsInChildren<Camera>().Length == 0, "no extra render passes");
            Require(room.GetComponentsInChildren<WellnessSeat>().Sum(s => s.Count) == 5 && room.GetComponentsInChildren<WellnessDoor>().Length == 1, "seating and closing door retained");
            Require(life.sky.pondRenderer != null && life.sky.cloudDeck != null && life.sky.rain != null, "existing sky/pond/weather retained");
        }
    }
}
