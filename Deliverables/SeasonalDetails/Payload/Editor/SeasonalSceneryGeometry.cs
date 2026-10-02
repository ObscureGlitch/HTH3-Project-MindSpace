using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TherapyGame.Editor
{
    // Editor-only geometry, shared vertex colors and four spatial batches per
    // season. Individual plants/leaves never become GameObjects or colliders.
    internal sealed class SeasonalSceneryGeometry
    {
        private readonly List<Vector3> vertices=new List<Vector3>();
        private readonly List<Color> colors=new List<Color>();
        private readonly List<int> indices=new List<int>();
        private float density;
        internal int Count {get;private set;}
        internal static readonly Color[] PetalColors={new Color(1,.27f,.50f),new Color(.62f,.34f,.94f),
            new Color(1,.78f,.15f),new Color(.30f,.60f,1),new Color(1,.94f,.80f),new Color(1,.43f,.23f)};
        internal void Flower(Vector3 root,float height,float yaw,int variety,float threshold)
        {
            density=threshold;Count++;
            Vector3 top=root+new Vector3(Mathf.Sin(yaw)*height*.12f,height,Mathf.Cos(yaw)*height*.12f);
            var stem=new Color(.24f,.48f,.16f);var foliage=new Color(.40f,.64f,.20f);
            Quad(root+Vector3.left*.009f,root+Vector3.right*.009f,top+Vector3.right*.008f,top+Vector3.left*.008f,stem);
            Quad(root+Vector3.back*.009f,root+Vector3.forward*.009f,top+Vector3.forward*.008f,top+Vector3.back*.008f,stem);
            Vector3 side=new Vector3(Mathf.Cos(yaw),0,Mathf.Sin(yaw));
            for(int i=0;i<2;i++)
            {
                Vector3 p=Vector3.Lerp(root,top,.32f+i*.25f),d=side*(i==0?1:-1),across=Vector3.Cross(d,Vector3.up)*.035f;
                Quad(p,p+d*height*.20f+across+Vector3.up*.025f,p+d*height*.42f+Vector3.up*.075f,p+d*height*.20f-across+Vector3.up*.025f,foliage);
            }
            int petals=variety==1?5:variety==2?6:7;
            float radius=height*(variety==2?.27f:.23f);
            Color petal=PetalColors[variety%PetalColors.Length];
            for(int p=0;p<petals;p++)
            {
                float angle=yaw+p*Mathf.PI*2/petals;
                Vector3 outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)),across=Vector3.Cross(outward,Vector3.up);
                Vector3 a=top+outward*radius*.18f,b=top+outward*radius*.56f-across*radius*.30f+Vector3.up*radius*.15f,
                    c=top+outward*radius*.93f-across*radius*.21f+Vector3.up*radius*.31f,
                    d=top+outward*radius+Vector3.up*radius*.38f,
                    e=top+outward*radius*.93f+across*radius*.21f+Vector3.up*radius*.31f,
                    f=top+outward*radius*.56f+across*radius*.30f+Vector3.up*radius*.15f;
                Vector3 middle=top+outward*radius*.60f+Vector3.up*radius*.23f;
                var edge=Color.Lerp(petal,Color.white,.14f);
                Tri(a,b,middle,petal);Tri(b,c,middle,edge);Tri(c,d,middle,edge);Tri(d,e,middle,edge);Tri(e,f,middle,edge);Tri(f,a,middle,petal);
            }
            Color center=variety==2?new Color(.48f,.24f,.06f):new Color(1,.69f,.12f);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;
                Tri(top+Vector3.up*.017f,top+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*radius*.25f,
                    top+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius*.25f,center);
            }
        }
        internal void Leaf(Vector3 root,Vector3 normal,float length,float yaw,Color color,float threshold)
        {
            density=threshold;Count++;
            Vector3 axis=Vector3.ProjectOnPlane(new Vector3(Mathf.Cos(yaw),0,Mathf.Sin(yaw)),normal).normalized;
            Vector3 across=Vector3.Cross(normal,axis);
            Vector2[] outline={new Vector2(0,-.5f),new Vector2(-.25f,-.2f),new Vector2(-.5f,.03f),new Vector2(-.22f,.1f),
                new Vector2(-.3f,.4f),new Vector2(-.08f,.27f),new Vector2(0,.64f),new Vector2(.10f,.29f),
                new Vector2(.31f,.4f),new Vector2(.24f,.10f),new Vector2(.49f,.03f),new Vector2(.23f,-.2f)};
            Vector3 middle=root+normal*.009f;
            for(int i=0;i<outline.Length;i++)
            {
                Vector2 a=outline[i],b=outline[(i+1)%outline.Length];
                Tri(middle,root+(across*a.x+axis*a.y)*length,root+(across*b.x+axis*b.y)*length,
                    Color.Lerp(color,new Color(.40f,.16f,.035f),i%3==0?.22f:0));
            }
            Quad(root-axis*length*.4f+normal*.012f-across*.003f,root-axis*length*.4f+normal*.012f+across*.003f,
                root+axis*length*.43f+normal*.012f+across*.002f,root+axis*length*.43f+normal*.012f-across*.002f,
                Color.Lerp(color,new Color(.48f,.27f,.08f),.5f));
        }
        private void Tri(Vector3 a,Vector3 b,Vector3 c,Color color)
        {
            int start=vertices.Count;color.a=density;vertices.Add(a);vertices.Add(b);vertices.Add(c);
            colors.Add(color);colors.Add(color);colors.Add(color);indices.Add(start);indices.Add(start+1);indices.Add(start+2);
        }
        private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color){Tri(a,b,c,color);Tri(a,c,d,color);}
        internal void Write(Mesh mesh)
        {
            mesh.Clear();mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        }
    }
}
