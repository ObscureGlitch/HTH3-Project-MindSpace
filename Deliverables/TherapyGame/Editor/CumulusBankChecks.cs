using System;
using TheLastWatch.Environment;

namespace TherapyGame.Editor
{
    public static class CumulusBankChecks
    {
        private static void Require(bool ok,string reason){if(!ok)throw new Exception("Cumulus bank: "+reason);}
        public static string Run()
        {
            Require(WellnessCumulusLife.Heating(0)==0&&WellnessCumulusLife.Heating(6)==0&&WellnessCumulusLife.Heating(20)==0,"night-time heating");
            Require(WellnessCumulusLife.Heating(14)==1&&WellnessCumulusLife.Heating(10)>0&&WellnessCumulusLife.Heating(18)<1,"daytime development");
            var growing=new WellnessCumulusLife(0);var fading=new WellnessCumulusLife(14);
            for(int i=0;i<36000;i++)
            {
                float before=growing.Development;growing.Tick(1f/60,14);fading.Tick(1f/60,0);
                Require(growing.Development>=before&&growing.Development<=1,"smooth monotonic development");
                Require(fading.Development>=0&&fading.Development<=1,"bounded evening dissipation");
            }
            Require(growing.Development>.999f&&fading.AutomaticOpacity<.0001f,"growth / dissipation did not settle");
            float still=growing.Development;growing.Tick(0,0);growing.Tick(float.NaN,0);
            Require(growing.Development==still,"paused or invalid update advanced development");
            var a=new WellnessCumulusLife(0);var b=new WellnessCumulusLife(0);
            for(int i=0;i<3600;i++){a.Tick(1f/30,14);for(int j=0;j<4;j++)b.Tick(1f/120,14);}
            Require(Math.Abs(a.Development-b.Development)<.0002,"frame-rate-dependent development");
            float azimuth=40;for(int i=0;i<3600;i++)azimuth=WellnessCumulusLife.AdvanceAzimuth(azimuth,2.4f,1f/60);
            float expected=2.4f*60/WellnessCumulusLife.Distance*180/(float)Math.PI;
            Require(Math.Abs((azimuth-40)-expected)<.015,"real-seconds wind drift");
            Require(WellnessCumulusLife.AdvanceAzimuth(40,0,1)==40,"zero wind");
            Require(WellnessCumulusLife.AdvanceAzimuth(40,4,0)==40,"paused wind");
            int single=0,other=0,hidden=0;
            for(int i=0;i<16;i++)
            {
                if(WellnessCumulusLife.CardVisible(true,i,true,1))single++;
                if(WellnessCumulusLife.CardVisible(false,i,true,1))other++;
                if(WellnessCumulusLife.CardVisible(true,i,true,0))hidden++;
                Require(!WellnessCumulusLife.CardVisible(false,i,false,1),"originally disabled card became visible");
            }
            Require(single==1&&other==16&&hidden==0,"one bank only / restore other family / Off");
            return "PASS: daytime growth, smooth evening dissipation, 30/120 FPS agreement, pause, calm real-seconds wind, exactly one puffy card, no hidden-card ghosts and restoring other families.\n";
        }
    }
}
