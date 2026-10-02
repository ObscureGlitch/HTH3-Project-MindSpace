using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastWatch.UI
{
    /// <summary>One bounded mesh per rare fish. Analytic ribbons, stars and reveal
    /// waves share one draw; no particle objects, lights, textures or extra cameras.</summary>
    public sealed class KoiRarityVfx : IDisposable
    {
        public enum Presentation { Catch, Held, Collection }
        public const int MaxQuads=768;
        const int RingSteps=64;
        readonly Vector3[] vertices=new Vector3[MaxQuads*4];
        readonly Color[] colors=new Color[MaxQuads*4];
        readonly Vector3[] uv=new Vector3[MaxQuads*4];
        readonly int[] indices=new int[MaxQuads*6];
        readonly Mesh mesh;
        readonly Material material;
        readonly Presentation presentation;
        readonly Color primary,secondary;
        readonly float length;
        int quads,lastFrame=-1,lastQuads=-1;
        bool disposed;
        public GameObject Root {get;private set;}
        public int Rank {get;}
        public int VertexCount=>quads*4;
        public int BuildCount {get;private set;}
        public static string Signature(string rarity)
        {
            switch(KoiFishingLoot.Rank(rarity))
            {case 2:return "Azure Wake";case 3:return "Astral Current";case 4:return "Solar Crown";case 5:return "Celestial Sovereign";default:return "";}
        }
        public static KoiRarityVfx Create(Transform parent,KoiCatch fish,float length,Presentation presentation,int layer=0)
        {return fish==null||KoiFishingLoot.Rank(fish.RarityName)<2?null:new KoiRarityVfx(parent,fish.RarityName,length,presentation,layer);}
        KoiRarityVfx(Transform parent,string rarity,float size,Presentation presentation,int layer)
        {
            Rank=KoiFishingLoot.Rank(rarity);length=Mathf.Clamp(size,.1f,2);this.presentation=presentation;
            primary=Rank==2?new Color(.12f,.65f,1.5f):Rank==3?new Color(.72f,.22f,1.4f):Rank==4?new Color(1.6f,.79f,.15f):new Color(1.3f,.20f,.80f);
            secondary=Rank==2?new Color(.40f,1.3f,1.4f):Rank==3?new Color(.24f,1.0f,1.45f):Rank==4?new Color(1.5f,.40f,.055f):new Color(.15f,1.2f,1.45f);
            Shader shader=Resources.Load<Shader>("KoiRarityAura");
            if(shader==null)throw new InvalidOperationException("KoiRarityAura shader resource is missing.");
            Root=new GameObject(Signature(rarity)+" (koi VFX)"){hideFlags=HideFlags.DontSave,layer=layer};Root.transform.SetParent(parent,false);
            mesh=new Mesh{name="Koi rarity aura (runtime)",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();
            material=new Material(shader){name="Koi rarity glow (runtime)",hideFlags=HideFlags.DontSave};
            Root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=Root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
            for(int i=0;i<MaxQuads;i++){int v=i*4,t=i*6;indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;indices[t+3]=v;indices[t+4]=v+2;indices[t+5]=v+3;}
        }
        public void SetVisible(bool show){if(Root!=null)Root.SetActive(show);}
        public void Pose(Vector3 position,Quaternion viewerRotation,float seconds)
        {
            if(disposed||Root==null||!Root.activeInHierarchy)return;
            Root.transform.SetPositionAndRotation(position,viewerRotation);Root.transform.localScale=Vector3.one*length;
            seconds=float.IsFinite(seconds)?Mathf.Max(0,seconds):0;
            int frame=Mathf.FloorToInt(seconds*30);if(frame==lastFrame)return;lastFrame=frame;
            float t=frame/30f;
            float strength=presentation==Presentation.Held?.55f:1;
            float entrance=presentation==Presentation.Catch?Mathf.SmoothStep(0,1,(t-.22f)/.55f):1;
            strength*=entrance;quads=0;
            // A soft halo sits behind the model; the original koi remains readable.
            Sprite(new Vector3(0,0,.30f),new Vector2(.65f,.39f),Tint(primary,.17f*strength),2,0);
            int rings=Rank==2?1:Rank==3?2:3;
            for(int ring=0;ring<rings;ring++)
            {
                float radius=.43f+ring*.078f;
                float tilt=Rank==3?(ring==0?.38f:-.38f):Rank==5?(ring-1)*.42f:0;
                Orbit(radius,tilt,t*(ring%2==0?.36f:-.25f),ring==0?.018f:.012f,
                    Tint(ring%2==0?primary:secondary,(ring==0?.68f:.42f)*strength),Rank==2?.70f:Rank==3?.84f:1);
            }
            if(Rank>=3)
            {
                // Two counter-wound flowing ribbons, violet/cyan or rose/aurora.
                int steps=36;
                for(int ribbon=0;ribbon<2;ribbon++)for(int i=0;i<steps;i++)
                {
                    float a=i/(float)steps,b=(i+1)/(float)steps;
                    Vector3 A=Ribbon(a,t,ribbon),B=Ribbon(b,t,ribbon);
                    float fade=Mathf.Sin((a+b)*.5f*Mathf.PI);
                    Beam(A,B,.036f*fade,Tint(ribbon==0?primary:secondary,.65f*fade*strength));
                }
            }
            int stars=Rank==2?12:Rank==3?22:Rank==4?32:48;
            for(int i=0;i<stars;i++)
            {
                float seed=Hash(i+11),phase=t*(.10f+Hash(i+45)*.13f)+seed*Mathf.PI*2;
                float radius=.29f+Hash(i+93)*.34f,twinkle=.48f+.52f*Mathf.Pow(.5f+.5f*Mathf.Sin(t*(1.3f+seed)+i*4.1f),2);
                Vector3 p=new Vector3(Mathf.Cos(phase)*radius,Mathf.Sin(phase)*radius*.72f,.1f+Mathf.Sin(phase+i)*.20f);
                Color color=Rank==5?Color.Lerp(primary,secondary,.5f+.5f*Mathf.Sin(phase*2+i)):i%3==0?secondary:primary;
                float size=(i%7==0?.048f:.022f)*twinkle;
                Sprite(p,Vector2.one*size,Tint(color,twinkle*.85f*strength),i%4==0?3:1,t*.15f+seed);
            }
            if(Rank>=4)Crown(t,strength);
            if(Rank==5)Wings(t,strength);
            if(presentation==Presentation.Catch)Reveal(t,strength);
            mesh.SetVertices(vertices,0,quads*4);mesh.SetColors(colors,0,quads*4);mesh.SetUVs(0,uv,0,quads*4);
            if(lastQuads!=quads){mesh.SetTriangles(indices,0,quads*6,0,false);lastQuads=quads;}
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(2.3f,1.75f,1.4f));BuildCount++;
        }
        static float Hash(int value)=>Mathf.Repeat(Mathf.Sin(value*127.1f+17.7f)*43758.5453f,1);
        static Color Tint(Color color,float alpha){color.a=alpha;return color;}
        Vector3 Ribbon(float progress,float t,int ribbon)
        {
            float angle=progress*Mathf.PI*2+t*(ribbon==0?.6f:-.5f)+ribbon*Mathf.PI;
            return new Vector3((progress-.5f)*1.30f,Mathf.Sin(angle)*.20f,Mathf.Cos(angle)*.23f+.07f);
        }
        void Orbit(float radius,float tilt,float rotation,float width,Color color,float arc)
        {
            Vector3 Point(float angle)
            {
                float x=Mathf.Cos(angle)*radius,y=Mathf.Sin(angle)*radius*.71f;
                return new Vector3(x,Mathf.Cos(tilt)*y,Mathf.Sin(tilt)*y+.13f);
            }
            for(int i=0;i<RingSteps;i++)
            {
                float a=rotation+i/(float)RingSteps*Mathf.PI*2*arc,b=rotation+(i+1)/(float)RingSteps*Mathf.PI*2*arc;
                float shimmer=.7f+.3f*Mathf.Sin(a*3-rotation*3);Color c=color;c.a*=shimmer;
                Beam(Point(a),Point(b),width,c);
            }
        }
        void Crown(float t,float strength)
        {
            int jewels=Rank==5?7:5;
            for(int i=0;i<jewels;i++)
            {
                float offset=(i-(jewels-1)*.5f)*.098f;
                Vector3 p=new Vector3(offset,.43f+.10f*(1-Mathf.Abs(offset)/.35f)+Mathf.Sin(t*1.7f+i*.5f)*.012f,.15f);
                Color color=Rank==5?Color.Lerp(primary,secondary,i/(float)(jewels-1)):primary;
                Sprite(p,new Vector2(.023f,.042f),Tint(color,.85f*strength),3,0);
                Sprite(p,Vector2.one*.060f,Tint(color,.14f*strength),2,0);
                if(i>0)Beam(p,p-new Vector3(.098f,.015f,0),.010f,Tint(color,.5f*strength));
            }
            // Fine rotating ticks around a golden/celestial sigil.
            for(int i=0;i<24;i++)
            {
                float angle=i*Mathf.PI/12+t*.11f;
                Vector3 p=new Vector3(Mathf.Cos(angle)*.61f,Mathf.Sin(angle)*.433f,.14f);
                Vector3 q=new Vector3(Mathf.Cos(angle)*.645f,Mathf.Sin(angle)*.458f,.14f);
                Beam(p,q,.009f,Tint(primary,.58f*strength));
            }
        }
        void Wings(float t,float strength)
        {
            for(int side=-1;side<=1;side+=2)for(int feather=0;feather<5;feather++)
            {
                float offset=feather*.043f;
                for(int segment=0;segment<10;segment++)
                {
                    Vector3 Point(float u)=>new Vector3(side*(.38f+u*(.49f-feather*.045f)),.06f+Mathf.Sin(u*Mathf.PI*.78f)*(.30f-offset)+Mathf.Sin(t*1.1f+u*2)*.025f,.20f);
                    float u=segment/10f,v=(segment+1)/10f;
                    Beam(Point(u),Point(v),.020f*(1-u*.7f),Tint(Color.Lerp(primary,secondary,feather/4f),(.65f-feather*.07f)*(1-u*.75f)*strength));
                }
            }
            Sprite(new Vector3(0,.59f,.12f),new Vector2(.073f,.068f),Tint(new Color(1.45f,1.35f,1.8f),.8f*strength),1,t*.12f);
        }
        void Reveal(float t,float strength)
        {
            // A lift-off wave and a second arrival burst. Both decay into the
            // persistent signature; neither shakes or flashes the whole screen.
            float age=t-KoiFishingCatchMotion.Duration;
            float liftAge=t-.48f;
            float life=Mathf.Clamp01(age/2.1f),lift=Mathf.Clamp01(liftAge/1.1f);
            Orbit(.22f+lift*.62f,0,0,.030f,Tint(primary,(liftAge>=0?1-lift:0)*.75f*strength),1);
            Orbit(.22f+life*.76f,0,-life,.025f,Tint(secondary,(age>=0?Mathf.Pow(1-life,2):0)*strength),1);
            float flare=Mathf.Clamp01(age/.95f),flareAlpha=Mathf.Sin(flare*Mathf.PI)*(1-flare)*strength;
            int rays=Rank==2?8:Rank==3?12:Rank==4?18:24;
            for(int ray=0;ray<rays;ray++)
            {
                float angle=ray*Mathf.PI*2/rays+Hash(ray+1021)*.12f;
                Vector3 direction=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle)*.68f,0);
                float start=.27f+flare*.38f,end=.40f+flare*.65f;
                Beam(direction*start+Vector3.forward*.16f,direction*end+Vector3.forward*.16f,
                    .040f*(1-flare),Tint(ray%2==0?primary:secondary,flareAlpha*.8f));
            }
            Sprite(new Vector3(0,0,.22f),new Vector2(.7f,.45f),Tint(primary,flareAlpha*.3f),2,0);
            int count=Rank==2?16:Rank==3?24:Rank==4?36:52;
            for(int i=0;i<count;i++)
            {
                float delay=Hash(i+401)*.22f,p=Mathf.Clamp01((age-delay)/1.7f),angle=i*Mathf.PI*2/count+.12f*Mathf.Sin(i*4);
                float radius=.15f+Mathf.Sin(p*Mathf.PI*.5f)*(.40f+Hash(i+217)*.46f);
                Vector3 at=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius*.68f-p*p*.08f,-.02f);
                float alpha=age>=delay?Mathf.Pow(1-p,1.5f)*strength:0;
                Color color=Rank==5?Color.Lerp(primary,secondary,Hash(i+108)):i%2==0?primary:secondary;
                Vector3 trail=at-new Vector3(Mathf.Cos(angle),Mathf.Sin(angle)*.68f,0)*(.045f*(1-p));
                Beam(trail,at,.012f,Tint(color,alpha*.65f));Sprite(at,Vector2.one*(.025f+(i%5==0?.022f:0)),Tint(color,alpha),1,angle);
            }
        }
        void Beam(Vector3 a,Vector3 b,float width,Color color)
        {
            Vector3 delta=b-a;Vector3 side=new Vector3(-delta.y,delta.x,0).normalized*width*.5f;
            Quad(a-side,b-side,b+side,a+side,color,0);
        }
        void Sprite(Vector3 p,Vector2 size,Color color,int shape,float angle)
        {
            Vector3 right=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*size.x,up=new Vector3(-Mathf.Sin(angle),Mathf.Cos(angle),0)*size.y;
            Quad(p-right-up,p+right-up,p+right+up,p-right+up,color,shape);
        }
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,int shape)
        {
            if(quads>=MaxQuads)throw new InvalidOperationException("Koi VFX geometry budget exceeded.");
            int v=quads++*4;vertices[v]=a;vertices[v+1]=b;vertices[v+2]=c;vertices[v+3]=d;
            colors[v]=colors[v+1]=colors[v+2]=colors[v+3]=color;
            uv[v]=new Vector3(0,0,shape);uv[v+1]=new Vector3(1,0,shape);uv[v+2]=new Vector3(1,1,shape);uv[v+3]=new Vector3(0,1,shape);
        }
        static void Destroy(UnityEngine.Object value)
        {if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        public void Dispose()
        {if(disposed)return;disposed=true;if(Root!=null)Root.SetActive(false);Destroy(Root);Destroy(mesh);Destroy(material);Root=null;}
    }
}
