using UnityEngine;

namespace TheLastWatch.UI
{
    public static class KoiFishingCatchMotion
    {
        public const float Duration=1.8f,LiftEnd=.36f;
        public static float Ease(float t){t=Mathf.Clamp01(t);return t*t*t*(t*(t*6-15)+10);}
        public static Vector3 Position(Vector3 water,Vector3 display,float progress)
        {
            progress=float.IsFinite(progress)?Mathf.Clamp01(progress):0;
            Vector3 submerged=water-Vector3.up*.18f,lifted=water+Vector3.up*.72f;
            if(progress<=LiftEnd)return Vector3.Lerp(submerged,lifted,Ease(progress/LiftEnd));
            float t=Ease((progress-LiftEnd)/(1-LiftEnd));
            return Vector3.Lerp(lifted,display,t)+Vector3.up*(.22f*4*t*(1-t));
        }
        public static Quaternion Rotation(float progress)
        {
            float t=Ease((Mathf.Clamp01(progress)-LiftEnd)/(1-LiftEnd));
            return Quaternion.Slerp(Quaternion.Euler(-72,30,0),Quaternion.Euler(-8,65,-7),t);
        }
        public static float PreviewYaw(float seconds)=>55+Mathf.Repeat(seconds*18,360);
    }
}
