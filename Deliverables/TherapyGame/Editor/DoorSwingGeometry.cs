using UnityEngine;

namespace TherapyGame.Editor
{
    // Room-local measurements. The pin is in front of the deepest exterior reveal,
    // not recessed at the old indoor trim. Closed leaf still fits the same opening.
    public static class DoorSwingGeometry
    {
        public static readonly Vector3 Pivot=new Vector3(-3.045f,0,-3.465f);
        public static readonly Vector3 Center=new Vector3(.615f,1.225f,.0575f);
        public static readonly Vector3 Size=new Vector3(1.22f,2.32f,.065f);
        public static Vector3 Rotate(Vector3 p,float degrees)
        {
            float a=degrees*Mathf.PI/180,c=Mathf.Cos(a),s=Mathf.Sin(a);
            return new Vector3(p.x*c+p.z*s,p.y,-p.x*s+p.z*c);
        }
        public static bool Hits(Vector3 a,Vector3 b,Vector3 c,Vector3 pivot,Vector3 center,Vector3 half,float yaw)
        {
            a=Rotate(a-pivot,-yaw)-center;b=Rotate(b-pivot,-yaw)-center;c=Rotate(c-pivot,-yaw)-center;
            if(Separate(Vector3.right,a,b,c,half)||Separate(Vector3.up,a,b,c,half)||Separate(Vector3.forward,a,b,c,half))return false;
            Vector3 e0=b-a,e1=c-b,e2=a-c;
            if(Separate(Vector3.Cross(e0,e1),a,b,c,half))return false;
            return !EdgeSeparates(e0,a,b,c,half)&&!EdgeSeparates(e1,a,b,c,half)&&!EdgeSeparates(e2,a,b,c,half);
        }
        private static bool EdgeSeparates(Vector3 edge,Vector3 a,Vector3 b,Vector3 c,Vector3 half)=>
            Separate(Vector3.Cross(edge,Vector3.right),a,b,c,half)||Separate(Vector3.Cross(edge,Vector3.up),a,b,c,half)||Separate(Vector3.Cross(edge,Vector3.forward),a,b,c,half);
        private static bool Separate(Vector3 axis,Vector3 a,Vector3 b,Vector3 c,Vector3 half)
        {
            if(axis.sqrMagnitude<1e-14f)return false;
            float p=Vector3.Dot(a,axis),q=Vector3.Dot(b,axis),r=Vector3.Dot(c,axis);
            float radius=half.x*Mathf.Abs(axis.x)+half.y*Mathf.Abs(axis.y)+half.z*Mathf.Abs(axis.z);
            return Mathf.Min(p,Mathf.Min(q,r))>radius||Mathf.Max(p,Mathf.Max(q,r)) < -radius;
        }
    }
}
