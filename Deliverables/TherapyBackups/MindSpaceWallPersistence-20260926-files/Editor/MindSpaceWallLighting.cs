using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TherapyGame.Editor
{
    public static class MindSpaceWallLighting
    {
        // Reproject the replacement wall patches onto their preserved pre-window wall.
        // This restores the original plaster UV scale and, when available, its existing baked GI.
        public static string Repair(Transform room,string folder)
        {
            var original=room.Find("Architecture/Walls/Entry front wall");
            var window=room.Find("Architecture/Front garden window");
            if(original==null||window==null)throw new Exception("Preserved front wall/window missing.");
            var sourceRenderer=original.GetComponent<MeshRenderer>();
            var source=original.GetComponent<MeshFilter>().sharedMesh;
            var vertices=source.vertices;var normals=source.normals;var triangles=source.triangles;
            var uv=source.uv;var uv2=source.uv2;
            bool hasBaked=sourceRenderer.lightmapIndex>=0&&sourceRenderer.lightmapIndex<LightmapSettings.lightmaps.Length&&uv2.Length==vertices.Length;
            int patches=0;
            foreach(Transform patch in window)
            {
                if(!patch.name.StartsWith("Plaster ",StringComparison.Ordinal))continue;
                var filter=patch.GetComponent<MeshFilter>();var renderer=patch.GetComponent<MeshRenderer>();
                if(filter==null||renderer==null)throw new Exception("Incomplete plaster patch.");
                var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name=patch.name+" matched lighting";
                var pv=mesh.vertices;var pn=mesh.normals;var texture=new Vector2[pv.Length];var baked=new Vector2[pv.Length];
                for(int i=0;i<pv.Length;i++)
                {
                    Vector3 p=original.InverseTransformPoint(patch.TransformPoint(pv[i]));
                    Vector3 n=original.InverseTransformDirection(patch.TransformDirection(pn[i])).normalized;
                    float best=float.PositiveInfinity;Vector3 weights=Vector3.zero;int bestTriangle=-1;
                    for(int t=0;t<triangles.Length;t+=3)
                    {
                        int ia=triangles[t],ib=triangles[t+1],ic=triangles[t+2];
                        Vector3 a=vertices[ia],b=vertices[ib],c=vertices[ic];
                        Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
                        if(Vector3.Dot(normal,n)<.25f)continue;
                        Vector3 closest=Closest(p,a,b,c,out Vector3 w);float distance=(p-closest).sqrMagnitude;
                        if(distance<best){best=distance;weights=w;bestTriangle=t;}
                    }
                    if(bestTriangle<0)throw new Exception("Cannot project replacement plaster surface.");
                    int ta=triangles[bestTriangle],tb=triangles[bestTriangle+1],tc=triangles[bestTriangle+2];
                    if(uv.Length==vertices.Length)texture[i]=uv[ta]*weights.x+uv[tb]*weights.y+uv[tc]*weights.z;
                    if(hasBaked)baked[i]=uv2[ta]*weights.x+uv2[tb]*weights.y+uv2[tc]*weights.z;
                }
                mesh.uv=texture;if(hasBaked)mesh.uv2=baked;
                string path=folder+"/"+patch.name.Replace(' ','_')+".asset";
                if(File.Exists(path))throw new IOException("Wall repair asset already exists: "+path);
                AssetDatabase.CreateAsset(mesh,path);
                Undo.RecordObject(filter,"Match plaster UVs");Undo.RecordObject(renderer,"Match plaster lighting");
                filter.sharedMesh=mesh;renderer.sharedMaterial=sourceRenderer.sharedMaterial;
                renderer.receiveShadows=sourceRenderer.receiveShadows;
                renderer.shadowCastingMode=sourceRenderer.shadowCastingMode;
                if(hasBaked)
                {
                    renderer.receiveGI=ReceiveGI.Lightmaps;renderer.lightmapIndex=sourceRenderer.lightmapIndex;
                    renderer.lightmapScaleOffset=sourceRenderer.lightmapScaleOffset;
                }
                else
                {
                    // No new bake on this laptop. Sample existing room probes from the occupied
                    // interior, not through the exterior wall. Keep the original plaster material.
                    renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
                    renderer.probeAnchor=room.Find("Lighting/Interior plaster probe anchor");
                    if(renderer.probeAnchor==null)
                    {
                        var anchor=new GameObject("Interior plaster probe anchor");Undo.RegisterCreatedObjectUndo(anchor,"Interior probe anchor");
                        anchor.transform.SetParent(room.Find("Lighting")??room,false);anchor.transform.localPosition=new Vector3(.85f,1.5f,-2.45f);
                        renderer.probeAnchor=anchor.transform;
                    }
                }
                EditorUtility.SetDirty(filter);EditorUtility.SetDirty(renderer);patches++;
            }
            if(patches!=4)throw new Exception("Expected exactly four preserved plaster patches.");
            return "PASS: four wall patches use original warm plaster and reprojected UVs. Original lightmap index "+sourceRenderer.lightmapIndex+
                "; "+(hasBaked?"original baked lighting reused without rebaking.":"interior light-probe fallback (no source bake available).")+"\n";
        }
        private static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c,out Vector3 w)
        {
            Vector3 ab=b-a,ac=c-a,ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
            if(d1<=0&&d2<=0){w=new Vector3(1,0,0);return a;}
            Vector3 bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);
            if(d3>=0&&d4<=d3){w=new Vector3(0,1,0);return b;}
            float vc=d1*d4-d3*d2;
            if(vc<=0&&d1>=0&&d3<=0){float v=d1/(d1-d3);w=new Vector3(1-v,v,0);return a+v*ab;}
            Vector3 cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);
            if(d6>=0&&d5<=d6){w=new Vector3(0,0,1);return c;}
            float vb=d5*d2-d1*d6;
            if(vb<=0&&d2>=0&&d6<=0){float v=d2/(d2-d6);w=new Vector3(1-v,0,v);return a+v*ac;}
            float va=d3*d6-d5*d4;
            if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0){float v=(d4-d3)/((d4-d3)+(d5-d6));w=new Vector3(0,1-v,v);return b+v*(c-b);}
            float sum=va+vb+vc;if(Mathf.Abs(sum)<.0000001f){w=new Vector3(1,0,0);return a;}
            float s=vb/sum,t=vc/sum;w=new Vector3(1-s-t,s,t);return a+ab*s+ac*t;
        }
    }
}
