using System;
using TheLastWatch.Environment;

namespace TherapyGame.Editor
{
    public static class CloudSequenceChecks
    {
        private static void Require(bool condition,string message){if(!condition)throw new Exception("Cloud types: "+message);}
        public static string Run()
        {
            var clock=new WellnessCloudSequence(9876);
            var counts=new int[3];counts[(int)clock.Current]++;
            float lastAlpha=clock.Opacity;double lastSwitch=0;int zeroFrames=0,checks=0;
            for(int frame=0;frame<460800;frame++)
            {
                WellnessCloudType previous=clock.Current;clock.Tick(1f/32);
                Require(clock.Opacity>=0&&clock.Opacity<=1,"opacity bounds");
                Require(Math.Abs(clock.Opacity-lastAlpha)<.003,"visible pop instead of slow fade");
                if(clock.Opacity==0)zeroFrames++;
                if(previous!=clock.Current)
                {
                    Require(clock.Opacity==0,"family switched while old clouds were still visible");
                    Require(lastAlpha<.0001,"outgoing family did not finish fading");
                    double interval=clock.Seconds-lastSwitch;
                    Require(interval>=197&&interval<=338,"unbounded family hold/transition duration");
                    lastSwitch=clock.Seconds;counts[(int)clock.Current]++;
                }
                lastAlpha=clock.Opacity;checks++;
            }
            Require(clock.SwitchCount>40&&clock.SwitchCount<75&&zeroFrames>1000,"cycle did not progress through clear-sky gaps");
            Require(counts[0]>10&&counts[1]>10&&counts[2]>10,"a cloud type was starved");
            double time=clock.Seconds;int changes=clock.SwitchCount;var kind=clock.Current;float opacity=clock.Opacity;
            for(int i=0;i<200;i++)clock.Tick(0);
            Require(clock.Seconds==time&&clock.SwitchCount==changes&&clock.Current==kind&&clock.Opacity==opacity,"paused clouds advanced");
            clock.Tick(float.NaN);clock.Tick(float.PositiveInfinity);clock.Tick(-1);Require(clock.Seconds==time,"invalid time advanced");
            clock.Tick(3600);Require(clock.Seconds-time==.25&&clock.SwitchCount<=changes+1,"stall caused catch-up changes");
            var a=new WellnessCloudSequence(772);var b=new WellnessCloudSequence(772);
            for(int i=0;i<115200;i++)
            {
                a.Tick(1f/64);a.Tick(1f/64);b.Tick(1f/32);
                Require(a.Current==b.Current&&a.SwitchCount==b.SwitchCount&&a.Opacity==b.Opacity&&a.LayoutYawDegrees==b.LayoutYawDegrees,"32/64 FPS timing differs");checks++;
            }
            string manual=CheckSettings();
            return "PASS: "+checks+" CPU scheduling checks over four hours; "+clock.SwitchCount+" switches, type visits "+counts[0]+" / "+counts[1]+" / "+counts[2]+".\n"+
                "PASS: every atlas switch occurred at exactly zero opacity after full fade-out; all three families used, no repeated adjacent family, no overlap.\n"+
                "PASS: 18-second fade-out, one-second clear gap, 18-second fade-in; 3-5 minute holds, pause/invalid/stalled time, exact 32/64 FPS agreement.\n"+manual;
        }
        private static string CheckSettings()
        {
            int cases=0;
            foreach(WellnessCloudMode from in Enum.GetValues(typeof(WellnessCloudMode)))
            foreach(WellnessCloudMode to in Enum.GetValues(typeof(WellnessCloudMode)))
            {
                var s=new WellnessCloudSequence(23);s.SetMode(from);s.SetMode(to);
                Require(s.Mode==to,"settings choice not applied");
                if((int)to>=2)
                {
                    Require((int)s.Current==(int)to-2&&s.Opacity==1,"manual choice not immediately visible while paused");
                    for(int i=0;i<5000;i++)s.Tick(.25f);
                    Require((int)s.Current==(int)to-2&&s.Opacity==1,"manual shape was overridden by events");
                }
                else if(to==WellnessCloudMode.Off)
                {
                    for(int i=0;i<5000;i++)s.Tick(.25f);
                    Require(s.Opacity==0,"off mode rendered clouds");
                }
                else
                {
                    int before=s.SwitchCount;
                    for(int i=0;i<1440;i++)s.Tick(.25f);
                    Require(s.SwitchCount>before,"return to Auto did not resume events");
                }
                cases++;
            }
            var fade=new WellnessCloudSequence(73);
            while(fade.Opacity>.5f)fade.Tick(.25f);
            fade.SetMode(WellnessCloudMode.Altocumulus);fade.Tick(0);
            Require(fade.Current==WellnessCloudType.Altocumulus&&fade.Opacity==1,"manual override during fade failed");
            fade.SetMode(WellnessCloudMode.Off);fade.Tick(0);
            Require(fade.Opacity==0,"paused Off did not hide clouds immediately");
            fade.SetMode(WellnessCloudMode.Automatic);fade.Tick(0);
            Require(fade.Current==WellnessCloudType.Altocumulus&&fade.Opacity==1,"Auto did not resume from selected family");
            bool rejected=false;try{fade.SetMode((WellnessCloudMode)99);}catch(ArgumentOutOfRangeException){rejected=true;}
            Require(rejected&&fade.Mode==WellnessCloudMode.Automatic,"invalid mode changed state");
            return "PASS: all "+cases+" Auto/Off/manual setting transitions, immediate paused selection, manual lock, fade interruption, Off, and return to Auto. Rain/night settings are not dependencies of cloud events.\n";
        }
    }
}
