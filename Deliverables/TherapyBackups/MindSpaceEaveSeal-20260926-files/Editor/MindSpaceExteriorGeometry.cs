using System;
using System.Collections.Generic;
using UnityEngine;

namespace TherapyGame.Editor
{
    // Room-local, low-poly mesh construction. Nothing in the garden or roof is regenerated.
    public static class MindSpaceExteriorGeometry
    {
        public static readonly Rect Door=new Rect(-3.07f,-.04f,1.28f,2.54f);
        public static readonly Rect Window=new Rect(-.06f,1.10f,1.82f,1.27f);
        public sealed class Batch
        {
            public readonly List<Vector3> vertices=new List<Vector3>();
            public readonly List<int> triangles=new List<int>();
            public readonly List<Vector2> uv=new List<Vector2>();
            public void Tri(Vector3 a,Vector3 b,Vector3 c,Vector3 normal)
            {
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)<0){var swap=b;b=c;c=swap;}
                int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);
                uv.Add(new Vector2(a.x+a.z,a.y));uv.Add(new Vector2(b.x+b.z,b.y));uv.Add(new Vector2(c.x+c.z,c.y));
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal){Tri(a,b,c,normal);Tri(a,c,d,normal);}
            public void Box(Vector3 p,Vector3 size,Quaternion q)
            {
                var v=new Vector3[8];for(int i=0;i<8;i++)v[i]=p+q*Vector3.Scale(size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Quad(v[0],v[1],v[3],v[2],q*Vector3.back);Quad(v[4],v[6],v[7],v[5],q*Vector3.forward);
                Quad(v[0],v[2],v[6],v[4],q*Vector3.left);Quad(v[1],v[5],v[7],v[3],q*Vector3.right);
                Quad(v[0],v[4],v[5],v[1],q*Vector3.down);Quad(v[2],v[3],v[7],v[6],q*Vector3.up);
            }
            public void Box(Vector3 p,Vector3 size)=>Box(p,size,Quaternion.identity);
            public void Beam(Vector3 a,Vector3 b,float width,float depth)=>Box((a+b)*.5f,new Vector3(width,Vector3.Distance(a,b),depth),Quaternion.FromToRotation(Vector3.up,b-a));
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
        }
        public static List<Rect> Subtract(Rect source,params Rect[] holes)
        {
            var pieces=new List<Rect>{source};
            foreach(var h in holes)
            {
                var next=new List<Rect>();
                foreach(var p in pieces)
                {
                    float x0=Mathf.Max(p.xMin,h.xMin),x1=Mathf.Min(p.xMax,h.xMax),y0=Mathf.Max(p.yMin,h.yMin),y1=Mathf.Min(p.yMax,h.yMax);
                    if(x1<=x0||y1<=y0){next.Add(p);continue;}
                    AddRect(next,p.xMin,p.yMin,x0,p.yMax);AddRect(next,x1,p.yMin,p.xMax,p.yMax);
                    AddRect(next,x0,p.yMin,x1,y0);AddRect(next,x0,y1,x1,p.yMax);
                }
                pieces=next;
            }
            return pieces;
        }
        private static void AddRect(List<Rect> rects,float x0,float y0,float x1,float y1){if(x1-x0>.0001f&&y1-y0>.0001f)rects.Add(Rect.MinMaxRect(x0,y0,x1,y1));}
        private static void Wall(Batch b,Rect r,bool side,float plane,float thickness,params Rect[] holes)
        {
            foreach(var p in Subtract(r,holes))
                b.Box(side?new Vector3(plane,p.center.y,p.center.x):new Vector3(p.center.x,p.center.y,plane),
                    side?new Vector3(thickness,p.height,p.width):new Vector3(p.width,p.height,thickness));
        }
        public static Dictionary<string,Batch> Build()
        {
            var all=new Dictionary<string,Batch>();Batch Get(string key){if(!all.TryGetValue(key,out var b))all[key]=b=new Batch();return b;}
            Batch backing=Get("Backing"),cedar=Get("Cedar"),trim=Get("Trim"),oak=Get("Oak");
            var front=new Rect(-3.78f,-.025f,7.56f,3.195f);var side=new Rect(-3.30f,-.025f,6.60f,3.195f);
            var sideWindow=new Rect(-2.03f,.94f,4.06f,1.82f);
            Wall(backing,front,false,-3.235f,.055f,Door,Window);Wall(backing,front,false,3.235f,.055f);
            Wall(backing,side,true,-3.735f,.055f);Wall(backing,side,true,3.735f,.055f,sideWindow);
            const int rows=20;float pitch=front.height/rows;
            for(int row=0;row<rows;row++)
            {
                float y=front.y+row*pitch;
                Wall(cedar,new Rect(front.x,y,front.width,pitch-.004f),false,-3.281f,.038f,Door,Window);
                Wall(cedar,new Rect(front.x,y,front.width,pitch-.004f),false,3.281f,.038f);
                Wall(cedar,new Rect(side.x,y,side.width,pitch-.004f),true,-3.778f,.038f);
                Wall(cedar,new Rect(side.x,y,side.width,pitch-.004f),true,3.778f,.038f,sideWindow);
            }
            // Equal corner posts overlap both cladding faces by the same amount.
            foreach(float x in new[]{-3.78f,3.78f})foreach(float z in new[]{-3.285f,3.285f})
                trim.Box(new Vector3(x,1.6275f,z),new Vector3(.20f,3.365f,.20f));
            foreach(float z in new[]{-3.29f,3.29f})trim.Box(new Vector3(0,3.15f,z),new Vector3(7.60f,.10f,.10f));
            // Full-depth reveals cover the siding ends without intruding into the existing leaf.
            oak.Box(new Vector3(-3.11f,1.24f,-3.225f),new Vector3(.08f,2.48f,.43f));
            oak.Box(new Vector3(-1.75f,1.24f,-3.225f),new Vector3(.08f,2.48f,.43f));
            oak.Box(new Vector3(-2.43f,2.49f,-3.225f),new Vector3(1.48f,.14f,.43f));
            var outer=new[]{new Vector3(-3.78f,3.12f,-3.282f),new Vector3(3.78f,3.12f,-3.282f),new Vector3(0,4.50f,-3.282f)};
            var inner=new[]{new Vector3(-3.03f,3.20f,-3.282f),new Vector3(3.03f,3.20f,-3.282f),new Vector3(0,4.29f,-3.282f)};
            // Pentagonal gable reaches the actual sloped roof underside at both eaves.
            // A simple low triangle here left the old 10–20 cm sky gap beside the posts.
            Vector3 leftEave=new Vector3(-3.78f,3.30f,-3.282f),rightEave=new Vector3(3.78f,3.30f,-3.282f);
            cedar.Quad(outer[0],outer[1],inner[1],inner[0],Vector3.back);
            cedar.Tri(outer[0],inner[0],inner[2],Vector3.back);cedar.Tri(outer[0],inner[2],outer[2],Vector3.back);cedar.Tri(outer[0],outer[2],leftEave,Vector3.back);
            cedar.Tri(outer[1],inner[1],inner[2],Vector3.back);cedar.Tri(outer[1],inner[2],outer[2],Vector3.back);cedar.Tri(outer[1],outer[2],rightEave,Vector3.back);
            for(int i=0;i<3;i++)
            {
                int j=(i+1)%3;
                oak.Beam(inner[i]+Vector3.back*.035f,inner[j]+Vector3.back*.035f,.085f,.12f);
            }
            foreach(var normal in new[]{Vector3.forward,Vector3.back})
            {
                cedar.Quad(new Vector3(-3.78f,3.12f,3.282f),new Vector3(3.78f,3.12f,3.282f),new Vector3(3.78f,3.30f,3.282f),new Vector3(-3.78f,3.30f,3.282f),normal);
                cedar.Tri(new Vector3(-3.78f,3.30f,3.282f),new Vector3(3.78f,3.30f,3.282f),new Vector3(0,4.50f,3.282f),normal);
            }
            var glass=Get("AtticGlass");glass.Tri(inner[0]+Vector3.back*.025f,inner[1]+Vector3.back*.025f,inner[2]+Vector3.back*.025f,Vector3.back);
            glass.Tri(inner[0]+Vector3.back*.025f,inner[1]+Vector3.back*.025f,inner[2]+Vector3.back*.025f,Vector3.forward);
            foreach(float x in new[]{-1.01f,0,1.01f})
            {
                float top=4.29f-Mathf.Abs(x)/3.03f*1.09f;
                oak.Beam(new Vector3(x,3.20f,-3.33f),new Vector3(x,top,-3.33f),.048f,.07f);
            }
            // A small physical plaque, with crisp mesh lettering rather than another texture.
            oak.Box(new Vector3(-1.035f,1.90f,-3.347f),new Vector3(1.40f,.79f,.075f));
            Get("Sage").Box(new Vector3(-1.035f,1.90f,-3.391f),new Vector3(1.31f,.70f,.028f));
            Lettering(Get("Lettering"),"MINDSPACE",new Vector3(-1.035f,2.07f,-3.410f),1.11f,.155f);
            Lettering(Get("Lettering"),"COUNSELING",new Vector3(-1.035f,1.865f,-3.410f),1.08f,.106f);
            Lettering(Get("Lettering"),"CENTER",new Vector3(-1.035f,1.70f,-3.410f),.65f,.106f);
            // Window box, flowers and two modest corner planters: no new lights or physics bodies.
            oak.Box(new Vector3(.85f,.86f,-3.48f),new Vector3(2.15f,.25f,.36f));
            Get("Soil").Box(new Vector3(.85f,.991f,-3.48f),new Vector3(2.02f,.018f,.26f));
            for(int i=0;i<9;i++)Plant(all,new Vector3(-.03f+i*.22f,1,-3.49f),.19f+(i%3)*.035f,i);
            foreach(var p in new[]{new Vector3(3.12f,.22f,-3.80f),new Vector3(-4.08f,.22f,.5f)})
            {
                Get("Sage").Box(p,new Vector3(.48f,.44f,.48f));
                Get("Soil").Box(p+Vector3.up*.224f,new Vector3(.41f,.014f,.41f));
                for(int i=0;i<5;i++)Plant(all,p+new Vector3(Mathf.Cos(i*2.4f)*.12f,.24f,Mathf.Sin(i*2.4f)*.12f),.34f+i*.025f,i);
            }
            // Restrained climbing greenery at the right corner, clear of the entry and windows.
            for(int i=0;i<9;i++)Plant(all,new Vector3(3.37f+Mathf.Sin(i)*.09f,.22f+i*.23f,-3.36f),.20f,i+1);
            var metal=Get("Metal");var lampCenter=new Vector3(-3.48f,2.05f,-3.47f);
            metal.Box(lampCenter+new Vector3(0,.23f,.13f),new Vector3(.055f,.15f,.26f));
            foreach(float y in new[]{-.16f,.16f})metal.Box(lampCenter+Vector3.up*y,new Vector3(.22f,.04f,.20f));
            foreach(float x in new[]{-.095f,.095f})foreach(float z in new[]{-.08f,.08f})metal.Box(lampCenter+new Vector3(x,0,z),new Vector3(.023f,.30f,.023f));
            Get("LanternGlow").Box(lampCenter,new Vector3(.14f,.25f,.13f));
            return all;
        }
        private static void Plant(Dictionary<string,Batch> all,Vector3 root,float height,int color)
        {
            Batch Get(string key){if(!all.TryGetValue(key,out var b))all[key]=b=new Batch();return b;}
            Vector3 tip=root+new Vector3(Mathf.Sin(color)*.035f,height,0);Get("Leaves").Beam(root,tip,.012f,.012f);
            for(int i=0;i<3;i++)
            {
                Vector3 basePoint=Vector3.Lerp(root,tip,.25f+i*.2f),end=basePoint+new Vector3((i%2==0?1:-1)*.12f,.065f,-.02f);
                Vector3 side=Vector3.forward*.045f;
                Get("Leaves").Quad(basePoint,(basePoint+end)*.5f+side,end,(basePoint+end)*.5f-side,Vector3.up);
                Get("Leaves").Quad(basePoint,(basePoint+end)*.5f+side,end,(basePoint+end)*.5f-side,Vector3.down);
            }
            for(int p=0;p<5;p++)
            {
                float a=p*Mathf.PI*2/5;Vector3 end=tip+new Vector3(Mathf.Cos(a)*.052f,.014f,Mathf.Sin(a)*.052f),side=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a))*.023f;
                Get(color%3==0?"Lavender":"Flowers").Tri(tip,end+side,end-side,Vector3.up);
                Get(color%3==0?"Lavender":"Flowers").Tri(tip,end+side,end-side,Vector3.down);
            }
            Get("Lettering").Box(tip+Vector3.up*.012f,new Vector3(.024f,.016f,.024f));
        }
        private static readonly Dictionary<char,string> Glyphs=new Dictionary<char,string>
        {
            ['M']="0,0 0,1 .5,.45 1,1 1,0",['I']=".1,1 .9,1|.5,1 .5,0|.1,0 .9,0",
            ['N']="0,0 0,1 1,0 1,1",['D']="0,0 0,1 .65,1 1,.75 1,.25 .65,0 0,0",
            ['S']="1,.9 .8,1 .2,1 0,.8 0,.65 1,.35 1,.2 .8,0 .2,0 0,.1",
            ['P']="0,0 0,1 .75,1 1,.8 1,.65 .75,.5 0,.5",
            ['A']="0,0 .4,1 .6,1 1,0|.2,.45 .8,.45",
            ['C']="1,.9 .8,1 .2,1 0,.8 0,.2 .2,0 .8,0 1,.1",
            ['E']="1,1 0,1 0,0 1,0|0,.5 .8,.5",
            ['O']=".2,0 0,.2 0,.8 .2,1 .8,1 1,.8 1,.2 .8,0 .2,0",
            ['U']="0,1 0,.2 .2,0 .8,0 1,.2 1,1",['L']="0,1 0,0 1,0",
            ['G']="1,.8 .8,1 .2,1 0,.8 0,.2 .2,0 1,0 1,.5 .5,.5",
            ['T']="0,1 1,1|.5,1 .5,0",['R']="0,0 0,1 .75,1 1,.8 1,.65 .75,.5 0,.5|.55,.5 1,0"
        };
        private static void Lettering(Batch b,string text,Vector3 center,float width,float height)
        {
            float advance=width/text.Length,letter=advance*.70f,stroke=height*.065f;
            for(int i=0;i<text.Length;i++)
            {
                if(!Glyphs.TryGetValue(text[i],out string lines))continue;
                foreach(string line in lines.Split('|'))
                {
                    string[] points=line.Split(' ');
                    Vector3 prev=Vector3.zero;
                    for(int k=0;k<points.Length;k++)
                    {
                        var xy=points[k].Split(',');float x=float.Parse(xy[0],System.Globalization.CultureInfo.InvariantCulture),y=float.Parse(xy[1],System.Globalization.CultureInfo.InvariantCulture);
                        var p=center+new Vector3(-width*.5f+i*advance+letter*x+advance*.15f,height*(y-.5f),0);
                        if(k>0){var side=Vector3.Cross((p-prev).normalized,Vector3.back)*stroke*.5f;b.Quad(prev-side,prev+side,p+side,p-side,Vector3.back);}prev=p;
                    }
                }
            }
        }
        public static string Validate(Dictionary<string,Batch> all)
        {
            int count=0;foreach(var b in all.Values){if(b.vertices.Count>65000)throw new Exception("Exterior batch exceeded small mesh budget.");count+=b.triangles.Count/3;}
            if(count>7000)throw new Exception("Exterior triangle budget exceeded.");
            var rectangle=new Rect(-3.78f,-.025f,7.56f,3.195f);
            float area=0;foreach(var p in Subtract(rectangle,Door,Window))
            {
                area+=p.width*p.height;
                foreach(var hole in new[]{Door,Window})
                    if(Mathf.Min(p.xMax,hole.xMax)-Mathf.Max(p.xMin,hole.xMin)>.00001f&&Mathf.Min(p.yMax,hole.yMax)-Mathf.Max(p.yMin,hole.yMin)>.00001f)
                        throw new Exception("Opening overlap.");
            }
            float expected=rectangle.width*rectangle.height-(Door.width*(Door.yMax-rectangle.yMin))-Window.width*Window.height;
            if(Mathf.Abs(area-expected)>.001f)throw new Exception("Facade coverage mismatch.");
            // Check actual triangles at the front of the new casing, not just the plan rectangles.
            int samples=0;
            for(float y=.12f;y<2.38f;y+=.1f)for(float x=-3.015f;x<-1.845f;x+=.09f)
            {
                foreach(string key in new[]{"Cedar","Backing","Oak","Trim"})
                {
                    var b=all[key];for(int i=0;i<b.vertices.Count;i+=3)
                    {
                        var a=b.vertices[i];var c=b.vertices[i+1];var d=b.vertices[i+2];
                        if(Mathf.Abs(a.z+3.28f)>.4f||Mathf.Abs(c.z+3.28f)>.4f||Mathf.Abs(d.z+3.28f)>.4f)continue;
                        if(InsideTriangle(new Vector2(x,y),a,c,d))throw new Exception("New wood crosses the doorway.");
                    }
                }samples++;
            }
            int eaveSamples=0;
            for(float x=-3.75f;x<=3.75f;x+=.075f)
            {
                float y=4.4862f-.3227f*Mathf.Abs(x)-.009f;bool covered=false;var b=all["Cedar"];
                for(int i=0;i<b.vertices.Count;i+=3)
                {
                    var a=b.vertices[i];var c=b.vertices[i+1];var d=b.vertices[i+2];
                    if(Mathf.Abs(a.z+3.282f)>.001f||Mathf.Abs(c.z+3.282f)>.001f||Mathf.Abs(d.z+3.282f)>.001f)continue;
                    if(InsideTriangle(new Vector2(x,y),a,c,d)){covered=true;break;}
                }
                if(!covered)throw new Exception("Unsealed eave at "+x);eaveSamples++;
            }
            return "PASS: "+count+" new triangles across "+all.Count+" small mesh batches; continuous cladding coverage; "+samples+" doorway clearance and "+eaveSamples+" roof-seam samples.\n";
        }
        private static bool InsideTriangle(Vector2 p,Vector3 a,Vector3 b,Vector3 c)
        {
            Vector2 v0=new Vector2(b.x-a.x,b.y-a.y),v1=new Vector2(c.x-a.x,c.y-a.y),v2=p-new Vector2(a.x,a.y);
            float den=v0.x*v1.y-v1.x*v0.y;if(Mathf.Abs(den)<.000001f)return false;
            float u=(v2.x*v1.y-v1.x*v2.y)/den,v=(v0.x*v2.y-v2.x*v0.y)/den;
            return u>0&&v>0&&u+v<1;
        }
    }
}
