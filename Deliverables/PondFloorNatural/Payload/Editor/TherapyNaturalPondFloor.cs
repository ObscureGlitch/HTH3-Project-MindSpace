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
    public static class TherapyNaturalPondFloor
    {
        const string Root = "Assets/TherapyGame/";
        const string DataPath = Root + "Editor/PondFloorSource/NaturalPondFloor.json";
        const string Report = Root + "Documentation/NaturalPondFloorCheck.txt";
        const string MeshName = "Natural pond floor v1 - 88 unique forms";
        [Serializable] public sealed class FloorData
        {
            public int version, seed;
            public float waterLevel;
            public float[] center, radii;
            public string terrainSourceSha256;
            public Piece[] pieces;
        }
        [Serializable] public sealed class Piece
        {
            public string id, kind, signature;
            public float[] anchor, positions, floorHeights, colors, normals;
        }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Natural pond floor: " + message); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static Vector3 V(float[] values, int i) => new Vector3(values[3*i], values[3*i+1], values[3*i+2]);

        [MenuItem("Therapy Game/Install Natural Pond Floor")]
        public static void Install()
        {
            try { InstallChecked(); }
            catch (Exception e)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Report));
                File.WriteAllText(Report, "FAILED\n" + e); Debug.LogException(e);
            }
        }
        static void InstallChecked()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play mode first.");
            Require(!EditorApplication.isCompiling && !EditorApplication.isUpdating && !Lightmapping.isRunning, "Wait for importing, compiling and baking to finish.");
            var scene = EditorSceneManager.GetActiveScene();
            Require(scene.name == "TherapyRoom" && scene.isLoaded, "Open TherapyRoom first.");
            var room = scene.GetRootGameObjects().SingleOrDefault(x => x.name == "TherapyRoom");
            var garden = room == null ? null : room.transform.Find("OutdoorGarden");
            var group = garden == null ? null : garden.Find("Koi fish and pond bed");
            Require(group != null, "The installed koi pond was not found.");
            var school = group.GetComponent<WellnessKoiPond>();
            Require(school != null && school.waterSurface != null && school.library != null, "Missing existing pond references.");
            var filters = group.GetComponentsInChildren<MeshFilter>(true);
            Require(filters.Length == 1, "Expected one existing floor mesh with Play stopped; refusing to replace other objects.");
            var filter = filters[0]; var renderer = filter.GetComponent<MeshRenderer>();
            Require(filter.sharedMesh != null && renderer != null && renderer.sharedMaterial == school.library.material, "Unexpected floor mesh or material.");
            Require(filter.transform.position.sqrMagnitude < .00001f && Quaternion.Angle(filter.transform.rotation, Quaternion.identity) < .001f && (filter.transform.lossyScale - Vector3.one).sqrMagnitude < .00001f, "Floor transform changed; inspect before applying world-space scenery.");
            var groundTransform = garden.Find("Walkable surfaces and safety/Walkable terrain");
            var ground = groundTransform == null ? null : groundTransform.GetComponent<MeshCollider>();
            Require(ground != null && ground.enabled, "Missing authored walkable terrain.");
            Physics.SyncTransforms();
            // Retain the current eighteen-fish controller, water simulation, and terrain.
            TherapyKoiPondSetup.Validate();
            var data = JsonUtility.FromJson<FloorData>(File.ReadAllText(DataPath));
            Require(data != null && data.version == 1 && data.pieces != null && data.pieces.Length == 88, "Invalid prepared decoration data.");
            Require(Mathf.Abs(data.waterLevel - school.pondCenter.y) < .002f && Mathf.Abs(data.center[0] - school.pondCenter.x) < .002f && Mathf.Abs(data.center[1] - school.pondCenter.z) < .002f, "Pond position changed since preparation.");
            Require(Mathf.Abs(data.radii[0] - school.pondRadii.x) < .002f && Mathf.Abs(data.radii[1] - school.pondRadii.y) < .002f, "Pond size changed since preparation.");
            ValidateData(data, school.waterSurface, ground);
            var counts = data.pieces.GroupBy(p => p.kind).OrderBy(p => p.Key).Select(p => p.Key + ": " + p.Count()).ToArray();
            int triangles = data.pieces.Sum(p => p.positions.Length / 9);
            if (filter.sharedMesh.name == MeshName)
            {
                Require(filter.sharedMesh.vertexCount == triangles * 3 && filter.sharedMesh.triangles.Length == triangles * 3, "Previously installed mesh differs from checked data.");
                WriteReport(counts, triangles, school.fishCount, "Already installed; no duplicate decorations created.");
                Debug.Log("NATURAL_POND_FLOOR_CHECKS_PASSED"); return;
            }
            string backup = "TherapyBackups/KoiPond/NaturalFloor-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            Require(EditorSceneManager.SaveScene(scene, backup + "/TherapyRoom.unity", true), "Could not back up the open scene.");
            string previousMesh = AssetDatabase.GetAssetPath(filter.sharedMesh);
            File.WriteAllText(backup + "/PreviousFloor.txt", previousMesh + "\nOriginal mesh asset retained; restore the scene backup or use Undo.\n");
            string folder = Root + "Exterior/KoiPond/NaturalFloor";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            Mesh mesh = BuildMesh(data);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/NaturalPondFloor.asset");
            AssetDatabase.CreateAsset(mesh, newPath); AssetDatabase.SaveAssetIfDirty(mesh);
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Natural pond-floor redesign");
            Undo.RecordObject(filter, "Replace repeated pond-floor clusters");
            Undo.RecordObject(filter.gameObject, "Name natural pond floor");
            try
            {
                filter.sharedMesh = mesh;
                filter.gameObject.name = "Natural pond floor - unique stones, wood, plants and leaves";
                EditorUtility.SetDirty(filter);
                TherapyKoiPondSetup.Validate();
                Require(group.GetComponentsInChildren<MeshRenderer>(true).Length == 1 && group.GetComponentsInChildren<Collider>(true).Length == 0, "Unexpected extra renderers or obstacles.");
                EditorSceneManager.MarkSceneDirty(scene);
                Require(EditorSceneManager.SaveScene(scene), "Could not save the redesigned pond.");
                Undo.CollapseUndoOperations(undo);
                WriteReport(counts, triangles, school.fishCount, "Scene saved. Backup: " + Path.GetFullPath(backup) + "\nNew mesh: " + newPath + "\nPrevious mesh retained: " + previousMesh);
                Debug.Log("NATURAL_POND_FLOOR_INSTALLED: 88 unique decorations in seven model families. " + Report);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
        static void ValidateData(FloorData data, Renderer water, MeshCollider ground)
        {
            var waterFilter = water.GetComponent<MeshFilter>();
            Require(waterFilter != null && waterFilter.sharedMesh != null, "Missing water surface geometry.");
            var wetVertices = waterFilter.sharedMesh.vertices; var wetIndices = waterFilter.sharedMesh.triangles;
            for (int i = 0; i < wetVertices.Length; i++) wetVertices[i] = water.transform.TransformPoint(wetVertices[i]);
            var ids = new HashSet<string>(); var signatures = new HashSet<string>(); var kinds = new HashSet<string>();
            int total = 0;
            foreach (var piece in data.pieces)
            {
                Require(!string.IsNullOrEmpty(piece.id) && ids.Add(piece.id), "Duplicate piece id.");
                Require(piece.signature != null && piece.signature.Length == 64 && signatures.Add(piece.signature), "Repeated local geometry signature.");
                kinds.Add(piece.kind);
                Require(piece.positions != null && piece.positions.Length % 9 == 0 && piece.normals.Length == piece.positions.Length && piece.colors.Length == piece.positions.Length && piece.floorHeights.Length == piece.positions.Length / 3, "Malformed model arrays.");
                for (int i = 0; i < piece.positions.Length / 3; i++)
                {
                    Vector3 v = V(piece.positions, i), n = V(piece.normals, i), color = V(piece.colors, i);
                    Require(Finite(v.x) && Finite(v.y) && Finite(v.z) && Finite(n.x) && Finite(n.y) && Finite(n.z) && Mathf.Abs(n.sqrMagnitude - 1) < .01f, "Invalid geometry or normal.");
                    Require(Finite(color.x) && Finite(color.y) && Finite(color.z) && color.x >= 0 && color.y >= 0 && color.z >= 0 && color.x <= 1 && color.y <= 1 && color.z <= 1, "Invalid vertex color.");
                    Require(ground.Raycast(new Ray(new Vector3(v.x,3,v.z), Vector3.down), out var hit, 10), "Decoration has no pond floor below it.");
                    Require(Mathf.Abs(hit.point.y - piece.floorHeights[i]) < .012f, "Loaded terrain differs from the prepared layout. No replacement applied.");
                    Require(v.y >= hit.point.y - .015f, "Decoration is buried too deeply.");
                    Require(v.y <= data.waterLevel - .085f, "Decoration reaches the water surface.");
                    float radius = new Vector2((v.x-data.center[0])/data.radii[0],(v.z-data.center[1])/data.radii[1]).magnitude;
                    Require(radius <= .901f, "Decoration reaches the shoreline.");
                    if (radius < .68f) Require(v.y <= data.waterLevel - WellnessKoiPond.SwimDepth - .15f + .0001f, "Decoration enters the fish swimming envelope.");
                    Require(!(Mathf.Abs(v.x-data.center[0]) < 1.449f && v.z < data.center[1] - 3.201f), "Decoration enters the reserved shallow entry.");
                    bool wet = false;
                    for (int t = 0; t < wetIndices.Length; t += 3)
                    {
                        if (Inside(v, wetVertices[wetIndices[t]], wetVertices[wetIndices[t+1]], wetVertices[wetIndices[t+2]])) { wet=true; break; }
                    }
                    Require(wet, "Decoration is outside the authored water surface.");
                }
                total += piece.positions.Length / 9;
            }
            Require(kinds.SetEquals(new[]{"driftwood","broadleaf-rosette","ribbon-grass","river-stone","slate-fragment","gravel-pocket","fallen-leaf"}), "Missing decoration family.");
            Require(total <= 22000, "Decoration geometry budget exceeded.");
        }
        static bool Inside(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z); if (Mathf.Abs(d)<.000001f) return false;
            float u=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.z-c.z))/d;
            float v=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.z-c.z))/d;
            return u>=-.00001f && v>=-.00001f && u+v<=1.00001f;
        }
        static Mesh BuildMesh(FloorData data)
        {
            int count=data.pieces.Sum(p=>p.positions.Length/3), offset=0;
            var vertices=new Vector3[count];var normals=new Vector3[count];var colors=new Color[count];var indices=new int[count];
            foreach(var piece in data.pieces) for(int i=0;i<piece.positions.Length/3;i++)
            {
                vertices[offset]=V(piece.positions,i);normals[offset]=V(piece.normals,i);Vector3 c=V(piece.colors,i);
                colors[offset]=new Color(c.x,c.y,c.z,1);indices[offset]=offset;offset++;
            }
            var mesh=new Mesh {name=MeshName,indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.triangles=indices;mesh.RecalculateBounds();return mesh;
        }
        static void WriteReport(string[] counts,int triangles,int fishCount,string tail)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report,"PASS: 88 individually generated pieces with unique local-geometry signatures; seven distinct model families.\n"+
                string.Join("; ",counts)+"\nPASS: every vertex checked against loaded terrain and authored water surface; south entry and fish clearance preserved.\n"+
                "PASS: only the floor mesh reference replaced; fish count remains "+fishCount+". Water, fish models, materials and terrain unchanged.\n"+
                "Geometry: "+triangles+" triangles; one combined renderer; zero colliders; zero runtime generation.\n"+
                "Appearance through the actual water and frame rate still require Play-mode visual confirmation.\n"+tail+"\n");
        }
    }
}
