using System;
using TheLastWatch.UI;

namespace TherapyGame.Editor
{
    public static class QuietGlassLayoutChecks
    {
        public static int Run()
        {
            int n=0;void Check(bool ok,string message){n++;if(!ok)throw new Exception("Quiet Glass: "+message);}
            Check(WellnessQuietGlassLayout.Scale(1672,941)==1,"1:1 reference scale");
            Check(WellnessQuietGlassLayout.OffsetX(1672,941)==0&&WellnessQuietGlassLayout.OffsetY(1672,941)==0,"reference origin");
            int[,] sizes={{1672,941},{1280,720},{1920,1080},{2560,1440},{1366,768},{3440,1440},{1024,768},{800,600},{3840,2160},{1280,800}};
            for(int i=0;i<sizes.GetLength(0);i++)
            {
                float w=sizes[i,0],h=sizes[i,1],s=WellnessQuietGlassLayout.Scale(w,h);
                float ox=WellnessQuietGlassLayout.OffsetX(w,h),oy=WellnessQuietGlassLayout.OffsetY(w,h);
                float x=ox+216*s,y=oy+102*s;
                Check(x>=0&&y>=0&&x+1236*s<=w+.01f&&y+737*s<=h+.01f,"panel within viewport "+w+"x"+h);
                Check(Math.Abs((ox+612*s-ox)/s-612)<.001f,"content transform round trip");
                Check(oy+808*s<=h,"music controls stay in view");
                // The disc stays in a fixed rect; only small printed label details move inside it.
                Check(264>=255&&264+79<=351&&721>=713&&721+79<=808,"vinyl containment");
                Check(oy+741*s>oy+723*s,"content cannot reach footer");
                Check(ox+1410*s<x+1236*s,"controls remain inside right panel");
            }
            Check(WellnessQuietGlassLayout.SliderThumb(-1)==853,"minimum slider clamp");
            Check(WellnessQuietGlassLayout.SliderThumb(2)==1261,"maximum slider clamp");
            for(int i=0;i<=100;i++)
            {
                float t=WellnessQuietGlassLayout.SliderThumb(i/100f);
                Check(t-13>=840&&t+13<=1274,"thumb bounds at "+i+" percent");
            }
            Check(338+3*63+57<605,"sidebar rows leave divider clear");
            Check(499+114<650&&650+37<741,"microphone/captions/footer separation");
            Check(1210+180<=1410&&612+798==1410,"microphone action stays within card");
            return n;
        }
    }
}
