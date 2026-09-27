namespace TheLastWatch.Integrations
{
    /// <summary>Pure, clock-driven doorway events. Never grants consent or opens a microphone.</summary>
    public sealed class WellnessDoorwayConversation
    {
        public enum Cue { None, Goodbye, WelcomeBack }
        public const string EventPrefix="[MindSpace doorway event; not player speech]";
        private bool initialized,inside,candidate;
        private float candidateSince,expires,lastGoodbye=-100,lastWelcome=-100;
        public Cue Pending {get;private set;}
        public void Observe(bool indoors,float now,bool allowed)
        {
            if(!initialized){initialized=true;inside=candidate=indoors;candidateSince=now;}
            if(candidate!=indoors){candidate=indoors;candidateSince=now;Pending=Cue.None;}
            if(inside!=candidate&&now-candidateSince>=.65f)
            {
                inside=candidate;
                bool cooled=now-(inside?lastWelcome:lastGoodbye)>=10;
                Pending=allowed&&cooled?(inside?Cue.WelcomeBack:Cue.Goodbye):Cue.None;
                expires=now+(inside?18:5);
            }
            if(!allowed||now>expires)Pending=Cue.None;
        }
        public Cue Ready(bool connected,bool inRange,bool focused,bool allowed,bool speaking,float now)
        {
            if(!allowed||!focused||now>expires){Pending=Cue.None;return Cue.None;}
            return connected&&inRange&&!speaking?Pending:Cue.None;
        }
        public void Sent(float now)
        {
            if(Pending==Cue.Goodbye)lastGoodbye=now;
            if(Pending==Cue.WelcomeBack)lastWelcome=now;
            Pending=Cue.None;
        }
        public static string Instruction(Cue cue)=>EventPrefix+(cue==Cue.Goodbye?
            " The player has just stepped outside the house. Give a brief, warm one-sentence goodbye for their garden break. Do not ask a question or end the session; they may still talk near the open door.":
            " The player has returned inside the house after being outside. Briefly welcome them back warmly and invite them to continue at their own pace. If you have just greeted them on reconnect, do not repeat yourself. This is a location event, not something they said.");
    }
}
