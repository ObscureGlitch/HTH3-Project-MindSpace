using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheLastWatch.Environment;
using TheLastWatch.Integrations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrailerCapture.Editor
{
    /// <summary>
    /// Capture-only editor utility. It opens a copy of the authored TherapyRoom scene,
    /// samples the real sky/weather materials at hand-picked times, and exports clean
    /// 16:9 PNGs without saving any scene or asset changes.
    /// </summary>
    [InitializeOnLoad]
    public static class MarketingCapture
    {
        private const string ScenePath = "Assets/TherapyGame/Scenes/TherapyRoom.unity";
        private const string RequestPath = "D:/Hack the Hill/Deliverables/MarketingCapture/CAPTURE_REQUEST.txt";
        private const string DefaultOutputPath = "D:/Hack the Hill/Deliverables/MarketingScreenshots";

        static MarketingCapture()
        {
            EditorApplication.delayCall += TryRunRequestedCapture;
        }

        private static void TryRunRequestedCapture()
        {
            if (!File.Exists(RequestPath) || File.ReadAllText(RequestPath).Trim() != "capture") return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            {
                EditorApplication.delayCall += TryRunRequestedCapture;
                return;
            }
            File.WriteAllText(RequestPath, "running");
            try
            {
                Run();
                File.WriteAllText(RequestPath, "complete");
            }
            catch (Exception exception)
            {
                File.WriteAllText(RequestPath, "failed\n" + exception);
                Debug.LogException(exception);
            }
        }

        private sealed class Shot
        {
            public string Name;
            public string Caption;
            public Vector3 Position;
            public Vector3 Target;
            public float Hour;
            public float Coverage;
            public float Fov;
            public bool Rain;
            public bool Aurora;
            public bool Fireflies;

            public Shot(string name, string caption, Vector3 position, Vector3 target,
                float hour, float coverage, float fov = 62f, bool rain = false,
                bool aurora = false, bool fireflies = false)
            {
                Name = name;
                Caption = caption;
                Position = position;
                Target = target;
                Hour = hour;
                Coverage = coverage;
                Fov = fov;
                Rain = rain;
                Aurora = aurora;
                Fireflies = fireflies;
            }
        }

        private sealed class SkyState : IDisposable
        {
            public Material Sky;
            public Material Clouds;
            public Material Pond;
            public Material PreviousSky;
            public Material PreviousPond;
            public Light Sun;
            public Color SunColor;
            public float SunIntensity;
            public Quaternion SunRotation;
            public bool SunEnabled;
            public AmbientMode AmbientMode;
            public Color AmbientSky;
            public Color AmbientEquator;
            public Color AmbientGround;
            public float AmbientIntensity;
            public float ReflectionIntensity;
            public readonly Dictionary<Renderer, Material> PreviousMaterials = new Dictionary<Renderer, Material>();
            public readonly Dictionary<Renderer, MaterialPropertyBlock> PreviousBlocks = new Dictionary<Renderer, MaterialPropertyBlock>();
            public readonly Dictionary<Transform, Tuple<Vector3, Quaternion, Vector3>> PreviousTransforms =
                new Dictionary<Transform, Tuple<Vector3, Quaternion, Vector3>>();
            public readonly Dictionary<Renderer, bool> PreviousEnabled = new Dictionary<Renderer, bool>();
            public readonly List<ParticleSystem> RainSystems = new List<ParticleSystem>();

            public void Dispose()
            {
                foreach (ParticleSystem system in RainSystems)
                    if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach (KeyValuePair<Renderer, Material> pair in PreviousMaterials)
                    if (pair.Key != null) pair.Key.sharedMaterial = pair.Value;
                foreach (KeyValuePair<Renderer, MaterialPropertyBlock> pair in PreviousBlocks)
                    if (pair.Key != null) pair.Key.SetPropertyBlock(pair.Value);
                foreach (KeyValuePair<Renderer, bool> pair in PreviousEnabled)
                    if (pair.Key != null) pair.Key.enabled = pair.Value;
                foreach (KeyValuePair<Transform, Tuple<Vector3, Quaternion, Vector3>> pair in PreviousTransforms)
                    if (pair.Key != null)
                    {
                        pair.Key.position = pair.Value.Item1;
                        pair.Key.rotation = pair.Value.Item2;
                        pair.Key.localScale = pair.Value.Item3;
                    }
                if (RenderSettings.skybox == Sky) RenderSettings.skybox = PreviousSky;
                RenderSettings.ambientMode = AmbientMode;
                RenderSettings.ambientSkyColor = AmbientSky;
                RenderSettings.ambientEquatorColor = AmbientEquator;
                RenderSettings.ambientGroundColor = AmbientGround;
                RenderSettings.ambientIntensity = AmbientIntensity;
                RenderSettings.reflectionIntensity = ReflectionIntensity;
                if (Sun != null)
                {
                    Sun.color = SunColor;
                    Sun.intensity = SunIntensity;
                    Sun.transform.rotation = SunRotation;
                    Sun.enabled = SunEnabled;
                }
                if (PreviousPond != null && Pond != null)
                {
                    Renderer pondRenderer = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .FirstOrDefault(renderer => renderer.sharedMaterial == Pond);
                    if (pondRenderer != null) pondRenderer.sharedMaterial = PreviousPond;
                }
                if (Sky != null) Object.DestroyImmediate(Sky);
                if (Clouds != null) Object.DestroyImmediate(Clouds);
                if (Pond != null) Object.DestroyImmediate(Pond);
            }
        }

        [MenuItem("Therapy Game/Capture Trailer Stills")]
        public static void Run()
        {
            string output = OutputDirectory();
            Directory.CreateDirectory(output);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform room = scene.GetRootGameObjects().Single(gameObject => gameObject.name == "TherapyRoom").transform;
            WellnessSkyCycle cycle = room.GetComponentInChildren<WellnessSkyCycle>(true);
            WellnessScenePipeline pipelineScope = room.GetComponent<WellnessScenePipeline>();
            if (cycle == null || pipelineScope == null || pipelineScope.pipeline == null)
                throw new InvalidOperationException("The authored sky cycle and render pipeline are required.");

            Shot[] shots =
            {
                new Shot("01_Cozy_Therapy_Room_Morning", "A warm morning welcome inside the therapy room.",
                    new Vector3(-.10f, 1.73f, -2.75f), new Vector3(.10f, 1.13f, .65f), 9.2f, .20f, 62f),
                new Shot("02_Conversation_Corner_Afternoon", "The calm conversation corner in soft afternoon light.",
                    new Vector3(-2.25f, 1.68f, -.25f), new Vector3(.65f, 1.0f, 1.75f), 15.2f, .24f, 61f),
                new Shot("03_Overcast_Window_Refuge", "Clouds gather over the garden while the room stays quiet and warm.",
                    new Vector3(1.45f, 1.65f, 0f), new Vector3(15.5f, 1.4f, -3.8f), 16.3f, 1f, 61f, rain: true),
                new Shot("04_Garden_Path_Clear_Morning", "The front path opens into the living garden on a clear morning.",
                    new Vector3(-2.43f, 1.70f, -5.5f), new Vector3(14f, 1f, -6f), 8f, .18f, 60f),
                new Shot("05_Pond_Bridge_Golden_Hour", "Golden-hour light over the bridge and shallow pond.",
                    new Vector3(7f, 1.70f, -5.9f), new Vector3(24f, 1.35f, -5.9f), 17f, .27f, 61f),
                new Shot("06_Meadow_Rabbit_Sunset", "A quiet wildlife encounter among the meadow flowers.",
                    new Vector3(2.2f, 1.15f, -9.3f), new Vector3(3.9f, .14f, -7.5f), 17.45f, .22f, 48f),
                new Shot("07_MindSpace_Exterior_Blue_Hour", "The MindSpace counseling center at blue hour.",
                    new Vector3(-1f, 1.9f, -9f), new Vector3(-1f, 1.65f, -3.3f), 17.8f, .34f, 59f),
                new Shot("08_Pond_Starlight_Aurora", "The pond and garden beneath stars and the northern lights.",
                    new Vector3(11.5f, 1.55f, -12.5f), new Vector3(17.2f, 1f, -4.5f), 23f, .14f, 64f, aurora: true, fireflies: true)
            };

            RenderPipelineAsset previousPipeline = QualitySettings.renderPipeline;
            try
            {
                QualitySettings.renderPipeline = pipelineScope.pipeline;
                foreach (Shot shot in shots)
                {
                    using (SkyState state = ApplySky(cycle, shot))
                        RenderShot(room, shot, Path.Combine(output, shot.Name + ".png"));
                }
                WriteManifest(output, shots);
                Debug.Log("MARKETING_CAPTURE_COMPLETE: " + shots.Length + " stills in " + output);
            }
            finally
            {
                QualitySettings.renderPipeline = previousPipeline;
            }
        }

        private static string OutputDirectory()
        {
            string[] args = global::System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], "-marketingOutput", StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(args[i + 1]);
            return Path.GetFullPath(DefaultOutputPath);
        }

        private static SkyState ApplySky(WellnessSkyCycle cycle, Shot shot)
        {
            SkyState state = new SkyState
            {
                PreviousSky = RenderSettings.skybox,
                AmbientMode = RenderSettings.ambientMode,
                AmbientSky = RenderSettings.ambientSkyColor,
                AmbientEquator = RenderSettings.ambientEquatorColor,
                AmbientGround = RenderSettings.ambientGroundColor,
                AmbientIntensity = RenderSettings.ambientIntensity,
                ReflectionIntensity = RenderSettings.reflectionIntensity,
                Sun = cycle.daylight
            };
            if (state.Sun != null)
            {
                state.SunColor = state.Sun.color;
                state.SunIntensity = state.Sun.intensity;
                state.SunRotation = state.Sun.transform.rotation;
                state.SunEnabled = state.Sun.enabled;
            }

            state.Sky = new Material(cycle.skyMaterial) { name = "Trailer sky", hideFlags = HideFlags.HideAndDontSave };
            state.Clouds = cycle.cloudDeck != null && cycle.cloudDeck.cloudMaterial != null
                ? new Material(cycle.cloudDeck.cloudMaterial) { name = "Trailer clouds", hideFlags = HideFlags.HideAndDontSave }
                : null;
            RenderSettings.skybox = state.Sky;

            Vector3 sun = WellnessSkyCycle.SunDirection(shot.Hour);
            float day = WellnessSkyCycle.DaylightAmount(shot.Hour);
            float storm = Mathf.InverseLerp(.4f, 1f, shot.Coverage);
            float twilight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Abs(sun.y) / .35f));
            Color top = Color.Lerp(new Color(.008f, .014f, .032f), new Color(.19f, .42f, .72f), day);
            Color horizon = Color.Lerp(new Color(.035f, .053f, .085f), new Color(.69f, .80f, .89f), day);
            horizon = Color.Lerp(horizon, new Color(.88f, .58f, .38f), twilight * .55f * (1f - storm));
            top = Color.Lerp(top, Color.Lerp(new Color(.019f, .026f, .040f), new Color(.38f, .46f, .52f), day), storm * .8f);
            horizon = Color.Lerp(horizon, Color.Lerp(new Color(.045f, .06f, .085f), new Color(.59f, .66f, .68f), day), storm * .8f);

            SetColor(state.Sky, "_TopColor", top);
            SetColor(state.Sky, "_HorizonColor", horizon);
            SetColor(state.Sky, "_GroundColor", horizon * .6f);
            SetVector(state.Sky, "_SunDirection", sun);
            SetFloat(state.Sky, "_Night", 1f - day);
            SetFloat(state.Sky, "_Storm", storm);
            SetFloat(state.Sky, "_StarRotation", shot.Hour / 24f * Mathf.PI * 2f);
            if (state.Sky.HasProperty("_NightEffects"))
            {
                float visibility = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.72f, .98f, 1f - day)) * (1f - storm);
                state.Sky.SetVector("_NightEffects", new Vector4(visibility, shot.Aurora ? .82f : .08f, 27f, -.55f));
            }

            Color cloudTop = Color.Lerp(new Color(.10f, .14f, .22f), new Color(.98f, .97f, .94f), day);
            cloudTop = Color.Lerp(cloudTop, Color.Lerp(new Color(.06f, .08f, .13f), new Color(.64f, .69f, .73f), day), storm);
            cloudTop = Color.Lerp(cloudTop, new Color(.98f, .77f, .59f), twilight * .30f * (1f - storm));
            if (state.Clouds != null)
            {
                SetColor(state.Clouds, "_Tint", cloudTop);
                SetColor(state.Clouds, "_HorizonColor", horizon);
            }

            if (cycle.cloudDeck != null)
            {
                foreach (WellnessCloudDeck.Card card in cycle.cloudDeck.cards)
                {
                    if (card == null || card.transform == null || card.renderer == null) continue;
                    RememberRenderer(state, card.renderer);
                    RememberTransform(state, card.transform);
                    if (state.Clouds != null) card.renderer.sharedMaterial = state.Clouds;
                    Vector3 direction = card.virtualPosition.normalized;
                    card.transform.SetPositionAndRotation(
                        WellnessCloudDeck.Project(card.virtualPosition, shot.Position),
                        Quaternion.LookRotation(-direction, Vector3.up) * Quaternion.Euler(0f, 0f, card.rollDegrees));
                    card.transform.localScale = WellnessCloudDeck.ProjectedScale(card.virtualPosition, card.sizeMetres);
                    float horizonFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.07f, .22f, direction.y));
                    float density = card.atlasTile < 6 ? Mathf.Lerp(.72f, 1f, shot.Coverage)
                        : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.2f, .85f, shot.Coverage));
                    MaterialPropertyBlock block = new MaterialPropertyBlock();
                    block.SetFloat("_Opacity", card.opacity * horizonFade * density);
                    card.renderer.SetPropertyBlock(block);
                }
            }

            if (cycle.pondRenderer != null && cycle.pondRenderer.sharedMaterial != null)
            {
                state.PreviousPond = cycle.pondRenderer.sharedMaterial;
                state.Pond = new Material(state.PreviousPond) { name = "Trailer pond", hideFlags = HideFlags.HideAndDontSave };
                cycle.pondRenderer.sharedMaterial = state.Pond;
                float brightness = Mathf.Lerp(.26f, 1f, day) * Mathf.Lerp(1f, .82f, storm);
                TintExisting(state.Pond, "_BaseColor", brightness);
                TintExisting(state.Pond, "_ShoreColor", brightness);
                SetColor(state.Pond, "_SkyTint", Color.Lerp(GetColor(state.Pond, "_SkyTint", horizon) * brightness, horizon, .35f));
                SetColor(state.Pond, "_TopColor", top);
                SetColor(state.Pond, "_HorizonColor", horizon);
                SetColor(state.Pond, "_GroundColor", horizon * .6f);
                SetVector(state.Pond, "_SunDirection", sun);
                SetFloat(state.Pond, "_Night", 1f - day);
                SetFloat(state.Pond, "_Storm", storm);
                SetFloat(state.Pond, "_StarRotation", shot.Hour / 24f * Mathf.PI * 2f);
                if (state.Pond.HasProperty("_NightEffects"))
                    state.Pond.SetVector("_NightEffects", new Vector4(1f - day, shot.Aurora ? .82f : .08f, 27f, -.55f));
            }

            if (state.Sun != null)
            {
                bool sunUp = sun.y >= 0f;
                Vector3 source = sunUp ? sun : -sun;
                state.Sun.enabled = true;
                state.Sun.transform.rotation = Quaternion.LookRotation(-source, Vector3.forward);
                state.Sun.color = sunUp
                    ? Color.Lerp(new Color(1f, .68f, .43f), new Color(1f, .95f, .84f), Mathf.Clamp01(sun.y * 2f))
                    : new Color(.59f, .72f, 1f);
                state.Sun.intensity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Abs(sun.y) / .20f))
                    * (sunUp ? 1.4f : .26f) * Mathf.Lerp(1f, .56f, storm);
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(.19f, .24f, .36f), new Color(.73f, .80f, .85f), day) * Mathf.Lerp(1f, .82f, storm);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.16f, .19f, .27f), new Color(.62f, .66f, .63f), day);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(.095f, .12f, .18f), new Color(.36f, .39f, .33f), day);
            RenderSettings.reflectionIntensity = Mathf.Lerp(.20f, .65f, day) * Mathf.Lerp(1f, .8f, storm);

            foreach (WellnessFireflies fireflies in Object.FindObjectsByType<WellnessFireflies>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (fireflies.glowRenderer == null) continue;
                if (!state.PreviousEnabled.ContainsKey(fireflies.glowRenderer)) state.PreviousEnabled.Add(fireflies.glowRenderer, fireflies.glowRenderer.enabled);
                RememberRendererBlock(state, fireflies.glowRenderer);
                float amount = shot.Fireflies ? 1f : 0f;
                fireflies.glowRenderer.enabled = amount > 0f;
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                block.SetFloat("_Visibility", amount);
                fireflies.glowRenderer.SetPropertyBlock(block);
            }
            if (shot.Rain) SeedRain(state, cycle.rain, shot);
            return state;
        }

        private static void SeedRain(SkyState state, WellnessRain rain, Shot shot)
        {
            if (rain == null || rain.drops == null) return;
            ParticleSystem system = rain.drops;
            state.RainSystems.Add(system);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Vector3 forward = (shot.Target - shot.Position).normalized;
            Vector3 center = shot.Position + forward * 10f + Vector3.up * 4.5f;
            System.Random random = new System.Random(270926);
            for (int i = 0; i < 850; i++)
            {
                Vector3 position = center + new Vector3(Range(random, -8f, 8f), Range(random, -3f, 5f), Range(random, -9f, 9f));
                ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = new Vector3(-.35f, -8.5f, .15f),
                    startLifetime = Range(random, .35f, 1.5f),
                    startSize = Range(random, .014f, .025f),
                    startColor = new Color(.72f, .83f, .88f, .33f),
                    applyShapeToPosition = false
                };
                system.Emit(emit, 1);
            }
            system.Simulate(.08f, true, false, true);
        }

        private static float Range(System.Random random, float min, float max)
        {
            return min + (max - min) * (float)random.NextDouble();
        }

        private static void RenderShot(Transform room, Shot shot, string path)
        {
            GameObject cameraObject = new GameObject("Trailer still camera") { hideFlags = HideFlags.HideAndDontSave };
            Camera camera = cameraObject.AddComponent<Camera>();
            Texture2D texture = null;
            RenderTexture target = null;
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                camera.transform.position = shot.Position;
                camera.transform.LookAt(shot.Target);
                camera.fieldOfView = shot.Fov;
                camera.nearClipPlane = .04f;
                camera.farClipPlane = 230f;
                camera.allowHDR = true;
                camera.allowMSAA = true;
                camera.clearFlags = CameraClearFlags.Skybox;
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;

                WellnessAtmosphere source = room.GetComponentInChildren<WellnessAtmosphere>(true);
                WellnessAtmosphere atmosphere = cameraObject.AddComponent<WellnessAtmosphere>();
                atmosphere.gardenTransition = true;
                if (source != null)
                {
                    atmosphere.density = source.density;
                    atmosphere.hazeColor = source.hazeColor;
                    atmosphere.hazeEnabled = source.hazeEnabled;
                    atmosphere.outdoorDensity = shot.Rain ? Mathf.Max(.016f, source.outdoorDensity) : source.outdoorDensity;
                    atmosphere.outdoorHazeColor = shot.Rain ? new Color(.48f, .57f, .61f) : source.outdoorHazeColor;
                }

                target = new RenderTexture(2560, 1440, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    antiAliasing = 4,
                    name = shot.Name
                };
                camera.targetTexture = target;
                camera.aspect = 16f / 9f;
                camera.Render();
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousTarget;
                camera.targetTexture = null;
                if (texture != null) Object.DestroyImmediate(texture);
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void WriteManifest(string output, IEnumerable<Shot> shots)
        {
            using (StreamWriter writer = new StreamWriter(Path.Combine(output, "README.md"), false))
            {
                writer.WriteLine("# MindSpace trailer stills");
                writer.WriteLine();
                writer.WriteLine("Clean 2560×1440 captures rendered from the authored TherapyRoom scene and its real game materials.");
                writer.WriteLine();
                foreach (Shot shot in shots)
                    writer.WriteLine("- `" + shot.Name + ".png` — " + shot.Caption + " (game time " + shot.Hour.ToString("0.00") + ")");
            }
        }

        private static void RememberRenderer(SkyState state, Renderer renderer)
        {
            if (!state.PreviousMaterials.ContainsKey(renderer)) state.PreviousMaterials.Add(renderer, renderer.sharedMaterial);
            RememberRendererBlock(state, renderer);
        }

        private static void RememberRendererBlock(SkyState state, Renderer renderer)
        {
            if (state.PreviousBlocks.ContainsKey(renderer)) return;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            state.PreviousBlocks.Add(renderer, block);
        }

        private static void RememberTransform(SkyState state, Transform transform)
        {
            if (!state.PreviousTransforms.ContainsKey(transform))
                state.PreviousTransforms.Add(transform, Tuple.Create(transform.position, transform.rotation, transform.localScale));
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material != null && material.HasProperty(property)) material.SetFloat(property, value);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material != null && material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetVector(Material material, string property, Vector3 value)
        {
            if (material != null && material.HasProperty(property)) material.SetVector(property, value);
        }

        private static Color GetColor(Material material, string property, Color fallback)
        {
            return material != null && material.HasProperty(property) ? material.GetColor(property) : fallback;
        }

        private static void TintExisting(Material material, string property, float brightness)
        {
            if (material != null && material.HasProperty(property)) material.SetColor(property, material.GetColor(property) * brightness);
        }
    }
}
