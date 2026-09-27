using System;
using TheLastWatch.Integrations;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastWatch.Environment
{
    /// <summary>Play-only sky/weather. One existing directional light, shared cloud mesh,
    /// no render textures, GI refresh, reflection capture, or volumetric simulation.</summary>
    [DisallowMultipleComponent]
    public sealed class WellnessSkyCycle : MonoBehaviour
    {
        public enum WeatherMode { Automatic, Clear, Cloudy, Rain }
        public Light daylight;
        public Material skyMaterial, cloudMaterial;
        public WellnessCloudDeck cloudDeck;
        public Transform[] clouds = Array.Empty<Transform>();
        public WellnessAtmosphere atmosphere;
        public WellnessRain rain;
        public Renderer pondRenderer;
        [Range(0,24)] public float hour = 15;
        [Range(4,30)] public float dayMinutes = 12;
        [Range(2,20)] public float weatherMinutes = 8;
        public bool timeRuns = true;
        public WeatherMode weatherMode = WeatherMode.Automatic;
        public bool shootingStars = true, auroraBorealis = true;
        [Range(0,1)] public float nightEffectsIntensity = .8f;
        public float Hour => hour;
        public string Clock => string.Format("{0:00}:{1:00}", Mathf.FloorToInt(hour), Mathf.FloorToInt(hour * 60) % 60);
        public string WeatherLabel => weatherMode == WeatherMode.Automatic ? PhaseName(weatherElapsed / Mathf.Max(120,weatherMinutes*60)) : weatherMode.ToString();
        public string Summary => Clock + " · " + WeatherLabel + (weatherMode == WeatherMode.Automatic ? " · Auto" : " · Manual");

        private float weatherElapsed, coverage, precipitation, lightTimer, starDrift;
        private bool captured, nightSeason;
        private Material liveSky, liveCloud, livePond, previousSky, previousPond;
        private Renderer[] cloudRenderers;
        private Vector3[] cloudPositions, cloudScales;
        private Material[] previousCloudMaterials;
        private Color oldSky, oldEquator, oldGround, oldLightColor, oldHaze, oldPondBase, oldPondShore, oldPondSky;
        private AmbientMode oldAmbientMode;
        private Quaternion oldLightRotation;
        private float oldAmbientIntensity, oldReflection, oldLightIntensity, oldHazeDensity;
        private bool oldLightEnabled, oldRainEnabled;
        private Light oldSun;
        private WellnessNightSkyEvents nightEvents;
        private readonly Vector4[] meteorHeads=new Vector4[3],meteorTangents=new Vector4[3],meteorSides=new Vector4[3];
        private static readonly int NightEffects=Shader.PropertyToID("_NightEffects"),MeteorHeads=Shader.PropertyToID("_MeteorHeads"),
            MeteorTangents=Shader.PropertyToID("_MeteorTangents"),MeteorSides=Shader.PropertyToID("_MeteorSides"),AuroraShape=Shader.PropertyToID("_AuroraShape");

        public static float WrapHour(float value) => Mathf.Repeat(value,24);
        public static Vector3 SunDirection(float atHour)
        {
            float angle=(WrapHour(atHour)-6)/24 * Mathf.PI*2;
            return new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),.22f).normalized;
        }
        public static float DaylightAmount(float atHour) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.12f,.3f,SunDirection(atHour).y));
        public static string PhaseName(float cycle)
        {
            float t=Mathf.Repeat(cycle,1);
            return t<.3125f?"Clear":t<.5f?"Cloudy":t<.75f?"Rain":"Clearing";
        }
        // XY = cloud coverage and rain multiplier. The start of each phase blends from
        // the previous phase, including across the end/start of the loop.
        public static Vector2 SampleWeather(float cycle)
        {
            float t=Mathf.Repeat(cycle,1), start;
            Vector2 from,to;
            if(t<.3125f){start=0;from=new Vector2(.4f,0);to=new Vector2(.18f,0);}
            else if(t<.5f){start=.3125f;from=new Vector2(.18f,0);to=new Vector2(.85f,0);}
            else if(t<.75f){start=.5f;from=new Vector2(.85f,0);to=new Vector2(1,1);}
            else {start=.75f;from=new Vector2(1,1);to=new Vector2(.4f,0);}
            return Vector2.Lerp(from,to,Mathf.SmoothStep(0,1,Mathf.Clamp01((t-start)/.065f)));
        }
        private Vector2 TargetWeather()
        {
            switch(weatherMode)
            {
                case WeatherMode.Clear:return new Vector2(.18f,0);
                case WeatherMode.Cloudy:return new Vector2(.85f,0);
                case WeatherMode.Rain:return new Vector2(1,1);
                default:return SampleWeather(weatherElapsed/Mathf.Max(120,weatherMinutes*60));
            }
        }
        public void SetWeather(WeatherMode value) { weatherMode=value; if(rain!=null)rain.rainEnabled=true; }
        public void ToggleRain() => SetWeather(weatherMode==WeatherMode.Rain || (weatherMode==WeatherMode.Automatic && precipitation>.05f) ? WeatherMode.Clear : WeatherMode.Rain);
        public void SetHour(float value) { hour=WrapHour(value); if(captured)ApplyLighting(); }
        public bool CanSummonAurora => Application.isPlaying && captured && nightEvents!=null && !nightEvents.AuroraSummoned;
        public bool AuroraSummoned => nightEvents!=null && nightEvents.AuroraSummoned;
        public float AuroraSummonSecondsRemaining => nightEvents!=null ? nightEvents.SummonSecondsRemaining : 0;
        public bool SummonAurora()
        {
            if(!CanSummonAurora)return false;
            // Explicit settings action: choose a visible night, clear any storm and
            // enable the effect. Cloud family, cycle speed and voice choices are untouched.
            auroraBorealis=true;
            if(nightEffectsIntensity<=.001f)nightEffectsIntensity=.8f;
            if(DaylightAmount(hour)>.02f)hour=22;
            SetWeather(WeatherMode.Clear);
            Vector2 target=TargetWeather();coverage=target.x;precipitation=target.y;
            ApplyLighting();UpdateNightEffects(0);
            float yaw=cloudDeck!=null&&cloudDeck.viewer!=null?cloudDeck.viewer.transform.eulerAngles.y*Mathf.Deg2Rad:0;
            bool started=nightEvents.SummonAurora(yaw);
            UpdateNightEffects(0);
            return started;
        }

        private void OnEnable()
        {
            if(!Application.isPlaying || skyMaterial==null || cloudMaterial==null || daylight==null)return;
            previousSky=RenderSettings.skybox;oldSun=RenderSettings.sun;oldAmbientMode=RenderSettings.ambientMode;
            oldSky=RenderSettings.ambientSkyColor;oldEquator=RenderSettings.ambientEquatorColor;oldGround=RenderSettings.ambientGroundColor;
            oldAmbientIntensity=RenderSettings.ambientIntensity;oldReflection=RenderSettings.reflectionIntensity;
            oldLightColor=daylight.color;oldLightIntensity=daylight.intensity;oldLightRotation=daylight.transform.rotation;oldLightEnabled=daylight.enabled;
            if(atmosphere!=null){oldHaze=atmosphere.outdoorHazeColor;oldHazeDensity=atmosphere.outdoorDensity;}
            if(rain!=null){oldRainEnabled=rain.rainEnabled;rain.rainEnabled=true;}
            liveSky=new Material(skyMaterial){name="Sky cycle (runtime)",hideFlags=HideFlags.DontSave};
            if(clouds.Length>0)liveCloud=new Material(cloudMaterial){name="Clouds (runtime)",hideFlags=HideFlags.DontSave};
            RenderSettings.skybox=liveSky;RenderSettings.sun=daylight;
            cloudPositions=new Vector3[clouds.Length];cloudScales=new Vector3[clouds.Length];
            cloudRenderers=new Renderer[clouds.Length];previousCloudMaterials=new Material[clouds.Length];
            for(int i=0;i<clouds.Length;i++)if(clouds[i]!=null)
            {
                cloudPositions[i]=clouds[i].localPosition;cloudScales[i]=clouds[i].localScale;
                cloudRenderers[i]=clouds[i].GetComponent<Renderer>();
                if(cloudRenderers[i]!=null){previousCloudMaterials[i]=cloudRenderers[i].sharedMaterial;cloudRenderers[i].sharedMaterial=liveCloud;}
            }
            if(pondRenderer!=null && pondRenderer.sharedMaterial!=null && pondRenderer.sharedMaterial.HasProperty("_ShoreColor"))
            {
                previousPond=pondRenderer.sharedMaterial;livePond=new Material(previousPond){name="Pond cycle (runtime)",hideFlags=HideFlags.DontSave};
                oldPondBase=livePond.GetColor("_BaseColor");oldPondShore=livePond.GetColor("_ShoreColor");oldPondSky=livePond.GetColor("_SkyTint");
                pondRenderer.sharedMaterial=livePond;
                if(cloudDeck!=null)cloudDeck.SetReflectionTarget(livePond);
            }
            hour=WrapHour(hour);weatherElapsed=0;lightTimer=0;starDrift=0;
            Vector2 target=TargetWeather();coverage=target.x;precipitation=target.y;
            nightEvents=new WellnessNightSkyEvents(global::System.Environment.TickCount);
            nightSeason=false;captured=true;ApplyLighting();UpdateClouds(0);UpdateNightEffects(0);
        }
        private void Update()
        {
            if(!captured || !Application.isPlaying || !Application.isFocused)return;
            float dt=Mathf.Min(Time.deltaTime,.25f);
            starDrift=Mathf.Repeat(starDrift+dt*Mathf.PI*2/86164,Mathf.PI*2);
            if(timeRuns)hour=WrapHour(hour+dt*24/Mathf.Max(240,dayMinutes*60));
            if(weatherMode==WeatherMode.Automatic)weatherElapsed=Mathf.Repeat(weatherElapsed+dt,Mathf.Max(120,weatherMinutes*60));
            Vector2 target=TargetWeather();coverage=Mathf.MoveTowards(coverage,target.x,dt/20);
            precipitation=Mathf.MoveTowards(precipitation,target.y,dt/20);
            if(rain!=null)rain.SetWeatherAmount(precipitation);
            UpdateClouds(dt);lightTimer+=dt;
            if(lightTimer>=.1f){lightTimer=0;ApplyLighting();}
            UpdateNightEffects(dt);
        }
        private void UpdateNightEffects(float dt)
        {
            float night=1-DaylightAmount(hour),storm=Mathf.InverseLerp(.4f,1,coverage);
            float visibility=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,.98f,night))
                *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.30f,.9f,storm)))*Mathf.Clamp01(nightEffectsIntensity);
            float yaw=cloudDeck!=null&&cloudDeck.viewer!=null?cloudDeck.viewer.transform.eulerAngles.y*Mathf.Deg2Rad:0;
            // Dusk/dawn hysteresis keeps the nightly lottery separate from rain
            // and from small changes at the twilight threshold.
            if(night>.75f)nightSeason=true;
            else if(night<.55f)nightSeason=false;
            nightEvents.Tick(dt,nightSeason,visibility>.002f,shootingStars,auroraBorealis,yaw);
            for(int i=0;i<WellnessNightSkyEvents.Capacity;i++)
            {
                var m=nightEvents.Sample(i);
                meteorHeads[i]=new Vector4(m.head.x,m.head.y,m.head.z,m.brightness);
                meteorTangents[i]=new Vector4(m.tangent.x,m.tangent.y,m.tangent.z,m.tail);
                meteorSides[i]=new Vector4(m.side.x,m.side.y,m.side.z,m.width);
            }
            var effects=new Vector4(visibility,nightEvents.AuroraStrength,nightEvents.Seconds,nightEvents.AuroraAzimuth);
            SetNightEffects(liveSky,effects);SetNightEffects(livePond,effects);
        }
        private void SetNightEffects(Material target,Vector4 effects)
        {
            if(target==null||!target.HasProperty(NightEffects))return;
            target.SetVector(NightEffects,effects);
            target.SetVector(AuroraShape,new Vector4(nightEvents.AuroraTilt,nightEvents.AuroraSpread,nightEvents.AuroraPalette,nightEvents.AuroraPhase));
            target.SetVectorArray(MeteorHeads,meteorHeads);target.SetVectorArray(MeteorTangents,meteorTangents);target.SetVectorArray(MeteorSides,meteorSides);
        }
        private void UpdateClouds(float dt)
        {
            for(int i=0;i<clouds.Length;i++)if(clouds[i]!=null)
            {
                Vector3 p=clouds[i].localPosition;
                p.x=Mathf.Repeat(p.x+110+dt*(.16f+.018f*i)*(1+coverage),220)-110;
                clouds[i].localPosition=p;
                // Wrap far from the garden; taper near the boundary instead of popping.
                float edge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(110,86,Mathf.Abs(p.x)));
                float scale=Mathf.Lerp(i<6?.8f:.07f,1.2f,coverage)*edge;
                clouds[i].localScale=cloudScales[i]*scale;
            }
        }
        private void ApplyLighting()
        {
            Vector3 sun=SunDirection(hour);float day=DaylightAmount(hour), storm=Mathf.InverseLerp(.4f,1,coverage);
            float twilight=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Abs(sun.y)/.35f));
            Color top=Color.Lerp(new Color(.008f,.014f,.032f),new Color(.19f,.42f,.72f),day);
            Color horizon=Color.Lerp(new Color(.035f,.053f,.085f),new Color(.69f,.80f,.89f),day);
            horizon=Color.Lerp(horizon,new Color(.88f,.58f,.38f),twilight*.55f*(1-storm));
            top=Color.Lerp(top,Color.Lerp(new Color(.019f,.026f,.040f),new Color(.38f,.46f,.52f),day),storm*.8f);
            horizon=Color.Lerp(horizon,Color.Lerp(new Color(.045f,.06f,.085f),new Color(.59f,.66f,.68f),day),storm*.8f);
            liveSky.SetColor("_TopColor",top);liveSky.SetColor("_HorizonColor",horizon);
            liveSky.SetColor("_GroundColor",horizon*.6f);liveSky.SetVector("_SunDirection",sun);
            liveSky.SetFloat("_Night",1-day);liveSky.SetFloat("_Storm",storm);
            if(liveSky.HasProperty("_StarRotation"))liveSky.SetFloat("_StarRotation",starDrift);
            Color cloudTop=Color.Lerp(new Color(.10f,.14f,.22f),new Color(.98f,.97f,.94f),day);
            cloudTop=Color.Lerp(cloudTop,Color.Lerp(new Color(.06f,.08f,.13f),new Color(.64f,.69f,.73f),day),storm);
            cloudTop=Color.Lerp(cloudTop,new Color(.98f,.77f,.59f),twilight*.30f*(1-storm));
            if(liveCloud!=null){liveCloud.SetColor("_TopColor",cloudTop);liveCloud.SetColor("_BottomColor",cloudTop*.70f);}
            if(cloudDeck!=null)cloudDeck.SetConditions(coverage,cloudTop,horizon);
            bool sunUp=sun.y>=0;Vector3 source=sunUp?sun:-sun;
            daylight.enabled=true;daylight.transform.rotation=Quaternion.LookRotation(-source,Vector3.forward);
            daylight.color=sunUp?Color.Lerp(new Color(1,.68f,.43f),new Color(1,.95f,.84f),Mathf.Clamp01(sun.y*2)):new Color(.59f,.72f,1);
            daylight.intensity=Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Abs(sun.y)/.20f))*(sunUp?1.4f:.26f)*Mathf.Lerp(1,.56f,storm);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientIntensity=1;
            RenderSettings.ambientSkyColor=Color.Lerp(new Color(.19f,.24f,.36f),new Color(.73f,.80f,.85f),day)*Mathf.Lerp(1,.82f,storm);
            RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.16f,.19f,.27f),new Color(.62f,.66f,.63f),day);
            RenderSettings.ambientGroundColor=Color.Lerp(new Color(.095f,.12f,.18f),new Color(.36f,.39f,.33f),day);
            RenderSettings.reflectionIntensity=Mathf.Lerp(.20f,.65f,day)*Mathf.Lerp(1,.8f,storm);
            if(atmosphere!=null){atmosphere.outdoorHazeColor=horizon;atmosphere.outdoorDensity=Mathf.Lerp(.0065f,.013f,storm);}
            if(livePond!=null)
            {
                float brightness=Mathf.Lerp(.26f,1,day)*Mathf.Lerp(1,.82f,storm);
                livePond.SetColor("_BaseColor",oldPondBase*brightness);livePond.SetColor("_ShoreColor",oldPondShore*brightness);
                livePond.SetColor("_SkyTint",Color.Lerp(oldPondSky*brightness,horizon,.35f));
                if(livePond.HasProperty("_ReflectionStrength"))
                {
                    livePond.SetColor("_TopColor",top);livePond.SetColor("_HorizonColor",horizon);
                    livePond.SetColor("_GroundColor",horizon*.6f);livePond.SetVector("_SunDirection",sun);
                    livePond.SetFloat("_Night",1-day);livePond.SetFloat("_Storm",storm);livePond.SetFloat("_StarRotation",starDrift);
                }
            }
            if(rain!=null)rain.SetWeatherAmount(precipitation);
        }
        private void OnDisable()
        {
            if(!captured)return;captured=false;
            if(RenderSettings.skybox==liveSky)RenderSettings.skybox=previousSky;
            RenderSettings.sun=oldSun;RenderSettings.ambientMode=oldAmbientMode;RenderSettings.ambientIntensity=oldAmbientIntensity;
            RenderSettings.ambientSkyColor=oldSky;RenderSettings.ambientEquatorColor=oldEquator;RenderSettings.ambientGroundColor=oldGround;
            RenderSettings.reflectionIntensity=oldReflection;
            if(daylight!=null){daylight.color=oldLightColor;daylight.intensity=oldLightIntensity;daylight.transform.rotation=oldLightRotation;daylight.enabled=oldLightEnabled;}
            if(atmosphere!=null){atmosphere.outdoorHazeColor=oldHaze;atmosphere.outdoorDensity=oldHazeDensity;}
            if(rain!=null){rain.ClearWeatherOverride();rain.rainEnabled=oldRainEnabled;}
            for(int i=0;i<clouds.Length;i++)if(clouds[i]!=null)
            {
                clouds[i].localPosition=cloudPositions[i];clouds[i].localScale=cloudScales[i];
                if(cloudRenderers[i]!=null && cloudRenderers[i].sharedMaterial==liveCloud)cloudRenderers[i].sharedMaterial=previousCloudMaterials[i];
            }
            if(pondRenderer!=null && pondRenderer.sharedMaterial==livePond)pondRenderer.sharedMaterial=previousPond;
            if(cloudDeck!=null)cloudDeck.SetReflectionTarget(null);
            if(liveSky!=null)Destroy(liveSky);if(liveCloud!=null)Destroy(liveCloud);if(livePond!=null)Destroy(livePond);
            nightEvents=null;
        }
    }
}
