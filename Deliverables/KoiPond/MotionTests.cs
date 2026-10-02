using System;
using TheLastWatch.Environment;
public static class MotionTests
{
    static void Check(bool v,string message){if(!v)throw new Exception(message);}
    public static void Main()
    {
        int restingFrames=0,movingFrames=0;double maximumStep=0;
        for(int seed=0;seed<128;seed++)for(int lane=0;lane<6;lane++)
        {
            double r=.26+lane*.06;var motion=new KoiSwimMotion(seed,r,6.7,5.7);
            var same=new KoiSwimMotion(seed,r,6.7,5.7);
            for(int frame=0;frame<12000;frame++)
            {
                double x=motion.X,z=motion.Z;motion.Step(1.0/30);same.Step(1.0/30);
                double distance=Math.Sqrt(Math.Pow(x-motion.X,2)+Math.Pow(z-motion.Z,2));maximumStep=Math.Max(distance,maximumStep);
                Check(distance<.011,"Teleport");Check(Math.Abs(Math.Sqrt(Math.Pow(motion.X/6.7,2)+Math.Pow(motion.Z/5.7,2))-r)<1e-10,"Route left lane");
                Check(motion.X==same.X&&motion.Z==same.Z,"Seed not deterministic");
                Check(!double.IsNaN(motion.YawDegrees)&&motion.Speed>=.012-1e-8&&motion.Speed<=.29,"Invalid state");
                if(motion.Speed<.035)restingFrames++;if(motion.Speed>.1)movingFrames++;
            }
            double stillX=motion.X,stillZ=motion.Z;motion.Step(0);motion.Step(-1);motion.Step(double.NaN);
            Check(motion.X==stillX&&motion.Z==stillZ,"Pause changed position");
            motion.Step(999);Check(Math.Sqrt(Math.Pow(motion.X-stillX,2)+Math.Pow(motion.Z-stillZ,2))<.031,"Stall teleported fish");
        }
        Check(restingFrames>1000&&movingFrames>1000,"Missing rest or swim intervals");
        Console.WriteLine("PASS: 128 seeds x 6 lanes x 400 seconds at 30 Hz. Continuous bounded movement, glides/rests, deterministic seeds, pause and stall safety. Max frame step: "+maximumStep+" m. Rest frames: "+restingFrames+"; moving frames: "+movingFrames);
    }
}
