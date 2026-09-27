using TheLastWatch.UI;
using UnityEngine;

namespace TheLastWatch.Integrations
{
    public sealed partial class WellnessVoiceChat
    {
        private WellnessOnboardingTheme onboardingTheme;
        private bool onboardingDetails,onboardingDiagnostics;
        private Vector2 onboardingScroll;
        private float onboardingContentHeight=900;

        private void BeginConsentReview()
        {
            // Reviewing a new choice never silently retains the previous grants.
            bool requestedConversation=conversationRequested;
            consentChoices.Reset();cameraSetup=false;consentScreen=true;panelOpen=false;
            onboardingDetails=onboardingDiagnostics=false;onboardingScroll=Vector2.zero;
            permission.Choose(false);
            if(biometrics!=null){biometrics.SetPreviewEnabled(false);biometrics.SetConsent(false,false);}
            _=StopAsync("Review your voice and camera choices.");
            conversationRequested=requestedConversation;SyncUiLock();
        }

        private void DrawOnboarding()
        {
            if(onboardingTheme==null)onboardingTheme=new WellnessOnboardingTheme(quietGlassSerif,quietGlassSans);
            var t=onboardingTheme;Matrix4x4 previous=GUI.matrix;Color color=GUI.color;bool enabled=GUI.enabled;int depth=GUI.depth;
            try
            {
                GUI.depth=-100;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
                // Live room behind the glass; no captured image, blur pass, or extra camera.
                WellnessQuietGlassTheme.Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.035f,.042f,.029f,.12f));
                float scale=Mathf.Min(Screen.width/1182f,Screen.height/665f);
                GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1182*scale)*.5f,(Screen.height-665*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
                GUI.Label(new Rect(40,46,200,36),"MindSpace",t.Brand);
                GUI.Label(new Rect(41,81,170,20),"A Quieter Place",t.Tagline);
                if(onboardingDetails)DrawConsentDetails(t);
                else if(onboardingDiagnostics)DrawCameraDiagnostics(t);
                else if(cameraSetup)DrawQuietCamera(t);
                else DrawQuietConsent(t);
            }
            finally{GUI.matrix=previous;GUI.color=color;GUI.enabled=enabled;GUI.depth=depth;}
        }

        private void DrawQuietConsent(WellnessOnboardingTheme t)
        {
            t.Surface(new Rect(202,55,776,558));
            GUI.Label(new Rect(228,79,724,59),"Your space. Your choice.",t.Title);
            GUI.Label(new Rect(234,142,714,31),"Choose what to enable for this session.",t.Subtitle);
            consentChoices.SetVoice(t.Permission(new Rect(234,184,714,76),"mic","Voice conversation",
                "Microphone audio and messages go to ElevenLabs and agent services.",consentChoices.Voice,true,"consent-voice"));
            if(biometrics==null)consentChoices.SetCamera(false);
            string cameraCopy=biometrics==null?"Wellness camera is unavailable in this scene.":
                "Presage estimates pulse, breathing and HRV locally.\nStable readings may be shared during voice chat.";
            consentChoices.SetCamera(t.Permission(new Rect(234,269,714,89),"camera","Wellness camera",cameraCopy,
                consentChoices.Camera,consentChoices.Voice&&biometrics!=null,"consent-camera"));
            consentChoices.SetProactive(t.Permission(new Rect(234,367,714,76),"leaf","Occasional grounding prompts",
                "Optional. Requires voice and camera.",consentChoices.Proactive,consentChoices.Camera,"consent-grounding"));
            t.Line(235,455,712);
            t.Icon("info",new Rect(360,467,20,20));
            GUI.Label(new Rect(391,467,550,23),"Video is not saved or sent to ElevenLabs. Providers may retain voice data.",t.Fine);
            t.Icon("shield",new Rect(360,493,20,21));
            GUI.Label(new Rect(391,493,426,24),"General wellness support, not medical monitoring.",t.Fine);
            if(t.TextButton(new Rect(834,488,117,30),"Details & privacy  ›")){onboardingDetails=true;onboardingScroll=Vector2.zero;onboardingContentHeight=900;}
            GUI.Label(new Rect(235,512,515,20),IsBusy?"Finishing the previous session…":
                !consentChoices.Voice?"Nothing starts until you confirm. Camera guidance requires voice.":"For this play session only. Change your choices in voice controls.",t.Tiny);
            if(t.TextButton(new Rect(757,511,191,23),"Companion: "+GuideName+"  ›")){onboardingDetails=true;onboardingScroll=Vector2.zero;onboardingContentHeight=900;}
            t.Line(235,529,712);
            if(t.Button(new Rect(274,541,308,50),"Continue with choices  ›",true,!IsBusy))ChooseVoice(consentChoices.Voice);
            if(t.Button(new Rect(600,541,308,50),"Play without voice or camera",false,!IsBusy))
            {consentChoices.Reset();ChooseVoice(false);}
        }

        private void DrawQuietCamera(WellnessOnboardingTheme t)
        {
            var provider=biometrics;
            if(provider==null){consentChoices.SetCamera(false);cameraSetup=false;DrawQuietConsent(t);return;}
            var check=provider.CameraCheck;double now=Time.unscaledTime;
            bool interrupted=provider.State==PresageBiometricState.Error||provider.State==PresageBiometricState.Paused||!provider.HasConsent;
            bool passed=provider.HasConsent&&!interrupted&&check.Passed(now);
            bool cameraReady=!interrupted&&check.CameraWorking(now),positionReady=cameraReady&&check.PositionValid(now);
            bool pulseReady=!interrupted&&check.Running&&check.PulseReady(now),breathingReady=!interrupted&&check.Running&&check.BreathingReady(now);
            t.Surface(new Rect(202,55,776,540));
            GUI.Label(new Rect(228,76,724,62),"Camera & Presage check",t.Title);
            GUI.Label(new Rect(231,139,721,35),"Keep your face, shoulders and upper chest in view.",t.Subtitle);
            t.Card(new Rect(229,178,407,326));
            t.Icon("camera",new Rect(245,189,23,23));
            GUI.Label(new Rect(278,188,134,28),"Camera "+provider.cameraDeviceIndex,t.Status);
            if(t.TextButton(new Rect(501,184,126,32),"Switch camera",provider.HasConsent))
            {provider.cameraDeviceIndex=(provider.cameraDeviceIndex+1)%9;provider.RetryCameraTest();cameraSetupStarted=Time.unscaledTime;}
            Rect preview=new Rect(234,217,395,279);
            t.Rounded(preview,new Color(.27f,.285f,.25f,.78f),10);
            if(provider.HasCameraPreview)
            {
                // Preserve the current colour-camera fix: never tint or alpha-blend the RGB feed.
                Color previous=GUI.color;bool prior=GUI.enabled;
                try{GUI.color=Color.white;GUI.enabled=true;GUI.DrawTexture(preview,provider.CameraPreview,ScaleMode.ScaleToFit,false);}
                finally{GUI.color=previous;GUI.enabled=prior;}
                PresagePositionOverlay.Draw(preview,provider.CameraPreview,positionReady,t.Copy);
            }
            else
            {
                PresagePositionOverlay.Draw(preview,null,false,t.Copy);
                t.Rounded(new Rect(249,451,365,32),new Color(.12f,.15f,.13f,.85f),8);
                GUI.Label(new Rect(256,454,351,26),interrupted?"Camera paused · use Retry / resume":"Waiting for your camera…",t.PreviewText);
            }
            GUI.Label(new Rect(239,505,400,23),"Local preview · not recorded or sent to ElevenLabs",t.Tiny);
            t.Card(new Rect(653,181,300,254));
            t.Signal(201,"camera","Camera",cameraReady,interrupted?(provider.State==PresageBiometricState.Error?"Error":"Paused"):"Opening",interrupted);
            t.Signal(246,"person","Position",positionReady,interrupted?"Waiting":"Adjust",interrupted);
            t.Signal(291,"heart","Pulse",pulseReady,interrupted?"Waiting":"Acquiring",interrupted);
            t.Signal(336,"lungs","Breathing",breathingReady,interrupted?"Waiting":"Acquiring",interrupted);
            t.Line(670,382,265);
            string guidance=passed?"You're ready. Keep this position as you play.":interrupted?"Camera stopped. Use Retry / resume. See diagnostics for help.":
                !cameraReady?"Uncover the camera and close other camera apps.":!check.Running?"Starting Presage. Keep your face and upper chest in view.":
                !positionReady?CameraPositionHint(check.ValidationCode):"Keep still in even light. Breathe normally.";
            GUI.Label(new Rect(670,389,265,44),guidance,t.Guidance);
            // This is the existing final confirmation interval, not a fabricated acquisition percentage.
            t.Rounded(new Rect(655,437,295,3),new Color(.43f,.48f,.39f,.16f),1.5f);
            if(!interrupted)t.Rounded(new Rect(655,437,295*(float)check.Progress(now),3),WellnessOnboardingTheme.Sage,1.5f);
            if(t.Button(new Rect(655,443,295,52),"Enter with camera",true,passed))
            {
                // Recheck at the actual click; a stale preview or interrupted signal cannot grant entry.
                if(provider.HasConsent&&provider.State!=PresageBiometricState.Error&&provider.State!=PresageBiometricState.Paused&&check.Passed(Time.unscaledTime))CompleteVoiceChoice(true);
            }
            if(t.Button(new Rect(447,521,176,46),"Retry / resume",false,provider.HasConsent))
            {provider.RetryCameraTest();cameraSetupStarted=Time.unscaledTime;}
            if(t.Button(new Rect(634,521,222,46),"Continue without camera",false))
            {consentChoices.SetCamera(false);provider.SetConsent(false,false);CompleteVoiceChoice(consentChoices.Voice);}
            if(t.Button(new Rect(867,521,84,46),"Back",false))
            {provider.SetPreviewEnabled(false);provider.SetConsent(false,false);cameraSetup=false;}
            string progress=passed?"Camera check passed":interrupted?"Camera needs attention":
                pulseReady&&breathingReady?"Confirming both signals…":pulseReady?"Pulse ready · acquiring breathing":breathingReady?"Breathing ready · acquiring pulse":"Acquiring pulse and breathing";
            GUI.Label(new Rect(236,573,520,18),progress+" · "+WellnessHud.TimeLabel((float)(now-cameraSetupStarted))+" elapsed",t.Tiny);
            if(t.TextButton(new Rect(773,571,180,22),"Diagnostics & help  ›")){onboardingDiagnostics=true;onboardingScroll=Vector2.zero;onboardingContentHeight=900;}
        }

        private static string CameraPositionHint(int code)
        {
            switch(code)
            {
                case 1:return "Face the camera and keep your whole face in view.";
                case 2:return "Only one person should be in the camera view.";
                case 3:return "Move your face toward the centre of the view.";
                case 4:return "Adjust your distance to include your face and upper chest.";
                case 5:return "Add light in front of you; avoid a bright window behind.";
                case 6:return "Reduce glare or strong light on your face.";
                case 7:return "Move back or tilt down to include your shoulders and upper chest.";
                case 10:return "The camera is adjusting exposure. Hold your position.";
                case 11:return "Close other camera apps to improve frame rate.";
                case 12:return "Steady the camera and keep your head and shoulders still.";
                case 13:return "Move a little farther from the camera.";
                case 14:return "Move closer, keeping your upper chest in view.";
                case 15:return "Move lower or tilt the camera up to centre your face.";
                case 16:return "Move higher or tilt the camera down to centre your face.";
                case 17:return "Face the camera straight on with your head upright.";
                default:return "Keep your face, shoulders and upper chest in view.";
            }
        }

        private void DrawConsentDetails(WellnessOnboardingTheme t)
        {
            t.Surface(new Rect(202,55,776,558));
            GUI.Label(new Rect(234,80,714,58),"Your choices, explained",t.Title);
            GUI.Label(new Rect(234,140,714,29),"All permissions are optional and last only for this play session.",t.Subtitle);
            Rect viewport=new Rect(237,189,710,319);
            onboardingScroll=GUI.BeginScrollView(viewport,onboardingScroll,new Rect(0,0,682,Mathf.Max(319,onboardingContentHeight)));
            float y=0;
            Detail(t,ref y,"Choose your companion", "Julien and Camille are AI companions, not clinicians or emergency services.");
            if(settings!=null&&settings.agents!=null)
            {
                for(int i=0;i<settings.agents.Length;i++)
                {
                    if(t.Button(new Rect((i%2)*334,y,322,38),settings.agents[i].displayName,i==selected,!IsBusy&&!IsConnected))SelectCharacter(i);
                    if(i%2==1||i==settings.agents.Length-1)y+=46;
                }
            }
            Detail(t,ref y,"Voice conversation", "Enabling voice sends microphone audio and typed messages to ElevenLabs and your selected agent's configured services. This game does not save transcripts; the provider or agent owner may retain them. Avoid sharing sensitive information.");
            Detail(t,ref y,"Camera measurements", "Presage estimates pulse, breathing and HRV locally. Video and the local preview are not saved or sent to ElevenLabs. Presage verifies subscription access over the network. Selected stable measurements and trends are shared with ElevenLabs during voice conversations. These approximate readings are not medical monitoring, diagnosis or treatment.");
            Detail(t,ref y,"Optional grounding prompts", "With voice and camera enabled, your companion may occasionally offer grounding guidance after a sustained change from your session baseline. This does not diagnose anxiety or any medical condition.");
            Detail(t,ref y,"You stay in control", "No microphone or camera starts just from changing these switches. Continue confirms your choices; camera consent opens the camera test first. M mutes, X pauses, and V opens voice controls to withdraw permission. Leaving the game window pauses capture until you resume. Turning a permission off also clears its dependent choices.");
            onboardingContentHeight=y;GUI.EndScrollView();
            t.Line(235,529,712);
            if(t.Button(new Rect(652,541,294,50),"Back to my choices",true)){onboardingDetails=false;onboardingScroll=Vector2.zero;}
        }
        private static void Detail(WellnessOnboardingTheme t,ref float y,string title,string copy)
        {
            GUI.Label(new Rect(0,y,669,30),title,t.RowTitle);y+=32;
            float height=t.Copy.CalcHeight(new GUIContent(copy),669);
            GUI.Label(new Rect(0,y,669,height),copy,t.Copy);y+=height+20;
        }
        private void DrawCameraDiagnostics(WellnessOnboardingTheme t)
        {
            if(biometrics==null){onboardingDiagnostics=false;return;}
            var provider=biometrics;var check=provider.CameraCheck;double now=Time.unscaledTime;
            t.Surface(new Rect(202,55,776,558));
            GUI.Label(new Rect(234,80,714,58),"Camera diagnostics",t.Title);
            GUI.Label(new Rect(234,142,714,30),"Live status from your camera and Presage",t.Subtitle);
            onboardingScroll=GUI.BeginScrollView(new Rect(237,189,710,319),onboardingScroll,new Rect(0,0,682,Mathf.Max(319,onboardingContentHeight)));
            float y=0;
            Detail(t,ref y,"Camera & Presage",provider.StatusText+"\nPresage measurement "+(check.Running?"running":"not running")+" · camera "+(check.CameraWorking(now)?"frames received":"frames unavailable"));
            Detail(t,ref y,"Position",PresageCameraCheck.Guidance(check.ValidationCode)+" The outline is a positioning guide, not detected landmarks.");
            Detail(t,ref y,"Signals","Pulse: "+CameraSignalLabel(provider,true)+"\nBreathing: "+CameraSignalLabel(provider,false)+"\nBoth must be fresh and stable before camera-enabled entry. The small bar only shows the final confirmation interval.");
            Detail(t,ref y,"Troubleshooting","Uncover the lens, allow camera access in Windows, and close other camera apps. Use even light in front of you and keep your face, shoulders and upper chest in view. Retry restarts calibration; time to acquire a signal varies. If pulse stays absent in good conditions, check that your Presage plan includes cardio / pulse measurements. You can always continue without camera.");
            onboardingContentHeight=y;GUI.EndScrollView();t.Line(235,529,712);
            if(t.Button(new Rect(652,541,294,50),"Back to camera check",true)){onboardingDiagnostics=false;onboardingScroll=Vector2.zero;}
        }
    }
}
