using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TheLastWatch.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyGardenBoundaryUpgrade
    {
        private const string Root = "Assets/TherapyGame", Folder = Root + "/Exterior/Horizon";
        private const string Request = Root + "/GardenBoundaryRequest.txt", Report = Folder + "/BoundaryCheck.txt";
        private const string ApronName = "Continuous scenery beyond the fence";
        static TherapyGardenBoundaryUpgrade() { EditorApplication.delayCall += Once; }
        private static void Once()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "repair-garden-boundary-once") return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Once; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            { File.WriteAllText(Request, "manual-only"); Debug.LogWarning("Stop Play/baking, then choose Therapy Game > Repair Garden Horizon."); return; }
            File.WriteAllText(Request, "installing-once");
            try { Install(); }
            catch (Exception e)
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Report, "Boundary installation stopped safely: " + DateTime.Now.ToString("s") + "\n" + e);
                File.WriteAllText(Request, "needs-attention-no-auto-retry"); Debug.LogException(e);
            }
        }
        [MenuItem("Therapy Game/Repair Garden Horizon")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) throw new Exception("Stop Play/baking first.");
            RequireMemory();
            Scene scene = SceneManager.GetSceneByPath(Root + "/Scenes/TherapyRoom.unity");
            if (!scene.isLoaded) throw new Exception("Keep TherapyRoom open; no scene switching is performed.");
            Transform room = scene.GetRootGameObjects().Single(g => g.name == "TherapyRoom").transform;
            Transform garden = room.Find("OutdoorGarden");
            if (garden == null || garden.position.sqrMagnitude > .00001f || Quaternion.Angle(garden.rotation, Quaternion.identity) > .001f
                || Vector3.Distance(garden.lossyScale, Vector3.one) > .0001f) throw new Exception("Unexpected garden layout; leave the current scene untouched.");
            MeshCollider terrain = garden.GetComponentsInChildren<MeshCollider>(true).Single(c => c.name == "Walkable terrain");
            var fence = garden.GetComponentsInChildren<BoxCollider>(true).Where(c => c.name == "Garden boundary").ToArray();
            var mountains = garden.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.name.StartsWith("Mountains \u00b7 ") || r.name == "Snow peaks \u00b7 snow").ToArray();
            if (mountains.Length != 3) throw new Exception("Expected the original three mountain/snow batches, not other scenery.");
            var cycle = room.GetComponentsInChildren<WellnessSkyCycle>(true).Single();
            var camera = cycle.cloudDeck != null ? cycle.cloudDeck.viewer : null;
            if (camera == null || camera.clearFlags != CameraClearFlags.Skybox || cycle.skyMaterial == null || cycle.pondRenderer == null)
                throw new Exception("Existing sky, player camera and pond references are required.");
            if (room.GetComponentInChildren<WellnessGardenSafety>(true) == null) throw new Exception("Keep the existing garden recovery safety component.");
            var controller = room.GetComponentInChildren<CharacterController>(true);
            if (controller == null) throw new Exception("Expected an existing player controller.");
            Physics.SyncTransforms();
            string checks = GardenHorizonGeometry.RunChecks() + CheckFence(fence, terrain, controller);
            var geometry = GardenHorizonGeometry.Build(ExtractPerimeter(terrain, garden));
            float clearance = Mathf.Min(-12 - geometry.minX, geometry.maxX - 32, -22 - geometry.minZ, geometry.maxZ - 16);
            if (camera.farClipPlane + 20 > clearance) throw new Exception("Camera sees beyond the ground extension; do not install.");
            var materials = new[] { "meadow", "grass", "moss", "glade" }
                .Select(n => AssetDatabase.LoadAssetAtPath<Material>(Root + "/Exterior/Materials/" + n + ".mat")).ToArray();
            if (materials.Any(m => m == null)) throw new Exception("Original ground materials missing.");
            // Sequential, single-pass verification only. Never flush shader caches,
            // launch Unity again, generate reflection captures, or retry after an error.
            CheckShader(cycle.skyMaterial);
            CheckShader(cycle.pondRenderer.sharedMaterial);
            RequireMemory();
            string backup = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TherapyBackups", "GardenBoundary", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(backup);
            File.Copy(scene.path, Path.Combine(backup, "TherapyRoom-on-disk.unity"));
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not preserve the open scene before changes.");
            File.Copy(scene.path, Path.Combine(backup, "TherapyRoom-before-boundary.unity"));
            EnsureFolder(Folder);
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Continuous garden scenery and sky horizon");
            try
            {
                var mesh = new Mesh { name = "Continuous four-sided garden apron" };
                mesh.SetVertices(geometry.vertices.Select(p => new Vector3(p.x, p.y, p.z)).ToList());
                mesh.subMeshCount = 4;
                for (int i = 0; i < 4; i++) mesh.SetTriangles(geometry.triangles[i], i);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/GardenApron.asset");
                if (savedMesh == null) { AssetDatabase.CreateAsset(mesh, Folder + "/GardenApron.asset"); savedMesh = mesh; }
                else { Undo.RecordObject(savedMesh, "Refresh ground apron"); EditorUtility.CopySerialized(mesh, savedMesh); UnityEngine.Object.DestroyImmediate(mesh); }
                Transform existing = garden.Find(ApronName);
                GameObject apron = existing != null ? existing.gameObject : new GameObject(ApronName);
                if (existing == null) { apron.transform.SetParent(garden, false); Undo.RegisterCreatedObjectUndo(apron, "Add surrounding ground"); }
                // Unity's missing-component sentinel compares equal to null but
                // is not CLR null, so do not use ?? for component lookup.
                var filter = apron.GetComponent<MeshFilter>();
                if (filter == null) filter = Undo.AddComponent<MeshFilter>(apron);
                var renderer = apron.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = Undo.AddComponent<MeshRenderer>(apron);
                Undo.RecordObjects(new UnityEngine.Object[] { filter, renderer }, "Assign surrounding ground");
                filter.sharedMesh = savedMesh; renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                foreach (var mountain in mountains) { Undo.RecordObject(mountain.gameObject, "Retire distant model mountains"); mountain.gameObject.SetActive(false); }
                if (apron.GetComponentsInChildren<Collider>(true).Length != 0 || mountains.Any(m => m.gameObject.activeInHierarchy))
                    throw new Exception("Non-playable scenery must not add colliders or retain active mountain models.");
                if (filter.sharedMesh.triangles.Length / 3 != 5376 || renderer.sharedMaterials.Length != 4) throw new Exception("Apron geometry budget changed.");
                EditorUtility.SetDirty(filter); EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(savedMesh);
                AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the boundary repair.");
                File.WriteAllText(Report, "Garden boundary installed " + DateTime.Now.ToString("s") + "\n" + checks +
                    "PASS: all 224 real terrain perimeter vertices stitched, including every corner; one continuous apron, 5376 triangles, four existing ground materials.\n" +
                    "PASS: outer edge at least " + clearance.ToString("F1") + "m from the fence; existing camera far clip " + camera.farClipPlane + "m unchanged.\n" +
                    "PASS: three original mountain/snow mesh batches disabled and recoverable. Distant hills, forest silhouettes and mountains are a seamless direction-only sky effect shared with pond reflections.\n" +
                    "PASS: original terrain, four fence colliders, garden, house, player recovery, weather, night effects and camera range preserved. No new collider, runtime script, texture, light, camera, bake, Play mode or microphone.\n" +
                    "PASS: sky and pond shader passes compiled sequentially without shader errors.\nBackup: " + backup + "\n" +
                    "Final appearance and frame time still need a user-controlled in-game check; no test frame was rendered.\n");
                File.WriteAllText(Request, "installed-live-check-pending"); Undo.CollapseUndoOperations(undo);
                Debug.Log("THERAPY_GARDEN_BOUNDARY_READY: complete ground extension, sky-only mountains, fence safety verified; scene backed up and saved.");
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
        private static GardenHorizonGeometry.Point[] ExtractPerimeter(MeshCollider terrain, Transform garden)
        {
            if (terrain.sharedMesh == null || !terrain.enabled || !terrain.gameObject.activeInHierarchy) throw new Exception("Walkable terrain unavailable.");
            var vertices = terrain.sharedMesh.vertices.Select(v => garden.InverseTransformPoint(terrain.transform.TransformPoint(v))).ToArray();
            float minX = vertices.Min(v => v.x), maxX = vertices.Max(v => v.x), minZ = vertices.Min(v => v.z), maxZ = vertices.Max(v => v.z);
            if (Mathf.Abs(minX + 40) > .001 || Mathf.Abs(maxX - 50) > .001 || Mathf.Abs(minZ + 42) > .001 || Mathf.Abs(maxZ - 36) > .001)
                throw new Exception("Terrain layout changed; do not apply a guessed edge.");
            var unique = new Dictionary<Vector2Int, Vector3>();
            foreach (var v in vertices)
            {
                if (Mathf.Abs(v.x - minX) > .001 && Mathf.Abs(v.x - maxX) > .001 && Mathf.Abs(v.z - minZ) > .001 && Mathf.Abs(v.z - maxZ) > .001) continue;
                var key = new Vector2Int(Mathf.RoundToInt(v.x * 1000), Mathf.RoundToInt(v.z * 1000));
                if (unique.TryGetValue(key, out var p) && Vector3.Distance(p, v) > .0001f) throw new Exception("Terrain has mismatched edge heights.");
                unique[key] = v;
            }
            if (unique.Count != 224) throw new Exception("Expected all 224 boundary vertices, not a partial perimeter.");
            var ordered = unique.Values.OrderBy(v => Mathf.Atan2(v.z + 3, v.x - 5)).ToArray();
            for (int i = 0; i < ordered.Length; i++)
            {
                Vector3 d = ordered[(i + 1) % ordered.Length] - ordered[i]; d.y = 0;
                if (Mathf.Abs(d.magnitude - 1.5f) > .001f) throw new Exception("Missing perimeter segment.");
            }
            return ordered.Select(v => new GardenHorizonGeometry.Point(v.x, v.y, v.z)).ToArray();
        }
        private static string CheckFence(BoxCollider[] fence, MeshCollider terrain, CharacterController controller)
        {
            if (fence.Length != 4 || fence.Any(c => !c.enabled || c.isTrigger || !c.gameObject.activeInHierarchy || Physics.GetIgnoreLayerCollision(controller.gameObject.layer, c.gameObject.layer)))
                throw new Exception("All four solid fence boundaries must block the player layer.");
            var corners = new[] { new Vector3(-12, 0, -22), new Vector3(32, 0, -22), new Vector3(32, 0, 16), new Vector3(-12, 0, 16) };
            int count = 0;
            for (int edge = 0; edge < 4; edge++)
            {
                Vector3 a = corners[edge], b = corners[(edge + 1) % 4];
                Vector3 outward = Vector3.Cross(Vector3.up, (b - a).normalized);
                int steps = Mathf.CeilToInt(Vector3.Distance(a, b) / .75f);
                for (int s = 0; s <= steps; s++) count += CheckCrossing(Vector3.Lerp(a, b, (float)s / steps), outward, fence, terrain, controller);
                // Diagonal corner exits as well as every straight section.
                count += CheckCrossing(a, (a - new Vector3(10, 0, -3)).normalized, fence, terrain, controller);
            }
            return "PASS: " + count + " Edit-mode player-sized capsule sweeps blocked by the existing fence, including all four corners and +2m elevated attempts.\n";
        }
        private static int CheckCrossing(Vector3 position, Vector3 outward, BoxCollider[] fence, MeshCollider terrain, CharacterController controller)
        {
            if (!terrain.Raycast(new Ray(position + Vector3.up * 50, Vector3.down), out var ground, 100)) throw new Exception("Terrain missing below the fence.");
            float radius = controller.radius * Mathf.Max(controller.transform.lossyScale.x, controller.transform.lossyScale.z);
            float height = Mathf.Max(radius * 2, controller.height * controller.transform.lossyScale.y);
            foreach (float lift in new[] { .05f, 2f })
            {
                Vector3 foot = new Vector3(position.x, ground.point.y + lift, position.z) - outward * 1.5f;
                var hits = Physics.CapsuleCastAll(foot + Vector3.up * radius, foot + Vector3.up * (height - radius), radius,
                    outward, 3, ~0, QueryTriggerInteraction.Ignore);
                if (!hits.Any(hit => fence.Contains(hit.collider))) throw new Exception("Fence allows an exit near " + position + ". Leave scene unchanged.");
            }
            return 2;
        }
        private static void CheckShader(Material material)
        {
            RequireMemory();
            if (material == null || material.shader == null) throw new Exception("Sky/pond material missing.");
            bool previous = ShaderUtil.allowAsyncCompilation;
            try { ShaderUtil.allowAsyncCompilation = false; ShaderUtil.CompilePass(material, 0, true); }
            finally { ShaderUtil.allowAsyncCompilation = previous; }
            if (ShaderUtil.ShaderHasError(material.shader)) throw new Exception("Shader check failed; no automatic retry: " + material.name + "\n"
                + string.Join("\n", ShaderUtil.GetShaderMessages(material.shader).Select(m => m.message)));
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint length, load;
            public ulong totalPhysical, availablePhysical, totalPageFile, availablePageFile, totalVirtual, availableVirtual, availableExtendedVirtual;
        }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        private static void RequireMemory()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor) return;
            var memory = new MemoryStatus { length = (uint)Marshal.SizeOf(typeof(MemoryStatus)) };
            if (!GlobalMemoryStatusEx(ref memory) || memory.availablePageFile < 1536UL * 1024 * 1024 || memory.availablePhysical < 1024UL * 1024 * 1024)
                throw new Exception("Not enough memory headroom for the limited import. Close unused apps, keep Play stopped, then use Therapy Game > Repair Garden Horizon. Nothing will retry automatically.");
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            if (Directory.Exists(path)) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); return; }
            int slash = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, slash)); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
