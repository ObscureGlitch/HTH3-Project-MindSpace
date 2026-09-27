using System;

namespace TheLastWatch.Environment
{
    // An artistic approximation of land-based convection, not a fluid/weather
    // simulation. Game time supplies solar heating; real time smooths development.
    public sealed class WellnessCumulusLife
    {
        public const float Distance=7000, BaseHeight=650, Width=9600, Height=4700;
        public float Development { get; private set; }
        public float HeightFactor => .78f+.22f*Development;
        public float WidthFactor => .94f+.06f*Development;
        public float AutomaticOpacity => Math.Min(1,Development*1.3f);
        public WellnessCumulusLife(float hour){Development=Heating(hour);}
        private static float Smooth(float x){x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x);}
        public static float Heating(float hour)
        {
            if(float.IsNaN(hour)||float.IsInfinity(hour))return 0;
            hour=(hour%24+24)%24;
            return Smooth((hour-7)/6)*(1-Smooth((hour-16)/4));
        }
        public void Tick(float seconds,float hour)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0)return;
            float dt=Math.Min(seconds,.25f);
            Development+=(Heating(hour)-Development)*(float)(1-Math.Exp(-dt/45));
        }
        public static float AdvanceAzimuth(float degrees,float wind,float seconds)
        {
            float next=degrees+Math.Max(0,Math.Min(4,wind))*Math.Max(0,Math.Min(.25f,seconds))/Distance*(180f/(float)Math.PI);
            next%=360;return next<0?next+360:next;
        }
        public static bool CardVisible(bool singleBank,int index,bool originallyEnabled,float opacity)
            => originallyEnabled&&opacity>0&&(!singleBank||index==0);
    }
}
