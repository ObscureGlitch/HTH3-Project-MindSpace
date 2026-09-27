using System;
using UnityEngine;

namespace TheLastWatch.UI
{
    // Small cached nine-slice textures; translucency without blur, extra cameras or post-processing.
    public sealed class WellnessUiTheme : IDisposable
    {
        public static readonly Color Ink=new Color(.19f,.23f,.20f),Muted=new Color(.40f,.44f,.39f),Sage=new Color(.36f,.53f,.41f);
        public readonly GUIStyle Panel,Shadow,Text,Small,Heading,Title,Button,Key,Tag;
        public readonly GUIStyle HudText,HudSmall,HudHeading,HudButton;
        private readonly Texture2D card,shadow,button,hover,key,dot;
        private readonly Texture2D[] skyIcons=new Texture2D[4];
        private readonly GUIStyle outlined;
        public WellnessUiTheme()
        {
            card=Round(new Color(.94f,.925f,.885f,.90f));shadow=Round(new Color(.09f,.14f,.11f,.09f));
            button=Round(new Color(.995f,.99f,.96f,.88f));hover=Round(new Color(.81f,.87f,.81f,.98f));key=Round(new Color(1,1,.99f,.96f));dot=Round(Color.white,32);
            Panel=Box(card);Shadow=Box(shadow);
            Text=Label(17,Ink);Small=Label(12,Muted);Heading=Label(13,Ink,FontStyle.Bold);Title=Label(25,Ink,FontStyle.Bold);
            Button=new GUIStyle(GUI.skin.button){fontSize=14,richText=false,wordWrap=true,border=new RectOffset(16,16,16,16),padding=new RectOffset(8,8,4,4)};
            Button.normal.background=button;Button.hover.background=hover;Button.active.background=hover;
            Button.focused.background=hover;Button.normal.textColor=Button.hover.textColor=Button.active.textColor=Button.focused.textColor=Ink;
            Button.onNormal.background=Button.onHover.background=Button.onActive.background=Button.onFocused.background=hover;
            Button.onNormal.textColor=Button.onHover.textColor=Button.onActive.textColor=Button.onFocused.textColor=Ink;
            Key=Box(key);Key.fontSize=11;Key.alignment=TextAnchor.MiddleCenter;Key.normal.textColor=Ink;
            Tag=Label(10,Muted,FontStyle.Bold);
            Color ivory=new Color(.96f,.97f,.93f,.94f);
            HudText=Label(17,ivory);HudSmall=Label(12,ivory);HudHeading=Label(13,ivory,FontStyle.Bold);
            HudButton=new GUIStyle(HudSmall){alignment=TextAnchor.MiddleCenter,wordWrap=false};
            HudButton.hover.textColor=HudButton.active.textColor=HudButton.focused.textColor=Color.white;
            outlined=new GUIStyle(HudText);
            for(int i=0;i<skyIcons.Length;i++)skyIcons[i]=SkyIcon(i);
        }
        private static GUIStyle Label(int size,Color color,FontStyle weight=FontStyle.Normal)=>
            new GUIStyle(GUI.skin.label){fontSize=size,fontStyle=weight,richText=false,wordWrap=true,normal={textColor=color},padding=new RectOffset(0,0,0,0)};
        private static GUIStyle Box(Texture2D texture)=>new GUIStyle{normal={background=texture},border=new RectOffset(16,16,16,16)};
        private static Texture2D Round(Color color,float radius=14)
        {
            const int n=64;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float dx=Mathf.Max(Mathf.Abs(x+.5f-n*.5f)-(n*.5f-radius),0),dy=Mathf.Max(Mathf.Abs(y+.5f-n*.5f)-(n*.5f-radius),0);
                Color c=color;c.a*=Mathf.Clamp01(radius+.5f-Mathf.Sqrt(dx*dx+dy*dy));pixels[y*n+x]=c;
            }
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        public void Card(Rect r,float opacity=1)
        {
            if(opacity<=0)return;
            GUI.Box(new Rect(r.x,r.y+3,r.width,r.height),GUIContent.none,Shadow);
            Color old=GUI.color;GUI.color=new Color(old.r,old.g,old.b,old.a*opacity);GUI.Box(r,GUIContent.none,Panel);GUI.color=old;
        }
        public void Dot(Rect r,Color color){Color old=GUI.color;GUI.color=color;GUI.DrawTexture(r,dot);GUI.color=old;}
        public void HudLabel(Rect r,string text,GUIStyle style,float opacity=1)
        {
            Color old=GUI.color;GUI.color=new Color(old.r,old.g,old.b,old.a*opacity);
            outlined.fontSize=style.fontSize;outlined.fontStyle=style.fontStyle;outlined.alignment=style.alignment;outlined.wordWrap=style.wordWrap;
            outlined.normal.textColor=new Color(.025f,.045f,.045f,.72f);
            GUI.Label(new Rect(r.x-1,r.y,r.width,r.height),text,outlined);
            GUI.Label(new Rect(r.x+1,r.y+1,r.width,r.height),text,outlined);
            GUI.Label(r,text,style);GUI.color=old;
        }
        public bool HudAction(Rect r,string text,string tooltip="")
        {
            bool clicked=GUI.Button(r,new GUIContent("",tooltip),HudButton);
            HudLabel(r,text,HudButton,GUI.enabled?1:.4f);return clicked;
        }
        public void WeatherIcon(Rect r,int kind)
        {
            kind=Mathf.Clamp(kind,0,3);Color old=GUI.color;
            GUI.color=new Color(.02f,.04f,.05f,.7f);GUI.DrawTexture(new Rect(r.x+1,r.y+1,r.width,r.height),skyIcons[kind]);
            GUI.color=new Color(.96f,.97f,.93f,.92f);GUI.DrawTexture(r,skyIcons[kind]);GUI.color=old;
        }
        private static Texture2D SkyIcon(int kind)
        {
            const int n=48;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};
            var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=(x+.5f)/n,v=(y+.5f)/n,dx=u-.5f,dy=v-.5f,d=Mathf.Sqrt(dx*dx+dy*dy);bool on=false;
                if(kind==0){float angle=Mathf.Atan2(dy,dx);on=d<.19f||(d>.29f&&d<.41f&&Mathf.Cos(angle*8)>.87f);}
                else if(kind==1)on=d<.36f&&Vector2.Distance(new Vector2(u,v),new Vector2(.67f,.66f))>.32f;
                else
                {
                    float cy=kind==3?.65f:.52f;
                    on=(u>.18f&&u<.82f&&v>cy-.14f&&v<cy+.06f)||
                        Vector2.Distance(new Vector2(u,v),new Vector2(.35f,cy+.02f))<.17f||
                        Vector2.Distance(new Vector2(u,v),new Vector2(.55f,cy+.09f))<.22f||
                        Vector2.Distance(new Vector2(u,v),new Vector2(.76f,cy))<.13f;
                    if(kind==3&&v>.11f&&v<.37f)
                    for(int k=0;k<3;k++)if(Mathf.Abs(u-(.28f+k*.23f+(v-.24f)*.38f))<.021f)on=true;
                }
                pixels[y*n+x]=on?Color.white:Color.clear;
            }
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        public static void Fill(Rect r,Color color){Color old=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        public void Dispose()
        {
            foreach(var t in new[]{card,shadow,button,hover,key,dot})if(t!=null)UnityEngine.Object.Destroy(t);
            foreach(var t in skyIcons)if(t!=null)UnityEngine.Object.Destroy(t);
        }
    }
}
