using System;
using TheLastWatch.Audio;
using TheLastWatch.Integrations;
using TheLastWatch.Environment;
using UnityEngine;

namespace TheLastWatch.UI
{
    // A functional recreation of the approved Quiet Glass composition, not a baked screenshot.
    public sealed class WellnessQuietGlassMenu : IDisposable
    {
        public enum Page { Audio, Display, Weather, Controls, Companions }
        private enum Confirmation { None, Defaults, Exit }
        private static readonly string[] Titles={"Audio & voice","Display & comfort","Sky & weather","Controls","Your companion"};
        private static readonly string[] Helpers={"Shape the sounds around you.","Make this space feel comfortable.","Let the world move at your pace.","A few simple ways to find your way.","A conversation, at your own pace."};
        private static readonly string[] Icons={"sound","display","cloud","controls"};
        private readonly WellnessVoiceChat chat;
        private readonly WellnessQuietGlassTheme ui;
        private Page page;
        private Confirmation confirmation;
        private float openedAt,vinylAngle,lastTime,noteClock;
        private int skyPage;
        private static readonly string[] SkyPages={"Weather","Clouds","Time of day","Night sky"};
        private static readonly string[] CloudShapes={"Soft banks","Puffy cumulus","Altocumulus"};
        private static readonly string[] WeatherModes={"Auto","Clear","Cloudy","Rain"};
        private static readonly string[] TimePresets={"Morning","Noon","Sunset","Night"};
        private static readonly float[] PresetHours={8,12,18,0};
        private bool reducedMotion,clearFocus;
        private Vector2 scroll;
        public WellnessQuietGlassMenu(WellnessVoiceChat owner)
        {
            chat=owner;ui=new WellnessQuietGlassTheme(owner.quietGlassSerif,owner.quietGlassSans);
        }
        public void Open(Page next)
        {
            page=next;confirmation=Confirmation.None;scroll=Vector2.zero;openedAt=Time.unscaledTime;lastTime=Time.unscaledTime;
            clearFocus=true;
        }
        public void Draw()
        {
            float scale=WellnessQuietGlassLayout.Scale(Screen.width,Screen.height);
            if(scale<=0)return;
            var previous=GUI.matrix;Color oldColor=GUI.color;bool oldEnabled=GUI.enabled;
            try
            {
                if(clearFocus){GUI.FocusControl(null);clearFocus=false;}
                // Dim only while paused. The live game remains the backdrop; no second camera or blur shader.
                GUI.matrix=Matrix4x4.identity;
                WellnessQuietGlassTheme.Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.09f,.12f,.085f,.20f));
                GUI.matrix=Matrix4x4.TRS(new Vector3(WellnessQuietGlassLayout.OffsetX(Screen.width,Screen.height),WellnessQuietGlassLayout.OffsetY(Screen.width,Screen.height),0),Quaternion.identity,new Vector3(scale,scale,1));
                float fade=reducedMotion?1:Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-openedAt)/.18f));
                GUI.color=new Color(oldColor.r,oldColor.g,oldColor.b,oldColor.a*fade);
                ui.Surface(new Rect(216,102,1236,737));
                WellnessQuietGlassTheme.Fill(new Rect(563,103,1.2f,734),WellnessQuietGlassTheme.LineColor);
                GUI.enabled=confirmation==Confirmation.None;
                Sidebar();
                GUI.Label(new Rect(612,149,798,66),Titles[(int)page],ui.Title);
                ui.Line(612,216,798);GUI.Label(new Rect(612,230,798,29),Helpers[(int)page],ui.Small);
                switch(page)
                {
                    case Page.Audio:AudioPage();break;
                    case Page.Display:DisplayPage();break;
                    case Page.Weather:WeatherPage();break;
                    case Page.Controls:ControlsPage();break;
                    case Page.Companions:LegacyPage(false);break;
                }
                Footer();GUI.enabled=true;
                if(confirmation!=Confirmation.None)Confirm();
            }
            finally{GUI.matrix=previous;GUI.color=oldColor;GUI.enabled=oldEnabled;}
        }
        private void Sidebar()
        {
            GUI.Label(new Rect(259,132,286,66),"MindSpace",ui.Brand);
            GUI.Label(new Rect(259,200,260,31),"P A U S E D",ui.Small);
            Rect resume=new Rect(254,251,272,65);
            ui.Rounded(resume,WellnessQuietGlassTheme.Sage,13);
            if(GUI.Button(resume,GUIContent.none,ui.Primary))chat.ClosePanel();
            ui.Icon("play",new Rect(275,268,29,29),new Color(1,1,.97f));
            GUI.Label(new Rect(324,262,99,42),"Resume",ui.Primary);
            for(int i=0;i<4;i++)
            {
                float y=338+i*63;
                Rect item=new Rect(251,y,276,57);
                bool selected=(int)page==i||(page==Page.Companions&&i==0);
                if(selected)
                {
                    ui.Rounded(item,new Color(.56f,.63f,.46f,.20f),11);
                    ui.Rounded(new Rect(251,y+3,4,51),new Color(.33f,.43f,.28f),2);
                }
                if(GUI.Button(item,Titles[i],selected?ui.Selected:ui.Nav)){page=(Page)i;scroll=Vector2.zero;GUI.FocusControl(null);}
                ui.Icon(Icons[i],new Rect(269,y+13,35,35),WellnessQuietGlassTheme.Ink);
            }
            ui.Line(254,605,271);
            if(GUI.Button(new Rect(251,616,276,57),"Exit game",ui.Nav))confirmation=Confirmation.Exit;
            ui.Icon("exit",new Rect(271,627,33,33),WellnessQuietGlassTheme.Ink);
            Music();
        }
        private void Music()
        {
            var music=BackgroundMusicPlayer.Instance;
            bool playing=music!=null&&music.IsPlaying;
            if(Event.current.type==EventType.Repaint)
            {
                float dt=Mathf.Clamp(Time.unscaledTime-lastTime,0,.1f);lastTime=Time.unscaledTime;
                if(playing&&!reducedMotion){vinylAngle=Mathf.Repeat(vinylAngle+dt*24,360);noteClock+=dt;}
            }
            ui.Rounded(new Rect(255,713,96,95),new Color(.59f,.55f,.42f,.30f),10);
            ui.Vinyl(new Rect(264,721,79,79),vinylAngle,noteClock,playing&&!reducedMotion);
            string title=music!=null?music.TrackTitle:"No music loaded",artist=music!=null?music.TrackArtist:"";
            GUI.Label(new Rect(373,719,166,27),ui.Fit(title,ui.Small,162),ui.Small);
            GUI.Label(new Rect(373,745,166,23),ui.Fit(artist,ui.Hint,162),ui.Hint);
            bool prior=GUI.enabled;GUI.enabled=prior&&music!=null;
            if(ui.IconAction("previous",new Rect(362,768,39,39),"Previous track"))music.PlayPrevious();
            if(ui.IconAction(playing?"pause":"play",new Rect(414,768,39,39),playing?"Pause music":"Play music"))music.TogglePause();
            if(ui.IconAction("next",new Rect(466,768,39,39),"Next track"))music.PlayNext();
            GUI.enabled=prior;
        }
        private void AudioPage()
        {
            var music=BackgroundMusicPlayer.Instance;
            bool prior=GUI.enabled;GUI.enabled=prior&&music!=null;
            float musicVolume=Volume("Music",285,music!=null?music.Volume:0,"QuietMusic");
            if(music!=null)music.Volume=musicVolume;
            GUI.enabled=prior;
            OutdoorNatureAmbience.MasterVolume=Volume("Nature sounds",354,OutdoorNatureAmbience.MasterVolume,"QuietNature");
            chat.companionVolume=Volume("Companion voice",423,chat.companionVolume,"QuietVoice");
            ui.Card(new Rect(612,499,798,114));
            GUI.Label(new Rect(634,523,480,36),"Microphone",ui.Body);
            GUI.Label(new Rect(634,558,495,26),"Used for voice conversations with your companion.",ui.Hint);
            string state=chat.IsBusy?"Wait…":chat.IsConnected&&!chat.IsMuted?"Live":chat.IsMuted?"Muted":chat.VoiceRequested?"Ready":"Off";
            GUI.Label(new Rect(1126,528,83,35),state,ui.Percent);
            GUI.enabled=prior&&!chat.IsBusy;
            string action=!chat.HasVoiceConsent?"Enable voice":!chat.VoiceRequested?"Resume voice":chat.IsMuted?"Unmute":"Mute voice";
            if(ui.Action(new Rect(1210,515,180,53),action))chat.MenuMicrophoneAction();
            GUI.enabled=prior;
            GUI.Label(new Rect(1193,580,205,24),"Voice is always your choice.",ui.Hint);
            if(GUI.Button(new Rect(634,584,471,25),"Choose companion & voice controls  →",ui.Hint))page=Page.Companions;
            GUI.Label(new Rect(612,650,580,39),"Closed captions",ui.Body);
            GUI.Label(new Rect(612,689,601,29),"Show your words and your companion’s replies as text.",ui.Small);
            GUI.enabled=prior&&chat.hud!=null;
            bool captions=chat.hud!=null&&chat.hud.showCaptions;
            bool next=ui.Toggle(new Rect(1260,650,64,37),captions,"QuietCaptions");
            if(chat.hud!=null)chat.hud.showCaptions=next;
            GUI.Label(new Rect(1345,649,59,38),next?"On":"Off",ui.Percent);GUI.enabled=prior;
        }
        private float Volume(string label,float y,float value,string control)
        {
            GUI.Label(new Rect(612,y,226,39),label,ui.Body);
            float next=ui.Slider(new Rect(840,y,434,40),value,0,1,control);
            GUI.Label(new Rect(1309,y,97,39),Mathf.RoundToInt(next*100)+"%",ui.Percent);return next;
        }
        private void DisplayPage()
        {
            var hud=chat.hud;bool prior=GUI.enabled;GUI.enabled=prior&&hud!=null;
            bool journey=hud!=null&&hud.showJourney;
            if(hud!=null)hud.showJourney=Option(285,"Gentle reminders","Keep the small, optional journey list in view.",journey,"Journey");
            if(hud!=null)hud.showCaptions=Option(377,"Closed captions","Read the conversation while you listen.",hud.showCaptions,"Captions");
            if(hud!=null)hud.largerCaptions=Option(469,"Larger captions","Give dialogue a little more room.",hud.largerCaptions,"Larger captions");
            GUI.enabled=prior;
            reducedMotion=Option(550,"Reduced menu motion","Keep the record still and remove the menu fade.",reducedMotion,"Reduced motion");
            GUI.enabled=prior&&hud!=null;
            if(hud!=null)hud.panelOpacity=Volume("HUD backdrop",631,hud.panelOpacity,"Backdrop");
            GUI.Label(new Rect(612,686,205,38),"Theatre view",ui.Body);
            GUI.Label(new Rect(815,693,315,28),"HUD hides · letterbox after 5 seconds · Esc returns",ui.Hint);
            if(hud!=null&&ui.Action(new Rect(1180,681,230,47),"Enter theatre mode"))hud.EnterTheatreMode();
            GUI.enabled=prior;
        }
        private bool Option(float y,string title,string hint,bool value,string control)
        {
            GUI.Label(new Rect(612,y,607,38),title,ui.Body);GUI.Label(new Rect(612,y+39,626,27),hint,ui.Small);
            bool next=ui.Toggle(new Rect(1260,y+7,64,37),value,control);
            GUI.Label(new Rect(1345,y+6,60,38),next?"On":"Off",ui.Percent);return next;
        }
        private void LegacyPage(bool weather)
        {
            // The legacy companion controls manage GUI.enabled internally; never give them input behind a modal.
            if(confirmation!=Confirmation.None)return;
            GUILayout.BeginArea(new Rect(612,271,798,452));
            scroll=GUILayout.BeginScrollView(scroll);
            chat.DrawQuietGlassContents(weather,ui);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        private void WeatherPage()
        {
            var sky=chat.skyCycle;
            if(sky==null){GUI.Label(new Rect(612,295,798,50),"The sky cycle is not available in this scene.",ui.Small);return;}
            skyPage=Segments(new Rect(612,273,798,43),SkyPages,skyPage,"Sky section");
            if(skyPage==0)
            {
                int mode=Segments(new Rect(612,344,798,48),WeatherModes,(int)sky.weatherMode,"Weather mode");
                if(mode!=(int)sky.weatherMode)sky.SetWeather((WellnessSkyCycle.WeatherMode)mode);
                GUI.Label(new Rect(612,405,798,30),"Choose Auto for a gentle cycle from clear skies to rain.",ui.Small);
                bool prior=GUI.enabled;GUI.enabled=prior&&chat.rain!=null;
                float rain=RangeRow("Rain strength",460,chat.rain!=null?chat.rain.intensity:0,0,1,"%","Rain strength");
                if(chat.rain!=null)chat.rain.intensity=rain;GUI.enabled=prior;
                sky.weatherMinutes=Mathf.Round(RangeRow("Weather cycle",538,sky.weatherMinutes,2,20,"min","Weather cycle"));
                ui.Card(new Rect(612,621,798,91));
                GUI.Label(new Rect(634,639,755,28),"Cloud shapes have their own events and controls in Clouds.",ui.Small);
                GUI.Label(new Rect(634,674,755,26),"Changing the rain never changes your selected cloud family.",ui.Hint);
            }
            else if(skyPage==1)CloudsPage(sky.cloudDeck);
            else if(skyPage==2)
            {
                sky.timeRuns=Option(342,"Day & night cycle","Let morning, sunset and night unfold naturally.",sky.timeRuns,"Day and night");
                float nextHour=RangeRow("Time of day",464,sky.Hour,0,23.99f,"clock","Time of day");
                if(Mathf.Abs(nextHour-sky.Hour)>.01f)sky.SetHour(nextHour);
                for(int i=0;i<4;i++)
                {
                    Rect r=new Rect(612+i*204,528,186,45);
                    if(ui.Action(r,TimePresets[i]))sky.SetHour(PresetHours[i]);
                }
                sky.dayMinutes=Mathf.Round(RangeRow("Full day length",616,sky.dayMinutes,4,30,"min","Full day length"));
                GUI.Label(new Rect(612,682,798,30),"Cycle lengths are measured in real-world minutes.",ui.Small);
            }
            else
            {
                sky.shootingStars=Option(332,"Shooting stars","Occasional streaks and small meteor showers.",sky.shootingStars,"Shooting stars");
                sky.auroraBorealis=Option(420,"Aurora borealis","Slow ribbons of northern light after dark.",sky.auroraBorealis,"Aurora borealis");
                sky.nightEffectsIntensity=RangeRow("Sky effects",510,sky.nightEffectsIntensity,0,1,"%","Night effects");
                ui.Card(new Rect(612,574,798,144));
                bool prior=GUI.enabled;GUI.enabled=prior&&sky.CanSummonAurora;
                GUI.SetNextControlName("Summon northern lights");
                if(ui.Action(new Rect(634,590,316,46),"Summon northern lights",true))sky.SummonAurora();
                GUI.enabled=prior;
                string status=sky.AuroraSummoned?"Active · "+Mathf.CeilToInt(sky.AuroraSummonSecondsRemaining)+"s remaining":"One gentle, two-minute display";
                GUI.Label(new Rect(970,601,418,28),status,ui.Hint);
                GUI.Label(new Rect(634,645,755,27),"Sets a clear night and enables aurora; fades out by dawn.",ui.Hint);
                GUI.Label(new Rect(634,679,755,27),"Future nights stay random. Choose Auto to resume the weather cycle.",ui.Hint);
            }
        }
        private void CloudsPage(WellnessCloudDeck deck)
        {
            if(deck==null){GUI.Label(new Rect(612,342,798,60),"The cloud deck is not available in this scene.",ui.Small);return;}
            bool prior=GUI.enabled;
            bool enabled=deck.CloudMode!=WellnessCloudMode.Off;
            bool nextEnabled=Option(332,"Clouds","Only one cloud family is visible at a time.",enabled,"Show clouds");
            if(nextEnabled!=enabled)deck.SetCloudMode(nextEnabled?WellnessCloudMode.Automatic:WellnessCloudMode.Off);
            GUI.enabled=prior&&nextEnabled&&deck.CloudTypesAvailable;
            bool automatic=deck.CloudMode==WellnessCloudMode.Automatic;
            bool nextAutomatic=Option(420,"Automatic cloud events","Change shapes every 3–5 minutes, separately from rain.",automatic,"Automatic cloud events");
            if(nextAutomatic!=automatic)deck.SetCloudMode(nextAutomatic?WellnessCloudMode.Automatic:(WellnessCloudMode)((int)deck.ActiveCloudType+2));
            GUI.Label(new Rect(612,514,798,36),"Cloud shape",ui.Body);
            Rect row=new Rect(612,554,798,48);ui.Rounded(row,new Color(.49f,.56f,.42f,.08f),12);
            for(int i=0;i<CloudShapes.Length;i++)
            {
                Rect item=new Rect(row.x+i*266+3,row.y+3,260,row.height-6);
                if((int)deck.ActiveCloudType==i)ui.Rounded(item,new Color(.51f,.60f,.43f,.23f),9);
                GUI.SetNextControlName("Cloud shape "+i);
                // Even clicking the active shape locks it in manual mode.
                if(GUI.Button(item,CloudShapes[i],ui.Button))deck.SetCloudMode((WellnessCloudMode)(i+2));
            }
            GUI.enabled=prior&&nextEnabled;
            deck.windMetresPerSecond=RangeRow("Cloud drift",629,deck.windMetresPerSecond,0,4,"m/s","Cloud drift");
            GUI.enabled=prior;
            string hint=!deck.CloudTypesAvailable?"The cloud textures need installing before shape selection is available.":
                !nextEnabled?"All clouds are hidden. Rain and night effects keep their own settings.":
                deck.CloudMode==WellnessCloudMode.Automatic?"Auto fades one family away before the next. Choose a shape to keep it.":
                "Manual shape stays selected. Turn on Automatic cloud events to resume.";
            GUI.Label(new Rect(612,686,798,30),hint,ui.Hint);
        }
        private int Segments(Rect row,string[] labels,int selected,string control)
        {
            ui.Rounded(row,new Color(.49f,.56f,.42f,.08f),12);
            float slot=row.width/labels.Length;
            for(int i=0;i<labels.Length;i++)
            {
                Rect r=new Rect(row.x+slot*i+3,row.y+3,slot-6,row.height-6);
                if(i==selected)ui.Rounded(r,new Color(.51f,.60f,.43f,.23f),9);
                GUI.SetNextControlName(control+" "+i);
                if(GUI.Button(r,labels[i],ui.Button))selected=i;
            }
            return selected;
        }
        private float RangeRow(string label,float y,float value,float min,float max,string units,string control)
        {
            GUI.Label(new Rect(612,y,226,39),label,ui.Body);
            float next=ui.Slider(new Rect(840,y,434,40),value,min,max,control);
            string text=units=="%"?Mathf.RoundToInt(next*100)+"%":units=="clock"?chat.skyCycle.Clock:
                units=="m/s"?next.ToString("0.0")+" m/s":Mathf.RoundToInt(next)+" min";
            GUI.Label(new Rect(1305,y,105,39),text,ui.Percent);return next;
        }
        private void ControlsPage()
        {
            string[] keys={"W A S D","Mouse","E","Esc","V / T","M / X","J / R"};
            string[] labels={"Move around the room and garden","Look around","Interact · sit · open or close the door","Open settings / return to your space","Companion controls / type a message","Mute microphone / pause voice","Show gentle reminders / toggle rain"};
            for(int i=0;i<keys.Length;i++)
            {
                float y=281+i*56;
                ui.Outline(new Rect(612,y+2,134,37),new Color(.39f,.45f,.35f,.40f),6);
                GUI.Label(new Rect(626,y+6,115,29),keys[i],ui.Small);
                GUI.Label(new Rect(773,y+3,635,38),labels[i],ui.Small);
            }
            GUI.Label(new Rect(612,689,798,30),"Voice controls never bypass your microphone permission.",ui.Hint);
        }
        private void Footer()
        {
            ui.Line(612,741,798);
            ui.Outline(new Rect(613,772,44,33),new Color(.34f,.40f,.30f,.65f),5);
            GUI.Label(new Rect(623,777,35,28),"Esc",ui.Hint);
            if(ui.TextAction(new Rect(677,773,191,38),"Resume"))chat.ClosePanel();
            if(page==Page.Audio||page==Page.Display)
            {if(ui.TextAction(new Rect(1130,773,143,38),"Reset defaults"))confirmation=Confirmation.Defaults;}
            WellnessQuietGlassTheme.Fill(new Rect(1276,772,1.2f,32),WellnessQuietGlassTheme.LineColor);
            bool day=chat.skyCycle!=null&&WellnessHud.IsDay(chat.skyCycle.Hour);
            ui.Icon(day?"sun":"moon",new Rect(1307,773,29,29),WellnessQuietGlassTheme.Muted);
            GUI.Label(new Rect(1355,775,69,34),chat.skyCycle!=null?chat.skyCycle.Clock:"--:--",ui.TextButton);
        }
        private void Confirm()
        {
            WellnessQuietGlassTheme.Fill(new Rect(216,102,1236,737),new Color(.10f,.14f,.09f,.37f));
            Rect card=new Rect(556,322,610,275);ui.Surface(card);
            bool exiting=confirmation==Confirmation.Exit;
            GUI.Label(new Rect(591,349,540,55),exiting?"Leave your quiet space?":"Restore these settings?",ui.Body);
            var message=new GUIStyle(ui.Small){wordWrap=true};
            GUI.Label(new Rect(591,408,536,75),exiting?"This ends the current play session and voice conversation.":"Only this page’s preferences will reset. Your voice permission and conversation will not change.",message);
            if(ui.Action(new Rect(591,514,245,51),"Stay here"))confirmation=Confirmation.None;
            if(ui.Action(new Rect(851,514,277,51),exiting?"Exit game":"Reset this page",true))
            {
                if(exiting)chat.ExitFromMenu();else ResetPage();confirmation=Confirmation.None;
            }
        }
        private void ResetPage()
        {
            // Deliberately no permission/session calls here.
            if(page==Page.Audio)
            {
                if(BackgroundMusicPlayer.Instance!=null)BackgroundMusicPlayer.Instance.Volume=.10f;
                OutdoorNatureAmbience.MasterVolume=1;chat.companionVolume=1;
                if(chat.hud!=null)chat.hud.showCaptions=true;
            }
            else if(page==Page.Display)
            {
                reducedMotion=false;
                if(chat.hud!=null){chat.hud.showJourney=true;chat.hud.showCaptions=true;chat.hud.largerCaptions=false;chat.hud.panelOpacity=0;}
            }
        }
        public void Dispose()=>ui.Dispose();
    }
}
