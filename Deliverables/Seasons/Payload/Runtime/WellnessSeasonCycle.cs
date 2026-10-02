using System;
using System.Collections.Generic;
using UnityEngine;
using TheLastWatch.Player;
using TheLastWatch.Integrations;

namespace TheLastWatch.Environment
{
    public enum WellnessSeason { Spring,Summer,Fall,Winter }
    public enum WellnessSeasonSurface { Ground,Deciduous,Evergreen,Rock,Path,Grass,Flower,Mountain }

    [DefaultExecutionOrder(-4000),DisallowMultipleComponent]
    public sealed class WellnessSeasonCycle : MonoBehaviour
    {
        [Serializable] public sealed class Surface
        {public Renderer renderer;public WellnessSeasonSurface[] slots=Array.Empty<WellnessSeasonSurface>();}
        public WellnessSeason season=WellnessSeason.Spring;
        public bool automatic=true;
        [Range(5,60)] public float seasonMinutes=24;
        public Surface[] surfaces=Array.Empty<Surface>();
        public Renderer[] winterDormant=Array.Empty<Renderer>(),butterflies=Array.Empty<Renderer>();
        public Renderer winterBranches;
        public ParticleSystem fallingLeaves;
        public Bounds[] leafCanopies=Array.Empty<Bounds>();
        public WellnessExplorer player;
        public WellnessSkyCycle sky;
        public WellnessSeason Current=>season;
        public Vector4 Weights {get;private set;}
        public bool IsSnowSeason=>Weights.w>=.5f;
        public float InsectActivity=>Mathf.Clamp01(Weights.x*.8f+Weights.y+Weights.z*.3f);
        public float SecondsRemaining=>Mathf.Max(0,Mathf.Max(300,seasonMinutes*60)-elapsed);
        public string Description=>season==WellnessSeason.Spring?"Fresh greens, returning blooms and gentle showers.":
            season==WellnessSeason.Summer?"Full foliage, bright meadows and longer clear spells.":
            season==WellnessSeason.Fall?"Amber trees, drifting leaves and cool, changeable skies.":
            "Snowy ground, bare branches, frosted evergreens and snowfall.";
        private float elapsed,blendClock=8,updateClock,leafClock;
        private Vector4 blendFrom;
        private bool ready,paletteDirty;
        private readonly List<Palette> palettes=new List<Palette>();
        private readonly List<Snapshot> snapshots=new List<Snapshot>();
        private readonly List<Visibility> visibility=new List<Visibility>();
        private System.Random random;
        private sealed class Palette{public Material original,live;public Color color;public WellnessSeasonSurface role;}
        private sealed class Snapshot{public Renderer renderer;public Material[] materials;}
        private sealed class Visibility{public Renderer renderer;public bool original,originalEnabled;public int kind;}

        public static Vector4 Weight(WellnessSeason value)
        {var result=Vector4.zero;result[(int)value]=1;return result;}
        public void SetSeason(WellnessSeason value,bool keepCycling=false)
        {
            if((int)value<0||(int)value>3)throw new ArgumentOutOfRangeException(nameof(value));
            automatic=keepCycling;elapsed=0;if(season==value)return;
            blendFrom=Weights;blendClock=0;season=value;paletteDirty=true;
            if(fallingLeaves!=null)fallingLeaves.Clear();
        }
        public void SetAutomatic(bool value){automatic=value;elapsed=0;}
        public void Tick(float seconds)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0)return;
            float dt=Mathf.Min(seconds,.25f);
            if(automatic){elapsed+=dt;if(elapsed>=Mathf.Max(300,seasonMinutes*60))SetSeason((WellnessSeason)(((int)season+1)%4),true);}
            blendClock=Mathf.Min(8,blendClock+dt);
            Weights=Vector4.Lerp(blendFrom,Weight(season),Mathf.SmoothStep(0,1,blendClock/8));
        }
        public static Vector2 Weather(WellnessSeason value,float cycle)
        {
            float t=Mathf.Repeat(cycle,1);
            float clear=value==WellnessSeason.Spring?.22f:value==WellnessSeason.Summer?.55f:value==WellnessSeason.Fall?.32f:.26f;
            float cloud=value==WellnessSeason.Spring?.43f:value==WellnessSeason.Summer?.70f:value==WellnessSeason.Fall?.53f:.45f;
            float wet=value==WellnessSeason.Spring?.80f:value==WellnessSeason.Summer?.79f:value==WellnessSeason.Fall?.84f:.84f;
            float clearCover=value==WellnessSeason.Summer?.12f:.24f,wetStrength=value==WellnessSeason.Winter?.72f:value==WellnessSeason.Summer?.7f:.9f;
            Vector2 from,to;float start;
            if(t<clear){start=0;from=new Vector2(.4f,0);to=new Vector2(clearCover,0);}
            else if(t<cloud){start=clear;from=new Vector2(clearCover,0);to=new Vector2(.85f,0);}
            else if(t<wet){start=cloud;from=new Vector2(.85f,0);to=new Vector2(1,wetStrength);}
            else{start=wet;from=new Vector2(1,wetStrength);to=new Vector2(.4f,0);}
            return Vector2.Lerp(from,to,Mathf.SmoothStep(0,1,Mathf.Clamp01((t-start)/.05f)));
        }
        public Vector2 SampleWeather(float cycle)=>Weather(WellnessSeason.Spring,cycle)*Weights.x+Weather(WellnessSeason.Summer,cycle)*Weights.y+
            Weather(WellnessSeason.Fall,cycle)*Weights.z+Weather(WellnessSeason.Winter,cycle)*Weights.w;
        public static string Phase(WellnessSeason value,float cycle)
        {
            float t=Mathf.Repeat(cycle,1);
            float clear=value==WellnessSeason.Spring?.22f:value==WellnessSeason.Summer?.55f:value==WellnessSeason.Fall?.32f:.26f;
            float cloud=value==WellnessSeason.Spring?.43f:value==WellnessSeason.Summer?.70f:value==WellnessSeason.Fall?.53f:.45f;
            float wet=value==WellnessSeason.Spring?.80f:value==WellnessSeason.Summer?.79f:.84f;
            return t<clear?"Clear":t<cloud?"Cloudy":t<wet?(value==WellnessSeason.Winter?"Snow":"Rain"):"Clearing";
        }
        public static Color PaletteColor(WellnessSeasonSurface role,Color original,WellnessSeason value)
        {
            if(value==WellnessSeason.Summer)return original;
            float variation=Mathf.InverseLerp(.23f,.75f,original.grayscale);Color target=original;
            if(value==WellnessSeason.Spring)
            {
                if(role==WellnessSeasonSurface.Ground||role==WellnessSeasonSurface.Grass)target=Color.Lerp(original,new Color(.50f,.72f,.31f),.28f);
                if(role==WellnessSeasonSurface.Deciduous)target=Color.Lerp(original,new Color(.59f,.77f,.34f),.32f);
                if(role==WellnessSeasonSurface.Flower)target=Color.Lerp(original,new Color(1,.90f,.86f),.13f);
            }
            else if(value==WellnessSeason.Fall)
            {
                if(role==WellnessSeasonSurface.Deciduous)target=Color.Lerp(new Color(.67f,.24f,.075f),new Color(.96f,.69f,.19f),variation);
                if(role==WellnessSeasonSurface.Ground||role==WellnessSeasonSurface.Grass)target=Color.Lerp(original,new Color(.58f,.49f,.26f),.57f);
                if(role==WellnessSeasonSurface.Flower)target=Color.Lerp(original,new Color(.63f,.41f,.24f),.42f);
                if(role==WellnessSeasonSurface.Evergreen)target=Color.Lerp(original,new Color(.20f,.37f,.23f),.12f);
            }
            else
            {
                var snow=Color.Lerp(new Color(.74f,.82f,.88f),new Color(.94f,.96f,.97f),variation);
                if(role==WellnessSeasonSurface.Ground)target=snow;
                if(role==WellnessSeasonSurface.Deciduous)target=Color.Lerp(original,snow,.87f);
                if(role==WellnessSeasonSurface.Evergreen)target=Color.Lerp(original,new Color(.69f,.79f,.79f),.44f);
                if(role==WellnessSeasonSurface.Rock)target=Color.Lerp(original,snow,.62f);
                if(role==WellnessSeasonSurface.Path)target=Color.Lerp(original,new Color(.79f,.82f,.82f),.33f);
                if(role==WellnessSeasonSurface.Grass||role==WellnessSeasonSurface.Flower)target=new Color(.54f,.56f,.50f);
                if(role==WellnessSeasonSurface.Mountain)target=Color.Lerp(original,new Color(.69f,.79f,.85f),.42f);
            }
            target.a=original.a;return target;
        }
        private void OnEnable(){if(Application.isPlaying)CaptureEnvironment();}
        private void CaptureEnvironment()
        {
            RestoreEnvironment();Weights=blendFrom=Weight(season);blendClock=8;elapsed=updateClock=leafClock=0;random=new System.Random(260930);
            foreach(var surface in surfaces)
            {
                if(surface.renderer==null)continue;
                var original=surface.renderer.sharedMaterials;var materials=(Material[])original.Clone();
                snapshots.Add(new Snapshot{renderer=surface.renderer,materials=original});
                for(int slot=0;slot<materials.Length;slot++)
                {
                    Material source=original[slot];if(source==null||slot>=surface.slots.Length||(int)surface.slots[slot]<0||!source.HasProperty("_BaseColor"))continue;
                    Palette palette=palettes.Find(p=>p.original==source&&p.role==surface.slots[slot]);
                    if(palette==null)
                    {
                        palette=new Palette{original=source,role=surface.slots[slot],color=source.GetColor("_BaseColor"),
                            live=new Material(source){name=source.name+" (season runtime)",hideFlags=HideFlags.DontSave}};
                        palettes.Add(palette);
                    }
                    materials[slot]=palette.live;
                }
                surface.renderer.sharedMaterials=materials;
            }
            foreach(var renderer in winterDormant)RememberVisibility(renderer,0);
            foreach(var renderer in butterflies)RememberVisibility(renderer,1);
            RememberVisibility(winterBranches,2);
            ready=true;ApplyEnvironment();paletteDirty=false;
            if(fallingLeaves!=null){fallingLeaves.Clear();fallingLeaves.Play();}
        }
        private void RememberVisibility(Renderer renderer,int kind)
        {if(renderer!=null)visibility.Add(new Visibility{renderer=renderer,original=renderer.forceRenderingOff,originalEnabled=renderer.enabled,kind=kind});}
        private void ApplyEnvironment()
        {
            foreach(var palette in palettes)
            {
                Color color=PaletteColor(palette.role,palette.color,WellnessSeason.Spring)*Weights.x+
                    PaletteColor(palette.role,palette.color,WellnessSeason.Summer)*Weights.y+
                    PaletteColor(palette.role,palette.color,WellnessSeason.Fall)*Weights.z+
                    PaletteColor(palette.role,palette.color,WellnessSeason.Winter)*Weights.w;
                palette.live.SetColor("_BaseColor",color);if(palette.live.HasProperty("_Color"))palette.live.SetColor("_Color",color);
            }
            foreach(var state in visibility)if(state.renderer!=null)
            {
                if(state.kind==2)state.renderer.enabled=Weights.w>=.65f;
                state.renderer.forceRenderingOff=state.kind==2?Weights.w<.65f:state.original||(state.kind==0?Weights.w>.65f:InsectActivity<.15f);
            }
        }
        private void Update()
        {
            if(!Application.isPlaying||!ready||!Application.isFocused)return;
            float dt=Mathf.Min(Time.deltaTime,.25f);Tick(dt);updateClock+=dt;
            if(updateClock>=.1f){updateClock=0;if(paletteDirty){ApplyEnvironment();paletteDirty=blendClock<8;}}
            EmitLeaves(dt);
        }
        private void EmitLeaves(float dt)
        {
            if(fallingLeaves==null||player==null||player.ViewCamera==null||leafCanopies.Length==0)return;
            float strength=Weights.z;
            if(strength<.05f){if(fallingLeaves.particleCount>0)fallingLeaves.Clear();return;}
            leafClock+=dt*5*strength;
            int count=Mathf.Min(4,Mathf.FloorToInt(leafClock));leafClock-=count;
            for(int i=0;i<count;i++)
            {
                Bounds authored=leafCanopies[random.Next(leafCanopies.Length)];
                Bounds bounds=new Bounds(transform.TransformPoint(authored.center),Vector3.Scale(authored.size,transform.lossyScale));
                if((bounds.center-player.ViewCamera.transform.position).sqrMagnitude>35*35)continue;
                Vector3 spawn=bounds.center+new Vector3(Range(-bounds.extents.x,bounds.extents.x),Range(-bounds.extents.y*.3f,bounds.extents.y*.7f),Range(-bounds.extents.z,bounds.extents.z));
                if(WellnessRain.UnderRoof(spawn))continue;
                float lifetime=5;
                if(Physics.Raycast(spawn,Vector3.down,out var ground,10,~0,QueryTriggerInteraction.Ignore))lifetime=Mathf.Clamp((spawn.y-ground.point.y)/.75f,.1f,7);
                fallingLeaves.Emit(new ParticleSystem.EmitParams{position=spawn,velocity=new Vector3(Range(.05f,.3f),-.75f,Range(-.15f,.2f)),
                    startLifetime=lifetime,startSize=Range(.065f,.13f),startColor=Color.Lerp(new Color(.78f,.29f,.075f,.9f),new Color(.96f,.69f,.18f,.9f),Range(0,1)),applyShapeToPosition=false},1);
            }
        }
        private float Range(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
        private void OnDisable(){RestoreEnvironment();}
        private void RestoreEnvironment()
        {
            if(fallingLeaves!=null)fallingLeaves.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach(var snapshot in snapshots)if(snapshot.renderer!=null)snapshot.renderer.sharedMaterials=snapshot.materials;
            foreach(var state in visibility)if(state.renderer!=null){state.renderer.forceRenderingOff=state.original;state.renderer.enabled=state.originalEnabled;}
            foreach(var palette in palettes)if(palette.live!=null){if(Application.isPlaying)Destroy(palette.live);else DestroyImmediate(palette.live);}
            snapshots.Clear();visibility.Clear();palettes.Clear();ready=false;
        }
    }
}
