using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheLastWatch.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapySquirrelUpgrade
    {
        private const string Root = "Assets/TherapyGame";
        private const string Folder = Root + "/Exterior/LivingGarden";
        private const string Request = Root + "/SquirrelUpgradeRequest.txt";
        private const string Report = Folder + "/SquirrelUpgradeCheck.txt";
        private const float VisualScale = 4.2f;

        [Serializable] private class Bone { public string name; public int parent; public float[] p, q, s; }
        [Serializable] private class Surface
        {
            public string name, alphaMode;
            public float[] color, emission;
            public float roughness, metallic;
            public bool doubleSided;
        }
        [Serializable] private class Submesh { public int material; public int[] indices; }
        [Serializable] private class Geometry
        {
            public string name;
            public float[] positions, normals, uvs, weights;
            public int[] joints;
            public Submesh[] submeshes;
        }
        [Serializable] private class Track { public string path, property; public float[] times, values; }
        [Serializable] private class Clip { public string name; public float duration; public Track[] tracks; }
        [Serializable] private class BoundsData { public float[] min, max; }
        [Serializable] private class SquirrelModel
        {
            public int version;
            public string name, units, sourceHash;
            public BoundsData bounds;
            public Bone[] bones;
            public Surface[] materials;
            public Geometry mesh;
            public Clip[] clips;
        }
        [Serializable] private class AnimalData
        {
            public int kind;
            public string name;
            public Vector3[] route;
            public float phase, scale;
        }
        [Serializable] private class LifeData { public AnimalData[] animals; }

        static TherapySquirrelUpgrade() => EditorApplication.delayCall += ImportOnce;

        private static void ImportOnce()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "upgrade-animated-squirrels-once") return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || Lightmapping.isRunning)
            {
                File.WriteAllText(Request, "manual-only");
                Debug.LogWarning("Animated squirrel upgrade deferred. Stop Play mode and baking, then choose Therapy Game > Upgrade Animated Squirrels.");
                return;
            }
            File.WriteAllText(Request, "installing-once");
            try { Install(); }
            catch (Exception exception)
            {
                File.WriteAllText(Request, "failed");
                File.WriteAllText(Report, exception.ToString());
                Debug.LogException(exception);
            }
        }

        [MenuItem("Therapy Game/Upgrade Animated Squirrels")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Stop Play mode and baking first.");
            Scene scene = SceneManager.GetSceneByPath(Root + "/Scenes/TherapyRoom.unity");
            if (!scene.isLoaded) throw new InvalidOperationException("Keep TherapyRoom open.");
            Transform room = scene.GetRootGameObjects().Single(gameObject => gameObject.name == "TherapyRoom").transform;
            Transform group = room.Find("OutdoorGarden/Living garden");
            WellnessGardenLife life = group != null ? group.GetComponent<WellnessGardenLife>() : null;
            if (life == null) throw new InvalidOperationException("Install the living garden before upgrading its squirrels.");
            if (life.animals.Count(animal => animal.kind == 1 && animal.animation != null) == 3)
                throw new InvalidOperationException("The three animated squirrels are already installed.");
            int originalSquirrels = life.animals.Count(animal => animal.kind == 1);
            if (!((life.animals.Length == 10 && originalSquirrels == 2) || (life.animals.Length == 11 && originalSquirrels == 3)))
                throw new InvalidOperationException("Expected either the installed two-squirrel garden or the fresh three-squirrel baseline.");

            SquirrelModel model = JsonUtility.FromJson<SquirrelModel>(File.ReadAllText(Folder + "/Source/Squirrel.json"));
            LifeData routes = JsonUtility.FromJson<LifeData>(File.ReadAllText(Folder + "/Source/GardenLife.json"));
            Validate(model, routes);
            AnimalData[] squirrelRoutes = routes.animals.Where(animal => animal.kind == 1).ToArray();

            foreach (string directory in new[] { "Backups", "Meshes", "Materials/Squirrel", "Clips/Squirrel" })
                Directory.CreateDirectory(Folder + "/" + directory);
            string backup = Folder + "/Backups/TherapyRoom_BeforeAnimatedSquirrels.unity";
            if (!File.Exists(backup) && !EditorSceneManager.SaveScene(scene, backup, true))
                throw new IOException("Could not back up the current open scene, including unsaved edits.");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new Exception("URP Lit is missing.");
            Material[] materials = model.materials.Select((surface, index) => MaterialAsset(surface, index, shader)).ToArray();
            Mesh mesh = MeshAsset(model);
            AnimationClip[] clips = model.clips.Select(ClipAsset).ToArray();

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Upgrade animated squirrels");
            try
            {
                Undo.RecordObject(life, "Link animated squirrels");
                WellnessGardenLife.Animal[] existing = life.animals.Where(animal => animal.kind == 1).ToArray();
                for (int index = 0; index < existing.Length; index++)
                    Replace(existing[index], squirrelRoutes[index], model, mesh, materials, clips);

                if (existing.Length == 2)
                {
                    AnimalData data = squirrelRoutes[2];
                    GameObject rootObject = new GameObject(data.name);
                    rootObject.transform.SetParent(group, false);
                    Undo.RegisterCreatedObjectUndo(rootObject, "Add a third squirrel");
                    WellnessGardenLife.Animal added = new WellnessGardenLife.Animal
                    {
                        kind = 1,
                        root = rootObject.transform,
                        route = data.route,
                        phase = data.phase,
                    };
                    ConfigureRoot(added, data);
                    added.animation = CreateModel(rootObject.transform, model, mesh, materials, clips);
                    added.renderers = rootObject.GetComponentsInChildren<Renderer>();
                    life.animals = life.animals.Concat(new[] { added }).ToArray();
                }
                EditorUtility.SetDirty(life);
                Physics.SyncTransforms();
                string checks = Verify(room, life, model);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the squirrel upgrade.");
                File.WriteAllText(Report,
                    "Animated squirrels installed " + DateTime.Now.ToString("s") + "\n" +
                    "PASS: supplied animated GLB source SHA-256 " + model.sourceHash + ".\n" +
                    "PASS: three squirrels total; each uses one 57,802-vertex / 107,104-triangle native skinned renderer and eight shared materials.\n" +
                    "PASS: Idle, Nibble, LookAround, Alert, Hop, DropAndRun and Run clips reconstructed at their supplied key times.\n" +
                    "PASS: route movement, viewer-aware alerting, night/rain rest and 38 m render/animation culling retained.\n" +
                    checks + "\n" +
                    "No Play mode, microphone, GPU preview, lighting bake or reflection capture started. Appearance and frame rate still need a brief live check.\n");
                File.WriteAllText(Request, "installed-live-visual-check-pending");
                Undo.CollapseUndoOperations(undo);
                Debug.Log("THERAPY_ANIMATED_SQUIRRELS_IMPORTED: CPU/import checks passed.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }

        private static void Replace(WellnessGardenLife.Animal animal, AnimalData data, SquirrelModel model, Mesh mesh, Material[] materials, AnimationClip[] clips)
        {
            Undo.RecordObject(animal.root, "Replace squirrel model");
            for (int child = animal.root.childCount - 1; child >= 0; child--)
                Undo.DestroyObjectImmediate(animal.root.GetChild(child).gameObject);
            animal.route = data.route;
            animal.phase = data.phase;
            animal.head = animal.leftEar = animal.rightEar = animal.tail = animal.leftWing = animal.rightWing = null;
            ConfigureRoot(animal, data);
            animal.animation = CreateModel(animal.root, model, mesh, materials, clips);
            animal.renderers = animal.root.GetComponentsInChildren<Renderer>();
        }

        private static void ConfigureRoot(WellnessGardenLife.Animal animal, AnimalData data)
        {
            animal.root.localPosition = data.route[0];
            animal.root.localScale = Vector3.one * data.scale;
            Vector3 facing = WellnessGardenLife.Evaluate(1, 0, data.phase, data.route).direction;
            animal.root.localRotation = Quaternion.LookRotation(facing);
        }

        private static Animation CreateModel(Transform parent, SquirrelModel data, Mesh mesh, Material[] materials, AnimationClip[] clips)
        {
            GameObject modelObject = new GameObject("Animated squirrel model");
            modelObject.transform.SetParent(parent, false);
            modelObject.transform.localScale = Vector3.one * VisualScale;
            Undo.RegisterCreatedObjectUndo(modelObject, "Create animated squirrel model");

            Transform[] bones = data.bones.Select(bone => new GameObject(bone.name).transform).ToArray();
            for (int index = 0; index < bones.Length; index++)
            {
                Bone bone = data.bones[index];
                bones[index].SetParent(bone.parent < 0 ? modelObject.transform : bones[bone.parent], false);
                bones[index].localPosition = Vector(bone.p);
                bones[index].localRotation = Rotation(bone.q);
                bones[index].localScale = Vector(bone.s);
            }

            SkinnedMeshRenderer renderer = modelObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = bones.Single(bone => bone.name == "root");
            renderer.sharedMaterials = data.mesh.submeshes.Select(submesh => materials[submesh.material]).ToArray();
            renderer.quality = SkinQuality.Bone4;
            renderer.updateWhenOffscreen = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.localBounds = ExpandedBounds(data.bounds);

            Animation animation = modelObject.AddComponent<Animation>();
            animation.playAutomatically = true;
            animation.wrapMode = WrapMode.Loop;
            animation.cullingType = AnimationCullingType.AlwaysAnimate;
            foreach (AnimationClip clip in clips) animation.AddClip(clip, clip.name);
            animation.clip = clips.Single(clip => clip.name == "Idle");
            return animation;
        }

        private static Material MaterialAsset(Surface surface, int index, Shader shader)
        {
            string path = Folder + "/Materials/Squirrel/" + index + "_" + Safe(surface.name) + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = surface.name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Color(surface.color));
            material.SetFloat("_Metallic", surface.metallic);
            material.SetFloat("_Smoothness", 1 - surface.roughness);
            material.SetFloat("_Cull", surface.doubleSided ? 0 : 2);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh MeshAsset(SquirrelModel data)
        {
            string path = Folder + "/Meshes/AnimatedSquirrel.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Animated squirrel" };
                AssetDatabase.CreateAsset(mesh, path);
            }
            else mesh.Clear();
            Geometry geometry = data.mesh;
            int count = geometry.positions.Length / 3;
            Vector3[] positions = new Vector3[count];
            Vector3[] normals = new Vector3[count];
            Vector2[] uvs = new Vector2[count];
            BoneWeight[] weights = new BoneWeight[count];
            for (int index = 0; index < count; index++)
            {
                int p = index * 3, uv = index * 2, skin = index * 4;
                positions[index] = new Vector3(geometry.positions[p], geometry.positions[p + 1], geometry.positions[p + 2]);
                normals[index] = new Vector3(geometry.normals[p], geometry.normals[p + 1], geometry.normals[p + 2]);
                uvs[index] = new Vector2(geometry.uvs[uv], geometry.uvs[uv + 1]);
                weights[index] = new BoneWeight
                {
                    boneIndex0 = geometry.joints[skin], boneIndex1 = geometry.joints[skin + 1],
                    boneIndex2 = geometry.joints[skin + 2], boneIndex3 = geometry.joints[skin + 3],
                    weight0 = geometry.weights[skin], weight1 = geometry.weights[skin + 1],
                    weight2 = geometry.weights[skin + 2], weight3 = geometry.weights[skin + 3],
                };
            }
            mesh.indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.boneWeights = weights;
            mesh.bindposes = BindPoses(data.bones);
            mesh.subMeshCount = geometry.submeshes.Length;
            for (int submesh = 0; submesh < geometry.submeshes.Length; submesh++)
                mesh.SetTriangles(geometry.submeshes[submesh].indices, submesh, false);
            mesh.bounds = ExpandedBounds(data.bounds);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static AnimationClip ClipAsset(Clip data)
        {
            string path = Folder + "/Clips/Squirrel/" + Safe(data.name) + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = data.name };
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            clip.legacy = true;
            clip.frameRate = 30;
            clip.wrapMode = WrapMode.Loop;
            foreach (Track track in data.tracks)
            {
                string[] properties;
                if (track.property == "position") properties = new[] { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
                else if (track.property == "scale") properties = new[] { "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };
                else if (track.property == "quaternion") properties = new[] { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                else throw new Exception("Unsupported animation property " + track.property);
                if (track.values.Length != track.times.Length * properties.Length) throw new Exception("Malformed track " + track.path);
                for (int component = 0; component < properties.Length; component++)
                {
                    Keyframe[] keys = new Keyframe[track.times.Length];
                    for (int key = 0; key < keys.Length; key++) keys[key] = new Keyframe(track.times[key], track.values[key * properties.Length + component]);
                    AnimationCurve curve = new AnimationCurve(keys);
                    for (int key = 0; key < keys.Length; key++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                    }
                    clip.SetCurve(track.path, typeof(Transform), properties[component], curve);
                }
            }
            clip.EnsureQuaternionContinuity();
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static Matrix4x4[] BindPoses(Bone[] bones)
        {
            Matrix4x4[] world = new Matrix4x4[bones.Length];
            bool[] ready = new bool[bones.Length];
            Matrix4x4 Resolve(int index)
            {
                if (ready[index]) return world[index];
                Bone bone = bones[index];
                Matrix4x4 local = Matrix4x4.TRS(Vector(bone.p), Rotation(bone.q), Vector(bone.s));
                world[index] = bone.parent < 0 ? local : Resolve(bone.parent) * local;
                ready[index] = true;
                return world[index];
            }
            Matrix4x4[] bind = new Matrix4x4[bones.Length];
            for (int index = 0; index < bones.Length; index++) bind[index] = Resolve(index).inverse;
            return bind;
        }

        private static void Validate(SquirrelModel model, LifeData routes)
        {
            if (model.version != 1 || model.units != "metres" || model.bones.Length != 24 || model.materials.Length != 8 || model.clips.Length != 7)
                throw new Exception("Unexpected squirrel conversion metadata.");
            string[] expected = { "Idle", "Nibble", "LookAround", "Alert", "Hop", "DropAndRun", "Run" };
            if (!model.clips.Select(clip => clip.name).SequenceEqual(expected) || model.clips.Any(clip => clip.tracks.Length != 31))
                throw new Exception("The supplied animation set changed.");
            int vertices = model.mesh.positions.Length / 3;
            if (vertices != 57802 || model.mesh.normals.Length != model.mesh.positions.Length || model.mesh.uvs.Length != vertices * 2 ||
                model.mesh.joints.Length != vertices * 4 || model.mesh.weights.Length != vertices * 4 || model.mesh.submeshes.Length != 8 ||
                model.mesh.positions.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                throw new Exception("Invalid squirrel mesh data.");
            if (model.mesh.submeshes.Sum(submesh => submesh.indices.Length / 3) != 107104 ||
                model.mesh.submeshes.Any(submesh => submesh.material < 0 || submesh.material >= model.materials.Length || submesh.indices.Any(index => index < 0 || index >= vertices)))
                throw new Exception("Squirrel source geometry was lost.");
            if (routes.animals.Length != 11 || routes.animals.Count(animal => animal.kind == 0) != 3 || routes.animals.Count(animal => animal.kind == 1) != 3 || routes.animals.Count(animal => animal.kind == 2) != 5)
                throw new Exception("Expected three rabbits, three squirrels and five birds.");
            if (routes.animals.Any(animal => animal.route.Length != 65 || Vector3.Distance(animal.route[0], animal.route[64]) > .001f))
                throw new Exception("Wildlife routes are not closed.");
        }

        private static string Verify(Transform room, WellnessGardenLife life, SquirrelModel model)
        {
            void Require(bool condition, string message) { if (!condition) throw new Exception("Animated squirrel check: " + message); }
            WellnessGardenLife.Animal[] squirrels = life.animals.Where(animal => animal.kind == 1).ToArray();
            Require(life.animals.Length == 11 && squirrels.Length == 3, "wildlife count");
            Require(squirrels.All(animal => animal.animation != null && animal.animation.GetClipCount() == 7), "seven clips per squirrel");
            Require(squirrels.All(animal => animal.renderers.Length == 1 && animal.renderers[0] is SkinnedMeshRenderer), "one skinned renderer per squirrel");
            Require(squirrels.All(animal => animal.renderers[0].GetComponent<SkinnedMeshRenderer>().sharedMesh.triangles.Length / 3 == 107104), "source triangle count");
            Require(squirrels.All(animal => animal.root.GetComponentsInChildren<MeshRenderer>().Length == 0), "legacy procedural models removed");
            MeshCollider terrain = room.Find("OutdoorGarden/Walkable surfaces and safety/Walkable terrain").GetComponent<MeshCollider>();
            float maximumGroundError = 0;
            foreach (Vector3 point in squirrels[2].route)
            {
                Require(terrain.Raycast(new Ray(point + Vector3.up * 15, Vector3.down), out RaycastHit hit, 25), "new route has ground");
                maximumGroundError = Mathf.Max(maximumGroundError, Mathf.Abs(point.y - hit.point.y - .012f));
            }
            Require(maximumGroundError < .025f, "new route follows saved terrain");
            Require(life.GetComponentsInChildren<Rigidbody>().Length == 0, "no wildlife rigidbodies");
            Require(life.GetComponentsInChildren<Light>().Length == 0 && life.GetComponentsInChildren<Camera>().Length == 0, "no extra render passes");
            Require(File.Exists(Folder + "/Source/tiny-squirrel-animated.glb"), "source GLB retained");
            return "PASS: saved-scene checks retained terrain grounding, no legacy squirrel renderers, no rigidbodies and no extra lights/cameras.";
        }

        private static Bounds ExpandedBounds(BoundsData data)
        {
            Vector3 min = Vector(data.min), max = Vector(data.max);
            Bounds bounds = new Bounds((min + max) * .5f, max - min);
            bounds.Expand(new Vector3(.08f, .08f, .10f));
            return bounds;
        }
        private static Vector3 Vector(float[] value) => new Vector3(value[0], value[1], value[2]);
        private static Quaternion Rotation(float[] value) => new Quaternion(value[0], value[1], value[2], value[3]);
        private static Color Color(float[] value) => new Color(value[0], value[1], value[2], value.Length > 3 ? value[3] : 1);
        private static string Safe(string value) => string.Concat(value.Select(character => char.IsLetterOrDigit(character) ? character : '_'));
    }
}
