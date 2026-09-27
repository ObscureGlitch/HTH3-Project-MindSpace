using System;

namespace TheLastWatch.Environment
{
    public enum WellnessCloudType { SoftBanks, PuffyCumulus, Altocumulus }
    public enum WellnessCloudMode { Automatic, Off, SoftBanks, PuffyCumulus, Altocumulus }

    /// <summary>One cloud family at a time. Fade to a clear sky, change the single
    /// active atlas, then fade back in. Real seconds, no Unity or frame allocations.</summary>
    public sealed class WellnessCloudSequence
    {
        public const float FadeSeconds=18, ClearGapSeconds=1;
        public const float MinimumHoldSeconds=180, MaximumHoldSeconds=300;
        private readonly Random random;
        private double clock, holdEnd, switchAt, gapEnd, fadeInEnd;
        private bool switched;
        private int unseen;
        public WellnessCloudType Current { get; private set; }
        public float Opacity { get; private set; } = 1;
        public float LayoutYawDegrees { get; private set; }
        public int SwitchCount { get; private set; }
        public WellnessCloudMode Mode { get; private set; } = WellnessCloudMode.Automatic;
        public double Seconds => clock;
        public WellnessCloudSequence(int seed,WellnessCloudType initial=WellnessCloudType.PuffyCumulus)
        {
            if((int)initial<0||(int)initial>2)throw new ArgumentOutOfRangeException(nameof(initial));
            random=new Random(seed);Current=initial;unseen=7&~(1<<(int)initial);Schedule(0);
        }
        private static float Smooth(double value){float t=(float)Math.Max(0,Math.Min(1,value));return t*t*(3-2*t);}
        private void Schedule(double start)
        {
            holdEnd=start+MinimumHoldSeconds+random.NextDouble()*(MaximumHoldSeconds-MinimumHoldSeconds);
            switchAt=holdEnd+FadeSeconds;gapEnd=switchAt+ClearGapSeconds;fadeInEnd=gapEnd+FadeSeconds;switched=false;
        }
        public void SetMode(WellnessCloudMode mode)
        {
            if((int)mode<0||(int)mode>4)throw new ArgumentOutOfRangeException(nameof(mode));
            if(Mode==mode)return;
            Mode=mode;
            if(mode==WellnessCloudMode.Off){Opacity=0;return;}
            if(mode!=WellnessCloudMode.Automatic)
            {
                WellnessCloudType next=(WellnessCloudType)((int)mode-2);
                if(next!=Current)
                {Current=next;LayoutYawDegrees=(float)random.NextDouble()*360;SwitchCount++;}
            }
            // Manual selection is an immediate single-atlas replacement, so it
            // also works from a paused settings screen. Never blend families.
            Opacity=1;unseen=7&~(1<<(int)Current);Schedule(clock);
        }
        public void Tick(float seconds)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0)return;
            if(Mode!=WellnessCloudMode.Automatic){Opacity=Mode==WellnessCloudMode.Off?0:1;return;}
            clock+=Math.Min(seconds,.25f);
            if(clock<holdEnd){Opacity=1;return;}
            if(clock<switchAt){Opacity=1-Smooth((clock-holdEnd)/FadeSeconds);return;}
            if(!switched)
            {
                if(unseen==0)unseen=7&~(1<<(int)Current);
                int count=0;for(int i=0;i<3;i++)if((unseen&(1<<i))!=0)count++;
                int pick=random.Next(count);
                for(int i=0;i<3;i++)if((unseen&(1<<i))!=0&&pick--==0){Current=(WellnessCloudType)i;break;}
                unseen&=~(1<<(int)Current);LayoutYawDegrees=(float)random.NextDouble()*360;
                SwitchCount++;switched=true;
            }
            if(clock<gapEnd){Opacity=0;return;}
            if(clock<fadeInEnd){Opacity=Smooth((clock-gapEnd)/FadeSeconds);return;}
            Opacity=1;Schedule(fadeInEnd);
        }
    }
}
