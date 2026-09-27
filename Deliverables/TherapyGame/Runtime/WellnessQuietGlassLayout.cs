using System;

namespace TheLastWatch.UI
{
    // Coordinates measured from the approved 1672 x 941 Quiet Glass concept.
    // Kept independent of Unity so resizing can be checked without launching the editor.
    public static class WellnessQuietGlassLayout
    {
        public const float Width=1672, Height=941;
        public const float PanelX=216, PanelY=102, PanelWidth=1236, PanelHeight=737;
        public const float ContentX=612, ContentWidth=798, FooterY=741;
        public static float Scale(float width,float height)=>Math.Min(width/Width,height/Height);
        public static float OffsetX(float width,float height)=>(width-Width*Scale(width,height))*.5f;
        public static float OffsetY(float width,float height)=>(height-Height*Scale(width,height))*.5f;
        public static float SliderThumb(float value)=>840+13+408*Math.Max(0,Math.Min(1,value));
    }
}
