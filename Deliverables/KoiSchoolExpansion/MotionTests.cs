using System;
using TheLastWatch.Environment;
public static class MotionTests
{
    static void Check(bool v,string message){if(!v)throw new Exception(message);}
    public static void Main()
    {
        int restingFrames=0,movingFrames=0;double maximumStep=0;
        double minimumPairGap=100;
        for(int seed=0;seed<128;seed++)for(int lane=0;lane<KoiSchoolLayout.RouteCount;lane++)
        {
            double r=KoiSchoolLayout.FirstLane+lane*KoiSchoolLayout.LaneSpacing;var motion=new KoiSwimMotion(seed,r,6.7,5.7);
            var same=new KoiSwimMotion(seed,r,6.7,5.7);
            var partner=new KoiSwimMotion(seed,r,6.7,5.7,Math.PI);
            for(int frame=0;frame<12000;frame++)
            {
                double x=motion.X,z=motion.Z;motion.Step(1.0/30);same.Step(1.0/30);partner.Step(1.0/30);
                double distance=Math.Sqrt(Math.Pow(x-motion.X,2)+Math.Pow(z-motion.Z,2));maximumStep=Math.Max(distance,maximumStep);
                Check(distance<.011,"Teleport");Check(Math.Abs(Math.Sqrt(Math.Pow(motion.X/6.7,2)+Math.Pow(motion.Z/5.7,2))-r)<1e-10,"Route left lane");
                Check(motion.X==same.X&&motion.Z==same.Z,"Seed not deterministic");
                double gap=Math.Sqrt(Math.Pow(motion.X-partner.X,2)+Math.Pow(motion.Z-partner.Z,2));minimumPairGap=Math.Min(minimumPairGap,gap);
                Check(gap>1.8,"Paired fish caught up or overlapped");
                Check(Math.Abs(motion.X+partner.X)<1e-7&&Math.Abs(motion.Z+partner.Z)<1e-7,"Paired phase spacing drifted");
                Check(!double.IsNaN(motion.YawDegrees)&&motion.Speed>=.012-1e-8&&motion.Speed<=.29,"Invalid state");
                if(motion.Speed<.035)restingFrames++;if(motion.Speed>.1)movingFrames++;
            }
            double stillX=motion.X,stillZ=motion.Z;motion.Step(0);motion.Step(-1);motion.Step(double.NaN);
            Check(motion.X==stillX&&motion.Z==stillZ,"Pause changed position");
            motion.Step(999);Check(Math.Sqrt(Math.Pow(motion.X-stillX,2)+Math.Pow(motion.Z-stillZ,2))<.031,"Stall teleported fish");
        }
        Check(restingFrames>1000&&movingFrames>1000,"Missing rest or swim intervals");
        for(int seed=0;seed<4096;seed++)for(int count=1;count<=KoiSchoolLayout.MaximumFish;count++)
        {
            var population=KoiSchoolLayout.Create(new Random(seed),count,8);
            var same=KoiSchoolLayout.Create(new Random(seed),count,8);
            var varieties=new int[8];var bands=new bool[count];
            float low=2,high=0;
            for(int i=0;i<count;i++)
            {
                var fish=population[i];Check(fish.Variety>=0&&fish.Variety<8,"Invalid variety index");varieties[fish.Variety]++;
                Check(fish.Scale>=KoiSchoolLayout.MinimumScale&&fish.Scale<=KoiSchoolLayout.MaximumScale,"Oversize fish");
                int band=Math.Min(count-1,(int)((fish.Scale-KoiSchoolLayout.MinimumScale)/(KoiSchoolLayout.MaximumScale-KoiSchoolLayout.MinimumScale)*count));
                Check(!bands[band],"Size distribution lost its spread");bands[band]=true;
                low=Math.Min(low,fish.Scale);high=Math.Max(high,fish.Scale);
                Check(fish.Scale==same[i].Scale&&fish.Variety==same[i].Variety&&fish.MotionSeed==same[i].MotionSeed&&fish.PhaseOffset==same[i].PhaseOffset,"Setup seed not deterministic");
                Check(fish.Radius>=.159f&&fish.Radius<=.601f,"Route outside validated pond");
                if(i>=KoiSchoolLayout.RouteCount)
                {
                    var other=population[i-KoiSchoolLayout.RouteCount];
                    Check(fish.MotionSeed==other.MotionSeed&&fish.Radius==other.Radius,"Paired schedule mismatch");
                    Check(Math.Abs(fish.PhaseOffset-other.PhaseOffset-Math.PI)<1e-12,"Missing antipodal spacing");
                }
            }
            foreach(int n in varieties){Check(n<=(count+7)/8,"Too many duplicates");if(count>=8)Check(n>=count/8,"Missing color variety");}
            if(count==18)Check(low<.911f&&high>1.069f,"Subtle size range not visible");
        }
        Check(KoiSchoolLayout.Create(new Random(1),99,8).Length==18,"Count cap failed");
        Check(KoiSchoolLayout.Create(new Random(1),0,8).Length==1,"Minimum count failed");
        Console.WriteLine("PASS: 128 seeds x 9 paired routes x 400 seconds at 30 Hz. Continuous bounded movement, glides/rests, pause and stall safety. Max frame step: "+maximumStep+" m; minimum paired-fish center gap: "+minimumPairGap+" m.");
        Console.WriteLine("PASS: 4,096 seeds x counts 1-18; valid indices beyond eight models, balanced colors, separated pair setup and guaranteed subtle 90%-108% size distribution.");
    }
}
