using System;
using System.Collections.Generic;
using TheLastWatch.Audio;
using TheLastWatch.Integrations;

namespace TherapyGame.Editor
{
    // Pure managed checks: also executable without Unity, audio devices or network access.
    public static class QuietHudStateChecks
    {
        public static int Run()
        {
            int n=0;void Check(bool ok,string name){n++;if(!ok)throw new Exception("Quiet HUD: "+name);}
            var c=new WellnessCaptions();Check(c.DisplayText=="...","idle ellipsis");
            c.User("A whole transcribed sentence.",1,0);
            Check(c.DisplayText=="A whole transcribed sentence.","player transcript immediate, no synthetic word delay");
            c.Stream("", "start",2,1);c.Stream("Hello ","delta",2,1.1f);
            Check(c.DisplayText=="Hello ","first streaming chunk");
            c.Stream("there.","delta",2,1.2f);Check(c.DisplayText=="Hello there.","append stream without paging");
            c.Stream("","stop",2,1.3f);Check(c.DisplayText=="Hello there.","stop does not erase text");
            c.Agent("Hello there.",2,1.4f);Check(c.DisplayText=="Hello there.","final response does not replay stream");
            c.Correct("Corrected.",1,1.5f);Check(c.Message=="Hello there.","ignore stale correction");
            c.Correct("Corrected.",2,1.5f);Check(c.DisplayText=="Corrected.","matching correction");
            c.Tick(true,false,false,1.6f);c.Tick(false,false,false,2);Check(c.DisplayText=="...","waiting after spoken response");
            c.Stream("late","delta",2,2.1f);Check(c.DisplayText=="...","late completed turn cannot resurrect");
            c.Tick(false,true,false,3);Check(c.Who==WellnessCaptions.Speaker.Listening,"speech activity hint");
            c.Tick(false,true,true,3.1f);Check(c.DisplayText=="...","mute clears speech hint");
            c.Agent("A new turn",3,4);c.Interrupt(4.1f);c.Stream("late","delta",3,4.2f);
            Check(c.DisplayText=="...","interrupted chunks ignored");
            c.Agent("older",2,4.3f);Check(c.DisplayText=="...","older final ignored");
            c.Reset();c.Agent("Fallback text",4,0);Check(c.DisplayText=="...","small alignment grace period");
            c.Tick(true,false,false,.3f);Check(c.DisplayText=="Fallback text","final fallback without fake typing");
            c.Reset();c.Agent("Hello",5,0);
            c.Align(new[]{"H","e","l","l","o"},new[]{0,100,200,300,400},new[]{100,100,100,100,100},5,.01f);
            c.Tick(true,false,false,.27f);Check(c.DisplayText=="Hel","character timestamps take priority over final fallback");
            c.Tick(true,false,false,.5f);Check(c.DisplayText=="Hello","complete aligned response");
            c.Reset();c.Align(new[]{"A","B"},new[]{0,200},new[]{200,200},6,0);
            c.Align(new[]{"C","D"},new[]{0,200},new[]{200,200},6,.05f);
            c.Tick(true,false,false,.25f);Check(c.DisplayText=="AB","queued audio chunk must not reveal early");
            c.Tick(true,false,false,.65f);Check(c.DisplayText=="ABCD","multiple local-timestamp chunks accumulate");
            foreach(int fps in new[]{10,15,30,60,120,240})
            {
                c.Reset();c.Align(new[]{"H","e","l","l","o"},new[]{0,100,200,300,400},new[]{100,100,100,100,100},7,0);
                for(int i=0;i<fps;i++){float t=i/(float)fps;if(t>.35f)break;c.Tick(true,false,false,t);}
                c.Tick(true,false,false,.35f);Check(c.DisplayText=="Hell","same clock result at "+fps+" fps");
            }
            c.Reset();c.Align(new[]{"bad"},new[]{-1},new[]{5},8,0);Check(c.DisplayText=="...","negative timestamps ignored");
            c.Align(new[]{"bad"},new int[0],new[]{5},8,0);Check(c.DisplayText=="...","mismatched arrays ignored");
            c.User("<b>Literal service data</b>",9,1);Check(c.DisplayText=="<b>Literal service data</b>","literal text");
            c.User(new string('a',3999)+char.ConvertFromUtf32(0x1F331),10,2);
            Check(c.Message.Length==3999&&!char.IsHighSurrogate(c.Message[c.Message.Length-1]),"bounded UTF16 text");
            c.Tick(false,false,false,11);Check(c.DisplayText=="...","old player text expires");
            c.Reset();Check(c.EventId==0&&c.DisplayText=="...","session reset clears text");
            foreach(int count in new[]{0,1,2,7,20})foreach(int seed in new[]{1,99,507})
            {
                var playlist=new WellnessPlaylist(count,seed);var visited=new HashSet<int>();
                int first=playlist.Move(1),current=first;
                Check(count==0?first==-1:first>=0&&first<count,"valid initial track");
                if(count==0){Check(playlist.Move(-1)==-1,"empty previous");continue;}
                visited.Add(first);
                for(int i=1;i<count;i++){current=playlist.Move(1);visited.Add(current);}
                Check(visited.Count==count,"shuffle covers each track exactly once");
                Check(playlist.Move(1)==first,"forward wrap");
                Check(playlist.Move(-1)==current,"backward wrap");
                Check(playlist.Move(1)==first,"next reverses previous");
                for(int i=0;i<count*2;i++){int before=playlist.Move(1);playlist.Move(-1);Check(playlist.Move(1)==before,"bidirectional navigation");}
            }
            return n;
        }
    }
}
