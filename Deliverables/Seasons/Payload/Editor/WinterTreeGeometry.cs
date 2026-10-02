using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TherapyGame.Editor
{
    // Editor-built only: one combined mesh, no runtime tree regeneration or extra renderers.
    internal static class WinterTreeGeometry
    {
        internal sealed class Tree { public Bounds trunk,crown; }
        internal sealed class Limb
        {
            public Vector3[] points;
            public float[] radii;
            public int parent=-1,tree;
            public float attachment;
            public int firstVertex;
        }
        internal sealed class Plan
        {
            public readonly List<Limb> limbs=new List<Limb>();
            public Tree[] trees;
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        internal static Vector3 Root(Bounds trunk)=>new Vector3(trunk.center.x,trunk.min.y+.02f,trunk.center.z);
        internal static float Height(Tree tree)=>Mathf.Max(2.4f,(tree.crown.max.y-tree.trunk.min.y)*.93f);
        static int Seed(Vector3 center)
        {
            unchecked
            {
                int result=216613626;
                result=(result^Mathf.RoundToInt(center.x*100))*16777619;
                result=(result^Mathf.RoundToInt(center.y*100))*16777619;
                return (result^Mathf.RoundToInt(center.z*100))*16777619;
            }
        }
        static float Range(System.Random random,float low,float high)=>Mathf.Lerp(low,high,(float)random.NextDouble());
        internal static Plan Generate(Tree[] trees)
        {
            var plan=new Plan{trees=trees};
            for(int tree=0;tree<trees.Length;tree++)
            {
                var input=trees[tree];var random=new System.Random(Seed(input.trunk.center));
                // Rebuild the complete winter tree from its original ground anchor.
                // A short clear bole and broad lower scaffold limbs replace the old
                // tall pole with a small crown grafted only above its trunk cap.
                Vector3 root=Root(input.trunk);float height=Height(input);
                float spread=Mathf.Max(.9f,Mathf.Max(input.crown.extents.x,input.crown.extents.z))*1.15f;
                float phase=Range(random,0,Mathf.PI*2);
                Vector3 direction=new Vector3(Mathf.Cos(phase),0,Mathf.Sin(phase));
                Vector3 side=new Vector3(-direction.z,0,direction.x);
                Vector3 lean=direction*spread*Range(random,.15f,.30f);
                float radius=Mathf.Min(input.trunk.extents.x,input.trunk.extents.z)*.90f;
                int stem=Add(plan,tree,-1,0,new[]{root,root+Vector3.up*height*.16f+lean*.025f,
                    root+Vector3.up*height*.31f+lean*.14f+side*spread*Range(random,-.025f,.025f),
                    root+Vector3.up*height*.49f+lean*.36f+side*spread*Range(random,-.055f,.055f),
                    root+Vector3.up*height*.68f+lean*.62f+side*spread*Range(random,-.075f,.075f),
                    root+Vector3.up*height*.85f+lean*.83f,root+Vector3.up*height+lean},
                    new[]{radius,radius*.85f,radius*.70f,radius*.51f,radius*.33f,radius*.17f,Mathf.Max(.007f,radius*.055f)});
                int arms=random.Next(6,9);float firstBranch=Range(random,.24f,.31f);
                for(int arm=0;arm<arms;arm++)
                {
                    float attachment=firstBranch+(.83f-firstBranch)*(arm+Range(random,.08f,.35f))/arms;
                    Vector3 start=Sample(plan.limbs[stem],attachment,out float parentRadius);
                    float angle=phase+arm*2.399963f+Range(random,-.32f,.32f);
                    Vector3 outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    Vector3 cross=new Vector3(-outward.z,0,outward.x);
                    float rounded=Mathf.Sqrt(Mathf.Max(.18f,1-Mathf.Pow((attachment-.46f)/.57f,2)));
                    float reach=spread*Range(random,.78f,1.12f)*rounded;
                    float rise=reach*Range(random,.24f,.67f);
                    Vector3 mid=start+outward*reach*.34f+Vector3.up*(rise*.20f+reach*Range(random,-.10f,.09f));
                    Vector3 bend=start+outward*reach*.69f+cross*reach*Range(random,-.22f,.22f)+Vector3.up*rise*.57f;
                    Vector3 end=start+outward*reach+cross*reach*Range(random,-.16f,.16f)+Vector3.up*rise;
                    float armRadius=parentRadius*Range(random,.60f,.73f);
                    int branch=Add(plan,tree,stem,attachment,new[]{start,mid,bend,end},
                        new[]{armRadius,armRadius*.65f,armRadius*.34f,Mathf.Max(.004f,armRadius*.12f)});
                    int twigs=random.Next(3,5);
                    for(int twig=0;twig<twigs;twig++)
                    {
                        float t=.34f+.49f*(twig+Range(random,.15f,.85f))/twigs;
                        Vector3 twigStart=Sample(plan.limbs[branch],t,out float twigParentRadius);
                        float sign=twig%2==0?1:-1;
                        Vector3 fork=(outward*Range(random,.35f,.65f)+cross*sign*Range(random,.45f,.85f)+Vector3.up*Range(random,.24f,.65f)).normalized;
                        float length=reach*Range(random,.28f,.47f);
                        Vector3 kink=twigStart+fork*length*.48f+Vector3.up*length*Range(random,-.14f,.12f);
                        Vector3 twigEnd=twigStart+fork*length+outward*length*Range(random,-.17f,.17f)+Vector3.up*length*.20f;
                        float twigRadius=twigParentRadius*.47f;
                        int twigBranch=Add(plan,tree,branch,t,new[]{twigStart,kink,twigEnd},new[]{twigRadius,twigRadius*.52f,Mathf.Max(.0025f,twigRadius*.12f)});
                        if(twig%2==0)
                        {
                            float tipT=Range(random,.53f,.76f);Vector3 tipStart=Sample(plan.limbs[twigBranch],tipT,out float tipRadius);
                            Vector3 tipDirection=(fork*.55f-cross*sign*.50f+Vector3.up*.30f).normalized;
                            float tipLength=length*Range(random,.30f,.46f);
                            Add(plan,tree,twigBranch,tipT,new[]{tipStart,tipStart+tipDirection*tipLength*.47f+cross*tipLength*.11f,tipStart+tipDirection*tipLength},
                                new[]{tipRadius*.43f,tipRadius*.22f,.0018f});
                        }
                    }
                }
                int leaders=random.Next(2,4);
                for(int leader=0;leader<leaders;leader++)
                {
                    float t=Range(random,.47f,.68f);Vector3 start=Sample(plan.limbs[stem],t,out float parentRadius);
                    float angle=phase+(leader==0?1.1f:-1.4f)+Range(random,-.3f,.3f);
                    Vector3 fork=new Vector3(Mathf.Cos(angle)*.55f,Range(random,.65f,.95f),Mathf.Sin(angle)*.55f).normalized;
                    float length=height*Range(random,.25f,.34f),forkRadius=parentRadius*.62f;
                    Add(plan,tree,stem,t,new[]{start,start+fork*length*.46f+side*length*.13f,start+fork*length},
                        new[]{forkRadius,forkRadius*.56f,Mathf.Max(.003f,forkRadius*.12f)});
                }
            }
            return plan;
        }
        static int Add(Plan plan,int tree,int parent,float attachment,Vector3[] points,float[] radii)
        {
            plan.limbs.Add(new Limb{tree=tree,parent=parent,attachment=attachment,points=points,radii=radii});return plan.limbs.Count-1;
        }
        internal static Vector3 Sample(Limb limb,float amount,out float radius)
        {
            float length=0;for(int i=1;i<limb.points.Length;i++)length+=Vector3.Distance(limb.points[i-1],limb.points[i]);
            float distance=Mathf.Clamp01(amount)*length;
            for(int i=1;i<limb.points.Length;i++)
            {
                float segment=Vector3.Distance(limb.points[i-1],limb.points[i]);
                if(distance<=segment||i==limb.points.Length-1)
                {float t=Mathf.Clamp01(distance/Mathf.Max(.00001f,segment));radius=Mathf.Lerp(limb.radii[i-1],limb.radii[i],t);return Vector3.Lerp(limb.points[i-1],limb.points[i],t);}
                distance-=segment;
            }
            radius=limb.radii[0];return limb.points[0];
        }
        internal static void Build(Plan plan,Transform space,Mesh mesh)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            foreach(var limb in plan.limbs)
            {
                int start=vertices.Count;limb.firstVertex=start;Vector3 right=Vector3.zero;
                for(int point=0;point<limb.points.Length;point++)
                {
                    Vector3 before=point==0?(limb.points[1]-limb.points[0]).normalized:(limb.points[point]-limb.points[point-1]).normalized;
                    Vector3 after=point==limb.points.Length-1?before:(limb.points[point+1]-limb.points[point]).normalized;
                    Vector3 tangent=(before+after).normalized;
                    // Transport each ring's frame along the bend to avoid twisting seams.
                    right=point==0?Vector3.Cross(tangent,Vector3.forward):right-tangent*Vector3.Dot(right,tangent);
                    if(right.sqrMagnitude<.001f)right=Vector3.Cross(tangent,Vector3.up);
                    right.Normalize();Vector3 across=Vector3.Cross(tangent,right).normalized;
                    for(int side=0;side<5;side++)
                    {float angle=side*Mathf.PI*2/5;vertices.Add(space.InverseTransformPoint(limb.points[point]+(right*Mathf.Cos(angle)+across*Mathf.Sin(angle))*limb.radii[point]));}
                }
                int bottom=vertices.Count;vertices.Add(space.InverseTransformPoint(limb.points[0]));
                int top=vertices.Count;vertices.Add(space.InverseTransformPoint(limb.points[limb.points.Length-1]));
                for(int point=0;point<limb.points.Length-1;point++)for(int side=0;side<5;side++)
                {
                    int a=start+point*5+side,b=start+point*5+(side+1)%5,c=a+5,d=b+5;triangles.AddRange(new[]{a,b,c,b,d,c});
                }
                for(int side=0;side<5;side++)
                {
                    int a=start+side,b=start+(side+1)%5,c=start+(limb.points.Length-1)*5+side,d=start+(limb.points.Length-1)*5+(side+1)%5;
                    triangles.AddRange(new[]{bottom,b,a,top,c,d});
                }
            }
            Require(vertices.Count>0&&vertices.Count<40000,"Natural winter mesh exceeds its vertex budget.");
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        }
        internal static string Check(Plan plan,Transform space,Mesh mesh,int preservedVertices=0)
        {
            var vertices=mesh.vertices;int offset=0,roots=0,bent=0;
            var silhouettes=new HashSet<string>();
            foreach(var limb in plan.limbs)
            {
                limb.firstVertex=offset;int cap=offset+limb.points.Length*5;
                Require((space.TransformPoint(vertices[cap])-limb.points[0]).sqrMagnitude<.000001f,"Branch cap is detached from its planned root.");
                for(int point=0;point<limb.points.Length;point++)
                {
                    Vector3 center=Vector3.zero;for(int side=0;side<5;side++)center+=space.TransformPoint(vertices[offset+point*5+side]);center/=5;
                    Require((center-limb.points[point]).sqrMagnitude<.000001f,"Continuous tube ring lost its branch joint.");
                }
                if(limb.parent<0)
                {
                    roots++;Bounds trunk=plan.trees[limb.tree].trunk;
                    Require(trunk.Contains(limb.points[0])&&Mathf.Abs(limb.points[0].y-trunk.min.y)<.04f,"Winter tree lost its authored ground anchor.");
                    for(int side=0;side<5;side++)Require(trunk.Contains(space.TransformPoint(vertices[offset+side])),"Tree base moved outside its original trunk footprint.");
                    Vector3 lean=limb.points.Last()-limb.points[0];lean.y=0;
                    Require(lean.magnitude>.08f,"Winter leader is perfectly vertical.");
                    var scaffold=plan.limbs.Where(p=>p.parent==plan.limbs.IndexOf(limb)).ToArray();
                    float height=Height(plan.trees[limb.tree]);float first=scaffold.Min(p=>p.points[0].y)-limb.points[0].y;
                    float last=scaffold.Max(p=>p.points[0].y)-limb.points[0].y;
                    Require(first/height>=.22f&&first/height<=.36f,"Clear trunk is too long or branches are too low.");
                    Require((last-first)/height>.35f,"Branch attachments are crowded only at the top.");
                    int count=plan.limbs.Count(p=>p.tree==limb.tree);
                    silhouettes.Add(lean.x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+":"+lean.z.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+":"+count);
                }
                else
                {
                    Vector3 attachment=Sample(plan.limbs[limb.parent],limb.attachment,out float parentRadius);
                    Require((limb.points[0]-attachment).sqrMagnitude<.000001f&&limb.radii[0]<parentRadius,"Fork or twig does not join its parent tube.");
                }
                bool hasBend=false;
                for(int i=1;i<limb.points.Length-1;i++)if(Vector3.Angle(limb.points[i]-limb.points[i-1],limb.points[i+1]-limb.points[i])>3)hasBend=true;
                if(hasBend)bent++;
                offset+=limb.points.Length*5+2;
            }
            Require(offset+preservedVertices==vertices.Length&&roots==plan.trees.Length,"Natural winter mesh/plan mismatch.");
            Require(silhouettes.Count==plan.trees.Length,"Two winter trees reused the same silhouette.");
            Require(bent>plan.limbs.Count*.8f,"Too many perfectly straight winter branches.");
            Require(vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"Invalid winter vertex.");
            return "PASS: "+roots+" uniquely seeded ground-rooted trees, "+plan.limbs.Count+" connected tapered limbs, "+bent+" bent paths; first scaffold limbs at 22–36% of total height, attachments spanning over 35% of height, every fork/twig connected. Mesh "+mesh.vertexCount+" vertices / "+mesh.triangles.Length/3+" triangles.\n";
        }
        internal static string CheckFixtures()
        {
            var fixture=new GameObject("Natural winter geometry fixture"){hideFlags=HideFlags.HideAndDontSave};var mesh=new Mesh();
            try
            {
                fixture.transform.position=new Vector3(17,2,-9);fixture.transform.rotation=Quaternion.Euler(0,37,0);fixture.transform.localScale=new Vector3(1.7f,.9f,1.2f);
                var tree=new Tree{trunk=new Bounds(new Vector3(10,2,-3),new Vector3(.5f,3,.5f)),crown=new Bounds(new Vector3(10,4.5f,-3),new Vector3(3,2.8f,3))};
                var plan=Generate(new[]{tree});Build(plan,fixture.transform,mesh);Check(plan,fixture.transform,mesh);
                var repeat=Generate(new[]{tree});
                Require(repeat.limbs.Count==plan.limbs.Count&&repeat.limbs.Zip(plan.limbs,(a,b)=>a.points.SequenceEqual(b.points)).All(v=>v),"Tree layout changes on repeated generation.");
            }
            finally{UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(fixture);}
            return "PASS: stable per-position randomness, connected bent tubes and attachment under translated/rotated/nonuniformly scaled parents.\n";
        }
    }
}
