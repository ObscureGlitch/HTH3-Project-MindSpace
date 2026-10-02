using System.Collections.Generic;
using UnityEngine;
namespace TheLastWatch.UI
{
    // Editor previews use the live HUD draw list, not a separate mockup layout.
    public sealed class FishingPaint
    {
        public Rect Rect;public Color Color;public float Radius,Stroke;public string Content;public GUIStyle Style;public Texture Texture;
        public static FishingPaint Text(Rect r,string text,GUIStyle style)=>new FishingPaint{Rect=r,Content=text,Style=style,Color=style.normal.textColor};
    }
    sealed class FishingTypography
    {
        public readonly GUIStyle Title,Body,Small,Hint,TextButton;
        public FishingTypography(Font font)
        {
            if(font==null)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Small=new GUIStyle{font=font,fontSize=19,alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Clip,padding=new RectOffset(),margin=new RectOffset()};
            Title=new GUIStyle(Small){fontSize=32};Body=new GUIStyle(Small){fontSize=23};Hint=new GUIStyle(Small){fontSize=15};
            TextButton=new GUIStyle(Small);WellnessUiText.Static(TextButton,Color.white);
        }
    }
    public sealed partial class WellnessFishing
    {
        static readonly Color Panel=new Color(.075f,.12f,.13f,1),Card=new Color(.12f,.18f,.19f,1);
        List<FishingPaint> paintCapture;bool drawingFilter;
        void Fill(Rect r,Color color,float radius)
        {if(paintCapture!=null){paintCapture.Add(new FishingPaint{Rect=r,Color=color,Radius=radius});return;}WellnessUiPrimitives.Round(r,color,radius);}
        void Outline(Rect r,Color color,float radius,float stroke=1.5f)
        {if(paintCapture!=null){paintCapture.Add(new FishingPaint{Rect=r,Color=color,Radius=radius,Stroke=stroke});return;}WellnessUiPrimitives.Round(r,color,radius,stroke);}
        bool Hit(Rect r)=>paintCapture==null&&(!filterMenuOpen||drawingFilter)&&GUI.Button(r,GUIContent.none,ui.TextButton);
        void Image(Rect r,Texture texture)
        {if(texture==null)return;if(paintCapture!=null){paintCapture.Add(new FishingPaint{Rect=r,Texture=texture,Color=Color.white});return;}GUI.DrawTexture(r,texture,ScaleMode.ScaleToFit);}
        void SetFilter(int rank){filter=rank;filterMenuOpen=false;RebuildVisible();Select(visible.Count>0?visible[0]:-1);}
        void DrawFilter()
        {
            if(!filterMenuOpen)return;Rect box=new Rect(439,217,183,296);Fill(box,Panel,12);Outline(box,Mint,12);drawingFilter=true;
            for(int option=-1;option<6;option++)
            {
                Rect row=new Rect(box.x+7,box.y+7+(option+1)*40,box.width-14,38);
                if(Action(row,option<0?"All fish":KoiFishingLoot.Tier(option),filter==option)){SetFilter(option);break;}
            }
            drawingFilter=false;
        }
    }
}
