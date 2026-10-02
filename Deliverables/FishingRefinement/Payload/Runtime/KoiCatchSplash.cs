using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace TheLastWatch.UI
{
    // One bounded animated mesh, no particle objects or lights, all rarity tiers.
    public sealed class KoiCatchSplash : IDisposable
    {
        public GameObject Root {get;private set;}
        readonly Mesh mesh;readonly Material material;
        readonly Vector3[] vertices=new Vector3[320];readonly Color[] colors=new Color[320];readonly Vector3[] uv=new Vector3[320];readonly int[] triangles=new int[480];
        int quads,lastFrame=-1;
        public KoiCatchSplash(Transform parent,Vector3 water)
        {
            Root=new GameObject("Catch splash (runtime)"){hideFlags=HideFlags.DontSave};Root.transform.SetParent(parent,false);Root.transform.position=water;Root.transform.rotation=Quaternion.identity;
            mesh=new Mesh{name="Catch water burst",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();material=new Material(Resources.Load<Shader>("KoiRarityAura")){hideFlags=HideFlags.DontSave};
            Root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=Root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.lightProbeUsage=LightProbeUsage.Off;
            for(int i=0;i<80;i++){int v=i*4,t=i*6;triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v;triangles[t+4]=v+2;triangles[t+5]=v+3;}
        }
        public void Pose(float seconds,Quaternion viewer)
        {
            float age=seconds-.06f;if(age<0||age>1.25f){Root.SetActive(false);return;}Root.SetActive(true);
            int frame=Mathf.FloorToInt(age*30);if(frame==lastFrame)return;lastFrame=frame;quads=0;
            float life=Mathf.Clamp01(age/1.25f),radius=.07f+life*.85f;
            var color=new Color(.50f,.91f,1.05f,(1-life)*.7f);
            for(int i=0;i<48;i++)
            {
                float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;
                Vector3 A=new Vector3(Mathf.Cos(a)*radius,.01f,Mathf.Sin(a)*radius),B=new Vector3(Mathf.Cos(b)*radius,.01f,Mathf.Sin(b)*radius);
                Vector3 width=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.012f*(1-life);
                Quad(A-width,B-width,B+width,A+width,color,0);
            }
            Vector3 right=viewer*Vector3.right,up=viewer*Vector3.up;
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI*2/24,speed=.35f+(i%5)*.085f;
                Vector3 p=new Vector3(Mathf.Cos(a)*speed*age,Mathf.Max(0,(1.5f+(i%3)*.18f)*age-1.65f*age*age),Mathf.Sin(a)*speed*age);
                Vector3 x=right*.014f*(1-life),y=up*.026f*(1-life);Quad(p-x-y,p+x-y,p+x+y,p-x+y,new Color(.72f,.95f,1.1f,(1-life)*.75f),2);
            }
            mesh.SetVertices(vertices,0,quads*4);mesh.SetColors(colors,0,quads*4);mesh.SetUVs(0,uv,0,quads*4);mesh.SetTriangles(triangles,0,quads*6,0,false);mesh.bounds=new Bounds(Vector3.up*.35f,new Vector3(2,2,2));
        }
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,int shape)
        {int v=quads++*4;vertices[v]=a;vertices[v+1]=b;vertices[v+2]=c;vertices[v+3]=d;colors[v]=colors[v+1]=colors[v+2]=colors[v+3]=color;uv[v]=new Vector3(0,0,shape);uv[v+1]=new Vector3(1,0,shape);uv[v+2]=new Vector3(1,1,shape);uv[v+3]=new Vector3(0,1,shape);}
        static void Destroy(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        public void Dispose(){if(Root!=null)Root.SetActive(false);Destroy(Root);Destroy(mesh);Destroy(material);Root=null;}
    }
}
