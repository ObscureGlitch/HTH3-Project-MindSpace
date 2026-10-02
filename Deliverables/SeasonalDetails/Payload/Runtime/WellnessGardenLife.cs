using System;
using UnityEngine;

namespace TheLastWatch.Environment
{
    /// <summary>Small, pre-authored wildlife loops. No NavMesh, rigidbodies or runtime meshes.</summary>
    [DisallowMultipleComponent]
    public sealed class WellnessGardenLife : MonoBehaviour
    {
        [Serializable] public sealed class Animal
        {
            public int kind; // 0 rabbit, 1 squirrel, 2 songbird. All models face +Z.
            public Transform root, head, leftEar, rightEar, tail, leftWing, rightWing;
            public Animation animation;
            public Vector3[] route;
            public Renderer[] renderers;
            public float phase;
        }
        public Animal[] animals = Array.Empty<Animal>();
        public Camera viewer;
        public WellnessSkyCycle sky;
        [Range(15, 60)] public float wildlifeDistance = 38;
        private float[] clocks;
        private Snapshot[] original;
        private string[] activeClips;
        private float[] specialClipUntil;
        private bool[] animalWasMoving;
        private bool ready;
        private struct Snapshot
        {
            public Vector3 position;
            public Quaternion rotation, head, leftEar, rightEar, tail, leftWing, rightWing;
            public bool[] visible;
            public bool animationEnabled;
        }
        public struct Pose
        {
            public Vector3 position, direction;
            public float moving, hop, flap, pitch, headPitch, ears, tail;
        }

        public static Pose Evaluate(int kind, float seconds, float phase, Vector3[] route)
        {
            if (route == null || route.Length < 2) return default;
            float t = Mathf.Max(0, seconds + phase), duration = kind == 0 ? 12 : kind == 1 ? 9 : 22;
            float rest = kind == 0 ? 7 : kind == 1 ? 6.5f : 17;
            float local = Mathf.Repeat(t, duration);
            float travel = Mathf.Clamp01((local - rest) / (duration - rest));
            float eased = Mathf.SmoothStep(0, 1, travel);
            float distance = kind == 2 ? eased : Mathf.Repeat((Mathf.Floor(t / duration) + eased) * (kind == 0 ? .24f : .36f), 1);
            float at = Mathf.Min(distance * (route.Length - 1), route.Length - 1.0001f);
            int index = Mathf.FloorToInt(at);
            Vector3 position = Vector3.Lerp(route[index], route[index + 1], at - index);
            Vector3 direction = route[index + 1] - route[index]; direction.y = 0;
            float moving = local > rest ? Mathf.Sin(travel * Mathf.PI) : 0;
            float hop = kind == 0 ? Mathf.Abs(Mathf.Sin((local - rest) * Mathf.PI * 1.8f)) * .065f * moving : 0;
            if (kind == 2) position.y += Mathf.Sin(eased * Mathf.PI) * 1.8f;
            return new Pose {
                position = position + Vector3.up * hop, direction = direction,
                moving = moving, hop = hop,
                flap = Mathf.Lerp(-62, Mathf.Sin(t * Mathf.PI * 2 * 6.6f) * 48, Mathf.SmoothStep(0, 1, Mathf.Clamp01(moving / .15f))),
                pitch = kind == 0 ? Mathf.Sin((local - rest) * Mathf.PI * 3.6f) * moving * 5 : kind == 1 ? moving * 8 : 0,
                headPitch = (1 - moving) * (kind == 0 ? 7 + Mathf.Sin(t * 1.3f) * 6 : Mathf.Sin(t * .8f) * 6),
                ears = Mathf.Sin(t * .72f + phase) * 5,
                tail = Mathf.Sin(t * (kind == 1 ? 2.2f : .8f)) * (4 + moving * 8)
            };
        }

        private static Quaternion Rotation(Transform t) => t != null ? t.localRotation : Quaternion.identity;
        private void OnEnable()
        {
            if (!Application.isPlaying || viewer == null) return;
            clocks = new float[animals.Length]; original = new Snapshot[animals.Length];
            activeClips = new string[animals.Length]; specialClipUntil = new float[animals.Length];
            animalWasMoving = new bool[animals.Length];
            for (int i = 0; i < animals.Length; i++)
            {
                Animal a = animals[i]; if (a.root == null) continue;
                var visible = new bool[a.renderers.Length]; for (int r = 0; r < visible.Length; r++) visible[r] = a.renderers[r].enabled;
                original[i] = new Snapshot { position = a.root.localPosition, rotation = a.root.localRotation,
                    head = Rotation(a.head), leftEar = Rotation(a.leftEar), rightEar = Rotation(a.rightEar), tail = Rotation(a.tail),
                    leftWing = Rotation(a.leftWing), rightWing = Rotation(a.rightWing), visible = visible,
                    animationEnabled = a.animation != null && a.animation.enabled };
                if (a.animation != null) PlayClip(i, a.animation, "Idle");
            }
            ready = true;
        }

        private void Update()
        {
            if (!ready || !Application.isFocused) return;
            float dt = Mathf.Min(Time.deltaTime, .06f);
            bool resting = sky != null && (sky.CurrentDaylight < .15f || (sky.rain != null && sky.rain.EffectiveIntensity > .32f));
            for (int i = 0; i < animals.Length; i++)
            {
                Animal a = animals[i]; if (a.root == null || a.route == null || a.route.Length < 2) continue;
                Vector3 viewerOffset = viewer.transform.position - a.root.position;
                float distance = viewerOffset.sqrMagnitude;
                float horizontalDistance = viewerOffset.x * viewerOffset.x + viewerOffset.z * viewerOffset.z;
                bool visible = distance < wildlifeDistance * wildlifeDistance;
                for (int r = 0; r < a.renderers.Length; r++) if (a.renderers[r] != null) a.renderers[r].enabled = visible && original[i].visible[r];
                if (a.animation != null) a.animation.enabled = visible && original[i].animationEnabled;
                if (!visible) continue;
                Pose current = Evaluate(a.kind, clocks[i], a.phase, a.route);
                // Finish a flight/hop before resting at night; never freeze a bird in midair.
                bool pause = (resting && current.moving < .001f) || (a.kind != 2 && horizontalDistance < 1.7f && current.hop < .008f);
                if (!pause) clocks[i] += dt;
                Pose pose = Evaluate(a.kind, clocks[i], a.phase, a.route);
                a.root.localPosition = pose.position;
                if (pose.direction.sqrMagnitude > .00001f)
                    a.root.localRotation = Quaternion.Slerp(a.root.localRotation, Quaternion.LookRotation(pose.direction) * Quaternion.Euler(pose.pitch, 0, 0), 1 - Mathf.Exp(-6 * dt));
                float notice = distance < 8 ? Mathf.Clamp(Vector3.SignedAngle(a.root.forward, viewer.transform.position - a.root.position, Vector3.up), -24, 24) : 0;
                Apply(a.head, original[i].head, new Vector3(pose.headPitch, notice, 0));
                Apply(a.leftEar, original[i].leftEar, new Vector3(pose.ears, 0, -pose.ears * .5f));
                Apply(a.rightEar, original[i].rightEar, new Vector3(-pose.ears * .65f, 0, -pose.ears * .3f));
                Apply(a.tail, original[i].tail, new Vector3(0, pose.tail, pose.tail * .3f));
                Apply(a.leftWing, original[i].leftWing, new Vector3(0, 0, -pose.flap));
                Apply(a.rightWing, original[i].rightWing, new Vector3(0, 0, pose.flap));
                if (a.animation != null)
                {
                    if (a.kind == 0) AnimateRabbit(i, a, pose, horizontalDistance);
                    else if (a.kind == 1) AnimateSquirrel(i, a, pose, horizontalDistance);
                }
            }
        }
        private void AnimateRabbit(int index, Animal animal, Pose pose, float horizontalDistance)
        {
            bool moving = pose.moving > .08f;
            string desired;
            if (moving)
            {
                if (!animalWasMoving[index])
                {
                    desired = "Hop";
                    AnimationState special = animal.animation[desired];
                    specialClipUntil[index] = Time.time + (special != null ? special.length / Mathf.Max(.01f, special.speed) : .8f);
                }
                else if (Time.time < specialClipUntil[index] && !string.IsNullOrEmpty(activeClips[index]))
                {
                    animalWasMoving[index] = true;
                    return;
                }
                else desired = "Run";
            }
            else if (horizontalDistance < 6) desired = "Alert";
            else
            {
                float cycle = Mathf.Repeat(clocks[index] + animal.phase * 2.3f, 24);
                desired = cycle < 6 ? "Idle" : cycle < 9 ? "Graze" : cycle < 13 ? "Idle" :
                    cycle < 16 ? "Groom" : cycle < 21 ? "Idle" : cycle < 22.3f ? "Binky" : "Idle";
            }
            animalWasMoving[index] = moving;
            PlayClip(index, animal.animation, desired);
        }
        private void AnimateSquirrel(int index, Animal animal, Pose pose, float horizontalDistance)
        {
            bool moving = pose.moving > .08f;
            string desired;
            if (moving)
            {
                if (!animalWasMoving[index])
                {
                    desired = ((index + Mathf.FloorToInt(clocks[index])) & 1) == 0 ? "DropAndRun" : "Hop";
                    AnimationState special = animal.animation[desired];
                    specialClipUntil[index] = Time.time + (special != null ? special.length / Mathf.Max(.01f, special.speed) : .5f);
                }
                else if (Time.time < specialClipUntil[index] && !string.IsNullOrEmpty(activeClips[index]))
                {
                    animalWasMoving[index] = true;
                    return;
                }
                else desired = "Run";
            }
            else if (horizontalDistance < 6) desired = "Alert";
            else
            {
                float cycle = Mathf.Repeat(clocks[index] + animal.phase, 14);
                desired = cycle < 5 ? "Idle" : cycle < 7 ? "Nibble" : cycle < 12 ? "LookAround" : "Idle";
            }
            animalWasMoving[index] = moving;
            PlayClip(index, animal.animation, desired);
        }
        private void PlayClip(int index, Animation animation, string clip)
        {
            if (animation == null || animation[clip] == null || activeClips[index] == clip) return;
            AnimationState state = animation[clip];
            state.speed = clip == "Run" ? 1.2f : clip == "DropAndRun" ? 1.1f : 1;
            animation.CrossFade(clip, .14f, PlayMode.StopAll);
            activeClips[index] = clip;
        }
        private static void Apply(Transform t, Quaternion original, Vector3 euler) { if (t != null) t.localRotation = original * Quaternion.Euler(euler); }
        private void OnDisable()
        {
            if (!ready) return; ready = false;
            for (int i = 0; i < animals.Length; i++)
            {
                Animal a = animals[i]; if (a.root == null) continue;
                a.root.localPosition = original[i].position; a.root.localRotation = original[i].rotation;
                Apply(a.head, original[i].head, Vector3.zero); Apply(a.leftEar, original[i].leftEar, Vector3.zero);
                Apply(a.rightEar, original[i].rightEar, Vector3.zero); Apply(a.tail, original[i].tail, Vector3.zero);
                Apply(a.leftWing, original[i].leftWing, Vector3.zero); Apply(a.rightWing, original[i].rightWing, Vector3.zero);
                for (int r = 0; r < a.renderers.Length; r++) if (a.renderers[r] != null) a.renderers[r].enabled = original[i].visible[r];
                if (a.animation != null) { a.animation.Stop(); a.animation.enabled = original[i].animationEnabled; }
            }
        }
    }
}
