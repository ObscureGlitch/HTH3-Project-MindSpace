using System;

namespace TheLastWatch.Environment
{
    /// <summary>Damped shallow-water wave equation, in metres and seconds.</summary>
    public sealed class PondWaveField
    {
        public const int Resolution = 128;
        public const float StepSeconds = 1f / 60f;
        public readonly float[] Height = new float[Resolution * Resolution];
        public readonly bool[] Wet = new bool[Resolution * Resolution];
        private readonly float[] velocity = new float[Resolution * Resolution];
        private readonly float[] next = new float[Resolution * Resolution];
        private readonly float dx, dz, speed;
        public bool IsActive { get; private set; }
        public PondWaveField(float width, float depth)
        {
            dx = Math.Max(.001f, width / (Resolution - 1));
            dz = Math.Max(.001f, depth / (Resolution - 1));
            // CFL bound also keeps unusually small ponds stable.
            speed = Math.Min(1.15f, .45f * Math.Min(dx, dz) / StepSeconds);
        }
        public void Impulse(float u, float v, float strength)
        {
            if (float.IsNaN(u) || float.IsNaN(v) || float.IsNaN(strength) ||
                float.IsInfinity(strength) || u <= 0 || u >= 1 || v <= 0 || v >= 1) return;
            int cx = (int)(u * (Resolution - 1)), cz = (int)(v * (Resolution - 1));
            strength = Math.Max(-.7f, Math.Min(.7f, strength));
            float sum=0;int count=0;
            for(int z=Math.Max(1,cz-2);z<=Math.Min(Resolution-2,cz+2);z++)
                for(int x=Math.Max(1,cx-2);x<=Math.Min(Resolution-2,cx+2);x++)
                    if(Wet[z*Resolution+x]){sum+=(float)Math.Exp(-((x-cx)*(x-cx)+(z-cz)*(z-cz))*.7);count++;}
            if(count<2||strength==0)return;
            IsActive = true;
            // A depression displaces water into its rim; it must not drain the pond.
            float mean=sum/count;
            for (int z = Math.Max(1, cz - 2); z <= Math.Min(Resolution - 2, cz + 2); z++)
                for (int x = Math.Max(1, cx - 2); x <= Math.Min(Resolution - 2, cx + 2); x++)
                {
                    int i = z * Resolution + x;
                    if (Wet[i]) velocity[i] += strength * ((float)Math.Exp(-((x-cx)*(x-cx)+(z-cz)*(z-cz)) * .7)-mean);
                }
        }
        public void Step()
        {
            if (!IsActive) return;
            const int n = Resolution;
            double volume=0,momentum=0;int wetCount=0;float peak=0,peakVelocity=0;
            for (int z = 1; z < n - 1; z++) for (int x = 1; x < n - 1; x++)
            {
                int i = z * n + x;
                if (!Wet[i]) continue;
                float h = Height[i];
                // No-flux banks reflect waves back into the pond.
                float left = Wet[i-1] ? Height[i-1] : h, right = Wet[i+1] ? Height[i+1] : h;
                float down = Wet[i-n] ? Height[i-n] : h, up = Wet[i+n] ? Height[i+n] : h;
                float laplacian = (left + right - 2*h)/(dx*dx) + (down + up - 2*h)/(dz*dz);
                velocity[i] = (velocity[i] + speed*speed*laplacian*StepSeconds) * .984f;
                next[i] = h + velocity[i]*StepSeconds;
                volume+=next[i];momentum+=velocity[i];wetCount++;peak=Math.Max(peak,Math.Abs(next[i]));
                peakVelocity=Math.Max(peakVelocity,Math.Abs(velocity[i]));
            }
            if(wetCount==0)return;
            float mean=(float)(volume/wetCount),meanVelocity=(float)(momentum/wetCount);
            // Bound exceptional repeated impacts without injecting/removing water through clipping.
            float scale=Math.Min(1,.07f/Math.Max(.000001f,peak+Math.Abs(mean)));
            for(int z=1;z<n-1;z++)for(int x=1;x<n-1;x++)
            {
                int i=z*n+x;if(!Wet[i])continue;
                Height[i]=(next[i]-mean)*scale;velocity[i]=(velocity[i]-meanVelocity)*scale;
            }
            // Sub-pixel settled ripples need no more solver work until the next impact.
            if (peak < .000001f && peakVelocity < .00001f)
            {
                Array.Clear(Height, 0, Height.Length);
                Array.Clear(velocity, 0, velocity.Length);
                IsActive = false;
            }
        }
    }
}
