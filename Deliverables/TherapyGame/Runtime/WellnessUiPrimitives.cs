using UnityEngine;

namespace TheLastWatch.UI
{
    public static class WellnessUiPrimitives
    {
        // Native anti-aliased rounded drawing respects the destination bounds even for a 4px strip.
        // A nine-sliced 52px border cannot safely be used for that strip or a small keycap.
        public static void Round(Rect r,Color color,float radius,float stroke=0)
        {
            if(r.width<=0||r.height<=0||Event.current.type!=EventType.Repaint)return;
            float safeRadius=WellnessUiMotion.Radius(r.width,r.height,radius);
            GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,
                Mathf.Clamp(stroke,0,Mathf.Min(r.width,r.height)*.5f),safeRadius);
        }
        public static void Vinyl(Rect r,Texture2D face,float angle,Color etching)
        {
            // The rotationally symmetric disc and its concentric hole are ALWAYS drawn to this exact rect.
            // Rotate only the small label markings mathematically, not GUI.matrix/GUIClip's coordinate frame.
            // This also avoids the GUI's pixel-snapping shimmer at subpixel canvas scales.
            GUI.DrawTexture(r,face);
            float size=Mathf.Max(.85f,r.width*.024f);
            float x=WellnessUiMotion.LabelX(r.center.x,r.width,angle),y=WellnessUiMotion.LabelY(r.center.y,r.height,angle);
            Round(new Rect(x-size*.5f,y-size*.5f,size,size),etching,size*.5f);
            for(int i=0;i<3;i++)
            {
                float phase=142+i*13;
                x=WellnessUiMotion.LabelX(r.center.x,r.width,angle,phase);
                y=WellnessUiMotion.LabelY(r.center.y,r.height,angle,phase);
                float dot=size*.53f;Round(new Rect(x-dot*.5f,y-dot*.5f,dot,dot),etching,dot*.5f);
            }
        }
    }
}
