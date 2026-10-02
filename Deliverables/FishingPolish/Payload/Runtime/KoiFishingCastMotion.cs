using UnityEngine;

namespace TheLastWatch.UI
{
    // Camera-local first-person motion, evaluated from simulation time rather than
    // coroutines or frame-dependent interpolation. No camera transform is changed.
    public readonly struct KoiFishingCastPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float Flight;
        public KoiFishingCastPose(Vector3 position,Vector3 angles,float flight)
        {Position=position;Rotation=Quaternion.Euler(angles);Flight=flight;}
        public KoiFishingCastPose(Vector3 position,Quaternion rotation,float flight)
        {Position=position;Rotation=rotation;Flight=flight;}
    }
    public static class KoiFishingCastMotion
    {
        public const float Duration=1.6f,Release=.42f,Forward=.70f;
        static readonly Vector3 RestPosition=new Vector3(.35f,-.35f,.68f),RestAngles=new Vector3(58,-10,-4);
        static readonly Vector3 BackPosition=new Vector3(.43f,-.23f,.64f),BackAngles=new Vector3(-28,12,-17);
        static readonly Vector3 ForwardPosition=new Vector3(.30f,-.39f,.86f),ForwardAngles=new Vector3(94,-17,7);
        // Quintic easing keeps velocity and acceleration continuous at each phase.
        static float Ease(float t)=>KoiFishingCatchMotion.Ease(t);
        public static KoiFishingCastPose Sample(float progress)
        {
            progress=float.IsFinite(progress)?Mathf.Clamp01(progress):0;
            Vector3 position;Quaternion rotation;
            if(progress<=Release)
            {float t=Ease(progress/Release);position=Vector3.Lerp(RestPosition,BackPosition,t);rotation=Quaternion.Slerp(Quaternion.Euler(RestAngles),Quaternion.Euler(BackAngles),t);}
            else if(progress<=Forward)
            {float t=Ease((progress-Release)/(Forward-Release));position=Vector3.Lerp(BackPosition,ForwardPosition,t);rotation=Quaternion.Slerp(Quaternion.Euler(BackAngles),Quaternion.Euler(ForwardAngles),t);}
            else
            {float t=Ease((progress-Forward)/(1-Forward));position=Vector3.Lerp(ForwardPosition,RestPosition,t);rotation=Quaternion.Slerp(Quaternion.Euler(ForwardAngles),Quaternion.Euler(RestAngles),t);}
            return new KoiFishingCastPose(position,rotation,Mathf.Clamp01((progress-Release)/(1-Release)));
        }
        public static Vector3 FloatPosition(Vector3 attachedTip,Vector3 launchTip,Vector3 water,KoiFishingCastPose pose)
        {
            if(pose.Flight<=0)return attachedTip;
            // A continuous flight arc starts at the exact release pose even when a
            // slow frame crosses the release point. It ends precisely on the water.
            float height=Mathf.Clamp(Vector3.Distance(launchTip,water)*.20f,.7f,1.4f);
            return Vector3.Lerp(launchTip,water,pose.Flight)+Vector3.up*(4*pose.Flight*(1-pose.Flight)*height);
        }
        public static bool CanBeginCast(bool freshClick,bool controlsReady,bool uiBlocked,bool cursorReleased,bool seated,bool otherActivity)
            =>freshClick&&controlsReady&&!uiBlocked&&!cursorReleased&&!seated&&!otherActivity;
    }
}
