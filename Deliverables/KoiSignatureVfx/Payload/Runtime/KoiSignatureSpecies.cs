using UnityEngine;
namespace TheLastWatch.UI
{
    public sealed partial class KoiRarityVfx
    {
        void Species(float t,float strength)
        {
            switch(variety)
            {
                case "tancho":Crane(t,strength);break;
                case "ryujin":Dragon(t,strength);break;
                case "raijin":Thunder(t,strength);break;
                case "hoo":Phoenix(t,strength);break;
                case "yuki":Frost(t,strength);break;
                case "abyss":Depths(t,strength);break;
                case "nebula":Nebula(t,strength);break;
                case "kitsune":Spirits(t,strength);break;
                case "amaterasu":Sun(t,strength);break;
                case "tsukuyomi":Moon(t,strength);break;
                case "void":Singularity(t,strength);break;
                case "genesis":Creation(t,strength);break;
                default:Gilded(t,strength);break;
            }
            // Preserve historical Godly catches without reclassifying their save.
            if(Rank==5&&KoiFishingLoot.ForVariety(variety).Rank<5)
            {FeatherFans(t,strength,6,.86f);Orbit(new Vector3(0,0,.2f),.96f,.64f,.22f,-t*.09f,1,.012f,Tint(secondary,.32f*strength));}
        }
        void FeatherFans(float t,float strength,int feathers,float extent,bool rainbow=false)
        {
            for(int side=-1;side<=1;side+=2)for(int f=0;f<feathers;f++)
            {
                float fraction=f/(float)Mathf.Max(1,feathers-1);Color color=rainbow?Spectrum(fraction*.75f+t*.018f):Color.Lerp(primary,secondary,fraction);
                for(int s=0;s<12;s++)
                {
                    Vector3 Point(float u)=>new Vector3(side*(.30f+u*(extent-f*.033f)),.12f+Mathf.Sin(u*Mathf.PI*.78f)*(.46f-f*.054f)+Mathf.Sin(t*.8f+u*3)*.025f,.27f);
                    float u=s/12f,v=(s+1)/12f;Beam(Point(u),Point(v),.028f*(1-u*.78f),Tint(color,(.54f-f*.035f)*(1-u*.55f)*strength));
                    if(s==11)Sprite(Point(v),new Vector2(.017f,.030f),Tint(color,.52f*strength),3,side*.4f);
                }
            }
        }
        void Crane(float t,float strength)
        {
            for(int side=-1;side<=1;side+=2)for(int plume=0;plume<4;plume++)for(int i=0;i<16;i++)
            {
                Vector3 Point(float u)=>new Vector3(side*(.2f+u*.67f),.19f+Mathf.Sin(u*Mathf.PI*.86f)*(.25f-plume*.045f),.22f);
                float u=i/16f,v=(i+1)/16f;Beam(Point(u),Point(v),.015f,Tint(Color.Lerp(secondary,primary,Mathf.Pow(u,4)),.48f*(1-u*.35f)*strength));
            }
            // The red crest and white plumage echo the Tancho's red head marking.
            Sprite(new Vector3(0,.50f,.2f),Vector2.one*.062f,Tint(primary,.66f*strength),2,0);
            Stream(t,220,Vector3.zero,1.1f,primary,false,15,strength,6);
        }
        void Dragon(float t,float strength)
        {
            for(int ribbon=0;ribbon<2;ribbon++)for(int i=0;i<44;i++)
            {
                Vector3 Point(float u){float phase=u*Mathf.PI*3+t*.75f+ribbon*Mathf.PI;return new Vector3((u-.5f)*1.65f,Mathf.Sin(phase)*.27f,Mathf.Cos(phase)*.18f+.20f);}
                float u=i/44f,v=(i+1)/44f,fade=Mathf.Sin(u*Mathf.PI);Beam(Point(u),Point(v),.036f*fade,Tint(ribbon==0?primary:secondary,.56f*fade*strength));
            }
            Trail(new Vector3(0,0,.04f),.77f,.34f,t*.36f,.9f,.031f,Tint(secondary,.75f*strength),20);
            Stream(t,230,Vector3.zero,1.25f,primary,true,24,strength,6);
        }
        void Thunder(float t,float strength)
        {
            for(int arc=0;arc<3;arc++)for(int i=0;i<23;i++)
            {
                Vector3 Point(int node)
                {
                    float phase=node/23f*Mathf.PI*1.5f+t*(arc%2==0?.22f:-.17f)+arc*2.1f;
                    float clock=t*2,step=Mathf.Floor(clock),blend=Mathf.SmoothStep(0,1,clock-step);
                    float jitter=(Mathf.Lerp(Hash(node+arc*71+(int)step*13),Hash(node+arc*71+((int)step+1)*13),blend)-.5f)*.07f;
                    float r=.57f+arc*.067f+jitter;return new Vector3(Mathf.Cos(phase)*r,Mathf.Sin(phase)*r*.69f,.17f);
                }
                Beam(Point(i),Point(i+1),i%4==0?.020f:.011f,Tint(arc%2==0?primary:secondary,.63f*strength));
            }
            for(int node=0;node<4;node++){float a=node*Mathf.PI*.5f+t*.15f;Sprite(new Vector3(Mathf.Cos(a)*.71f,Mathf.Sin(a)*.49f,.16f),Vector2.one*.048f,Tint(accent,.7f*strength),1,a);}
        }
        void Phoenix(float t,float strength)
        {FeatherFans(t,strength,5,.64f);Trail(new Vector3(0,-.03f,0),.76f,.39f,-t*.23f,1.35f,.038f,Tint(secondary,.55f*strength),22);Stream(t,240,Vector3.zero,1.1f,primary,true,26,strength,6);}
        void Snowflake(Vector3 p,float radius,float angle,Color color)
        {
            for(int branch=0;branch<6;branch++)
            {
                float a=angle+branch*Mathf.PI/3;Vector3 direction=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0),side=new Vector3(-direction.y,direction.x,0);
                Beam(p,p+direction*radius,.008f,color);
                Beam(p+direction*radius*.58f,p+direction*radius*.78f+side*radius*.21f,.005f,color);
                Beam(p+direction*radius*.58f,p+direction*radius*.78f-side*radius*.21f,.005f,color);
            }
        }
        void Frost(float t,float strength)
        {
            Orbit(new Vector3(0,0,.22f),.69f,.43f,.1f,t*.09f,.93f,.010f,Tint(primary,.38f*strength),48);
            for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5+t*.11f;Vector3 p=new Vector3(Mathf.Cos(a)*.64f,Mathf.Sin(a)*.47f,.16f);Snowflake(p,.071f,t*.13f+i,Tint(i%2==0?primary:secondary,.69f*strength));}
            Stream(t,260,Vector3.zero,1.4f,secondary,false,30,strength,3);
        }
        void Depths(float t,float strength)
        {
            for(int tendril=0;tendril<7;tendril++)for(int i=0;i<24;i++)
            {
                Vector3 Point(float u){float side=tendril%2==0?-1:1;return new Vector3(side*(.28f+u*.46f),-.05f+Mathf.Sin(u*4.1f+t*.7f+tendril)*(.17f+u*.12f)+((tendril-3)*.055f),.26f);}
                float u=i/24f,v=(i+1)/24f;Beam(Point(u),Point(v),.023f*(1-u*.8f),Tint(tendril%2==0?primary:secondary,.52f*(1-u*.6f)*strength));
            }
            Stream(t,280,Vector3.zero,1.2f,primary,true,26,strength,2);
        }
        void Nebula(float t,float strength)
        {
            for(int arm=0;arm<3;arm++)for(int i=0;i<40;i++)
            {
                Vector3 Point(float u){float a=u*4.7f+arm*Mathf.PI*2/3+t*.10f,r=.29f+u*.49f;return new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r*.66f,.25f);}
                float u=i/40f,v=(i+1)/40f;Beam(Point(u),Point(v),.055f*(1-u*.75f),Tint(Color.Lerp(primary,secondary,u),.40f*(1-u*.6f)*strength));
            }
            for(int i=0;i<26;i++){float a=Hash(i+33)*Mathf.PI*2+t*.08f,r=.38f+Hash(i+99)*.36f;Sprite(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r*.66f,.20f),Vector2.one*.012f,Tint(i%2==0?accent:secondary,.60f*strength),1,0);}
            Trail(new Vector3(0,0,.08f),.72f,.49f,-t*.21f,1.15f,.030f,Tint(secondary,.63f*strength),20);
        }
        void Spirits(float t,float strength)
        {
            for(int flame=0;flame<9;flame++)
            {
                float phase=flame*Mathf.PI*2/9+t*.14f;Vector3 p=new Vector3(Mathf.Cos(phase)*.66f,Mathf.Sin(phase)*.41f,.22f);
                float pulse=.75f+.25f*Mathf.Sin(t*1.4f+flame);Sprite(p,new Vector2(.038f,.07f)*pulse,Tint(primary,.62f*strength),6,-phase*.25f);
                Sprite(p,Vector2.one*.068f,Tint(secondary,.13f*strength),2,0);
                for(int i=0;i<12;i++)
                {
                    Vector3 Point(float u)=>p+new Vector3(-Mathf.Cos(phase)*u*.24f+Mathf.Sin(u*6+t*.9f+flame)*.032f,-u*.14f,.01f);
                    float u=i/12f,v=(i+1)/12f;Beam(Point(u),Point(v),.027f*(1-u),Tint(secondary,.48f*(1-u)*strength));
                }
            }
        }
        void Sun(float t,float strength)
        {
            Sprite(new Vector3(0,0,.31f),new Vector2(.55f,.37f),Tint(secondary,.13f*strength),2,0);
            Orbit(new Vector3(0,0,.28f),.61f,.43f,0,t*.06f,1,.026f,Tint(primary,.71f*strength));
            Orbit(new Vector3(0,0,.27f),.69f,.49f,.22f,-t*.08f,1,.010f,Tint(secondary,.60f*strength));
            for(int i=0;i<36;i++)
            {
                float a=i*Mathf.PI*2/36+t*.06f,r=.69f,tip=.80f+(i%3==0?.13f:.05f)+Mathf.Sin(t*.8f+i)*.020f;
                Vector3 dir=new Vector3(Mathf.Cos(a),Mathf.Sin(a)*.70f,0);Beam(dir*r+Vector3.forward*.24f,dir*tip+Vector3.forward*.24f,i%3==0?.030f:.014f,Tint(i%2==0?primary:secondary,.55f*strength));
            }
            for(int flare=0;flare<5;flare++)
            {
                float baseAngle=flare*Mathf.PI*2/5+t*.07f;
                for(int i=0;i<24;i++)
                {Vector3 Point(float u){float angle=baseAngle+u*.38f,r=.63f+Mathf.Sin(u*Mathf.PI)*(.20f+.06f*Mathf.Sin(t*.5f+flare));return new Vector3(Mathf.Cos(angle)*r,Mathf.Sin(angle)*r*.70f,.26f);}float u=i/24f,v=(i+1)/24f;Beam(Point(u),Point(v),.025f,Tint(Color.Lerp(primary,secondary,u),.61f*strength));}
            }
            Stream(t,320,Vector3.zero,1.3f,accent,true,34,strength,3);
        }
        void Moon(float t,float strength)
        {
            Sprite(new Vector3(0,.62f,.25f),Vector2.one*.175f,Tint(secondary,.88f*strength),4,-.22f);
            Sprite(new Vector3(0,.62f,.28f),Vector2.one*.24f,Tint(primary,.10f*strength),2,0);
            Orbit(new Vector3(0,0,.28f),.83f,.50f,.28f,t*.08f,1,.014f,Tint(primary,.45f*strength));
            Orbit(new Vector3(0,0,.28f),.75f,.43f,-.4f,-t*.07f,.83f,.009f,Tint(secondary,.40f*strength));
            for(int i=0;i<5;i++)
            {
                float a=i*Mathf.PI*2/5+t*.11f;Vector3 p=new Vector3(Mathf.Cos(a)*.76f,Mathf.Sin(a)*.49f,.23f);Sprite(p,Vector2.one*.048f,Tint(i%2==0?primary:secondary,.69f*strength),4,a);
                Trail(Vector3.zero,.84f,.53f,a-.25f,.64f,.021f,Tint(secondary,.50f*strength),14);
            }
            Constellation(t,strength,false);Stream(t,340,Vector3.zero,1.5f,primary,false,24,strength,3);
        }
        void Singularity(float t,float strength)
        {
            // A dark, translucent lens BEHIND the fish, edged by accretion streams.
            Sprite(new Vector3(0,0,.40f),new Vector2(.60f,.40f),Tint(new Color(.012f,.005f,.024f),.76f*strength),5,0);
            for(int ring=0;ring<3;ring++)Orbit(new Vector3(0,0,.30f),.60f+ring*.08f,.34f+ring*.054f,(ring-1)*.40f,t*(ring%2==0?.15f:-.11f),.93f,.025f-ring*.006f,Tint(ring%2==0?primary:secondary,(.62f-ring*.09f)*strength));
            for(int spiral=0;spiral<4;spiral++)for(int i=0;i<32;i++)
            {
                Vector3 Point(float u){float a=u*4.1f+spiral*Mathf.PI*.5f-t*.16f,r=.31f+u*.56f;return new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r*.64f,.28f);}
                float u=i/32f,v=(i+1)/32f;Beam(Point(u),Point(v),.032f*(1-u*.7f),Tint(Color.Lerp(primary,secondary,u),.49f*(1-u*.6f)*strength));
            }
            for(int mote=0;mote<10;mote++){float a=mote*Mathf.PI/5-t*.14f;Trail(Vector3.zero,.91f,.56f,a,.48f,.021f,Tint(secondary,.51f*strength),8);}
            for(int i=0;i<12;i++){float a=i*Mathf.PI/6+t*.04f;Sprite(new Vector3(Mathf.Cos(a)*.78f,Mathf.Sin(a)*.52f,.24f),new Vector2(.014f,.035f),Tint(primary,.60f*strength),3,a);}
        }
        void Constellation(float t,float strength,bool rainbow)
        {
            for(int i=0;i<7;i++)
            {
                Vector3 Point(int n)=>new Vector3((n-3)*.18f,.60f+Mathf.Sin(n*1.7f+t*.4f)*.075f,.24f);
                Color color=rainbow?Spectrum(i/7f+t*.018f):secondary;Sprite(Point(i),Vector2.one*.033f,Tint(color,.68f*strength),3,t*.12f);
                if(i<6)Beam(Point(i),Point(i+1),.008f,Tint(color,.33f*strength));
            }
        }
        void Creation(float t,float strength)
        {
            FeatherFans(t,strength,6,.83f,true);
            for(int ribbon=0;ribbon<4;ribbon++)for(int i=0;i<36;i++)
            {
                Vector3 Point(float u){float phase=u*Mathf.PI*2+t*.28f+ribbon*Mathf.PI*.5f;return new Vector3((u-.5f)*1.8f,Mathf.Sin(phase)*(.26f+ribbon*.025f),Mathf.Cos(phase)*.21f+.27f);}
                float u=i/36f,v=(i+1)/36f,fade=Mathf.Sin(u*Mathf.PI);Beam(Point(u),Point(v),.028f*fade,Tint(Spectrum(u*.7f+ribbon*.18f+t*.018f),.53f*fade*strength));
            }
            for(int ring=0;ring<2;ring++)Orbit(new Vector3(0,0,.24f),.76f+ring*.09f,.48f+ring*.07f,ring==0?.55f:-.55f,t*(ring==0?.10f:-.08f),.96f,.012f,Tint(ring==0?primary:secondary,.39f*strength));
            Constellation(t,strength,true);
            for(int i=0;i<24;i++){float a=i*Mathf.PI/12+t*.06f;Vector3 p=new Vector3(Mathf.Cos(a)*.88f,Mathf.Sin(a)*.59f,.22f);Sprite(p,Vector2.one*.015f,Tint(Spectrum(i/24f+t*.018f),.66f*strength),3,a);}
        }
        void Gilded(float t,float strength)
        {Orbit(new Vector3(0,0,.25f),.67f,.43f,0,t*.09f,.95f,.018f,Tint(primary,.51f*strength));Stream(t,380,Vector3.zero,1.3f,secondary,true,24,strength,3);}
    }
}
