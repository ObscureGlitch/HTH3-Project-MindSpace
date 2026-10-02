using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TherapyGame.Editor
{
    // The original trunk renderer batches both deciduous and evergreen trees.
    // Winter hides that renderer, so copy all NON-deciduous triangles exactly
    // into the winter mesh instead of accidentally removing evergreen trunks.
    internal static class WinterTrunkBatch
    {
        internal sealed class Remainder
        {
            internal readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            internal readonly List<int> triangles=new List<int>();
            internal int sourceTriangles,removedTriangles;
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        internal static MeshRenderer[] Sources(Transform garden)=>garden.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r=>r.name.StartsWith("Tree trunks",StringComparison.Ordinal)&&r.sharedMaterials.Any(m=>m!=null&&m.name=="bark")).ToArray();
        internal static Remainder Extract(WinterTreeGeometry.Plan plan,Transform garden)
        {
            var result=new Remainder();var removed=new int[plan.trees.Length];
            Bounds[] bounds=plan.trees.Select(t=>{var b=t.trunk;b.Expand(.006f);return b;}).ToArray();
            var sources=Sources(garden);Require(sources.Length>0,"Original batched trunk source missing.");
            foreach(var source in sources)
            {
                Require(source.sharedMaterials.Length==1&&source.sharedMaterial.name=="bark","Trunk source has mixed materials; refusing to hide it.");
                var mesh=source.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;var triangles=mesh.triangles;
                var normalMatrix=source.transform.localToWorldMatrix.inverse.transpose;
                var world=vertices.Select(source.transform.TransformPoint).ToArray();var remap=new Dictionary<int,int>();
                result.sourceTriangles+=triangles.Length/3;
                for(int at=0;at<triangles.Length;at+=3)
                {
                    int owner=-1;
                    for(int tree=0;tree<bounds.Length;tree++)
                        if(bounds[tree].Contains(world[triangles[at]])&&bounds[tree].Contains(world[triangles[at+1]])&&bounds[tree].Contains(world[triangles[at+2]])){owner=tree;break;}
                    if(owner>=0){removed[owner]++;result.removedTriangles++;continue;}
                    for(int corner=0;corner<3;corner++)
                    {
                        int original=triangles[at+corner];
                        if(!remap.TryGetValue(original,out int index))
                        {
                            index=result.vertices.Count;remap.Add(original,index);result.vertices.Add(world[original]);
                            result.normals.Add(normalMatrix.MultiplyVector(normals[original]).normalized);
                        }
                        result.triangles.Add(index);
                    }
                }
            }
            Require(removed.All(count=>count>=14),"A deciduous trunk was not fully identified in its source batch.");
            Require(result.triangles.Count>0&&result.sourceTriangles==result.removedTriangles+result.triangles.Count/3,"Original evergreen/non-deciduous trunk triangles were lost.");
            return result;
        }
        internal static void Append(Remainder remaining,Transform space,Mesh mesh)
        {
            var vertices=mesh.vertices.ToList();var normals=mesh.normals.ToList();var triangles=mesh.triangles.ToList();int prefix=vertices.Count;
            var normalMatrix=space.localToWorldMatrix.transpose;
            vertices.AddRange(remaining.vertices.Select(space.InverseTransformPoint));
            normals.AddRange(remaining.normals.Select(n=>normalMatrix.MultiplyVector(n).normalized));
            triangles.AddRange(remaining.triangles.Select(i=>i+prefix));
            Require(vertices.Count<40000,"Winter forest exceeded its combined-mesh budget.");
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        }
        internal static string Check(WinterTreeGeometry.Plan plan,Remainder remaining,Transform space,Mesh mesh)
        {
            string geometry=WinterTreeGeometry.Check(plan,space,mesh,remaining.vertices.Count);
            var vertices=mesh.vertices;int prefix=vertices.Length-remaining.vertices.Count;
            for(int i=0;i<remaining.vertices.Count;i++)
                Require((space.TransformPoint(vertices[prefix+i])-remaining.vertices[i]).sqrMagnitude<.000001f,"Evergreen trunk vertex moved during winter replacement.");
            var triangles=mesh.triangles;int offset=triangles.Length-remaining.triangles.Count;
            Require(offset==plan.limbs.Sum(l=>l.points.Length*30),"Winter tree triangle budget mismatch.");
            for(int i=0;i<remaining.triangles.Count;i++)Require(triangles[offset+i]==remaining.triangles[i]+prefix,"Evergreen trunk triangle changed.");
            return geometry+"PASS: "+remaining.removedTriangles+" original deciduous-trunk triangles replaced only in Winter; all "+remaining.triangles.Count/3+" remaining trunk triangles and positions preserved exactly in the same combined mesh.\n";
        }
    }
}
