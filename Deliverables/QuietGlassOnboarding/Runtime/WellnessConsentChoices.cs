using System;

namespace TheLastWatch.UI
{
    // Draft choices only. No device or network operation is possible from a toggle.
    public sealed class WellnessConsentChoices
    {
        public bool Voice {get;private set;}
        public bool Camera {get;private set;}
        public bool Proactive {get;private set;}
        public void Reset(){Voice=Camera=Proactive=false;}
        public void SetVoice(bool enabled){Voice=enabled;if(!enabled)Camera=Proactive=false;}
        public void SetCamera(bool enabled){Camera=Voice&&enabled;if(!Camera)Proactive=false;}
        public void SetProactive(bool enabled){Proactive=Voice&&Camera&&enabled;}
        public static string Checks()
        {
            var c=new WellnessConsentChoices();
            if(c.Voice||c.Camera||c.Proactive)throw new Exception("Permissions must default off.");
            c.SetCamera(true);c.SetProactive(true);
            if(c.Camera||c.Proactive)throw new Exception("Camera/grounding dependencies.");
            c.SetVoice(true);c.SetProactive(true);if(c.Proactive)throw new Exception("Grounding requires camera.");
            c.SetCamera(true);c.SetProactive(true);if(!c.Proactive)throw new Exception("Explicit choices lost.");
            c.SetCamera(false);if(c.Camera||c.Proactive||!c.Voice)throw new Exception("Camera decline must preserve voice only.");
            c.SetCamera(true);if(c.Proactive)throw new Exception("Grounding must not re-enable implicitly.");
            c.SetProactive(true);c.SetVoice(false);if(c.Voice||c.Camera||c.Proactive)throw new Exception("Voice off clears dependents.");
            c.SetVoice(true);if(c.Camera||c.Proactive)throw new Exception("Voice re-enable must not grant camera.");
            c.SetCamera(true);c.SetProactive(true);c.Reset();if(c.Voice||c.Camera||c.Proactive)throw new Exception("Session reset.");
            return "PASS: default-off draft choices, explicit opt-in dependencies, camera-only withdrawal, no implicit re-grants, session reset.";
        }
    }
}
