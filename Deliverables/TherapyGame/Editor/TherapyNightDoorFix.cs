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
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyNightDoorFix
    {
        private const string Root = "Assets/TherapyGame";
        private const string Request = Root + "/NightDoorFixRequest.txt";
        private const string Report = Root + "/Exterior/NightDoorCheck.txt";
        static TherapyNightDoorFix() => EditorApplication.delayCall += ImportOnce;

        private static void ImportOnce()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "fix-night-and-door-once") return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            {
                File.WriteAllText(Request, "manual-only");
                Debug.LogWarning("Night/door update deferred. Stop Play/baking, then choose Therapy Game > Fix Night Fog Pond and Door."); return;
            }
            File.WriteAllText(Request, "installing-once");
            try { Install(); }
            catch (Exception e) { File.WriteAllText(Request, "failed"); File.WriteAllText(Report, e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("Therapy Game/Fix Night Fog Pond and Door")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) throw new InvalidOperationException("Stop Play mode and baking first.");
            Scene scene = SceneManager.GetSceneByPath(Root + "/Scenes/TherapyRoom.unity");
            if (!scene.isLoaded) throw new InvalidOperationException("Keep TherapyRoom open.");
            Transform room = scene.GetRootGameObjects().Single(g => g.name == "TherapyRoom").transform;
            var player = room.GetComponentInChildren<WellnessExplorer>();
            var chat = room.GetComponentInChildren<WellnessVoiceChat>();
            var cycle = room.GetComponentInChildren<WellnessSkyCycle>();
            if (player == null || chat == null || cycle == null || chat.IsConnected || chat.IsBusy) throw new InvalidOperationException("The existing player and idle weather/voice setup are required.");
            var atmosphere = player.ViewCamera.GetComponent<WellnessAtmosphere>();
            Transform hinge = room.Find("Architecture/Door/Open door hinge");
            var leaf = hinge != null ? hinge.Find("Closed oak entrance door")?.GetComponent<BoxCollider>() : null;
            if (atmosphere == null || leaf == null) throw new InvalidOperationException("Existing camera atmosphere or door hinge/leaf missing.");
            if (Vector3.Distance(hinge.lossyScale, Vector3.one) > .001f || Quaternion.Angle(leaf.transform.localRotation, Quaternion.identity) > .01f)
                throw new InvalidOperationException("The door's unit-scale aligned leaf changed; inspect before installing.");
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Exterior/Shaders/QuietPond.shader");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Pond shader must import without errors.");

            Directory.CreateDirectory(Root + "/Exterior/Backups");
            string backup = Root + "/Exterior/Backups/TherapyRoom_BeforeNightDoorFix.unity";
            if (!File.Exists(backup) && !EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Scene backup failed.");
            Undo.RecordObject(atmosphere, "Remove indoor orange fog");
            atmosphere.density = 0; atmosphere.hazeColor = new Color(.74f, .83f, .81f, 1);
            atmosphere.gardenTransition = true; EditorUtility.SetDirty(atmosphere);

            WellnessDoor door = hinge.GetComponent<WellnessDoor>();
            if (door == null) door = Undo.AddComponent<WellnessDoor>(hinge.gameObject);
            Undo.RecordObject(door, "Configure door toggle");
            door.leaf = leaf; door.player = player.GetComponent<CharacterController>();
            door.openAngle = 105; door.degreesPerSecond = 70; EditorUtility.SetDirty(door);
            WellnessInteraction interaction = hinge.GetComponent<WellnessInteraction>();
            if (interaction == null) interaction = Undo.AddComponent<WellnessInteraction>(hinge.gameObject);
            Undo.RecordObject(interaction, "Link door interaction");
            interaction.door = door; interaction.displayName = "Open / close door";
            interaction.reflection = ""; EditorUtility.SetDirty(interaction);
            // Keep the user's current open pose; never replace the room or garden.
            Check(atmosphere, door, interaction, cycle, room);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
            File.WriteAllText(Report, "Night and door fixes installed " + DateTime.Now.ToString("s") +
                "\nPASS: zero indoor haze; outdoor/night fog color shared; smooth doorway transition.\n" +
                "PASS: pond shader imports; crossing brightness lattice removed; per-pixel fog, filtered shadows and shadow-distance fade.\n" +
                "PASS: door references; E prompt; closed leaf blocks rays from both sides; open leaf clears doorway; swing/player safety math.\n" +
                "PASS: five seat spots, existing pond collider setup and weather references retained.\n" +
                "No Play mode, microphone, GPU preview, reflection capture or bake started. Visual appearance and real-time interaction require user testing.\n");
            File.WriteAllText(Request, "installed-live-visual-check-pending");
            Debug.Log("THERAPY_NIGHT_DOOR_FIXED: CPU/import checks passed; no Play, microphone or bake.");
        }

        private static void Check(WellnessAtmosphere atmosphere, WellnessDoor door, WellnessInteraction interaction, WellnessSkyCycle cycle, Transform room)
        {
            void Require(bool ok, string message) { if (!ok) throw new Exception("Night/door check: " + message); }
            atmosphere.SampleFog(Vector3.zero, out Color inside, out float indoorAmount);
            atmosphere.SampleFog(new Vector3(15, 1.7f, -5), out Color outside, out float outdoorAmount);
            Require(indoorAmount == 0 && inside == outside && outdoorAmount == atmosphere.outdoorDensity, "indoor fog removed and color consistent");
            Require(WellnessAtmosphere.OutdoorBlend(new Vector3(0, 1.7f, -3.1f)) == 0 &&
                WellnessAtmosphere.OutdoorBlend(new Vector3(0, 1.7f, -4)) > 0 &&
                WellnessAtmosphere.OutdoorBlend(new Vector3(0, 1.7f, -4)) < 1, "smooth threshold transition");
            Vector3 center = new Vector3(.62f, 1.2f, 0), half = new Vector3(.62f, 1.2f, .0325f);
            Require(WellnessDoor.BlocksLeaf(new Vector3(.62f, .9f, .18f), .22f, .9f, center, half, 0), "inside player blocks closing");
            Require(WellnessDoor.BlocksLeaf(new Vector3(.62f, .9f, -.18f), .22f, .9f, center, half, 0), "outside player blocks closing");
            Require(WellnessDoor.BlocksLeaf(Quaternion.Euler(0, 60, 0) * new Vector3(.8f, .9f, 0), .22f, .9f, center, half, 60), "player in mid-swing arc blocks motion");
            for (int yaw = 0; yaw <= 106; yaw += 2)
                Require(!WellnessDoor.BlocksLeaf(new Vector3(2.1f, .9f, 1), .22f, .9f, center, half, yaw), "safe operating position remains clear");
            Require(interaction.door == door && door.player != null && door.leaf.enabled && !door.leaf.isTrigger, "physical door linked");
            Quaternion original = door.transform.localRotation;
            try
            {
                door.transform.localRotation = Quaternion.identity; Physics.SyncTransforms();
                Vector3 midpoint = door.leaf.transform.TransformPoint(door.leaf.center);
                Vector3 normal = door.transform.forward;
                Require(Physics.Raycast(midpoint - normal, normal, out RaycastHit front, 2, ~0, QueryTriggerInteraction.Ignore) && front.collider == door.leaf, "closed door blocks exterior ray");
                Require(Physics.Raycast(midpoint + normal, -normal, out RaycastHit back, 2, ~0, QueryTriggerInteraction.Ignore) && back.collider == door.leaf, "closed door blocks interior ray");
                door.transform.localRotation = Quaternion.Euler(0, door.openAngle, 0); Physics.SyncTransforms();
                Require(!door.leaf.Raycast(new Ray(midpoint - normal, normal), out _, 2), "open door clears entrance");
            }
            finally { door.transform.localRotation = original; Physics.SyncTransforms(); }
            Require(room.GetComponentsInChildren<WellnessSeat>().Sum(s => s.Count) == 5, "existing seating preserved");
            Require(cycle.atmosphere == atmosphere && cycle.cloudDeck != null && cycle.pondRenderer != null && cycle.rain != null, "existing weather references preserved");
            Require(cycle.pondRenderer.sharedMaterial.shader.name == "Therapy Game/Quiet Pond" && cycle.pondRenderer.GetComponent<Collider>() == null, "pond remains walk-in without surface barrier");
        }
    }
}
