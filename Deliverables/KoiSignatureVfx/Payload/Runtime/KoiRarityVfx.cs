using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace TheLastWatch.UI
{
    /// <summary>Species-authored Legendary/Godly effects. One bounded draw, 30 Hz,
    /// no lights, particles, textures, per-frame allocations or extra cameras.</summary>
    public sealed partial class KoiRarityVfx : IDisposable
    {
        public enum Presentation { Catch, Held, Collection }
        public const int MaxQuads=1024;
        readonly Vector3[] vertices=new Vector3[MaxQuads*4];
        readonly Color[] colors=new Color[MaxQuads*4];
        readonly Vector3[] uv=new Vector3[MaxQuads*4];
        readonly int[] indices=new int[MaxQuads*6];
        readonly Mesh mesh;readonly Material material;readonly Presentation presentation;
        readonly Color primary,secondary,accent;readonly float length;readonly string variety;
        int quads,lastFrame=-1,lastQuads=-1;bool disposed;
        public GameObject Root {get;private set;}
        public int Rank {get;}
        public int VertexCount=>quads*4;
        public int BuildCount {get;private set;}
        public string Theme=>ThemeFor(variety);
        public Color Primary=>primary;
        public Color Secondary=>secondary;
        public static bool Eligible(KoiCatch fish)=>fish!=null&&KoiFishingLoot.Rank(fish.RarityName)>=4;
        public static string Signature(string rarity)=>KoiFishingLoot.Rank(rarity)==5?"Divine signature":KoiFishingLoot.Rank(rarity)==4?"Mythic signature":"";
        public static string Signature(KoiCatch fish)=>Eligible(fish)?ThemeFor(fish.variety):"";
        public static string ThemeFor(string name)
        {
            switch(name)
            {
                case "tancho":return "Crimson crane";
                case "ryujin":return "Dragonfire helix";
                case "raijin":return "Thunderstorm coils";
                case "hoo":return "Phoenix plumage";
                case "yuki":return "Crystal frostfall";
                case "abyss":return "Bioluminescent depths";
                case "nebula":return "Spiral nebula";
                case "kitsune":return "Nine spirit flames";
                case "amaterasu":return "Solar ascension";
                case "tsukuyomi":return "Lunar procession";
                case "void":return "Event horizon";
                case "genesis":return "Prismatic creation";
                default:return "Gilded sanctuary";
            }
        }
        public static KoiRarityVfx Create(Transform parent,KoiCatch fish,float length,Presentation presentation,int layer=0)
        {return !Eligible(fish)?null:new KoiRarityVfx(parent,fish.variety,fish.RarityName,length,presentation,layer);}
        KoiRarityVfx(Transform parent,string variety,string rarity,float size,Presentation presentation,int layer)
        {
            Rank=KoiFishingLoot.Rank(rarity);length=Mathf.Clamp(size,.1f,2);this.presentation=presentation;this.variety=variety;
            Palette(variety,out primary,out secondary,out accent);
            Shader shader=Resources.Load<Shader>("KoiSignatureAura");if(shader==null)throw new InvalidOperationException("KoiSignatureAura resource is missing.");
            Root=new GameObject(Theme+" (koi VFX)"){hideFlags=HideFlags.DontSave,layer=layer};Root.transform.SetParent(parent,false);
            mesh=new Mesh{name=Theme+" (runtime)",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();
            material=new Material(shader){name="Koi signature glow (runtime)",hideFlags=HideFlags.DontSave};
            // Luminous signatures accumulate color; Void alone keeps a dark lens.
            material.SetFloat("_DstBlend",(float)(variety=="void"?BlendMode.OneMinusSrcAlpha:BlendMode.One));
            Root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=Root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
            for(int i=0;i<MaxQuads;i++){int v=i*4,t=i*6;indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;indices[t+3]=v;indices[t+4]=v+2;indices[t+5]=v+3;}
        }
        static void Palette(string name,out Color a,out Color b,out Color c)
        {
            a=new Color(1.3f,.78f,.12f);b=new Color(1.05f,.38f,.035f);c=new Color(1.15f,1.03f,.72f);
            switch(name)
            {
                case "tancho":a=new Color(1.32f,.07f,.16f);b=new Color(1.06f,1.02f,.92f);c=new Color(.34f,.23f,.27f);break;
                case "ryujin":a=new Color(1.5f,.15f,.03f);b=new Color(1.4f,.83f,.06f);c=new Color(1.18f,.38f,.04f);break;
                case "raijin":a=new Color(.10f,.88f,1.4f);b=new Color(.55f,.27f,1.25f);c=new Color(.76f,1.12f,1.22f);break;
                case "hoo":a=new Color(1.4f,.40f,.025f);b=new Color(1.45f,.94f,.08f);c=new Color(1.2f,.10f,.025f);break;
                case "yuki":a=new Color(.72f,1.1f,1.3f);b=new Color(1.14f,1.19f,1.22f);c=new Color(.35f,.71f,1.16f);break;
                case "abyss":a=new Color(.04f,1.14f,.82f);b=new Color(.10f,.63f,.9f);c=new Color(.08f,.24f,.26f);break;
                case "nebula":a=new Color(.72f,.21f,1.3f);b=new Color(1.2f,.24f,.75f);c=new Color(.38f,.58f,1.3f);break;
                case "kitsune":a=new Color(.08f,.57f,1.4f);b=new Color(.72f,1.09f,1.28f);c=new Color(.21f,.85f,1.35f);break;
                case "amaterasu":a=new Color(1.6f,.91f,.12f);b=new Color(1.5f,.28f,.02f);c=new Color(1.35f,1.14f,.54f);break;
                case "tsukuyomi":a=new Color(.64f,.71f,1.4f);b=new Color(1.02f,1.11f,1.35f);c=new Color(.27f,.59f,1.14f);break;
                case "void":a=new Color(.62f,.09f,1.4f);b=new Color(1.07f,.27f,.93f);c=new Color(.17f,.09f,.34f);break;
                case "genesis":a=new Color(1.4f,.34f,.69f);b=new Color(.18f,1.25f,.76f);c=new Color(.38f,.65f,1.45f);break;
            }
        }
        public void SetVisible(bool show){if(Root!=null)Root.SetActive(show);}
        public void Pose(Vector3 position,Quaternion viewerRotation,float seconds)
        {
            if(disposed||Root==null||!Root.activeInHierarchy)return;
            Root.transform.SetPositionAndRotation(position,viewerRotation);
            Root.transform.localScale=Vector3.one*length*(presentation==Presentation.Held?.65f:1);
            seconds=float.IsFinite(seconds)?Mathf.Max(0,seconds):0;
            int frame=Mathf.FloorToInt(Mathf.Min(seconds,1000000)*30);if(frame==lastFrame)return;lastFrame=frame;
            float t=frame/30f,strength=presentation==Presentation.Held?.58f:1;
            if(presentation==Presentation.Catch)strength*=Mathf.SmoothStep(0,1,(t-.20f)/.55f);
            quads=0;
            Sprite(new Vector3(0,0,.36f),new Vector2(.57f,.32f),Tint(primary,.075f*strength),2,0);
            Species(t,strength);
            // Sparse sparks live around the silhouette, never a white veil over it.
            Stars(t,strength,Rank==5?36:18,Rank==5?.96f:.72f);
            if(presentation==Presentation.Catch)Reveal(t,strength);
            mesh.SetVertices(vertices,0,quads*4);mesh.SetColors(colors,0,quads*4);mesh.SetUVs(0,uv,0,quads*4);
            if(lastQuads!=quads){mesh.SetTriangles(indices,0,quads*6,0,false);lastQuads=quads;}
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(2.8f,2.0f,1.4f));BuildCount++;
        }
        static float Hash(int value)=>Mathf.Repeat(Mathf.Sin(value*127.1f+17.7f)*43758.5453f,1);
        static Color Tint(Color color,float alpha){color.a=Mathf.Clamp01(alpha);return color;}
        Color Spectrum(float phase)=>Color.HSVToRGB(Mathf.Repeat(phase,1),.74f,1.28f);
        void Stars(float t,float strength,int count,float radius)
        {
            for(int i=0;i<count;i++)
            {
                float phase=t*(.065f+Hash(i+63)*.045f)+Hash(i+77)*Mathf.PI*2,r=radius*(.65f+Hash(i+24)*.35f);
                Vector3 p=new Vector3(Mathf.Cos(phase)*r,Mathf.Sin(phase)*r*.72f,.16f);
                float pulse=.35f+.65f*Mathf.Pow(.5f+.5f*Mathf.Sin(t*1.3f+i*3.7f),3);
                Color color=variety=="genesis"?Spectrum(i/(float)count+t*.018f):i%3==0?secondary:primary;
                Sprite(p,Vector2.one*(i%5==0?.035f:.016f),Tint(color,pulse*.68f*strength),1,t*.08f);
            }
        }
        void Orbit(Vector3 center,float rx,float ry,float tilt,float rotation,float arc,float width,Color color,int steps=64)
        {
            Vector3 Point(float angle){float x=Mathf.Cos(angle)*rx,y=Mathf.Sin(angle)*ry;return center+new Vector3(x,Mathf.Cos(tilt)*y,Mathf.Sin(tilt)*y);}
            for(int i=0;i<steps;i++){float a=rotation+i/(float)steps*Mathf.PI*2*arc,b=rotation+(i+1)/(float)steps*Mathf.PI*2*arc;Beam(Point(a),Point(b),width,color);}
        }
        void Stream(float t,int seed,Vector3 origin,float range,Color color,bool rise,int count,float strength,int shape=2)
        {
            for(int i=0;i<count;i++)
            {
                float life=Mathf.Repeat(t*(.12f+Hash(i+seed)*.06f)+Hash(i+seed+12),1);
                float x=(Hash(i+seed+38)-.5f)*range,drift=Mathf.Sin(life*5+i)*.06f;
                Vector3 p=origin+new Vector3(x+drift,(rise?life:1-life)*.84f-.42f,.15f);
                float size=.014f+Hash(i+seed+31)*.021f,fade=Mathf.Sin(life*Mathf.PI);
                Sprite(p,new Vector2(size,size*(rise?1.5f:.75f)),Tint(color,fade*.6f*strength),shape,t*.22f+i);
            }
        }
        void Trail(Vector3 center,float rx,float ry,float phase,float length,float width,Color color,int steps=12)
        {
            for(int i=0;i<steps;i++)
            {
                float u=i/(float)steps,v=(i+1)/(float)steps;
                Vector3 A=center+new Vector3(Mathf.Cos(phase-u*length)*rx,Mathf.Sin(phase-u*length)*ry,.15f);
                Vector3 B=center+new Vector3(Mathf.Cos(phase-v*length)*rx,Mathf.Sin(phase-v*length)*ry,.15f);
                Beam(A,B,width*(1-u*.88f),Tint(color,color.a*Mathf.Pow(1-u,1.5f)));
            }
            Vector3 head=center+new Vector3(Mathf.Cos(phase)*rx,Mathf.Sin(phase)*ry,.15f);Sprite(head,Vector2.one*width*1.4f,color,1,phase);
        }
        void Reveal(float t,float strength)
        {
            float liftAge=t-.4f,arrival=t-KoiFishingCatchMotion.Duration;
            for(int wave=0;wave<(Rank==5?3:2);wave++)
            {
                float age=arrival-wave*.17f,life=Mathf.Clamp01(age/1.25f),alpha=age>=0?Mathf.Pow(1-life,2)*strength:0;
                Orbit(new Vector3(0,0,.22f),.27f+life*.77f,(.27f+life*.77f)*.65f,wave*.2f,wave*.5f,.98f,.019f,Tint(wave%2==0?primary:secondary,alpha*.60f),48);
            }
            float lift=Mathf.Clamp01(liftAge/.9f);Orbit(new Vector3(0,0,.22f),.2f+lift*.55f,(.2f+lift*.55f)*.65f,0,0,1,.017f,Tint(primary,(liftAge>=0?1-lift:0)*strength*.42f),48);
            int rays=Rank==5?28:14;
            for(int i=0;i<rays;i++)
            {
                float delay=Hash(i+700)*.16f,p=Mathf.Clamp01((arrival-delay)/1.15f),angle=i*Mathf.PI*2/rays;
                Vector3 dir=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle)*.67f,0);
                Color color=variety=="genesis"?Spectrum(i/(float)rays):i%2==0?primary:secondary;
                float fade=arrival>=delay?Mathf.Sin(p*Mathf.PI)*Mathf.Pow(1-p,1.2f)*strength:0;
                float r=.26f+p*.75f;Beam(dir*(r-.10f*(1-p))+Vector3.forward*.15f,dir*r+Vector3.forward*.15f,.019f,Tint(color,fade));
                Sprite(dir*r+Vector3.forward*.15f,Vector2.one*.034f,Tint(color,fade),3,angle);
            }
        }
        void Beam(Vector3 a,Vector3 b,float width,Color color)
        {Vector3 delta=b-a,side=new Vector3(-delta.y,delta.x,0).normalized*width*.5f;Quad(a-side,b-side,b+side,a+side,color,0);}
        void Sprite(Vector3 p,Vector2 size,Color color,int shape,float angle)
        {Vector3 right=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*size.x,up=new Vector3(-Mathf.Sin(angle),Mathf.Cos(angle),0)*size.y;Quad(p-right-up,p+right-up,p+right+up,p-right+up,color,shape);}
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,int shape)
        {
            if(quads>=MaxQuads)throw new InvalidOperationException("Koi signature geometry budget exceeded.");
            int v=quads++*4;vertices[v]=a;vertices[v+1]=b;vertices[v+2]=c;vertices[v+3]=d;colors[v]=colors[v+1]=colors[v+2]=colors[v+3]=color;
            uv[v]=new Vector3(0,0,shape);uv[v+1]=new Vector3(1,0,shape);uv[v+2]=new Vector3(1,1,shape);uv[v+3]=new Vector3(0,1,shape);
        }
        static void Destroy(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        public void Dispose(){if(disposed)return;disposed=true;if(Root!=null)Root.SetActive(false);Destroy(Root);Destroy(mesh);Destroy(material);Root=null;}
    }
}
