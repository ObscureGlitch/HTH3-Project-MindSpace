using UnityEngine;
using UnityEngine.Rendering;
using TheLastWatch.Player;

namespace TheLastWatch.Integrations
{
    // Adapted from Hackathon26's AmbientZone indoor/outdoor rain idea.
    // Local rain uses ray-tested landing times to disturb the pond surface.
    [DisallowMultipleComponent]
    public sealed class WellnessRain : MonoBehaviour
    {
        public WellnessExplorer player;
        public ParticleSystem drops;
        public AudioSource rainAudio;
        public AudioLowPassFilter indoorMuffle;
        public bool rainEnabled = true;
        [Range(0, 1)] public float intensity = .55f;
        public bool VoiceDucking { get; set; }
        [System.NonSerialized] public TheLastWatch.Environment.WellnessPondWater pond;
        private float weatherAmount = -1;
        public float EffectiveIntensity => rainEnabled ? Mathf.Clamp01(intensity) * (weatherAmount < 0 ? 1 : weatherAmount) : 0;
        public void SetWeatherAmount(float amount) => weatherAmount = Mathf.Clamp01(amount);
        public void ClearWeatherOverride() => weatherAmount = -1;
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private System.Random random;
        private float emissionRemainder, mix;

        public static bool UnderRoof(Vector3 position) => Mathf.Abs(position.x) < 4.05f && Mathf.Abs(position.z) < 3.65f;
        public static float IndoorBlend(Vector3 position)
        {
            float outside = Mathf.Max(Mathf.Abs(position.x) - 3.6f, Mathf.Abs(position.z) - 3.1f);
            return 1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01((outside + .15f) / 1.3f));
        }
        public static float DropLifetime(float spawnY, float groundY) => Mathf.Clamp((spawnY - groundY - .08f) / 8.5f, .04f, 1.8f);

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            random = new System.Random(260926); mix = 0; emissionRemainder = 0;
            foreach (var renderer in FindObjectsByType<MeshRenderer>())
                if(renderer.gameObject.scene==gameObject.scene&&renderer.sharedMaterial!=null&&renderer.sharedMaterial.shader.name=="Therapy Game/Quiet Pond")
                {
                    pond=renderer.GetComponent<TheLastWatch.Environment.WellnessPondWater>();
                    if(pond==null)pond=renderer.gameObject.AddComponent<TheLastWatch.Environment.WellnessPondWater>();
                    pond.rain=this;break;
                }
            if (drops != null) { drops.Clear(); drops.Play(); }
            if (rainAudio != null && rainAudio.clip != null) { rainAudio.volume = 0; rainAudio.Play(); }
        }
        private void OnDisable()
        {
            if (drops != null) drops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (rainAudio != null) rainAudio.Stop();
            VoiceDucking = false;
        }
        private void Update()
        {
            if (!Application.isPlaying || player == null || player.ViewCamera == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            mix = Mathf.MoveTowards(mix, EffectiveIntensity, dt * .6f);
            Vector3 eye = player.ViewCamera.transform.position;
            float indoors = IndoorBlend(eye);
            if (rainAudio != null)
            {
                float target = mix * Mathf.Lerp(.35f, .075f, indoors) * (VoiceDucking ? .28f : 1) * TheLastWatch.Audio.OutdoorNatureAmbience.MasterVolume;
                rainAudio.volume = Mathf.MoveTowards(rainAudio.volume, target, dt * .3f);
                if (mix > .001f && rainAudio.clip != null && !rainAudio.isPlaying) rainAudio.Play();
                if (mix <= .001f && rainAudio.isPlaying) rainAudio.Pause();
            }
            if (indoorMuffle != null) indoorMuffle.cutoffFrequency = Mathf.Lerp(12000, 1700, indoors);
            if (drops == null || mix <= .001f) return;
            emissionRemainder += 280 * mix * dt;
            int count = Mathf.Min(24, Mathf.FloorToInt(emissionRemainder)); emissionRemainder -= count;
            for (int i = 0; i < count; i++)
            {
                Vector3 spawn = eye + new Vector3(Range(-7,7), Range(4.8f,6.2f), Range(-7,7));
                if (UnderRoof(spawn)) continue; // No rain through the cabin or its ceiling.
                float floorY = eye.y - 5;
                int hitCount = Physics.RaycastNonAlloc(spawn, Vector3.down, hits, 18, ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                for (int j = 0; j < hitCount; j++)
                    if (!hits[j].transform.IsChildOf(player.transform) && hits[j].distance < nearest)
                    { nearest = hits[j].distance; floorY = hits[j].point.y; }
                bool hitsPond=pond!=null&&pond.Contains(spawn)&&spawn.y>pond.WaterLevel&&floorY<pond.WaterLevel+.015f;
                float lifetime=hitsPond?(spawn.y-pond.WaterLevel)/8.5f:DropLifetime(spawn.y,floorY);
                if(hitsPond)pond.QueueDrop(spawn,lifetime,-Range(.32f,.65f));
                var emit = new ParticleSystem.EmitParams
                {
                    position = spawn, velocity = new Vector3(0,-8.5f,0),
                    startLifetime = lifetime, startSize = Range(.012f,.022f),
                    startColor = new Color(.72f,.83f,.88f,.27f), applyShapeToPosition = false
                };
                drops.Emit(emit,1);
            }
        }
        private float Range(float min,float max) => min + (max-min) * (float)random.NextDouble();

        public static void ConfigureParticles(ParticleSystem system, Material material)
        {
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main; main.loop=true; main.playOnAwake=false; main.maxParticles=600;
            main.simulationSpace=ParticleSystemSimulationSpace.World; main.startSpeed=0;
            main.startLifetime=1.2f; main.startSize=.018f; main.gravityModifier=0; main.useUnscaledTime=true;
            var emission=system.emission; emission.enabled=false;
            var shape=system.shape; shape.enabled=false;
            var collision=system.collision; collision.enabled=false;
            var trails=system.trails; trails.enabled=false;
            var color=system.colorOverLifetime; color.enabled=true;
            var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.8f,.85f),new GradientAlphaKey(0,1)});
            color.color=gradient;
            var renderer=system.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=material;
            renderer.renderMode=ParticleSystemRenderMode.Stretch; renderer.velocityScale=.025f; renderer.lengthScale=2;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off; renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
    }
}
