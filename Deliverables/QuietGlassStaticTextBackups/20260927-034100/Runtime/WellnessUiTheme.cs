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
        private readonly Texture2D vinyl,note,musicPanel;
        private readonly GUIStyle musicBox;
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
            vinyl=MusicTexture(false);note=MusicTexture(true);musicPanel=Round(new Color(.055f,.08f,.075f,.43f),12);musicBox=Box(musicPanel);
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
        public void MusicCard(Rect r)=>GUI.Box(r,GUIContent.none,musicBox);
        public static Matrix4x4 VinylMatrix(Matrix4x4 canvas,Vector2 pivot,float degrees)
        {
            // Retained for an older installer's numerical checks only. Vinyl rendering now uses a fixed rect.
            Vector3 center=new Vector3(pivot.x,pivot.y,0);
            return canvas*Matrix4x4.Translate(center)*Matrix4x4.Rotate(Quaternion.Euler(0,0,degrees))*Matrix4x4.Translate(-center);
        }
        public void Vinyl(Rect r,float degrees,float seconds,bool playing)
        {
            WellnessUiPrimitives.Vinyl(r,vinyl,degrees,new Color(.94f,.92f,.80f));
            if(!playing)return;
            Color old=GUI.color;
            for(int i=0;i<3;i++)
            {
                float life=Mathf.Repeat(seconds/3.2f+i/3f,1),size=6+i*.6f;
                GUI.color=new Color(.88f,.94f,.86f,Mathf.Sin(life*Mathf.PI)*.6f);
                GUI.DrawTexture(new Rect(r.x+18+Mathf.Sin(life*4+i)*3,r.y+16-life*20,size,size),note);
            }
            GUI.color=old;
        }
        private static Texture2D MusicTexture(bool musicalNote)
        {
            const int n=64;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};
            var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=(x+.5f)/n,v=(y+.5f)/n,d=Vector2.Distance(new Vector2(u,v),new Vector2(.5f,.5f));Color c=Color.clear;
                if(musicalNote)
                {
                    bool on=((u-.33f)*(u-.33f)/.038f+(v-.24f)*(v-.24f)/.018f<1)||
                        (u>.43f&&u<.53f&&v>.24f&&v<.89f)||(u>.5f&&u<.78f&&v>.67f&&v<.84f);
                    if(on)c=Color.white;
                }
                else if(d<.48f)
                {
                    float groove=.06f+(.5f+.5f*Mathf.Cos(d*140))*.045f;
                    c=new Color(groove,groove+.014f,groove+.012f,Mathf.Clamp01((.48f-d)*n));
                    if(d<.14f)c=new Color(.62f,.74f,.59f);
                    if(d<.028f)c=new Color(.05f,.08f,.06f);
                    if(d>.25f&&d<.44f&&Mathf.Abs((u-.5f)-(v-.5f)*.55f)<.045f)c=Color.Lerp(c,new Color(.65f,.73f,.69f),.28f);
                }
                pixels[y*n+x]=c;
            }
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
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
            foreach(var t in new[]{card,shadow,button,hover,key,dot,vinyl,note,musicPanel})if(t!=null)UnityEngine.Object.Destroy(t);
            foreach(var t in skyIcons)if(t!=null)UnityEngine.Object.Destroy(t);
        }
    }
}
