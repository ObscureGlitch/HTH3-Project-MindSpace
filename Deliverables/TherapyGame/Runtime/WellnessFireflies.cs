using UnityEngine;

namespace TheLastWatch.Environment
{
    /// <summary>One 112-triangle batch, no lights/particles/physics. Blinking runs on the vertex shader.</summary>
    [DisallowMultipleComponent]
    public sealed class WellnessFireflies : MonoBehaviour
    {
        public WellnessSkyCycle sky;
        public Renderer glowRenderer;
        private MaterialPropertyBlock properties;
        private float nextUpdate;
        private static readonly int Visibility=Shader.PropertyToID("_Visibility");
        public static float NightVisibility(float hour,float rain)
        {
            float darkness=1-WellnessSkyCycle.DaylightAmount(hour);
            return Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.9f,darkness))*Mathf.Lerp(1,.25f,Mathf.Clamp01(rain));
        }
        private void OnEnable(){properties=new MaterialPropertyBlock();nextUpdate=0;}
        private void Update()
        {
            if(!Application.isPlaying||sky==null||glowRenderer==null||Time.unscaledTime<nextUpdate)return;
            nextUpdate=Time.unscaledTime+.15f;
            float rain=sky.rain!=null?sky.rain.EffectiveIntensity:0;
            float visibility=NightVisibility(sky.Hour,rain);
            glowRenderer.enabled=visibility>.001f;
            properties.SetFloat(Visibility,visibility);glowRenderer.SetPropertyBlock(properties);
        }
        private void OnDisable()
        {
            if(glowRenderer!=null){glowRenderer.SetPropertyBlock(null);glowRenderer.enabled=false;}
        }
    }
}
