using System;
using TheLastWatch.Integrations;

namespace TherapyGame.Editor
{
    public static class DoorwayConversationChecks
    {
        public static int Run()
        {
            int n=0;void Check(bool yes,string label){n++;if(!yes)throw new Exception("Doorway check: "+label);}
            var d=new WellnessDoorwayConversation();
            d.Observe(true,0,true);Check(d.Pending==WellnessDoorwayConversation.Cue.None,"no duplicate initial greeting");
            for(int i=0;i<30;i++){d.Observe(i%2==0,.1f+i*.1f,true);Check(d.Pending==WellnessDoorwayConversation.Cue.None,"door threshold jitter ignored");}
            d.Observe(true,4,true);d.Observe(true,5,true);
            d.Observe(false,6,true);d.Observe(false,6.7f,true);
            Check(d.Pending==WellnessDoorwayConversation.Cue.Goodbye,"exit requests farewell");
            Check(d.Ready(true,true,true,true,true,6.8f)==WellnessDoorwayConversation.Cue.None,"do not interrupt speech");
            Check(d.Ready(false,true,true,true,false,6.8f)==WellnessDoorwayConversation.Cue.None,"no disconnected send");
            Check(d.Ready(true,false,true,true,false,6.8f)==WellnessDoorwayConversation.Cue.None,"no out-of-range send");
            Check(d.Ready(true,true,true,true,false,6.8f)==WellnessDoorwayConversation.Cue.Goodbye,"audible farewell");
            d.Sent(6.8f);Check(d.Pending==WellnessDoorwayConversation.Cue.None,"exactly once");
            d.Observe(true,8,true);d.Observe(true,8.7f,true);
            Check(d.Ready(true,true,true,true,false,9)==WellnessDoorwayConversation.Cue.WelcomeBack,"return can follow recent goodbye");
            d.Sent(9);d.Observe(false,10,true);d.Observe(false,10.7f,true);
            Check(d.Pending==WellnessDoorwayConversation.Cue.None,"cooldown prevents repeated farewell");
            d.Observe(true,12,true);d.Observe(true,12.7f,true);
            Check(d.Pending==WellnessDoorwayConversation.Cue.None,"cooldown prevents repeated welcome");
            foreach(bool permission in new[]{false,true})foreach(bool focused in new[]{false,true})
            foreach(bool connected in new[]{false,true})foreach(bool range in new[]{false,true})foreach(bool speaking in new[]{false,true})
            {
                var gate=new WellnessDoorwayConversation();gate.Observe(true,0,true);gate.Observe(false,1,true);gate.Observe(false,2,true);
                var response=gate.Ready(connected,range,focused,permission,speaking,2.1f);
                Check((response!=WellnessDoorwayConversation.Cue.None)==(permission&&focused&&connected&&range&&!speaking),"all safety gates");
            }
            foreach(bool indoors in new[]{false,true})
            {
                var denied=new WellnessDoorwayConversation();denied.Observe(indoors,0,false);denied.Observe(!indoors,1,false);denied.Observe(!indoors,2,false);
                Check(denied.Ready(true,true,true,true,false,3)==WellnessDoorwayConversation.Cue.None,"no deferred prompt after muted/paused crossing");
                var expired=new WellnessDoorwayConversation();expired.Observe(indoors,0,true);expired.Observe(!indoors,1,true);expired.Observe(!indoors,2,true);
                Check(expired.Ready(true,true,true,true,false,23)==WellnessDoorwayConversation.Cue.None,"stale events expire");
            }
            var reverse=new WellnessDoorwayConversation();reverse.Observe(true,0,true);reverse.Observe(false,1,true);reverse.Observe(false,2,true);reverse.Observe(true,2.1f,true);
            Check(reverse.Pending==WellnessDoorwayConversation.Cue.None,"cancel obsolete goodbye on reentry");
            Check(WellnessDoorwayConversation.Instruction(WellnessDoorwayConversation.Cue.Goodbye).StartsWith(WellnessDoorwayConversation.EventPrefix),"environment event labeled");
            return n;
        }
    }
}
