using System;

namespace TheLastWatch.UI
{
    // Pure UI geometry: angular motion must never move or rotate the destination rectangle.
    public static class WellnessUiMotion
    {
        public static float Radius(float width,float height,float desired)=>Math.Max(0,Math.Min(desired,Math.Min(width,height)*.5f));
        public static float LabelX(float center,float diameter,float angle,float phase=0)=>center+(float)Math.Cos((angle+phase)*Math.PI/180)*diameter*.082f;
        public static float LabelY(float center,float diameter,float angle,float phase=0)=>center+(float)Math.Sin((angle+phase)*Math.PI/180)*diameter*.082f;
        public static float NoteLife(float clock,int index)
        {
            double phase=clock/3.8+index/3.0;
            float life=(float)(phase-Math.Floor(phase));return life>=1?0:life;
        }
        public static float NoteX(float life,int index)=>(float)Math.Sin(life*2.8+index*.8)*3+index*7;
        public static float NoteY(float life)=>-life*27;
        public static float NoteAlpha(float life)=>(float)Math.Sin(Math.PI*life)*.50f;
    }
}
