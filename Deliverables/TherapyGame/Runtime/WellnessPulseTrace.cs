using System;
using UnityEngine;

namespace TheLastWatch.UI
{
    public sealed class WellnessPulseTrace : IDisposable
    {
        private Texture2D wave;
        private double phase;
        private float lastTime = -1;
        private bool wasUsable;

        public void Draw(Rect rect, float bpm, bool usable)
        {
            if (Event.current.type != EventType.Repaint) return;
            usable = usable && bpm > 0 && !float.IsNaN(bpm) && !float.IsInfinity(bpm);
            float now = Time.unscaledTime;
            float elapsed = lastTime < 0 ? 0 : Mathf.Max(0, now - lastTime);
            // Restart after a hidden panel, pause, or signal interruption.
            if (!usable || !wasUsable || elapsed > .5f) phase = 0;
            else phase = (phase + elapsed * bpm / 60) % 1;
            lastTime = now;
            if (wave == null && usable)
            {
                wave=MakeTexture("BPM illustration (not ECG)",PresageUiRaster.PulseWave());
                wave.wrapModeU=TextureWrapMode.Repeat;
            }
            wasUsable = usable;
            if(usable)
            {
                float cycles=2*bpm/60;
                GUI.DrawTextureWithTexCoords(rect,wave,new Rect((float)phase-cycles,0,cycles,1));
            }
        }

        private static Texture2D MakeTexture(string name,byte[] pixels)
        {
            var texture=new Texture2D(PresageUiRaster.TraceWidth,PresageUiRaster.TraceHeight,TextureFormat.RGBA32,false)
            {name=name,hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.LoadRawTextureData(pixels);texture.Apply(false,true);
            return texture;
        }

        public void Dispose()
        {
            if (wave != null) UnityEngine.Object.Destroy(wave);
            wave=null;lastTime=-1;phase=0;wasUsable=false;
        }
    }
}
