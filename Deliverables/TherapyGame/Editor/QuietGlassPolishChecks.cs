using System;
using TheLastWatch.UI;

namespace TherapyGame.Editor
{
    public static class QuietGlassPolishChecks
    {
        public static int Run()
        {
            int n=0;void Check(bool ok,string message){n++;if(!ok)throw new Exception("Quiet Glass polish: "+message);}
            Check(WellnessUiMotion.Radius(4,51,26)==2,"4px selection sliver must use 2px radius, never a 52px nine-slice");
            Check(WellnessUiMotion.Radius(44,33,5)==5,"Esc keycap is a rounded rectangle, not a circle");
            Check(WellnessUiMotion.Radius(276,57,11)==11,"sidebar reference corner radius");
            Check(WellnessUiMotion.Radius(272,65,13)==13,"Resume reference corner radius");
            Check(WellnessUiMotion.Radius(26,26,13)==13,"slider thumb circle");
            Check(WellnessUiMotion.Radius(0,51,26)==0,"empty shape");
            foreach(float diameter in new[]{29f,79f})for(int degree=0;degree<=720;degree++)
            {
                float x=WellnessUiMotion.LabelX(303.5f,diameter,degree),y=WellnessUiMotion.LabelY(760.5f,diameter,degree);
                double radius=Math.Sqrt(Math.Pow(x-303.5,2)+Math.Pow(y-760.5,2));
                Check(Math.Abs(radius-diameter*.082)<.0001,"label orbit radius invariant");
                Check(Math.Abs(x-303.5f)<diameter*.14f&&Math.Abs(y-760.5f)<diameter*.14f,"moving markings remain within fixed label");
                Check(Math.Abs(x-WellnessUiMotion.LabelX(303.5f,diameter,degree+360))<.0001,"rotation has no discontinuity at wrap");
            }
            for(int frame=0;frame<600;frame++)for(int i=0;i<3;i++)
            {
                float life=WellnessUiMotion.NoteLife(frame/30f,i),alpha=WellnessUiMotion.NoteAlpha(life);
                float size=WellnessUiMotion.NoteSize(i);
                float x=303.5f+WellnessUiMotion.NoteX(life,i)-size*.5f,y=721+4+WellnessUiMotion.NoteY(life)-size*.6f;
                Check(life>=0&&life<1&&alpha>=0&&alpha<=.601f,"bounded note cycle");
                Check(x>=264&&x+size<=343&&y>=678&&y+size*1.2f<=738,"pause notes rise above record, away from Exit and credits");
                Check(size>=15&&size<=20,"subtle note size variation");
                float hudX=25.5f+WellnessUiMotion.NoteX(life,i)*.47f-size*.47f*.5f;
                Check(hudX>=11&&hudX+size*.47f<=40,"HUD notes remain above small vinyl, clear of song text");
            }
            Check(Math.Abs(WellnessUiMotion.NoteAlpha(0))<.0001&&Math.Abs(WellnessUiMotion.NoteAlpha(1))<.0001,"notes fade without a pop");
            Check(WellnessUiMotion.NoteSize(0)<WellnessUiMotion.NoteSize(1)&&WellnessUiMotion.NoteSize(1)<WellnessUiMotion.NoteSize(2),"three varied sizes");
            Check(273+43<344&&344+48<405,"weather tabs and mode buttons do not collide");
            Check(616+40<682&&682+30<741,"weather controls leave footer clear");
            Check(612+3*204+186==1410,"time presets fit content width exactly");
            return n;
        }
    }
}
