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
            Vector3 submerged=water-Vector3.up*.18f,lifted=water+Vector3.up*1.08f;
            if(progress<=LiftEnd)
            {
                float launchTime=progress/LiftEnd;
                // Quick break through the surface, then decelerate into a hang.
                float launch=1-Mathf.Pow(1-launchTime,3);return Vector3.Lerp(submerged,lifted,launch);
            }
            float t=Ease((progress-LiftEnd)/(1-LiftEnd));
            return Vector3.Lerp(lifted,display,t)+Vector3.up*(.36f*4*t*(1-t));
        }
        public static Quaternion Rotation(float progress)
        {
            float t=Ease((Mathf.Clamp01(progress)-LiftEnd)/(1-LiftEnd));
            float flourish=Mathf.Sin(t*Mathf.PI)*(1-t);
            return Quaternion.Slerp(Quaternion.Euler(-72,30,0),Quaternion.Euler(-8,65,-7),t)*Quaternion.Euler(0,flourish*58,flourish*32);
        }
        public static float ShowcaseScale(float seconds)
        {
            float age=seconds-Duration;if(age<0||age>=.5f)return 1;
            return 1+.16f*Mathf.Sin(Mathf.Clamp01(age/.5f)*Mathf.PI)*Mathf.Pow(1-age/.5f,2);
        }
        public static float RodTug(float progress)=>Mathf.Sin(Mathf.Clamp01(progress)/LiftEnd*Mathf.PI)*Mathf.Clamp01(1-progress/LiftEnd);
        public static float PreviewYaw(float seconds)=>55+Mathf.Repeat(seconds*18,360);
    }
}
