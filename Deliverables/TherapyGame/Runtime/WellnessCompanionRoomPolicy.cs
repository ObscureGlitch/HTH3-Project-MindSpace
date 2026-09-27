namespace TheLastWatch.Integrations
{
    // Pure state: no wall-clock timers, random choices, scene objects or voice-device access.
    public sealed class WellnessCompanionRoomPolicy
    {
        public const double DepartureDelaySeconds=5;
        public double OutsideSeconds {get;private set;}
        public void Reset()=>OutsideSeconds=0;
        public void Observe(bool playerInside,bool advancing,float deltaTime)
        {
            if(playerInside){Reset();return;}
            if(advancing&&!float.IsNaN(deltaTime)&&!float.IsInfinity(deltaTime)&&deltaTime>0)
                OutsideSeconds+=deltaTime;
        }
        public bool CanLeave(bool doorAllowsPassage)=>OutsideSeconds>DepartureDelaySeconds&&doorAllowsPassage;
        public static bool DoorAllowsPassage(bool requestedOpen,float actualAngle)=>requestedOpen&&actualAngle>=65;
        public static bool ShouldWelcome(bool voiceEnabled,bool initialChoice,bool playerInside)
            =>voiceEnabled&&initialChoice&&playerInside;
    }
}
