using System;
using TheLastWatch.Environment;

public static class AuroraSummonChecks
{
    private static void Require(bool condition,string message)
    { if(!condition)throw new Exception("Aurora summon: "+message); }
    private static void Advance(WellnessNightSkyEvents sky,float seconds,bool visible=true,bool enabled=true)
    { for(int i=0;i<(int)(seconds*32);i++)sky.Tick(1f/32,true,visible,true,enabled,0); }
    private static WellnessNightSkyEvents QuietNight()
    {
        for(int seed=1;seed<100;seed++)
        {
            var sky=new WellnessNightSkyEvents(seed);sky.Tick(0,true,true,true,true,0);
            if(!sky.AuroraThisNight)return sky;
        }
        throw new Exception("No quiet-night seed found.");
    }
    public static string Run()
    {
        var day=new WellnessNightSkyEvents(3);
        Require(!day.SummonAurora(0),"must establish night first");
        var sky=QuietNight();int nights=sky.NightCount,selected=sky.AuroraNightCount;
        Require(sky.SummonAurora(float.NaN),"quiet night must allow one occurrence");
        Require(sky.AuroraSummoned&&!sky.AuroraThisNight,"manual action must not rewrite natural nightly decision");
        float phase=sky.AuroraPhase,azimuth=sky.AuroraAzimuth;
        Require(!float.IsNaN(azimuth)&&azimuth>=0&&azimuth<Math.PI*2,"invalid viewer angle leaked into composition");
        Require(!sky.SummonAurora(2)&&sky.AuroraPhase==phase,"repeat click restarted or moved curtains");
        sky.Tick(0,true,true,true,true,0);Require(sky.AuroraStrength==0,"initial frame should fade from zero");
        float previous=0;
        for(int i=0;i<32*7;i++)
        {
            sky.Tick(1f/32,true,true,true,true,i);
            Require(sky.AuroraStrength>=previous&&sky.AuroraStrength-previous<.008,"fade-in must be smooth");
            Require(sky.AuroraPhase==phase&&sky.AuroraAzimuth==azimuth,"looking around moved a summoned curtain");
            previous=sky.AuroraStrength;
        }
        Require(sky.AuroraStrength>.99f,"summoned curtain did not appear within six seconds");
        float remaining=sky.SummonSecondsRemaining;
        for(int i=0;i<100;i++)sky.Tick(0,true,true,true,true,0);
        Require(sky.SummonSecondsRemaining==remaining,"paused occurrence kept expiring");
        Advance(sky,3,false);
        Require(sky.AuroraStrength==0&&sky.AuroraSummoned,"storm gating lost the occurrence");
        sky.Tick(0,true,true,true,true,0);
        Require(sky.AuroraStrength>.99f&&sky.AuroraPhase==phase,"clearing changed the composition");
        Advance(sky,90);previous=sky.AuroraStrength;
        for(int i=0;i<32*20;i++)
        {
            sky.Tick(1f/32,true,true,true,true,0);
            Require(sky.AuroraStrength<=previous+.0001f&&previous-sky.AuroraStrength<.0025f,"fade-out flashes");
            previous=sky.AuroraStrength;
        }
        Require(!sky.AuroraSummoned&&sky.AuroraStrength==0&&sky.SummonSecondsRemaining==0,"occurrence must expire at 120 seconds");
        Require(sky.NightCount==nights&&sky.AuroraNightCount==selected,"summon counted as another dusk");
        Require(sky.SummonAurora(float.PositiveInfinity),"expired occurrence must be summonable again");
        Advance(sky,8);sky.Tick(0,true,true,true,false,0);
        Require(!sky.AuroraSummoned&&sky.AuroraStrength==0,"off toggle must cancel occurrence");
        Require(sky.SummonAurora(-9),"new occurrence missing");
        sky.Tick(0,false,false,true,true,0);
        Require(!sky.AuroraSummoned&&sky.AuroraStrength==0,"dawn must clear occurrence");

        // A manual display has its own RNG. It cannot change future nightly rolls,
        // scheduled compositions, meteor trajectories or auto timing.
        var natural=new WellnessNightSkyEvents(9351);var manual=new WellnessNightSkyEvents(9351);
        for(int night=0;night<40;night++)
        {
            natural.Tick(0,false,false,true,true,0);manual.Tick(0,false,false,true,true,0);
            natural.Tick(0,true,true,true,true,0);manual.Tick(0,true,true,true,true,0);
            Require(natural.AuroraThisNight==manual.AuroraThisNight,"manual action altered nightly lottery");
            if(night%2==0)Require(manual.SummonAurora(1.2f),"summon failed on a new night");
            for(int frame=0;frame<32*240;frame++)
            {
                natural.Tick(1f/32,true,true,true,true,.2f);manual.Tick(1f/32,true,true,true,true,.2f);
                Require(natural.EventCount==manual.EventCount&&natural.GroupCount==manual.GroupCount,"meteor schedule changed");
                Require(natural.AuroraDisplayCount==manual.AuroraDisplayCount,"natural schedule changed");
                for(int i=0;i<3;i++)Require(natural.Sample(i).brightness==manual.Sample(i).brightness,"meteor sampling changed");
                if(!manual.AuroraSummoned)
                    Require(natural.AuroraPhase==manual.AuroraPhase&&natural.AuroraAzimuth==manual.AuroraAzimuth,"future natural composition changed");
            }
        }
        // Summoning an already-visible natural display must not teleport its shape.
        var active=new WellnessNightSkyEvents(1);
        for(int tries=0;tries<1000&&active.AuroraStrength<.4f;tries++)
        {
            if(tries%200==0)active.Tick(0,false,false,true,true,0);
            active.Tick(.25f,true,true,true,true,0);
        }
        Require(active.AuroraStrength>=.4f,"test needs an active natural display");
        phase=active.AuroraPhase;float strength=active.AuroraStrength;
        Require(active.SummonAurora(4),"active display summon failed");
        active.Tick(0,true,true,true,true,0);
        Require(active.AuroraPhase==phase&&Math.Abs(active.AuroraStrength-strength)<.00001f,"active display popped when summoned");

        var a=QuietNight();var b=QuietNight();a.SummonAurora(2);b.SummonAurora(2);
        for(int i=0;i<32*121;i++)
        {
            a.Tick(1f/64,true,true,true,true,0);a.Tick(1f/64,true,true,true,true,0);
            b.Tick(1f/32,true,true,true,true,0);
            Require(a.AuroraStrength==b.AuroraStrength&&a.AuroraSummoned==b.AuroraSummoned,"summon depends on frame rate");
        }
        return "PASS: manual aurora on quiet/active nights; six-second fade-in, two-minute expiry, smooth fade-out, repeat-click guard, pause, dawn/off cancellation, storm visibility, invalid yaw, 32/64 FPS agreement.\n"+
            "PASS: 40 paired nights preserve natural nightly decisions, automatic scheduling/compositions and meteor sampling with manual occurrences.\n";
    }
}
