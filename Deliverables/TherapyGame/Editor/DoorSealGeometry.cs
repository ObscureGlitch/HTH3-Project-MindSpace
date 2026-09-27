using System;
using System.Collections.Generic;
using UnityEngine;

namespace TherapyGame.Editor
{
    public static class DoorSealGeometry
    {
        public struct Piece
        {
            public string name;public Vector3 center,size;public bool gasket;
            public Piece(string n,Vector3 lo,Vector3 hi,bool seal=false){name=n;center=(lo+hi)*.5f;size=hi-lo;gasket=seal;}
        }
        public static Piece[] Pieces()=>new[]{
            // Continuous lining bridges the indoor architrave to the exterior door stop.
            new Piece("Hinge jamb lining",new Vector3(-3.125f,.018f,-3.370f),new Vector3(-3.055f,2.455f,-3.100f)),
            new Piece("Latch jamb lining",new Vector3(-1.805f,.018f,-3.370f),new Vector3(-1.735f,2.455f,-3.100f)),
            new Piece("Header lining",new Vector3(-3.125f,2.405f,-3.370f),new Vector3(-1.735f,2.475f,-3.100f)),
            new Piece("Threshold sill",new Vector3(-3.055f,.018f,-3.370f),new Vector3(-1.805f,.060f,-2.935f)),
            // The stop overlaps the closed leaf in silhouette but sits behind its back face.
            new Piece("Hinge stop",new Vector3(-3.08f,.049f,-3.370f),new Vector3(-3.02f,2.425f,-3.317f)),
            new Piece("Latch stop",new Vector3(-1.84f,.049f,-3.370f),new Vector3(-1.78f,2.425f,-3.317f)),
            new Piece("Header stop",new Vector3(-3.08f,2.365f,-3.370f),new Vector3(-1.78f,2.425f,-3.317f)),
            new Piece("Threshold stop",new Vector3(-3.08f,.049f,-3.370f),new Vector3(-1.78f,.085f,-3.200f)),
            // A dark, thin gasket removes the bright slit without coplanar surfaces.
            new Piece("Hinge seal",new Vector3(-3.074f,.052f,-3.3735f),new Vector3(-3.023f,2.419f,-3.3705f),true),
            new Piece("Latch seal",new Vector3(-1.837f,.052f,-3.3735f),new Vector3(-1.786f,2.419f,-3.3705f),true),
            new Piece("Header seal",new Vector3(-3.074f,2.368f,-3.3735f),new Vector3(-1.786f,2.419f,-3.3705f),true),
            new Piece("Threshold seal",new Vector3(-3.074f,.052f,-3.3735f),new Vector3(-1.786f,.082f,-3.3705f),true)
        };
        public static Dictionary<string,MindSpaceExteriorGeometry.Batch> Build()
        {
            var wood=new MindSpaceExteriorGeometry.Batch();var seal=new MindSpaceExteriorGeometry.Batch();
            foreach(var p in Pieces())(p.gasket?seal:wood).Box(p.center,p.size);
            return new Dictionary<string,MindSpaceExteriorGeometry.Batch>{{"Oak jamb and stops",wood},{"Recessed weather seal",seal}};
        }
        public static bool SegmentHitsBox(Vector3 from,Vector3 to,Vector3 center,Vector3 half)
        {
            Vector3 p=from-center,d=to-from;float near=0,far=1;
            return Slab(p.x,d.x,half.x,ref near,ref far)&&Slab(p.y,d.y,half.y,ref near,ref far)&&Slab(p.z,d.z,half.z,ref near,ref far);
        }
        private static bool Slab(float p,float d,float h,ref float near,ref float far)
        {
            if(Mathf.Abs(d)<1e-7f)return Mathf.Abs(p)<=h;
            float a=(-h-p)/d,b=(h-p)/d;
            near=Mathf.Max(near,Mathf.Min(a,b));far=Mathf.Min(far,Mathf.Max(a,b));return near<=far;
        }
        public static string Check()
        {
            var pieces=Pieces();var batches=Build();int samples=0,swing=0;
            Vector3 doorCenter=DoorSwingGeometry.Pivot+DoorSwingGeometry.Center,half=DoorSwingGeometry.Size*.5f;
            bool Occluded(Vector3 eye,Vector3 point)
            {
                Vector3 end=eye+(point-eye)*2;
                if(SegmentHitsBox(eye,end,doorCenter,half))return true;
                foreach(var p in pieces)if(SegmentHitsBox(eye,end,p.center,p.size*.5f))return true;
                return false;
            }
            // Near/far standing and seated views, including oblique left/right viewing angles.
            foreach(float x in new[]{-3.35f,-2.43f,-1.50f})foreach(float y in new[]{.70f,1.21f,1.70f,2.05f})foreach(float z in new[]{-2.80f,-1.5f})
            {
                var eye=new Vector3(x,y,z);
                for(int ix=0;ix<=26;ix++)for(int iy=0;iy<=48;iy++)
                {
                    Vector3 point=new Vector3(-3.055f+1.25f*ix/26,.055f+2.35f*iy/48,-3.3735f);
                    samples++;if(!Occluded(eye,point))throw new Exception("Closed door shows daylight from "+x+","+y+","+z+" at opening sample "+ix+","+iy);
                }
            }
            foreach(var batch in batches.Values)for(int i=0;i<batch.vertices.Count;i+=3)for(int angle=0;angle<=420;angle++)
            {
                swing++;if(DoorSwingGeometry.Hits(batch.vertices[i],batch.vertices[i+1],batch.vertices[i+2],DoorSwingGeometry.Pivot,DoorSwingGeometry.Center,half,angle*.25f))
                    throw new Exception("Door intersects new seal at "+angle*.25f);
            }
            // Continuous proof between angle samples: every leaf point has positive local X/Z.
            // For 0..105deg, z'=-x sin(a)+z cos(a) never exceeds the closed leaf's maximum Z.
            float back=doorCenter.z+half.z;
            foreach(var p in pieces)if(p.center.z-p.size.z*.5f-back<.0014f)throw new Exception("Seal lost continuous swing-plane clearance.");
            return "PASS: "+samples+" closed-opening occlusion rays across 24 interior views; "+swing+" new-trim triangle/swing checks; >=1.4mm continuous leaf-plane separation.\n";
        }
    }
}
