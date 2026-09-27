using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastWatch.UI
{
    // Code-native Quiet Glass artwork. Cached tiny icons, no shader/blur/capture pass.
    public sealed class WellnessOnboardingTheme : IDisposable
    {
        public static readonly Color Ink=new Color(.055f,.083f,.067f),Muted=new Color(.32f,.33f,.32f),Sage=new Color(.44f,.51f,.38f);
        public readonly GUIStyle Brand,Tagline,Title,Subtitle,RowTitle,Copy,Fine,Tiny,Status,Guidance,PreviewText;
        private readonly GUIStyle button,primary,empty,link,companion;
        public bool IsValid=>Title!=null&&Title.fontSize==48&&button!=null&&button.fontSize==17;
        private readonly Dictionary<string,Texture2D> icons=new Dictionary<string,Texture2D>();
        public WellnessOnboardingTheme(Font serif,Font sans)
        {
            // Carlito's softer sans-serif forms replace the pointed serif headings.
            serif=sans!=null?sans:GUI.skin.font;sans=serif;
            Brand=Label(serif,30,new Color(.99f,.96f,.86f));Tagline=Label(sans,13,new Color(.96f,.95f,.87f));
            Title=Label(serif,48,Ink,TextAnchor.MiddleCenter);Subtitle=Label(sans,22,Muted,TextAnchor.MiddleCenter);
            RowTitle=Label(serif,21,Ink);Copy=Label(sans,15,Muted);Copy.wordWrap=true;
            Fine=Label(sans,13,Muted);Tiny=Label(sans,12,Muted);Status=Label(serif,16,Ink,TextAnchor.MiddleLeft);
            Guidance=Label(sans,12,Muted);Guidance.wordWrap=true;
            PreviewText=Label(sans,14,new Color(.98f,.97f,.92f),TextAnchor.MiddleCenter);
            button=Label(serif,17,Ink,TextAnchor.MiddleCenter);primary=Label(serif,18,new Color(.99f,.985f,.96f),TextAnchor.MiddleCenter);
            link=Label(sans,13,Ink,TextAnchor.MiddleCenter);empty=new GUIStyle {padding=new RectOffset(),margin=new RectOffset()};
            companion=Label(sans,18,Ink,TextAnchor.MiddleLeft);
        }
        private static GUIStyle Label(Font font,int size,Color color,TextAnchor alignment=TextAnchor.UpperLeft)
        {
            return WellnessUiText.Static(new GUIStyle {font=font,fontSize=size,fontStyle=FontStyle.Normal,alignment=alignment,
                clipping=TextClipping.Clip,richText=false,padding=new RectOffset(),margin=new RectOffset(),normal={textColor=color}},color);
        }
        public void Rounded(Rect r,Color color,float radius)=>WellnessUiPrimitives.Round(r,color,radius);
        public void Surface(Rect r)
        {
            Rounded(new Rect(r.x,r.y+4,r.width,r.height),new Color(.02f,.025f,.02f,.12f),24);
            Rounded(r,new Color(.925f,.916f,.878f,.92f),24);
            WellnessUiPrimitives.Round(r,new Color(1,.99f,.95f,.65f),24,1.2f);
        }
        public void Card(Rect r)
        {
            Rounded(r,new Color(1,.99f,.965f,.27f),13);
            WellnessUiPrimitives.Round(r,new Color(1,1,.98f,.27f),13,.8f);
        }
        public void Line(float x,float y,float width)=>Rounded(new Rect(x,y,width,1),new Color(.35f,.38f,.32f,.22f),.5f);
        private bool Hit(Rect r,string name,bool enabled)
        {
            bool prior=GUI.enabled;GUI.enabled=prior&&enabled;GUI.SetNextControlName(name);
            bool clicked=GUI.Button(r,GUIContent.none,empty);GUI.enabled=prior;return clicked;
        }
        public bool Button(Rect r,string text,bool filled,bool enabled=true)
        {
            string name="onboarding-"+text;
            bool clicked=Hit(r,name,enabled);
            if(filled)Rounded(r,enabled?Sage:new Color(.46f,.49f,.42f,.42f),12);
            else WellnessUiPrimitives.Round(r,new Color(.40f,.46f,.35f,enabled?.75f:.30f),12,1.15f);
            if(enabled&&GUI.GetNameOfFocusedControl()==name)WellnessUiPrimitives.Round(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),Sage,15,1);
            GUI.Label(r,text,filled?primary:button);return clicked;
        }
        public bool TextButton(Rect r,string text,bool enabled=true)
        {
            string name="onboarding-"+text;bool clicked=Hit(r,name,enabled);
            GUI.Label(r,text,link);
            if(enabled&&GUI.GetNameOfFocusedControl()==name)WellnessUiPrimitives.Round(r,Sage,5,1);
            return clicked;
        }
        public bool CompanionButton(Rect r,string name)
        {
            bool clicked=Hit(r,"onboarding-choose-companion",true);
            Rounded(r,new Color(.57f,.65f,.47f,.24f),11);
            WellnessUiPrimitives.Round(r,new Color(.38f,.48f,.30f,.8f),11,1.3f);
            Icon("person",new Rect(r.x+12,r.y+7,26,26));
            GUI.Label(new Rect(r.x+48,r.y,r.width-59,r.height),"Companion: "+name+"  ›",companion);
            if(GUI.GetNameOfFocusedControl()=="onboarding-choose-companion")WellnessUiPrimitives.Round(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),Sage,13,1);
            return clicked;
        }
        public bool Permission(Rect r,string icon,string title,string copy,bool value,bool available,string control)
        {
            Card(r);Icon(icon,new Rect(r.x+24,r.y+20,42,42));
            GUI.Label(new Rect(r.x+96,r.y+17,484,28),title,RowTitle);
            GUI.Label(new Rect(r.x+96,r.y+44,491,r.height-44),copy,Copy);
            Rect hit=new Rect(r.xMax-124,r.y+12,113,r.height-24);
            if(Hit(hit,control,available))value=!value;
            Rect toggle=new Rect(r.xMax-115,r.center.y-15,49,30);
            Rounded(toggle,value?Sage:new Color(.50f,.51f,.49f,.67f),15);
            Rounded(new Rect(value?toggle.xMax-27:toggle.x+4,toggle.y+4,22,22),new Color(.99f,.99f,.97f),11);
            GUI.Label(new Rect(toggle.xMax+16,toggle.y,43,30),value?"On":"Off",Status);
            if(available&&GUI.GetNameOfFocusedControl()==control)WellnessUiPrimitives.Round(new Rect(toggle.x-3,toggle.y-3,55,36),Sage,18,1);
            return available&&value;
        }
        public void Signal(float y,string icon,string label,bool ready,string waiting,bool interrupted)
        {
            Icon(icon,new Rect(676,y+2,27,27));GUI.Label(new Rect(720,y,93,31),label,Status);
            Color dot=ready?new Color(.45f,.64f,.43f):interrupted?new Color(.60f,.55f,.48f):new Color(.98f,.79f,.37f);
            Rounded(new Rect(814,y+8,15,15),dot,7.5f);
            WellnessUiPrimitives.Round(new Rect(814,y+8,15,15),new Color(.32f,.39f,.27f,.8f),7.5f,.8f);
            GUI.Label(new Rect(846,y,100,31),ready?"Ready":waiting,Status);
        }
        public void Icon(string name,Rect r)
        {
            if(!icons.TryGetValue(name,out var texture)){texture=MakeIcon(name);icons.Add(name,texture);}
            Color prior=GUI.color;GUI.color*=Ink;GUI.DrawTexture(r,texture);GUI.color=prior;
        }
        private struct Segment
        {
            public Vector2 a,b;
            public Segment(float ax,float ay,float bx,float by){a=new Vector2(ax,ay);b=new Vector2(bx,by);}
            public float Distance(Vector2 p){Vector2 d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.0001f,d.sqrMagnitude)));}
        }
        private static Texture2D MakeIcon(string name)
        {
            var segments=new List<Segment>();
            Action<float,float,float,float> line=(a,b,c,d)=>segments.Add(new Segment(a,b,c,d));
            Action<float[]> path=p=>{for(int i=2;i<p.Length;i+=2)line(p[i-2],p[i-1],p[i],p[i+1]);};
            Action<float,float,float,float,float,float> arc=(cx,cy,rx,ry,start,end)=>{
                float px=cx+rx*Mathf.Cos(start),py=cy+ry*Mathf.Sin(start);
                for(int i=1;i<=32;i++){float a=Mathf.Lerp(start,end,i/32f),x=cx+rx*Mathf.Cos(a),y=cy+ry*Mathf.Sin(a);line(px,py,x,y);px=x;py=y;}
            };
            const float pi=Mathf.PI;
            switch(name)
            {
                case "mic":arc(32,18,9,9,pi,2*pi);line(23,18,23,35);line(41,18,41,35);arc(32,35,9,9,0,pi);
                    line(15,30,15,35);line(49,30,49,35);arc(32,35,17,17,0,pi);line(32,52,32,59);line(23,59,41,59);break;
                case "camera":path(new[]{9f,16,40,16,43,19,43,45,40,48,9,48,6,45,6,19,9,16});path(new[]{43f,25,57,18,57,47,43,40});break;
                case "leaf":arc(32,30,21,26,.06f,pi*1.37f);path(new[]{24f,6,54,4,53,25});line(13,58,44,17);break;
                case "info":arc(32,32,24,24,0,2*pi);line(32,30,32,46);arc(32,20,1.4f,1.4f,0,2*pi);break;
                case "shield":path(new[]{32f,6,51,16,50,37,45,47,32,57,19,47,14,37,13,16,32,6});break;
                case "person":arc(32,19,10,12,0,2*pi);arc(32,48,21,13,pi,2*pi);path(new[]{11f,48,11,54,53,54,53,48});break;
                case "heart":arc(21,22,13,13,pi,2*pi);arc(46,22,13,13,pi,2*pi);path(new[]{8f,22,10,33,32,56,55,33,59,22});break;
                case "lungs":path(new[]{29f,9,29,27,19,35});path(new[]{35f,9,35,27,45,35});
                    path(new[]{24f,21,18,22,12,31,7,43,7,51,11,55,24,51,26,46,26,26,24,21});
                    path(new[]{40f,21,46,22,52,31,57,43,57,51,53,55,40,51,38,46,38,26,40,21});break;
            }
            const int size=96;var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var p=new Vector2((x+.5f)*64/size,(y+.5f)*64/size);float distance=100;
                foreach(var segment in segments)distance=Mathf.Min(distance,segment.Distance(p));
                byte alpha=(byte)Mathf.RoundToInt(255*Mathf.Clamp01((1.45f-distance)*size/64));
                pixels[(size-1-y)*size+x]=new Color32(255,255,255,alpha);
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Quiet Glass onboarding "+name,hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);texture.Apply(false,true);return texture;
        }
        public void Dispose(){foreach(var texture in icons.Values)if(texture!=null)UnityEngine.Object.Destroy(texture);icons.Clear();}
    }
}
