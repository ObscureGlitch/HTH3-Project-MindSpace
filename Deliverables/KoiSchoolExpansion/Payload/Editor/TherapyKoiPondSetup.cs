using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using TheLastWatch.Environment;

namespace TherapyGame.Editor
{
    public static class TherapyKoiPondSetup
    {
        const string Root = "Assets/TherapyGame/";
        const string Assets = Root + "Exterior/KoiPond";
        const string GroupName = "Koi fish and pond bed";
        const string Report = Root + "Documentation/KoiPondCheck.txt";
        [Serializable] public sealed class Source { public int version; public string sourceSha256; public SourceFish[] fish; }
        [Serializable] public sealed class SourceFish { public string name; public SourcePart[] parts; }
        [Serializable] public sealed class SourcePart { public string name; public float[] pivot, positions, normals, colors; public int[] triangles; }
        static void Require(bool test, string message) { if (!test) throw new InvalidOperationException("Koi pond: " + message); }
        static Transform Garden()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play mode first.");
            Require(!EditorApplication.isCompiling && !EditorApplication.isUpdating && !Lightmapping.isRunning, "Wait for importing, compiling and baking to finish.");
            var scene = EditorSceneManager.GetActiveScene();
            Require(scene.name == "TherapyRoom" && scene.isLoaded, "Open TherapyRoom first.");
            var room = scene.GetRootGameObjects().SingleOrDefault(x => x.name == "TherapyRoom");
            var garden = room == null ? null : room.transform.Find("OutdoorGarden");
            Require(garden != null, "Existing OutdoorGarden was not found.");
            Require(garden.position.sqrMagnitude < .0001f && Quaternion.Angle(garden.rotation, Quaternion.identity) < .001f && (garden.lossyScale - Vector3.one).sqrMagnitude < .0001f, "Garden transform changed; check world-space routes before installing.");
            return garden;
        }
        static Renderer Water(Transform garden)
        {
            var surfaces = garden.GetComponentsInChildren<MeshRenderer>(true).Where(x => x.sharedMaterial != null && x.sharedMaterial.shader.name == "Therapy Game/Quiet Pond").ToArray();
            Require(surfaces.Length == 1, "Expected exactly one authored pond surface.");
            return surfaces[0];
        }
        static MeshCollider Ground(Transform garden)
        {
            var t = garden.Find("Walkable surfaces and safety/Walkable terrain");
            var c = t == null ? null : t.GetComponent<MeshCollider>();
            Require(c != null && c.enabled && c.sharedMesh != null, "Missing walkable pond bed."); return c;
        }
        static float Floor(MeshCollider ground, float x, float z)
        {
            Require(ground.Raycast(new Ray(new Vector3(x, 3, z), Vector3.down), out var hit, 10), "No pond bed at " + x + ", " + z);
            return hit.point.y;
        }
        [MenuItem("Therapy Game/Install Koi Pond")]
        public static void Install()
        {
            try { InstallChecked(); }
            catch (Exception e) { Directory.CreateDirectory(Path.GetDirectoryName(Report)); File.WriteAllText(Report, "FAILED\n" + e); Debug.LogException(e); }
        }
        static void InstallChecked()
        {
            var garden = Garden(); var water = Water(garden); var ground = Ground(garden);
            if (garden.Find(GroupName) != null) { Validate(); return; }
            Physics.SyncTransforms();
            Vector4 settings = water.sharedMaterial.GetVector("_Pond");
            Vector3 center = new Vector3(settings.x, water.bounds.center.y, settings.y);
            Vector2 radii = new Vector2(settings.z, settings.w);
            Require(radii.x >= 6.69f && radii.y >= 5.69f, "Pond is too small for the nine paired routes.");
            Require(water.sharedMaterial.HasProperty("_BedVisibility"), "Import the updated Quiet Pond shader first.");
            ValidateRoutes(water, ground, center, radii);
            var data = JsonUtility.FromJson<Source>(File.ReadAllText(Root + "Editor/KoiSource/KoiModels.json"));
            Require(data != null && data.version == 1 && data.fish.Length == 8, "Invalid source library.");
            foreach (var f in data.fish) foreach (var p in f.parts) ValidatePart(p);
            // Capture the open scene, including unsaved edits, without changing its path.
            string backup = "TherapyBackups/KoiPond/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            Require(EditorSceneManager.SaveScene(garden.gameObject.scene, backup + "/TherapyRoom.unity", true), "Could not make the scene backup.");
            Directory.CreateDirectory(Assets); AssetDatabase.Refresh();
            var library = AssetDatabase.LoadAssetAtPath<KoiPondLibrary>(Assets + "/KoiLibrary.asset");
            if (library == null) library = BuildLibrary(data);
            Require(library.sourceSha256 == data.sourceSha256, "Existing fish assets belong to a different source; keep them and inspect before replacing.");
            var bed = AssetDatabase.LoadAssetAtPath<Mesh>(Assets + "/PondBed.asset");
            if (bed == null) { bed = MakeBed(ground, center, radii); AssetDatabase.CreateAsset(bed, Assets + "/PondBed.asset"); }
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add swimming koi and pond bed");
            var group = new GameObject(GroupName); group.transform.SetParent(garden, false);
            Undo.RegisterCreatedObjectUndo(group, "Add koi pond");
            try
            {
                var scenery = new GameObject("Submerged pebbles, sand and aquatic plants"); scenery.transform.SetParent(group.transform, false);
                scenery.AddComponent<MeshFilter>().sharedMesh = bed;
                var renderer = scenery.AddComponent<MeshRenderer>(); renderer.sharedMaterial = library.material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var school = group.AddComponent<WellnessKoiPond>(); school.library = library; school.waterSurface = water;
                school.pondCenter = center; school.pondRadii = radii;
                EditorUtility.SetDirty(school); Validate();
                AssetDatabase.SaveAssetIfDirty(library); AssetDatabase.SaveAssetIfDirty(bed);
                EditorSceneManager.MarkSceneDirty(garden.gameObject.scene);
                Require(EditorSceneManager.SaveScene(garden.gameObject.scene), "Could not save the installed scene.");
                File.AppendAllText(Report, "Scene saved. Backup: " + Path.GetFullPath(backup) + "\n");
                Undo.CollapseUndoOperations(undo);
                Debug.Log("KOI_POND_INSTALLED: eighteen koi with subtle size variation, intermittent swimming, and submerged pond-bed details. " + Report);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
        [MenuItem("Therapy Game/Update Koi School")]
        public static void UpdateSchool()
        {
            try
            {
                var garden = Garden(); var group = garden.Find(GroupName);
                Require(group != null, "Install Koi Pond first.");
                var school = group.GetComponent<WellnessKoiPond>();
                Require(school != null, "Missing existing koi controller.");
                // Validate the wider route set and larger fish before changing the saved count.
                Validate();
                string backup = "TherapyBackups/KoiPond/School-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                Directory.CreateDirectory(backup);
                Require(EditorSceneManager.SaveScene(garden.gameObject.scene, backup + "/TherapyRoom.unity", true), "Could not back up the open scene.");
                Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Increase koi school to eighteen");
                Undo.RecordObject(school, "Increase koi school to eighteen");
                try
                {
                    school.fishCount = KoiSchoolLayout.MaximumFish;
                    EditorUtility.SetDirty(school); Validate();
                    EditorSceneManager.MarkSceneDirty(garden.gameObject.scene);
                    Require(EditorSceneManager.SaveScene(garden.gameObject.scene), "Could not save the updated school.");
                    File.AppendAllText(Report, "School update saved: 18 koi, sizes 90% to 108%. Existing fish assets and pond decoration retained.\nBackup: " + Path.GetFullPath(backup) + "\n");
                    Undo.CollapseUndoOperations(undo);
                    Debug.Log("KOI_SCHOOL_UPDATED: 18 koi with subtle size variation.");
                }
                catch { Undo.RevertAllDownToGroup(undo); throw; }
            }
            catch (Exception e) { Directory.CreateDirectory(Path.GetDirectoryName(Report)); File.WriteAllText(Report, "FAILED\n" + e); Debug.LogException(e); }
        }
        static void ValidatePart(SourcePart p)
        {
            Require(p.positions != null && p.positions.Length % 3 == 0 && p.normals.Length == p.positions.Length && p.colors.Length == p.positions.Length, "Invalid mesh arrays.");
            Require(p.pivot.Length == 3 && p.triangles.Length % 3 == 0, "Invalid part metadata.");
            Require(p.positions.All(v => !float.IsNaN(v) && !float.IsInfinity(v)) && p.normals.All(v => !float.IsNaN(v) && !float.IsInfinity(v)), "Non-finite mesh data.");
            Require(p.triangles.All(i => i >= 0 && i < p.positions.Length / 3), "Out-of-range triangle index.");
        }
        static KoiPondLibrary BuildLibrary(Source data)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "Exterior/LivingGarden/Shaders/GardenLife.shader");
            Require(shader != null, "Missing existing Garden Life shader.");
            var library = ScriptableObject.CreateInstance<KoiPondLibrary>();
            library.name = "Provided koi varieties"; library.sourceSha256 = data.sourceSha256;
            AssetDatabase.CreateAsset(library, Assets + "/KoiLibrary.asset");
            library.material = new Material(shader) { name = "Koi and natural pond bed" };
            library.material.SetColor("_Tint", Color.white); AssetDatabase.AddObjectToAsset(library.material, library);
            library.varieties = new KoiPondLibrary.Variety[data.fish.Length];
            for (int f = 0; f < data.fish.Length; f++)
            {
                var input = data.fish[f]; var variety = new KoiPondLibrary.Variety { name = input.name, parts = new KoiPondLibrary.Part[input.parts.Length] };
                for (int i = 0; i < input.parts.Length; i++)
                {
                    var p = input.parts[i]; int count = p.positions.Length / 3;
                    var vertices = new Vector3[count]; var normals = new Vector3[count]; var colors = new Color[count];
                    for (int v = 0; v < count; v++)
                    { vertices[v] = V(p.positions, v); normals[v] = V(p.normals, v).normalized; colors[v] = new Color(p.colors[v * 3], p.colors[v * 3 + 1], p.colors[v * 3 + 2], 1); }
                    var mesh = new Mesh { name = input.name + " " + p.name };
                    mesh.vertices = vertices; mesh.normals = normals; mesh.colors = colors; mesh.triangles = p.triangles; mesh.RecalculateBounds();
                    AssetDatabase.AddObjectToAsset(mesh, library);
                    variety.parts[i] = new KoiPondLibrary.Part { name = p.name, mesh = mesh, pivot = V(p.pivot, 0) };
                }
                library.varieties[f] = variety;
            }
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library); return library;
        }
        static Vector3 V(float[] values, int i) => new Vector3(values[i * 3], values[i * 3 + 1], values[i * 3 + 2]);
        static bool Inside(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(d) < .000001f) return false;
            float u = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.z - c.z)) / d;
            float v = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.z - c.z)) / d;
            return u >= 0 && v >= 0 && u + v <= 1;
        }
        static void ValidateRoutes(Renderer water, MeshCollider ground, Vector3 center, Vector2 radii)
        {
            var mesh = water.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices; int[] indices = mesh.triangles;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = water.transform.TransformPoint(vertices[i]);
            // Includes the complete fish envelope, fin swing, and conservative ripple allowance.
            for (int lane = 0; lane < KoiSchoolLayout.RouteCount; lane++) for (int a = 0; a < 360; a += 2)
            {
                float angle = a * Mathf.Deg2Rad, r = WellnessKoiPond.FirstLane + lane * WellnessKoiPond.LaneSpacing;
                Vector3 pos = center + new Vector3(radii.x * r * Mathf.Cos(angle), -WellnessKoiPond.SwimDepth, radii.y * r * Mathf.Sin(angle));
                Vector3 forward = new Vector3(-radii.x * Mathf.Sin(angle), 0, radii.y * Mathf.Cos(angle)).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, forward);
                foreach (float z in new[] { -.4f, 0f, .4f }) foreach (float x in new[] { -.14f, 0f, .14f })
                {
                    Vector3 test = pos + forward * z + side * x;
                    bool wet = false;
                    for (int t = 0; t < indices.Length; t += 3) if (Inside(test, vertices[indices[t]], vertices[indices[t + 1]], vertices[indices[t + 2]])) { wet = true; break; }
                    Require(wet, "A route reaches the shore; no changes applied.");
                    Require(pos.y - .1f - Floor(ground, test.x, test.z) >= .07f, "Insufficient clearance above the pond bed.");
                }
                Require(pos.y + .12f < center.y - .09f, "A fish can breach the displaced water surface.");
            }
        }
        [MenuItem("Therapy Game/Validate Koi Pond")]
        public static void Validate()
        {
            var garden = Garden(); var group = garden.Find(GroupName);
            Require(group != null, "Install the pond first.");
            var school = group.GetComponent<WellnessKoiPond>();
            Require(school != null && school.library != null && school.waterSurface == Water(garden), "Missing installed references.");
            Require(school.library.varieties.Length == 8 && school.fishCount >= 1 && school.fishCount <= KoiSchoolLayout.MaximumFish, "Expected eight varieties and between one and eighteen runtime fish.");
            Require(school.pondRadii.x >= 6.69f && school.pondRadii.y >= 5.69f, "Pond is too small for the tested route spacing.");
            Require(group.GetComponentsInChildren<Collider>(true).Length == 0, "Decorations must not obstruct the shallow pond.");
            int maxTriangles = 0;
            foreach (var variety in school.library.varieties)
            {
                Require(variety.parts.Length == 4, "Missing animatable parts."); int triangles = 0;
                foreach (var part in variety.parts)
                {
                    Require(part.mesh != null && part.mesh.vertexCount < 16000, "Missing or oversized fish mesh.");
                    Require(part.mesh.colors.Length == part.mesh.vertexCount, "Lost supplied color markings.");
                    var b = part.mesh.bounds; b.center += part.pivot;
                    Require(b.min.y * WellnessKoiPond.MaximumScale >= -.10f && b.max.y * WellnessKoiPond.MaximumScale <= .12f, "Fish exceeds tested vertical envelope.");
                    Require(Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)) * WellnessKoiPond.MaximumScale < .14f && Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z)) * WellnessKoiPond.MaximumScale < .4f, "Fish exceeds route envelope.");
                    triangles += part.mesh.triangles.Length / 3;
                }
                Require(triangles < 10000, "Fish polygon budget exceeded."); maxTriangles = Mathf.Max(maxTriangles, triangles);
            }
            Physics.SyncTransforms(); ValidateRoutes(school.waterSurface, Ground(garden), school.pondCenter, school.pondRadii);
            var bed = group.GetComponentInChildren<MeshFilter>(); Require(bed != null && bed.sharedMesh != null, "Missing bed decoration.");
            Require(bed.sharedMesh.bounds.max.y < school.pondCenter.y - .06f, "Decoration rises above the water.");
            var shader = school.waterSurface.sharedMaterial.shader;
            Require(shader != null && !ShaderUtil.ShaderHasError(shader), "Water shader has import errors.");
            Require(!ShaderUtil.ShaderHasError(school.library.material.shader), "Fish shader has import errors.");
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, "PASS: supplied GLB varieties linked, 4 articulated parts per fish, vertex markings retained.\n" +
                "PASS: 9 paired routes, 14,580 bed/shore envelope samples, vertical wave clearance, zero new colliders.\n" +
                "PASS: submerged combined decoration mesh, no water or terrain geometry replaced.\n" +
                "School: " + school.fishCount + " fish; sizes 90% to 108%, stratified and shuffled each session.\n" +
                "Budget: at most " + maxTriangles * school.fishCount + " fish triangles, " + school.fishCount * 4 + " fish renderers, one bed renderer.\n" +
                "No texture imports, additional reflection camera, runtime mesh generation, physics bodies, or per-frame collection allocation.\n" +
                "Visual appearance, shader rendering and actual frame rate still require a Play-mode check.\n");
            Debug.Log("KOI_POND_CHECKS_PASSED");
        }
        static Mesh MakeBed(MeshCollider ground, Vector3 center, Vector2 radii)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
            var random = new System.Random(290926);
            float R(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
            Color[] stones = { new Color(.32f,.35f,.30f), new Color(.48f,.44f,.35f), new Color(.23f,.29f,.28f), new Color(.57f,.53f,.42f) };
            for (int cluster = 0; cluster < 16; cluster++)
            {
                float a = cluster * Mathf.PI * 2 / 16, radius = cluster < 3 ? .10f : R(.67f, .74f);
                Vector3 p = center + new Vector3(Mathf.Cos(a) * radii.x * radius, 0, Mathf.Sin(a) * radii.y * radius);
                p.y = Floor(ground, p.x, p.z);
                if (p.y > center.y - .25f) continue;
                Pebble(p + Vector3.up * .003f, new Vector3(.43f,.009f,.32f), new Color(.35f,.36f,.24f), vertices, normals, colors, triangles);
                for (int i = 0; i < 6; i++)
                {
                    Vector3 q = p + new Vector3(R(-.33f,.33f), 0, R(-.24f,.24f)); q.y = Floor(ground, q.x, q.z) + .012f;
                    Pebble(q, new Vector3(R(.075f,.15f),R(.018f,.037f),R(.06f,.12f)), stones[random.Next(stones.Length)], vertices, normals, colors, triangles);
                }
                if (cluster >= 3 && cluster % 2 == 0)
                    for (int blade = 0; blade < 7; blade++)
                    {
                        Vector3 q = p + new Vector3(R(-.11f,.11f),0,R(-.11f,.11f)); q.y = Floor(ground,q.x,q.z)+.006f;
                        Leaf(q,R(.075f,.15f),R(0,360),new Color(.13f,.25f,.13f),vertices,normals,colors,triangles);
                    }
            }
            var mesh = new Mesh { name = "Combined submerged pebbles and aquatic plants" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); return mesh;
        }
        static void Pebble(Vector3 p, Vector3 scale, Color color, List<Vector3> v, List<Vector3> n, List<Color> c, List<int> t)
        {
            int start = v.Count; const int rings = 4, sides = 8;
            for (int y = 0; y <= rings; y++) for (int x = 0; x <= sides; x++)
            {
                float lat = y * Mathf.PI / rings, a = x * Mathf.PI * 2 / sides;
                Vector3 normal = new Vector3(Mathf.Sin(lat)*Mathf.Cos(a), Mathf.Cos(lat), Mathf.Sin(lat)*Mathf.Sin(a));
                v.Add(p + Vector3.Scale(normal, scale)); n.Add(new Vector3(normal.x / scale.x, normal.y / scale.y, normal.z / scale.z).normalized); c.Add(color);
            }
            for (int y = 0; y < rings; y++) for (int x = 0; x < sides; x++)
            { int a=start+y*(sides+1)+x,b=a+sides+1; t.Add(a);t.Add(a+1);t.Add(b);t.Add(a+1);t.Add(b+1);t.Add(b); }
        }
        static void Leaf(Vector3 p, float height, float yaw, Color color, List<Vector3> v, List<Vector3> n, List<Color> c, List<int> t)
        {
            Vector3 right = Quaternion.Euler(0,yaw,0)*Vector3.right*.018f, bend = Quaternion.Euler(0,yaw,0)*Vector3.forward*.045f;
            Vector3[] points = {p-right,p+right,p+Vector3.up*height*.65f+bend+right*.7f,p+Vector3.up*height*.65f+bend-right*.7f,p+Vector3.up*height+bend*1.4f};
            int[] faces={0,1,2,0,2,3,3,2,4};
            for(int side=0;side<2;side++)for(int i=0;i<faces.Length;i+=3)
            {
                Vector3 a=points[faces[i]],b=points[faces[i+(side==0?1:2)]],d=points[faces[i+(side==0?2:1)]];
                Vector3 normal=Vector3.Cross(b-a,d-a).normalized;
                foreach(Vector3 point in new[]{a,b,d}){t.Add(v.Count);v.Add(point);n.Add(normal);c.Add(color);}
            }
        }
    }
}
