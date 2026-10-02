using System;
using UnityEngine;

namespace TheLastWatch.UI
{
    // Pure, deterministic simulation: no input, scene, file, or provider access.
    public sealed class KoiFishingRound
    {
        public enum Phase { Casting,Waiting,Bite,Reeling,Caught,Escaped,Cancelled }
        public const float BarWidth=.27f;
        public Phase State {get;private set;}=Phase.Casting;
        public float Bar {get;private set;}=.36f;
        public float Fish {get;private set;}=.50f;
        public float Progress {get;private set;}=.30f;
        public float CastFraction=>Mathf.Clamp01(clock/KoiFishingCastMotion.Duration);
        public float ElapsedSeconds {get;private set;}
        public float Velocity {get;private set;}
        public float Accuracy=>reelTime<=0?0:Mathf.Clamp01(trackedTime/reelTime);
        public float Flow=>Mathf.Clamp01(flowTime/2);
        public float BiteRemaining=>State==Phase.Bite?Mathf.Clamp01(1-clock/4):0;
        public bool PerfectHook {get;private set;}
        public bool Surging=>State==Phase.Reeling&&surgeClock>0&&surgeClock<.8f;
        public string Intent=>Surging?"Surge! Follow the current":target>Fish+.1f?"Rising":target<Fish-.1f?"Diving":"Holding steady";
        public bool InBar=>Fish>=Bar&&Fish<=Bar+BarWidth;
        public bool Finished=>State==Phase.Caught||State==Phase.Escaped||State==Phase.Cancelled;
        readonly System.Random random;
        readonly float wait,difficulty;
        readonly int style,rank;
        float clock,target=.5f,targetClock,reelTime,trackedTime,flowTime,surgeClock;
        bool claimed;
        public KoiFishingRound(int seed,float difficulty=1,int style=0,int rank=0)
        {random=new System.Random(seed);wait=Range(1.8f,3.8f);this.difficulty=Mathf.Clamp(difficulty,.65f,1.15f);this.style=Mathf.Clamp(style,0,5);this.rank=Mathf.Clamp(rank,0,5);}
        float Range(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
        public void Step(float seconds,bool held,bool pressed)
        {
            if(!float.IsFinite(seconds)||seconds<=0||Finished)return;
            float remaining=Mathf.Min(seconds,.25f);
            // Bounded substeps keep the same difficulty at low/high frame rates.
            while(remaining>0){float dt=Mathf.Min(remaining,1f/120);remaining-=dt;Advance(dt,held,pressed);pressed=false;}
        }
        void Advance(float dt,bool held,bool pressed)
        {
            if(Finished)return;
            ElapsedSeconds+=dt;
            clock+=dt;
            if(State==Phase.Casting){if(clock>=KoiFishingCastMotion.Duration){State=Phase.Waiting;clock=0;}return;}
            if(State==Phase.Waiting){if(clock>=wait){State=Phase.Bite;clock=0;}return;}
            if(State==Phase.Bite)
            {
                if(pressed){PerfectHook=clock<=.65f;Progress=PerfectHook?.42f:.30f;State=Phase.Reeling;clock=0;targetClock=0;return;}
                if(clock>4){State=Phase.Escaped;}return;
            }
            reelTime+=dt;targetClock-=dt;surgeClock=Mathf.Max(0,surgeClock-dt);
            if(targetClock<=0)
            {
                target=Range(.13f,.87f);targetClock=Range(.9f,1.8f);
                if(style==1||style==5){surgeClock=1.2f;targetClock=Range(1.5f,2.3f);}
                if(style==3)targetClock=Range(.6f,1.05f);
            }
            if(style==2)target=.5f+Mathf.Sin(reelTime*1.25f)*.28f;
            if(style==4)target=.5f+Mathf.Sin(reelTime*.86f)*.36f;
            float speed=(.16f+.04f*difficulty+rank*.015f)*(Surging?1.5f:1);
            Fish=Mathf.MoveTowards(Fish,target,dt*speed);
            Velocity=Mathf.Clamp((Velocity+(held?1.7f:-1.35f)*dt)*Mathf.Exp(-2.2f*dt),-.72f,.72f);
            float next=Bar+Velocity*dt;Bar=Mathf.Clamp(next,0,1-BarWidth);
            if(next!=Bar)Velocity=-Velocity*.12f;
            if(InBar){trackedTime+=dt;flowTime=Mathf.Min(2,flowTime+dt);}else flowTime=Mathf.Max(0,flowTime-dt*1.8f);
            Progress=Mathf.Clamp01(Progress+dt*(InBar?.14f+.04f*Flow:(reelTime<1?0:-.08f)));
            if(Progress>=1)State=Phase.Caught;
            else if(Progress<=0||reelTime>=50)State=Phase.Escaped;
        }
        public bool TryClaim(){if(State!=Phase.Caught||claimed)return false;claimed=true;return true;}
        public void Cancel(){if(!Finished)State=Phase.Cancelled;}
    }

    public static class KoiFishingWater
    {
        public static float Radius(Vector3 p,Vector3 center,Vector2 radii)
        {float x=(p.x-center.x)/Mathf.Max(.01f,radii.x),z=(p.z-center.z)/Mathf.Max(.01f,radii.y);return Mathf.Sqrt(x*x+z*z);}
        public static bool Nearby(Vector3 eye,Vector3 center,Vector2 radii)
            =>radii.x>0&&radii.y>0&&eye.y-center.y>.1f&&eye.y-center.y<4.5f&&Radius(eye,center,radii)<=1.48f;
        public static bool Candidate(Vector3 eye,Vector3 forward,Vector3 center,Vector2 radii,int option,out Vector3 target)
        {
            target=center;if(!Nearby(eye,center,radii))return false;
            forward.y=0;if(forward.sqrMagnitude<.01f)forward=center-eye;forward.y=0;forward.Normalize();
            Vector3 toCenter=center-eye;toCenter.y=0;
            if(Radius(eye,center,radii)>1&&Vector3.Dot(forward,toCenter.normalized)<.12f)return false;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            float side=option==0?0:option%2==1?1:-1;
            target=eye+forward*(option<3?3.2f:1.6f)+right*side*(option<3?2.0f:3.0f);target.y=center.y+.055f;
            Vector3 delta=target-center;float radius=Radius(target,center,radii);
            if(radius>.82f){delta.x*=.82f/radius;delta.z*=.82f/radius;target=center+delta;}
            return Radius(target,center,radii)<.84f;
        }
    }
}
