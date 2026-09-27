using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastWatch.UI
{
    // CPU-created, cached nine-slices and small icons. No shaders, cameras, blur passes or readbacks.
    public sealed class WellnessQuietGlassTheme : IDisposable
    {
        public static readonly Color Ink=new Color(.09f,.13f,.105f), Muted=new Color(.43f,.45f,.42f);
        public static readonly Color Sage=new Color(.45f,.51f,.39f), LineColor=new Color(.30f,.34f,.28f,.21f);
        public readonly GUIStyle Brand,Title,Body,Small,Hint,Percent,Button,TextButton,Primary,Nav,Selected;
        public readonly GUIStyle Panel,Inset,Track,Thumb,ScrollLabel,LegacyButton,LegacyToggle,LegacyField;
        private readonly List<Texture2D> textures=new List<Texture2D>();
        private readonly Dictionary<string,Texture2D> icons=new Dictionary<string,Texture2D>();
        private readonly GUIStyle round,outline,shadow;
        private readonly Texture2D disc;

        public WellnessQuietGlassTheme(Font serif,Font sans)
        {
            var rounded=Round(64,26,false);var edge=Round(64,26,true);
            round=Box(rounded,26);outline=Box(edge,26);shadow=Box(rounded,26);
            Panel=Box(Round(96,26,false),26);
            Inset=Box(Round(64,20,false),20);
            Brand=Label(serif,49,Ink);Title=Label(serif,48,Ink);Body=Label(serif,25,Ink);
            Small=Label(sans,20,Muted);Hint=Label(sans,17,Muted);
            Percent=Label(serif,24,Ink);
            TextButton=Clickable(Label(serif,21,Ink),null,null);
            Button=Clickable(Label(serif,22,Ink),null,null);Button.alignment=TextAnchor.MiddleCenter;
            Button.padding=new RectOffset(16,16,2,2);
            Primary=Clickable(Label(serif,25,new Color(.99f,.985f,.95f)),null,null);
            Primary.alignment=TextAnchor.MiddleCenter;
            Nav=Clickable(Label(serif,24,Ink),null,null);Nav.padding=new RectOffset(75,12,0,0);Nav.alignment=TextAnchor.MiddleLeft;
            Selected=new GUIStyle(Nav);
            Track=new GUIStyle { fixedHeight=12, margin=new RectOffset(0,0,0,0),padding=new RectOffset(0,0,0,0) };
            // Invisible built-in slider handles mouse/keyboard input; the visible track is drawn separately.
            Thumb=new GUIStyle {fixedWidth=26,fixedHeight=26};
            Thumb.normal.background=Thumb.hover.background=Thumb.active.background=Thumb.focused.background=null;
            ScrollLabel=Label(sans,19,Ink);ScrollLabel.wordWrap=true;
            var soft=TintTexture(Round(64,15,false),new Color(.78f,.81f,.73f,.57f));
            var hover=TintTexture(Round(64,15,false),new Color(.64f,.71f,.60f,.66f));
            LegacyButton=Clickable(Label(sans,20,Ink),soft,hover);
            LegacyButton.alignment=TextAnchor.MiddleCenter;LegacyButton.wordWrap=true;
            LegacyButton.border=new RectOffset(16,16,16,16);LegacyButton.padding=new RectOffset(12,12,7,7);
            LegacyButton.margin=new RectOffset(3,3,4,4);
            LegacyToggle=new GUIStyle(GUI.skin.toggle){font=sans,fontSize=19,wordWrap=true,padding=new RectOffset(23,2,3,3)};
            SetTextColors(LegacyToggle,Ink);
            LegacyField=new GUIStyle(GUI.skin.textField){font=sans,fontSize=20,padding=new RectOffset(10,10,5,5),border=new RectOffset(16,16,16,16)};
            LegacyField.normal.background=LegacyField.focused.background=soft;SetTextColors(LegacyField,Ink);
            foreach(string name in new[]{"play","pause","previous","next","sound","display","cloud","controls","exit","moon","sun"})
                icons[name]=IconTexture(name);
            disc=DiscTexture();
        }
        private static GUIStyle Label(Font font,int size,Color color)=>new GUIStyle(GUI.skin.label){font=font,fontSize=size,fontStyle=FontStyle.Normal,
            richText=false,wordWrap=false,clipping=TextClipping.Clip,padding=new RectOffset(),margin=new RectOffset(),normal={textColor=color}};
        private static GUIStyle Box(Texture2D image,int border)=>new GUIStyle{normal={background=image},border=new RectOffset(border,border,border,border)};
        private static void SetTextColors(GUIStyle style,Color c)
        {
            style.normal.textColor=style.hover.textColor=style.active.textColor=style.focused.textColor=c;
            style.onNormal.textColor=style.onHover.textColor=style.onActive.textColor=style.onFocused.textColor=c;
        }
        private static GUIStyle Clickable(GUIStyle label,Texture2D normal,Texture2D hover)
        {
            var style=new GUIStyle(label);style.normal.background=normal;
            style.hover.background=style.active.background=style.focused.background=hover;
            SetTextColors(style,label.normal.textColor);return style;
        }
        public void Surface(Rect r)
        {
            BoxTint(new Rect(r.x,r.y+5,r.width,r.height),shadow,new Color(.035f,.055f,.04f,.11f));
            BoxTint(r,Panel,new Color(.925f,.914f,.872f,.92f));
            BoxTint(r,outline,new Color(1,.995f,.94f,.55f));
        }
        public void Rounded(Rect r,Color color)=>BoxTint(r,round,color);
        public void Outline(Rect r,Color color)=>BoxTint(r,outline,color);
        public void Card(Rect r)=>BoxTint(r,Inset,new Color(1,.995f,.95f,.25f));
        private static void BoxTint(Rect r,GUIStyle style,Color tint)
        {
            Color before=GUI.color;GUI.color*=tint;GUI.Box(r,GUIContent.none,style);GUI.color=before;
        }
        public static void Fill(Rect r,Color color)
        {
            Color before=GUI.color;GUI.color*=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=before;
        }
        public void Line(float x,float y,float width)=>Fill(new Rect(x,y,width,1.2f),LineColor);
        public void Icon(string name,Rect r,Color color)
        {
            Color before=GUI.color;GUI.color*=color;GUI.DrawTexture(r,icons[name]);GUI.color=before;
        }
        public bool Action(Rect r,string text,bool filled=false)
        {
            bool hovering=r.Contains(Event.current.mousePosition);
            if(filled)Rounded(r,hovering?new Color(.40f,.48f,.34f):Sage);
            else {Outline(r,new Color(.45f,.53f,.39f,.7f));if(hovering)Rounded(r,new Color(.53f,.61f,.45f,.10f));}
            return GUI.Button(r,text,filled?Primary:Button);
        }
        public bool TextAction(Rect r,string text)
        {
            if(r.Contains(Event.current.mousePosition))Rounded(r,new Color(.5f,.59f,.43f,.11f));
            return GUI.Button(r,text,TextButton);
        }
        public bool IconAction(string name,Rect r,string tooltip)
        {
            if(r.Contains(Event.current.mousePosition))Rounded(r,new Color(.5f,.59f,.43f,.17f));
            bool pressed=GUI.Button(r,new GUIContent("",tooltip),TextButton);
            Icon(name,new Rect(r.x+8,r.y+8,r.width-16,r.height-16),Ink);return pressed;
        }
        public float Slider(Rect r,float value,float min,float max,string control)
        {
            GUI.SetNextControlName(control);
            float next=GUI.HorizontalSlider(new Rect(r.x,r.y+7,r.width,26),value,min,max,Track,Thumb);
            // Unity's horizontal slider reserves half of the thumb at either end.
            float t=Mathf.InverseLerp(min,max,next),center=r.x+13+(r.width-26)*t;
            Rounded(new Rect(r.x,r.y+14,r.width,12),new Color(.49f,.51f,.47f,.28f));
            Rounded(new Rect(r.x,r.y+14,Mathf.Max(12,center-r.x),12),new Color(.48f,.55f,.41f,.92f));
            Rounded(new Rect(center-13,r.y+7,26,26),new Color(.37f,.46f,.33f));
            if(GUI.GetNameOfFocusedControl()==control)Outline(new Rect(center-16,r.y+4,32,32),new Color(.20f,.29f,.17f,.7f));
            return next;
        }
        public bool Toggle(Rect r,bool value,string control)
        {
            GUI.SetNextControlName(control);
            bool next=GUI.Toggle(r,value,GUIContent.none,TextButton);
            Rounded(r,next?new Color(.40f,.49f,.35f):new Color(.50f,.52f,.46f,.5f));
            float diameter=r.height-8;
            Rounded(new Rect(next?r.xMax-diameter-4:r.x+4,r.y+4,diameter,diameter),new Color(1,1,.97f));
            if(GUI.GetNameOfFocusedControl()==control)Outline(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),Sage);
            return next;
        }
        public string Fit(string value,GUIStyle style,float width)
        {
            value=value??"";if(style.CalcSize(new GUIContent(value)).x<=width)return value;
            int count=value.Length;while(count>0&&style.CalcSize(new GUIContent(value.Substring(0,count)+"…")).x>width)count--;
            return value.Substring(0,count)+"…";
        }
        public void Vinyl(Rect r,float angle)
        {
            Matrix4x4 before=GUI.matrix;
            // Compose with the reference-canvas transform: never replace it with a screen-space rotation.
            Vector2 p=r.center;GUI.matrix=before*Matrix4x4.Translate(new Vector3(p.x,p.y,0))*Matrix4x4.Rotate(Quaternion.Euler(0,0,angle))*Matrix4x4.Translate(new Vector3(-p.x,-p.y,0));
            GUI.DrawTexture(r,disc);GUI.matrix=before;
        }
        private Texture2D Round(int size,float radius,bool stroke)
        {
            return Texture(size,(x,y)=>{
                float dx=Mathf.Max(Mathf.Abs(x-size*.5f)-(size*.5f-radius),0),dy=Mathf.Max(Mathf.Abs(y-size*.5f)-(size*.5f-radius),0);
                float distance=Mathf.Sqrt(dx*dx+dy*dy)-radius;
                if(stroke)distance=Mathf.Abs(distance+1)-.8f;
                return new Color(1,1,1,Mathf.Clamp01(.5f-distance));
            });
        }
        private Texture2D TintTexture(Texture2D source,Color color)
        {
            var pixels=source.GetPixels();for(int i=0;i<pixels.Length;i++)pixels[i]*=color;
            source.SetPixels(pixels);source.Apply(false,false);return source;
        }
        private Texture2D Texture(int size,Func<float,float,Color> pixel)
        {
            var tex=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Quiet Glass cached UI",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var data=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++)data[y*size+x]=pixel(x+.5f,size-y-.5f);
            tex.SetPixels(data);tex.Apply(false,false);textures.Add(tex);return tex;
        }
        private static float Segment(Vector2 p,float ax,float ay,float bx,float by)
        {
            Vector2 a=new Vector2(ax,ay),b=new Vector2(bx,by),v=b-a;
            return Vector2.Distance(p,a+v*Mathf.Clamp01(Vector2.Dot(p-a,v)/v.sqrMagnitude));
        }
        private static bool Tri(Vector2 p,Vector2 a,Vector2 b,Vector2 c)
        {
            float s1=(p.x-b.x)*(a.y-b.y)-(a.x-b.x)*(p.y-b.y);
            float s2=(p.x-c.x)*(b.y-c.y)-(b.x-c.x)*(p.y-c.y);
            float s3=(p.x-a.x)*(c.y-a.y)-(c.x-a.x)*(p.y-a.y);
            return !((s1<0||s2<0||s3<0)&&(s1>0||s2>0||s3>0));
        }
        private Texture2D IconTexture(string name)=>Texture(64,(x,y)=>{
            Vector2 p=new Vector2(x,y);float d=100;
            Func<float,float,float,float,float> seg=(ax,ay,bx,by)=>Segment(p,ax,ay,bx,by);
            switch(name)
            {
                case "play":d=Tri(p,new Vector2(18,10),new Vector2(51,32),new Vector2(18,54))?0:100;break;
                case "pause":d=((x>=18&&x<=26)||(x>=38&&x<=46))&&y>=12&&y<=52?0:100;break;
                case "previous":case "next":
                    if(name=="next")p.x=64-p.x;
                    d=Tri(p,new Vector2(45,12),new Vector2(17,32),new Vector2(45,52))||p.x>=13&&p.x<=18&&p.y>=12&&p.y<=52?0:100;break;
                case "sound":
                    d=Tri(p,new Vector2(32,8),new Vector2(10,32),new Vector2(32,56))||x>=8&&x<22&&y>=23&&y<=41?0:100;
                    if(x>=38)d=Mathf.Min(d,Mathf.Abs(Vector2.Distance(p,new Vector2(23,32))-24));
                    if(x>=47)d=Mathf.Min(d,Mathf.Abs(Vector2.Distance(p,new Vector2(23,32))-35));break;
                case "display":
                    d=Mathf.Min(Mathf.Min(seg(9,12,55,12),seg(9,12,9,47)),Mathf.Min(seg(55,12,55,47),seg(9,47,55,47)));
                    d=Mathf.Min(d,Mathf.Min(seg(32,47,32,55),seg(23,56,41,56)));break;
                case "cloud":
                    float cloud=Mathf.Min(Vector2.Distance(p,new Vector2(19,39))-12,Mathf.Min(Vector2.Distance(p,new Vector2(32,27))-15,Vector2.Distance(p,new Vector2(46,39))-12));
                    cloud=Mathf.Min(cloud,Mathf.Max(Mathf.Abs(x-33)-14,Mathf.Abs(y-43)-8));d=Mathf.Abs(cloud);break;
                case "controls":
                    d=Mathf.Min(Mathf.Min(seg(18,16,46,16),seg(18,16,9,48)),Mathf.Min(seg(46,16,55,48),Mathf.Min(seg(9,48,18,49),seg(55,48,46,49))));
                    d=Mathf.Min(d,Mathf.Min(seg(18,49,26,38),Mathf.Min(seg(46,49,38,38),seg(26,38,38,38))));
                    d=Mathf.Min(d,Mathf.Min(seg(17,29,29,29),seg(23,23,23,35)));
                    d=Mathf.Min(d,Mathf.Min(Vector2.Distance(p,new Vector2(43,26)),Vector2.Distance(p,new Vector2(38,32))));break;
                case "exit":
                    d=Mathf.Min(Mathf.Min(seg(21,9,49,9),seg(49,9,49,55)),Mathf.Min(seg(21,55,49,55),seg(21,9,21,24)));
                    d=Mathf.Min(d,Mathf.Min(seg(21,41,21,55),Mathf.Min(seg(10,32,35,32),Mathf.Min(seg(29,26,35,32),seg(29,38,35,32)))));break;
                case "moon":d=Vector2.Distance(p,new Vector2(31,32))<23&&Vector2.Distance(p,new Vector2(43,19))>24?0:100;break;
                case "sun":
                    d=Mathf.Abs(Vector2.Distance(p,new Vector2(32,32))-13);
                    for(int i=0;i<8;i++){float a=i*Mathf.PI/4;d=Mathf.Min(d,seg(32+Mathf.Cos(a)*21,32+Mathf.Sin(a)*21,32+Mathf.Cos(a)*28,32+Mathf.Sin(a)*28));}break;
            }
            return new Color(1,1,1,Mathf.Clamp01(2.6f-d));
        });
        private Texture2D DiscTexture()=>Texture(128,(x,y)=>{
            float dx=x-64,dy=y-64,r=Mathf.Sqrt(dx*dx+dy*dy),a=Mathf.Clamp01(62-r);
            float groove=.035f*Mathf.Sin(r*2.9f),shine=.025f+.045f*Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.Atan2(dy,dx)+.6f)),9);
            Color color=new Color(.07f+groove+shine,.076f+groove+shine,.063f+groove+shine,a);
            if(r<19)color=new Color(.82f,.80f,.67f,a);if(r<2.8f)color=new Color(.1f,.12f,.09f,a);return color;
        });
        public void Dispose(){foreach(var texture in textures)if(texture!=null)UnityEngine.Object.Destroy(texture);textures.Clear();}
    }
}
