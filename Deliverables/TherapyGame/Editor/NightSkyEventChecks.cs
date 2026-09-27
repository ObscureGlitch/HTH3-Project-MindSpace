using System;
using TheLastWatch.Environment;

namespace TherapyGame.Editor
{
    /// <summary>Pure CPU checks. Never enters Play or allocates Unity objects.</summary>
    public static class NightSkyEventChecks
    {
        private static void Require(bool condition,string message){if(!condition)throw new Exception("Night sky: "+message);}
        private static float Dot(WellnessNightSkyEvents.Direction a,WellnessNightSkyEvents.Direction b)=>a.x*b.x+a.y*b.y+a.z*b.z;
        private static int SelectedSeed()
        {
            for(int seed=1;seed<100;seed++)
            {
                var trial=new WellnessNightSkyEvents(seed);trial.Tick(0,true,true,true,true,0);
                if(trial.AuroraThisNight)return seed;
            }
            throw new Exception("No aurora night found in deterministic seed set.");
        }
        public static string Run()
        {
            var sky=new WellnessNightSkyEvents(SelectedSeed());
            for(int i=0;i<2400;i++)sky.Tick(.05f,false,true,true,0);
            Require(sky.EventCount==0&&sky.AuroraStrength==0,"daytime effects appeared");
            int samples=0,peak=0,auroraFrames=0;float previousAurora=0,brightest=0;
            for(int frame=0;frame<72000;frame++)
            {
                sky.Tick(.05f,true,true,true,.4f);int concurrent=0;
                Require(sky.AuroraStrength>=0&&sky.AuroraStrength<=1,"aurora envelope bounds");
                Require(Math.Abs(sky.AuroraStrength-previousAurora)<.0043f,"aurora flashes instead of slow fading");
                previousAurora=sky.AuroraStrength;if(previousAurora>.2f)auroraFrames++;
                for(int j=0;j<WellnessNightSkyEvents.Capacity;j++)
                {
                    var m=sky.Sample(j);if(m.brightness<=0)continue;concurrent++;samples++;
                    brightest=Math.Max(brightest,m.brightness);
                    Require(m.brightness<=1.5001f&&m.tail>=0&&m.tail<=.191f&&m.width>=.000599f&&m.width<=.001001f,"trail bounds");
                    Require(Math.Abs(Dot(m.head,m.head)-1)<.00001f&&Math.Abs(Dot(m.tangent,m.tangent)-1)<.00001f&&Math.Abs(Dot(m.side,m.side)-1)<.00001f,"non-unit sky direction");
                    Require(Math.Abs(Dot(m.head,m.tangent))<.00001f&&Math.Abs(Dot(m.head,m.side))<.00001f,"trail not tangent to sky");
                    Require(m.head.y>.1f&&!float.IsNaN(m.head.x),"invalid or below-horizon meteor");
                }
                peak=Math.Max(peak,concurrent);Require(concurrent<=3,"particle budget exceeded");
            }
            Require(sky.EventCount>100&&sky.EventCount<150&&sky.GroupCount>20&&sky.TripleCount>2&&peak==3,"more frequent single/pair/trio mix missing");
            Require(brightest>1.30f,"meteor brightness was not increased");
            Require(auroraFrames>28000&&auroraFrames<56000,"selected night needs visible curtains and quiet gaps");
            Require(sky.NightCount==1&&sky.AuroraNightCount==1,"one clear night rerolled");
            int count=sky.EventCount;float time=sky.Seconds;
            for(int i=0;i<200;i++)sky.Tick(0,true,true,true,1);
            Require(sky.EventCount==count&&sky.Seconds==time,"paused sky advanced");
            sky.Tick(3600,true,true,true,1);Require(Math.Abs(sky.Seconds-time-.25f)<.001f&&sky.EventCount<=count+1,"resume caused catch-up burst");
            time=sky.Seconds;sky.Tick(float.NaN,true,true,true,0);sky.Tick(float.PositiveInfinity,true,true,true,0);sky.Tick(-1,true,true,true,0);
            Require(sky.Seconds==time,"invalid dt advanced time");
            sky.Tick(.05f,false,true,true,0);Require(sky.AuroraStrength==0,"day/storm failed to hide aurora");
            for(int j=0;j<3;j++)Require(sky.Sample(j).brightness==0,"day/storm failed to hide shooting stars");
            count=sky.EventCount;for(int i=0;i<600;i++)sky.Tick(.05f,true,false,false,0);
            Require(sky.AuroraStrength==0&&sky.EventCount==count,"comfort toggles ignored");

            // Exact binary time steps: event results must not depend on display FPS.
            var a=new WellnessNightSkyEvents(431);var b=new WellnessNightSkyEvents(431);var noAurora=new WellnessNightSkyEvents(431);
            a.Tick(0,true,true,true,0);b.Tick(0,true,true,true,0);noAurora.Tick(0,true,true,false,0);
            for(int i=0;i<38400;i++)
            {
                a.Tick(1f/64,true,true,true,0);a.Tick(1f/64,true,true,true,0);
                b.Tick(1f/32,true,true,true,0);noAurora.Tick(1f/32,true,true,false,0);
                Require(a.EventCount==b.EventCount&&a.GroupCount==b.GroupCount&&a.EventCount==noAurora.EventCount,"frame rate or aurora toggle changes meteor schedule");
                Require(Math.Abs(a.AuroraStrength-b.AuroraStrength)<.00001f,"aurora depends on frame rate");
                for(int j=0;j<3;j++)Require(Math.Abs(a.Sample(j).brightness-b.Sample(j).brightness)<.00001f&&Math.Abs(b.Sample(j).brightness-noAurora.Sample(j).brightness)<.00001f,"trail motion depends on frame rate");
            }
            return "PASS: 3,600-second deterministic clear-night simulation; "+samples+" active trail samples, "+count+" events, "+sky.GroupCount+" groups, "+sky.TripleCount+" trios; peak "+peak+" trails.\n"+
                "PASS: normalized directions, horizon clearance, bounded brightness, slow aurora fades and quiet intervals.\n"+
                "PASS: daytime/storm gating, independent toggles, invalid/stalled dt, pause/no catch-up, 32/64 FPS agreement.\n"+CheckLottery();
        }
        private static string CheckLottery()
        {
            // Weather, toggling aurora, camera heading and meteor activity must
            // never count as a new dusk or change the chosen composition.
            var clear=new WellnessNightSkyEvents(9351);var variable=new WellnessNightSkyEvents(9351);
            var directions=new bool[8];var palettes=new bool[3];int overhead=0,displays=0;
            for(int night=0;night<400;night++)
            {
                clear.Tick(.25f,false,false,true,true,0);variable.Tick(.25f,false,false,false,false,2);
                for(int frame=0;frame<720;frame++)
                {
                    bool visible=frame%77>18,enabled=frame%91>20;
                    clear.Tick(.25f,true,true,true,true,0);
                    variable.Tick(.25f,true,visible,false,enabled,frame*.031f);
                    Require(clear.NightCount==night+1&&variable.NightCount==night+1,"cloud/toggle counted as dusk");
                    Require(clear.AuroraThisNight==variable.AuroraThisNight&&clear.AuroraAzimuth==variable.AuroraAzimuth&&clear.AuroraPhase==variable.AuroraPhase,"clouds/settings/head tracking moved or rerolled aurora");
                    if(!clear.AuroraThisNight)Require(clear.AuroraStrength==0&&variable.AuroraStrength==0,"quiet night displayed aurora");
                    if(!visible||!enabled)Require(variable.AuroraStrength==0,"storm/comfort gating failed");
                    else Require(clear.AuroraStrength==variable.AuroraStrength,"aurora restarted after cloud/toggle");
                    if(clear.AuroraDisplayCount!=displays)
                    {
                        displays=clear.AuroraDisplayCount;
                        Require(clear.AuroraTilt>=.12f&&clear.AuroraTilt<=1.04001f&&clear.AuroraSpread>=.95f&&clear.AuroraSpread<=1.40001f,"shape bounds");
                        Require(clear.AuroraPalette>=0&&clear.AuroraPalette<=1&&clear.AuroraAzimuth>=0&&clear.AuroraAzimuth<Math.PI*2+.00001,"palette/azimuth bounds");
                        directions[Math.Min(7,(int)(clear.AuroraAzimuth/(Math.PI*2)*8))]=true;
                        palettes[Math.Min(2,(int)(clear.AuroraPalette*3))]=true;
                        if(clear.AuroraTilt>.5f)overhead++;
                    }
                }
            }
            Require(clear.AuroraNightCount>130&&clear.AuroraNightCount<230,"45% nightly selection needs both aurora and quiet nights");
            Require(Array.TrueForAll(directions,v=>v)&&Array.TrueForAll(palettes,v=>v),"random directions or colors missing");
            Require(overhead>displays*.55f&&overhead<displays*.85f,"overhead and horizon variety missing");
            return "PASS: 400 simulated nights: "+clear.AuroraNightCount+" selected, "+(400-clear.AuroraNightCount)+" quiet; "+displays+" compositions, "+overhead+" overhead, all azimuth octants and palette ranges.\n"+
                "PASS: storms, comfort toggles and moving the camera do not reroll the nightly decision or reposition an active aurora.\n";
        }
    }
}
