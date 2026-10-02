using System;
using TheLastWatch.Environment;
public static class PopulationTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static double Distance(double x, double z, KoiSwimMotion m) => Math.Sqrt(Math.Pow(x-m.X,2)+Math.Pow(z-m.Z,2));
    static double Wrap(double angle) => (angle % (Math.PI*2) + Math.PI*2) % (Math.PI*2);
    public static void Main()
    {
        int restingFrames=0, movingFrames=0;
        double maximumStep=0, minimumGroupGap=100;
        for(int seed=0;seed<32;seed++) for(int lane=0;lane<KoiSchoolLayout.RouteCount;lane++) for(int count=2;count<=4;count++)
        {
            double r=KoiSchoolLayout.FirstLane+lane*KoiSchoolLayout.LaneSpacing;
            var group=new KoiSwimMotion[count];
            for(int i=0;i<count;i++) group[i]=new KoiSwimMotion(seed,r,6.7,5.7,i*Math.PI*2/count);
            var same=new KoiSwimMotion(seed,r,6.7,5.7);
            for(int frame=0;frame<12000;frame++)
            {
                same.Step(1.0/30);
                for(int i=0;i<count;i++)
                {
                    var m=group[i]; double x=m.X,z=m.Z; m.Step(1.0/30);
                    double distance=Distance(x,z,m);maximumStep=Math.Max(distance,maximumStep);
                    Check(distance<.012,"Teleport");
                    Check(Math.Abs(Math.Sqrt(Math.Pow(m.X/6.7,2)+Math.Pow(m.Z/5.7,2))-r)<1e-10,"Route left lane");
                    Check(!double.IsNaN(m.YawDegrees)&&m.Speed>=.012-1e-8&&m.Speed<=.29,"Invalid motion state");
                    if(m.Speed<.035)restingFrames++;if(m.Speed>.1)movingFrames++;
                }
                Check(group[0].X==same.X&&group[0].Z==same.Z,"Seed not deterministic");
                for(int i=0;i<count;i++) for(int j=i+1;j<count;j++)
                {
                    double gap=Distance(group[i].X,group[i].Z,group[j]);minimumGroupGap=Math.Min(minimumGroupGap,gap);
                    Check(gap>1.28,"Fish on shared route caught up or overlapped");
                    double phase=Wrap(Math.Atan2(group[j].Z/5.7,group[j].X/6.7)-Math.Atan2(group[i].Z/5.7,group[i].X/6.7));
                    Check(Math.Abs(phase-(j-i)*Math.PI*2/count)<1e-7,"Route phase spacing drifted");
                    Check(group[i].Resting==group[j].Resting,"Route rest schedules diverged");
                }
            }
            foreach(var m in group)
            {
                double x=m.X,z=m.Z;m.Step(0);m.Step(-1);m.Step(double.NaN);m.Step(double.PositiveInfinity);
                Check(m.X==x&&m.Z==z,"Pause changed position");
                m.Step(999);Check(Distance(x,z,m)<.035,"Stall teleported fish");
            }
        }
        Check(restingFrames>1000&&movingFrames>1000,"Missing rest or swim intervals");
        var rolledCounts=new int[16];
        for(int seed=0;seed<65536;seed++)
        {
            int count=KoiSchoolLayout.RollCount(new Random(seed));
            Check(count>=15&&count<=30,"Population roll out of range");
            Check(count==KoiSchoolLayout.RollCount(new Random(seed)),"Count seed not deterministic");
            rolledCounts[count-15]++;
        }
        foreach(int frequency in rolledCounts) Check(frequency>0,"An inclusive endpoint or intermediate count was never rolled");
        for(int seed=0;seed<4096;seed++) for(int count=1;count<=KoiSchoolLayout.MaximumFish;count++)
        {
            var population=KoiSchoolLayout.Create(new Random(seed),count,8);
            var same=KoiSchoolLayout.Create(new Random(seed),count,8);
            Check(population.Length==count,"Wrong population length");
            var varieties=new int[8];var bands=new bool[count];var routes=new int[9];
            float low=2,high=0;
            for(int i=0;i<count;i++)
            {
                var fish=population[i];Check(fish.Variety>=0&&fish.Variety<8,"Invalid variety index");varieties[fish.Variety]++;
                Check(fish.Scale>=KoiSchoolLayout.MinimumScale&&fish.Scale<=KoiSchoolLayout.MaximumScale,"Oversize fish");
                int band=Math.Min(count-1,(int)((fish.Scale-KoiSchoolLayout.MinimumScale)/(KoiSchoolLayout.MaximumScale-KoiSchoolLayout.MinimumScale)*count));
                Check(!bands[band],"Size distribution lost its spread");bands[band]=true;
                low=Math.Min(low,fish.Scale);high=Math.Max(high,fish.Scale);
                Check(fish.Scale==same[i].Scale&&fish.Variety==same[i].Variety&&fish.MotionSeed==same[i].MotionSeed&&fish.PhaseOffset==same[i].PhaseOffset&&fish.Radius==same[i].Radius,"Setup seed not deterministic");
                Check(fish.Radius>=.159f&&fish.Radius<=.601f,"Route outside validated pond");
                int route=(int)Math.Round((fish.Radius-KoiSchoolLayout.FirstLane)/KoiSchoolLayout.LaneSpacing);routes[route]++;
                int slot=i%KoiSchoolLayout.RouteCount;
                int occupants=count/KoiSchoolLayout.RouteCount+(slot<count%KoiSchoolLayout.RouteCount?1:0);
                Check(Math.Abs(fish.PhaseOffset-(i/KoiSchoolLayout.RouteCount)*Math.PI*2/occupants)<1e-12,"Wrong route phase");
                if(i>=KoiSchoolLayout.RouteCount)
                {
                    var other=population[i-KoiSchoolLayout.RouteCount];
                    Check(fish.MotionSeed==other.MotionSeed&&fish.Radius==other.Radius,"Route schedule mismatch");
                    Check(Math.Abs(fish.PhaseOffset-other.PhaseOffset-Math.PI*2/occupants)<1e-12,"Incorrect group spacing");
                }
            }
            foreach(int n in routes) Check(n>=count/9&&n<=(count+8)/9&&n<=4,"Unbalanced or crowded route");
            foreach(int n in varieties) Check(n>=count/8&&n<=(count+7)/8,"Unbalanced color selection");
            if(count>=15) Check(low<.912f&&high>1.068f,"Subtle size range not visible");
        }
        Check(KoiSchoolLayout.Create(new Random(1),99,8).Length==30,"Count cap failed");
        Check(KoiSchoolLayout.Create(new Random(1),0,8).Length==1,"Fixed-count minimum failed");
        Console.WriteLine("PASS: 32 seeds x 9 routes x groups of 2, 3 and 4 fish x 400 seconds. Bounded movement, rest/glide, deterministic spacing, pause/stall safety. Maximum frame step: "+maximumStep+" m; minimum same-route center gap: "+minimumGroupGap+" m.");
        Console.WriteLine("PASS: 65,536 population rolls, all counts 15 through 30 inclusive reached. Frequencies: "+string.Join(", ",rolledCounts));
        Console.WriteLine("PASS: 4,096 seeds x counts 1-30; balanced colors and route occupancy, valid indices, subtle 90%-108% size distribution.");
    }
}
