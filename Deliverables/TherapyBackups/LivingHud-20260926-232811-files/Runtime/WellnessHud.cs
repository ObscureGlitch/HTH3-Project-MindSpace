using System;
using System.Collections.Generic;
using TheLastWatch.Audio;
using TheLastWatch.Integrations;
using TheLastWatch.Interaction;
using TheLastWatch.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastWatch.UI
{
    public struct WellnessHudLayout
    {
        public Rect journey,settings,music,captions,interaction,clock,microphone,biometrics;
        public static WellnessHudLayout At(float width,float height,bool largeCaptions=false)=>new WellnessHudLayout
        {
            journey=new Rect(24,24,290,174),settings=new Rect(width-150,24,122,32),
            clock=new Rect(width-276,height-61,252,32),microphone=new Rect(width-398,64,370,24),biometrics=new Rect(width-398,88,370,24),
            music=new Rect(20,height-98,236,78),
            captions=new Rect((width-620)/2,height-(largeCaptions?167:147),620,largeCaptions?143:123),
            interaction=new Rect((width-340)/2,height*.535f,340,38)
        };
    }

    [DisallowMultipleComponent]
    public sealed class WellnessHud : MonoBehaviour
    {
        public WellnessVoiceChat chat;
        public WellnessExplorer player;
        public Transform room;
        [Range(0,1)] public float panelOpacity;
        public bool showJourney=true,showCaptions=true,largerCaptions;
        private bool collapsed=true,settled,talked,breathed;
        private float settledTime,captionOffset,captionTarget;
        private float vinylAngle,subtitleEnteredAt,noteClock;
        private int lastCaptionId=-1;
        private WellnessCaptions.Speaker lastSpeaker;
        private readonly HashSet<WellnessInteraction> noticed=new HashSet<WellnessInteraction>();
        private WellnessInteraction[] interactions=Array.Empty<WellnessInteraction>();
        private WellnessUiTheme theme;
        private GUIStyle largeCaption;
        public WellnessUiTheme Theme=>theme??(theme=new WellnessUiTheme());
        public int JourneyCompleted=>(settled?1:0)+(talked?1:0)+(breathed?1:0)+(noticed.Count>=3?1:0);
        public static bool CaptionVisible(bool preference,bool inRange,bool active)=>preference&&inRange&&active;
        public static bool IsDay(float hour)=>Mathf.Repeat(hour,24)>=6&&Mathf.Repeat(hour,24)<18;
        public static int WeatherIconKind(bool raining,string weather)=>
            raining?3:(weather=="Cloudy"||weather=="Clearing"||weather=="Rain")?2:-1;
        private bool HasCaptions=>chat!=null&&CaptionVisible(showCaptions,chat.IsInTalkingZone,chat.IsConnected||chat.IsBusy);
        private void OnEnable()
        {
            if(!Application.isPlaying)return;
            settled=talked=breathed=false;collapsed=true;noticed.Clear();settledTime=0;
            if(room!=null){interactions=room.GetComponentsInChildren<WellnessInteraction>(true);foreach(var item in interactions)item.Noticed+=OnNoticed;}
            if(chat!=null)chat.UserCaptionReceived+=OnUserCaption;
        }
        private void OnDisable()
        {
            foreach(var item in interactions)if(item!=null)item.Noticed-=OnNoticed;
            if(chat!=null)chat.UserCaptionReceived-=OnUserCaption;
        }
        private void OnDestroy(){theme?.Dispose();theme=null;}
        private void OnUserCaption()=>talked=true;
        private void OnNoticed(WellnessInteraction item)
        {
            if(item.breathingOrb!=null)breathed=true;
            else if(item.area==WellnessInteraction.Area.Grounding&&item.door==null&&item.therapist==null)noticed.Add(item);
        }
        private void Update()
        {
            if(!Application.isPlaying||chat==null||player==null||chat.IsPanelOpen)return;
            if(!HasCaptions){captionOffset=captionTarget=0;lastCaptionId=-1;}
            else
            {
                if(lastCaptionId!=chat.Captions.EventId||lastSpeaker!=chat.Captions.Who)
                {captionOffset=captionTarget=0;lastCaptionId=chat.Captions.EventId;lastSpeaker=chat.Captions.Who;subtitleEnteredAt=Time.unscaledTime;}
                // Continuous time-based scroll follows new lines without page timers or frame steps.
                captionOffset=Mathf.Lerp(captionOffset,captionTarget,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
            }
            var playingMusic=BackgroundMusicPlayer.Instance;
            if(playingMusic!=null&&playingMusic.IsPlaying)
            {vinylAngle=Mathf.Repeat(vinylAngle+Time.unscaledDeltaTime*42,360);noteClock+=Time.unscaledDeltaTime;}
            if(!settled&&room!=null&&player.ViewCamera!=null&&chat.localRoom.Contains(room.InverseTransformPoint(player.ViewCamera.transform.position)))
            {settledTime+=Time.unscaledDeltaTime;if(settledTime>=5||player.IsSeated)settled=true;}
            var keyboard=Keyboard.current;if(keyboard==null)return;
            if(keyboard.jKey.wasPressedThisFrame)collapsed=!collapsed;
            if(keyboard.pKey.wasPressedThisFrame)BackgroundMusicPlayer.Instance?.TogglePause();
            if(keyboard.leftBracketKey.wasPressedThisFrame)BackgroundMusicPlayer.Instance?.PlayPrevious();
            if(keyboard.rightBracketKey.wasPressedThisFrame)BackgroundMusicPlayer.Instance?.PlayNext();
        }
        private void Backdrop(Rect r)
        {
            if(panelOpacity>0)WellnessUiTheme.Fill(r,new Color(.025f,.04f,.04f,panelOpacity*.65f));
        }
        private void OnGUI()
        {
            if(!Application.isPlaying||chat==null||player==null||chat.IsPanelOpen)return;
            var ui=Theme;float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);if(scale<=0)return;
            Matrix4x4 old=GUI.matrix;int oldDepth=GUI.depth;GUI.depth=0;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            float width=Screen.width/scale,height=Screen.height/scale;var layout=WellnessHudLayout.At(width,height,largerCaptions);
            try
            {
                if(showJourney)DrawJourney(layout.journey);
                Backdrop(layout.settings);
                if(ui.HudAction(layout.settings,"Settings   F1","Open settings"))chat.OpenSettings();
                DrawClock(layout.clock);
                DrawMusic(layout.music);
                DrawInteraction(layout.interaction,width,height);
                if(HasCaptions)DrawCaptions(layout.captions);
                // Mic status remains visible even outside the zone or with captions switched off.
                Rect mic=layout.microphone;Backdrop(mic);
                ui.Dot(new Rect(mic.x+8,mic.y+8,6,6),chat.IsConnected&&!chat.IsMuted?new Color(.65f,.87f,.69f):new Color(.8f,.81f,.79f));
                ui.HudLabel(new Rect(mic.x+25,mic.y+3,mic.width-25,20),chat.MicrophoneBadge,ui.HudSmall,.85f);
                if(chat.biometrics!=null)
                {
                    Rect bio=layout.biometrics;Backdrop(bio);
                    ui.Dot(new Rect(bio.x+8,bio.y+8,6,6),chat.biometrics.IsMeasuring?new Color(.58f,.82f,.91f):new Color(.8f,.81f,.79f));
                    ui.HudLabel(new Rect(bio.x+25,bio.y+3,bio.width-25,20),chat.BiometricBadge,ui.HudSmall,.82f);
                }
                if(!string.IsNullOrEmpty(player.NoticeMessage))
                {
                    Rect notice=new Rect(layout.captions.x+16,layout.captions.y-82,layout.captions.width-32,70);Backdrop(notice);
                    ui.HudLabel(notice,player.NoticeMessage,ui.HudText);
                }
                if(player.TransitionFade>0)WellnessUiTheme.Fill(new Rect(0,0,width,height),new Color(.04f,.05f,.04f,player.TransitionFade));
            }
            finally{GUI.matrix=old;GUI.depth=oldDepth;}
        }
        private void DrawClock(Rect r)
        {
            var ui=Theme;var sky=chat.skyCycle;if(sky==null)return;Backdrop(r);
            bool day=IsDay(sky.Hour);
            bool raining=sky.rain!=null&&sky.rain.EffectiveIntensity>.02f;
            int weather=WeatherIconKind(raining,sky.WeatherLabel);
            ui.WeatherIcon(new Rect(r.x+4,r.y+3,25,25),day?0:1);
            ui.HudLabel(new Rect(r.x+39,r.y+7,62,24),sky.Clock,ui.HudHeading);
            if(weather>=0)ui.WeatherIcon(new Rect(r.x+108,r.y+3,25,25),weather);
            string label=raining?"Rain":weather==2?"Cloudy":day?"Sunny":"Clear night";
            ui.HudLabel(new Rect(r.x+(weather>=0?142:112),r.y+8,116,22),label,ui.HudSmall,.88f);
        }
        private void DrawJourney(Rect r)
        {
            var ui=Theme;if(collapsed)r.height=34;Backdrop(r);
            ui.HudLabel(new Rect(r.x+8,r.y+9,218,22),"When you're ready  "+JourneyCompleted+"/4",ui.HudHeading,.84f);
            if(ui.HudAction(new Rect(r.x+r.width-60,r.y+2,54,30),collapsed?"J  +":"J  −","Expand or collapse optional checklist"))collapsed=!collapsed;
            if(collapsed)return;
            Row(r,49,settled,"Settle into the room");
            Row(r,76,talked,"Tell your guide how today felt");
            ui.HudLabel(new Rect(r.x+28,r.y+94,r.width-36,18),"A word or two is plenty. No rush.",ui.HudSmall,.7f);
            Row(r,124,breathed,"Try the breathing orb");
            Row(r,151,noticed.Count>=3,"Notice three things  "+Mathf.Min(3,noticed.Count)+"/3");
        }
        private void Row(Rect r,float y,bool done,string label)
        {
            var ui=Theme;ui.Dot(new Rect(r.x+9,r.y+y,done?6:3,done?6:3),new Color(.85f,.95f,.84f,done?1:.6f));
            ui.HudLabel(new Rect(r.x+28,r.y+y-5,r.width-36,25),label,ui.HudSmall,done?.72f:.9f);
        }
        private void DrawMusic(Rect r)
        {
            var ui=Theme;var music=BackgroundMusicPlayer.Instance;ui.MusicCard(r);
            ui.Vinyl(new Rect(r.x+11,r.y+13,29,29),vinylAngle,noteClock,music!=null&&music.IsPlaying);
            string title=music!=null?music.TrackTitle:"Room sounds";
            ui.HudLabel(new Rect(r.x+50,r.y+9,r.width-58,19),Fit(title,ui.HudHeading,r.width-58),ui.HudHeading,.97f);
            ui.HudLabel(new Rect(r.x+50,r.y+29,r.width-58,17),music!=null?music.TrackArtist:"Ambience",ui.HudSmall,.76f);
            GUI.enabled=music!=null;
            if(ui.HudAction(new Rect(r.x+47,r.y+46,28,24),"|<","Previous song ["))music.PlayPrevious();
            if(ui.HudAction(new Rect(r.x+79,r.y+46,28,24),music!=null&&music.IsPaused?">":"II","Pause / resume P"))music.TogglePause();
            if(ui.HudAction(new Rect(r.x+111,r.y+46,28,24),">|","Next song ]"))music.PlayNext();
            GUI.enabled=true;
            float duration=music!=null?music.Duration:0,elapsed=music!=null?music.Elapsed:0;
            ui.HudLabel(new Rect(r.x+151,r.y+52,78,18),TimeLabel(elapsed)+" / "+TimeLabel(duration),ui.HudSmall,.65f);
            Rect track=new Rect(r.x+12,r.y+74,r.width-24,1);
            WellnessUiTheme.Fill(track,new Color(.85f,.91f,.86f,.24f));
            track.width*=duration>0?Mathf.Clamp01(elapsed/duration):0;WellnessUiTheme.Fill(track,new Color(.85f,.95f,.86f,.72f));
        }
        private static string Fit(string value,GUIStyle style,float width)
        {
            if(style.CalcSize(new GUIContent(value)).x<=width)return value;
            int length=value.Length;
            while(length>1&&style.CalcSize(new GUIContent(value.Substring(0,length)+"…")).x>width)length--;
            return value.Substring(0,length)+"…";
        }
        private void DrawInteraction(Rect r,float width,float height)
        {
            var ui=Theme;
            if(!player.CursorReleased&&!player.IsTransitioning)ui.Dot(new Rect(width/2-1.5f,height/2-1.5f,3,3),new Color(1,1,.95f,.8f));
            string prompt=player.HudPrompt;if(string.IsNullOrEmpty(prompt))return;
            Backdrop(r);ui.HudLabel(new Rect(r.x+8,r.y+9,r.width-16,26),player.HudPromptKey+"   ·   "+prompt,ui.HudHeading,.92f);
        }
        private void DrawCaptions(Rect r)
        {
            float entrance=Mathf.Clamp01((Time.unscaledTime-subtitleEnteredAt)/.3f);
            float eased=1-Mathf.Pow(1-entrance,3);r.y+=(1-eased)*7;
            var ui=Theme;Backdrop(r);var captions=chat.Captions;
            bool user=captions.Who==WellnessCaptions.Speaker.Player||captions.Who==WellnessCaptions.Speaker.Listening;
            ui.HudLabel(new Rect(r.x+14,r.y+6,r.width-28,22),user?"You":chat.GuideName+"  ·  AI companion",ui.HudHeading,.75f*eased);
            string body=chat.IsConnected?captions.DisplayText:chat.StatusText;
            if(largeCaption==null)largeCaption=new GUIStyle(ui.HudText){fontSize=21};
            GUIStyle style=largerCaptions?largeCaption:ui.HudText;
            Rect viewport=new Rect(r.x+14,r.y+34,r.width-28,r.height-65);
            float textHeight=style.CalcHeight(new GUIContent(body),viewport.width-4);
            captionTarget=Mathf.Max(0,textHeight-viewport.height);
            GUI.BeginGroup(viewport);
            // A short, continuous soft reveal, not simulated one-letter-per-frame transcription.
            float reveal=Mathf.Clamp01(captions.RevealAge(Time.unscaledTime)/.16f);
            if(body!="...")ui.HudLabel(new Rect(2,Mathf.Lerp(1.5f,0,reveal)-captionOffset,viewport.width-4,Mathf.Max(textHeight,viewport.height)),body,style,Mathf.Lerp(.82f,1,reveal)*eased);
            if(body=="...")
                for(int i=0;i<3;i++)ui.Dot(new Rect(4+i*7,10+Mathf.Sin(Time.unscaledTime*2.5f-i*.75f)*1.5f,3,3),new Color(.9f,.95f,.91f,eased*.8f));
            GUI.EndGroup();
            if(player.CursorReleased)
            {
                float y=r.y+r.height-28;
                if(ui.HudAction(new Rect(r.x+5,y,139,25),"V  Voice controls"))chat.OpenPanel();
                if(ui.HudAction(new Rect(r.x+153,y,77,25),"T  Type"))chat.OpenTyping();
                if(ui.HudAction(new Rect(r.x+242,y,116,25),"Tab  Not now"))chat.PauseFromHud();
            }
        }
        public void DrawSettings()
        {
            var ui=Theme;GUILayout.Space(12);GUILayout.Label("Make the room comfortable for you",ui.Text);
            showJourney=GUILayout.Toggle(showJourney," Show the gentle checklist",chat.ToggleStyle);
            showCaptions=GUILayout.Toggle(showCaptions," Show conversation captions (inside talking zone only)",chat.ToggleStyle);
            largerCaptions=GUILayout.Toggle(largerCaptions," Larger caption text",chat.ToggleStyle);
            GUILayout.Label("HUD backdrop · transparent by default",ui.Small);panelOpacity=GUILayout.HorizontalSlider(panelOpacity,0,1);
            var music=BackgroundMusicPlayer.Instance;
            if(music!=null)
            {
                GUILayout.Space(12);GUILayout.Label("Music · "+music.TrackTitle,ui.Text);
                music.Volume=GUILayout.HorizontalSlider(music.Volume,0,.5f);
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Previous [",ui.Button))music.PlayPrevious();
                if(GUILayout.Button(music.IsPaused?"Resume (P)":"Pause (P)",ui.Button))music.TogglePause();
                if(GUILayout.Button("Next ]",ui.Button))music.PlayNext();
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(16);GUILayout.Label("F1 Settings · J Checklist · P Music · [ / ] Previous / next song\nV Voice controls · T Type · M Mute mic · X / Tab Pause voice\nWASD Walk · Mouse Look · E Interact · Space Stand\nEsc Cursor / close panel · Enter Resume exploring",ui.Small);
            GUILayout.Space(12);GUILayout.Label("Captions follow streamed AI text and audio timing when supplied. Your speech appears as soon as the service returns the completed utterance. No extra recording service is used. The clock follows game time.",ui.Small);
        }
        public static string TimeLabel(float seconds)
        {int value=Mathf.Max(0,Mathf.FloorToInt(seconds));return (value/60)+":"+(value%60).ToString("00");}
    }
}
