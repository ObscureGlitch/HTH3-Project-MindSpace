using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastWatch.Environment
{
    [DisallowMultipleComponent, DefaultExecutionOrder(120)]
    public sealed class WellnessKoiPond : MonoBehaviour
    {
        public KoiPondLibrary library;
        public Renderer waterSurface;
        public Vector3 pondCenter = new Vector3(15, -.31f, -5);
        public Vector2 pondRadii = new Vector2(6.7f, 5.7f);
        [Range(1, KoiSchoolLayout.MaximumFish)] public int fishCount = KoiSchoolLayout.MaximumFish;
        [Range(0, 1)] public float bedVisibility = .85f;
        public const float SwimDepth = .24f;
        public const float FirstLane = KoiSchoolLayout.FirstLane;
        public const float LaneSpacing = KoiSchoolLayout.LaneSpacing;
        public const float MaximumScale = KoiSchoolLayout.MaximumScale;
        static readonly int VisibilityId = Shader.PropertyToID("_BedVisibility");
        sealed class Fish
        {
            public Transform root, tail, left, right;
            public KoiSwimMotion motion;
        }
        Fish[] fish;
        GameObject runtimeRoot;
        MaterialPropertyBlock properties;
        float previousVisibility;
        bool changedVisibility;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (library == null || library.material == null || library.varieties == null || library.varieties.Length < 6 || waterSurface == null)
            { Debug.LogWarning("Koi pond needs its installed library and water surface.", this); return; }
            var random = new System.Random(Guid.NewGuid().GetHashCode());
            var population = KoiSchoolLayout.Create(random, fishCount, library.varieties.Length);
            runtimeRoot = new GameObject("Swimming koi (runtime)") { hideFlags = HideFlags.DontSave };
            runtimeRoot.transform.SetParent(transform, false);
            fish = new Fish[population.Length];
            for (int i = 0; i < fish.Length; i++)
            {
                var member = population[i];
                var variety = library.varieties[member.Variety];
                var root = new GameObject(variety.name) { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(runtimeRoot.transform, false);
                root.localScale = Vector3.one * member.Scale;
                var entry = new Fish { root = root, motion = new KoiSwimMotion(member.MotionSeed, member.Radius, pondRadii.x, pondRadii.y, member.PhaseOffset) };
                foreach (var part in variety.parts)
                {
                    var obj = new GameObject(part.name) { hideFlags = HideFlags.DontSave };
                    obj.transform.SetParent(root, false); obj.transform.localPosition = part.pivot;
                    obj.AddComponent<MeshFilter>().sharedMesh = part.mesh;
                    var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = library.material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    if (part.name == "tail") entry.tail = obj.transform;
                    if (part.name == "leftFin") entry.left = obj.transform;
                    if (part.name == "rightFin") entry.right = obj.transform;
                }
                fish[i] = entry; Pose(entry);
            }
            properties = new MaterialPropertyBlock();
            waterSurface.GetPropertyBlock(properties);
            previousVisibility = properties.GetFloat(VisibilityId);
            properties.SetFloat(VisibilityId, bedVisibility); waterSurface.SetPropertyBlock(properties);
            changedVisibility = true;
        }
        void Update()
        {
            if (fish == null || Time.deltaTime <= 0) return;
            float dt = Mathf.Min(Time.deltaTime, .1f);
            for (int i = 0; i < fish.Length; i++) { fish[i].motion.Step(dt); Pose(fish[i]); }
        }
        void Pose(Fish entry)
        {
            var m = entry.motion;
            entry.root.SetPositionAndRotation(pondCenter + new Vector3((float)m.X, -SwimDepth, (float)m.Z), Quaternion.Euler(0, (float)m.YawDegrees, 0));
            float phase = (float)m.FinPhase, effort = Mathf.Clamp01((float)m.Speed / .29f);
            if (entry.tail != null) entry.tail.localRotation = Quaternion.Euler(0, Mathf.Sin(phase) * Mathf.Lerp(3, 13, effort), 0);
            if (entry.left != null) entry.left.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase * .7f) * 9);
            if (entry.right != null) entry.right.localRotation = Quaternion.Euler(0, 0, -Mathf.Sin(phase * .7f + .4f) * 9);
        }
        void OnDisable()
        {
            fish = null;
            if (runtimeRoot != null) { runtimeRoot.SetActive(false); Destroy(runtimeRoot); runtimeRoot = null; }
            if (changedVisibility && waterSurface != null)
            {
                waterSurface.GetPropertyBlock(properties); properties.SetFloat(VisibilityId, previousVisibility);
                waterSurface.SetPropertyBlock(properties);
            }
            changedVisibility = false;
        }
    }
}
