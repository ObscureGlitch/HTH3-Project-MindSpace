using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ElevenLabs.Agents;
using ElevenLabs.Protocol;
using TheLastWatch.Player;
using TheLastWatch.Environment;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastWatch.Integrations
{
    // Uses the same public agents and Conversation API as Hackathon26's TalkingBox.
    // Unlike the sample, no trigger, Awake or OnEnable call can start the microphone.
    [DisallowMultipleComponent]
    public sealed class WellnessVoiceChat : MonoBehaviour
    {
        public WellnessVoiceSettings settings;
        public WellnessExplorer player;
        public WellnessRain rain;
        public AudioSource voiceAudio;
        public WellnessSkyCycle skyCycle;
        public bool IsConnected => conversation != null && conversation.Status == Status.Connected;
        public bool IsPanelOpen => panelOpen;
        public bool IsBusy => connecting || closing;
        private Conversation conversation;
        private CancellationTokenSource startup;
        private int generation, selected;
        private bool panelOpen, consent, connecting, closing, muted, changingMute;
        private float connectedAt;
        private string status = "Microphone off", typedMessage = "";
        private Vector2 scroll, panelScroll;
        private int panelTab;
        private static readonly string[] PanelTabs={"Companions","Sky & weather"};
        private static readonly string[] WeatherTabs={"Auto","Clear","Cloudy","Rain"};
        private GUIStyle titleStyle, textStyle, smallStyle, buttonStyle;
        private Texture2D panelTexture;
        private readonly List<Line> transcript = new List<Line>();
        private readonly ConcurrentQueue<Notice> notices = new ConcurrentQueue<Notice>();
        private Action<UserTranscriptArgs> userHandler;
        private Action<AgentResponseArgs> agentHandler;
        private Action<AgentResponseCorrectionArgs> correctionHandler;
        private Action<DisconnectionDetails> disconnectHandler;
        private Action<string> errorHandler;
        private sealed class Line { public string role, text; public int id; }
        private sealed class Notice { public int generation, id; public string role, text; }

        private void Update()
        {
            if (!Application.isPlaying) return;
            var keyboard = Keyboard.current;
            // While editing a text field, letter keys belong to that field.
            if (!panelOpen && keyboard != null)
            {
                if (keyboard.vKey.wasPressedThisFrame) OpenPanel();
                if (keyboard.rKey.wasPressedThisFrame)
                {
                    if(skyCycle!=null && skyCycle.isActiveAndEnabled)skyCycle.ToggleRain();
                    else if(rain!=null)rain.rainEnabled=!rain.rainEnabled;
                }
                if (keyboard.xKey.wasPressedThisFrame) _ = StopAsync();
                if (keyboard.mKey.wasPressedThisFrame && IsConnected) _ = ToggleMuteAsync();
            }
            else if (panelOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            { ClosePanel(); _ = StopAsync(); }
            for (int i=0; i<16 && notices.TryDequeue(out Notice notice); i++)
            {
                if (notice.generation != generation) continue;
                if (notice.role == "error") { _ = StopAsync("Connection ended. Check network and agent access."); continue; }
                if (notice.role == "end") { _ = StopAsync(); continue; }
                if (notice.role == "correction")
                {
                    Line line = transcript.FindLast(l => l.role == "Companion" && l.id == notice.id);
                    if (line != null) line.text = notice.text;
                }
                else
                {
                    transcript.Add(new Line { role=notice.role, text=notice.text, id=notice.id });
                    if (transcript.Count>12) transcript.RemoveAt(0);
                }
                scroll.y = float.MaxValue;
            }
            if (IsConnected && settings != null && Time.realtimeSinceStartup-connectedAt > settings.maximumSessionSeconds)
                _ = StopAsync("Session time limit reached. You can start a new conversation.");
            if (rain != null) rain.VoiceDucking = IsConnected || connecting;
        }

        public void OpenPanel()
        {
            panelOpen = true;
            if (player != null) player.SetUiInputBlocked(true);
        }
        public void ClosePanel()
        {
            panelOpen = false;
            if (player != null) player.SetUiInputBlocked(false);
        }
        private static string Limit(string value) => string.IsNullOrEmpty(value) ? "" : value.Substring(0,Math.Min(1400,value.Length));
        private void Queue(int session, string role, string text, int id = 0)
        {
            if (notices.Count<64) notices.Enqueue(new Notice { generation=session, role=role, text=Limit(text), id=id });
        }
        private void Subscribe(Conversation session, int epoch)
        {
            userHandler = e => Queue(epoch,"You",e.UserTranscript,e.EventId);
            agentHandler = e => Queue(epoch,"Companion",e.AgentResponse,e.EventId);
            correctionHandler = e => Queue(epoch,"correction",e.CorrectedAgentResponse,e.EventId);
            disconnectHandler = _ => Queue(epoch,"end","");
            errorHandler = _ => Queue(epoch,"error",""); // No raw service payloads in UI or logs.
            session.UserTranscriptReceived += userHandler; session.AgentResponded += agentHandler;
            session.AgentResponseCorrected += correctionHandler; session.Disconnected += disconnectHandler;
            session.ErrorOccurred += errorHandler;
        }
        private void Unsubscribe(Conversation session)
        {
            session.UserTranscriptReceived -= userHandler; session.AgentResponded -= agentHandler;
            session.AgentResponseCorrected -= correctionHandler; session.Disconnected -= disconnectHandler;
            session.ErrorOccurred -= errorHandler;
        }

        private async Awaitable StartAsync()
        {
            if (!Application.isPlaying || !consent || IsBusy || conversation != null) return;
            if (settings == null || settings.agents == null || settings.agents.Length == 0)
            { status="No agent configured. Check the Voice Companions settings asset."; return; }
            selected=Mathf.Clamp(selected,0,settings.agents.Length-1);
            WellnessVoiceSettings.Agent agent=settings.agents[selected];
            if (agent == null || string.IsNullOrWhiteSpace(agent.agentId)) { status="This companion has no agent ID."; return; }
#if !UNITY_WEBGL || UNITY_EDITOR
            if (Microphone.devices.Length == 0) { status="No microphone available. Check Windows microphone permissions."; return; }
#endif
            connecting=true; muted=false; transcript.Clear(); typedMessage="";
            int epoch=++generation; status="Connecting… X or End cancels.";
            var cancel=new CancellationTokenSource(); startup=cancel;
            cancel.CancelAfter(TimeSpan.FromSeconds(Mathf.Clamp(settings.connectionTimeoutSeconds,10,30)));
            Conversation opened=null;
            try
            {
                var options=new ConversationOptions
                {
                    AgentId=agent.agentId, ConnectionType=ConnectionType.WebSocket,
                    OutputAudioSource=voiceAudio, EnableDebugLogging=false,
                    StartupCancellationToken=cancel.Token,
                    DynamicVariables=new Dictionary<string,object>{{"color",agent.colorVariable ?? "yellow"}}
                };
                opened=await Conversation.StartSessionAsync(options);
                await Awaitable.MainThreadAsync();
                if (cancel.IsCancellationRequested || this == null || !isActiveAndEnabled || generation != epoch)
                { await opened.EndSession(); opened=null; return; }
                conversation=opened; Subscribe(opened,epoch); opened.SetVolume(.85f);
                connectedAt=Time.realtimeSinceStartup; status="Microphone live — speaking with "+agent.displayName;
            }
            catch (OperationCanceledException)
            { if (this != null && epoch==generation) status="Connection cancelled or timed out. Microphone off."; }
            catch (Exception)
            {
                if (opened != null) { try { await opened.EndSession(); } catch { } }
                if (this != null && epoch==generation) { conversation=null; status="Could not connect. Check internet, microphone permission and public-agent access."; }
            }
            finally
            {
                if (this != null)
                {
                    if (ReferenceEquals(startup,cancel)) startup=null; connecting=false;
                    if (generation!=epoch && conversation==null && !closing) status="Microphone off";
                }
                cancel.Dispose();
            }
        }
        private async Awaitable StopAsync(string finalStatus="Microphone off")
        {
            ++generation; startup?.Cancel();
            Conversation current=conversation; conversation=null; consent=false; muted=false;
            transcript.Clear(); typedMessage=""; while(notices.TryDequeue(out _)) { }
            if (rain != null) rain.VoiceDucking=false;
            if (current==null) { status=connecting?"Cancelling connection…":finalStatus; return; }
            closing=true; status="Ending conversation…"; Unsubscribe(current);
            try { await current.EndSession(); }
            catch { finalStatus="Session closed. Check microphone permissions if the device stays busy."; }
            finally { if(this != null){closing=false;status=finalStatus;} }
        }
        private async Awaitable ToggleMuteAsync()
        {
            Conversation current=conversation; if(current==null||closing||changingMute)return;
            changingMute=true;
            bool next=!muted;
            try { await current.SetMicMuted(next); if(ReferenceEquals(current,conversation)){muted=next;status=muted?"Muted — no microphone audio sent":"Microphone live";} }
            catch { _ = StopAsync("Audio connection failed. Microphone off."); }
            finally { changingMute=false; }
        }
        private void SendText()
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(typedMessage)) return;
            string text=Limit(typedMessage.Trim()); typedMessage="";
            try { conversation.SendUserMessage(text); Queue(generation,"You",text); }
            catch { _ = StopAsync("Message could not be sent. Microphone off."); }
        }
        private void OnApplicationFocus(bool focused) { if(!focused) _=StopAsync("Conversation stopped when the game lost focus."); }
        private void OnApplicationPause(bool paused) { if(paused) _=StopAsync(); }
        private void OnDisable() { ClosePanel(); _=StopAsync(); }
        private void OnDestroy() { if(panelTexture!=null)Destroy(panelTexture); }

        private void Styles()
        {
            if(panelTexture!=null)return;
            panelTexture=new Texture2D(1,1);panelTexture.SetPixel(0,0,new Color(.10f,.16f,.15f,.97f));panelTexture.Apply();
            textStyle=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true,richText=false,normal={textColor=new Color(.93f,.92f,.85f)}};
            titleStyle=new GUIStyle(textStyle){fontSize=25,fontStyle=FontStyle.Bold};
            smallStyle=new GUIStyle(textStyle){fontSize=13};
            buttonStyle=new GUIStyle(GUI.skin.button){fontSize=15,wordWrap=true,padding=new RectOffset(10,10,8,8)};
        }
        private void OnGUI()
        {
            if(!Application.isPlaying)return;Styles();Matrix4x4 previous=GUI.matrix;
            float scale=Mathf.Max(.3f,Mathf.Min(Screen.width/1280f,Screen.height/720f));GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            float width=Screen.width/scale,height=Screen.height/scale;
            try
            {
                GUI.DrawTexture(new Rect(width-390,24,366,98),panelTexture);
                GUI.Label(new Rect(width-374,34,338,28),IsConnected?(muted?"Voice muted · M unmute · X end":"MIC LIVE · M mute · X end"):"V  Companions     R  Rain",smallStyle);
                GUI.Label(new Rect(width-374,61,338,24),skyCycle!=null?skyCycle.Summary:rain!=null&&rain.rainEnabled?"Gentle rain on":"Rain off",smallStyle);
                if(GUI.Button(new Rect(width-374,86,338,27),panelOpen?"Close & end conversation":"Voice & weather controls",buttonStyle))
                {if(panelOpen){ClosePanel();_=StopAsync();}else OpenPanel();}
                if(!panelOpen)return;
                Rect panel=new Rect((width-550)/2,(height-632)/2,550,632);GUI.DrawTexture(panel,panelTexture);
                GUILayout.BeginArea(new Rect(panel.x+20,panel.y+16,panel.width-40,panel.height-32));
                GUILayout.Label(panelTab==1?"A sky that changes gently":"A voice beside you",titleStyle);
                if(skyCycle!=null)panelTab=GUILayout.SelectionGrid(panelTab,PanelTabs,2,buttonStyle);
                if(panelTab==1 && skyCycle!=null)
                {
                    panelScroll=GUILayout.BeginScrollView(panelScroll);
                    DrawWeatherControls();GUILayout.EndScrollView();
                    if(GUILayout.Button(IsConnected?"Continue walking — microphone stays live":"Return to the garden",buttonStyle))ClosePanel();
                    GUILayout.EndArea();return;
                }
                panelScroll=GUILayout.BeginScrollView(panelScroll);
                GUILayout.Label("AI companions · not clinicians or emergency services",smallStyle);
                GUI.enabled=!IsBusy&&!IsConnected;
                if(settings!=null&&settings.agents!=null&&settings.agents.Length>0)
                {
                    var names=new string[settings.agents.Length];for(int i=0;i<names.Length;i++)names[i]=settings.agents[i].displayName;
                    selected=GUILayout.SelectionGrid(selected,names,Mathf.Min(2,names.Length),buttonStyle);
                }
                GUI.enabled=true;GUILayout.Label(status,textStyle);
                if(!IsConnected&&!IsBusy)
                {
                    GUILayout.Label("Starting sends your microphone audio and typed messages to ElevenLabs and the selected agent's configured services. This game does not save transcripts. The provider/agent owner may retain them.",smallStyle);
                    consent=GUILayout.Toggle(consent," I agree to start this voice conversation");
                    GUI.enabled=consent; if(GUILayout.Button("Start conversation",buttonStyle))_=StartAsync();GUI.enabled=true;
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUI.enabled=IsConnected&&!closing;if(GUILayout.Button(muted?"Unmute":"Mute",buttonStyle))_=ToggleMuteAsync();GUI.enabled=true;
                    if(GUILayout.Button("End conversation",buttonStyle))_=StopAsync();
                    GUILayout.EndHorizontal();
                    GUILayout.Label("Mute sends silence. End releases the microphone. Losing focus also ends the call.",smallStyle);
                }
                scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(145));
                if(transcript.Count==0)GUILayout.Label("Conversation text appears here while connected.",smallStyle);
                foreach(Line line in transcript)GUILayout.Label(line.role+": "+line.text,smallStyle);
                GUILayout.EndScrollView();
                GUI.enabled=IsConnected;GUILayout.BeginHorizontal();typedMessage=GUILayout.TextField(typedMessage,1400,GUILayout.Height(30));
                if(GUILayout.Button("Send",buttonStyle,GUILayout.Width(72)))SendText();GUILayout.EndHorizontal();GUI.enabled=true;
                if(rain!=null && skyCycle==null)
                {
                    GUILayout.BeginHorizontal();rain.rainEnabled=GUILayout.Toggle(rain.rainEnabled," Gentle rain",GUILayout.Width(130));
                    GUILayout.Label("Intensity",smallStyle,GUILayout.Width(70));rain.intensity=GUILayout.HorizontalSlider(rain.intensity,0,1);GUILayout.EndHorizontal();
                }
                if(skyCycle!=null)GUILayout.Label("Clouds, time and rain: open the Sky & weather tab.",smallStyle);
                GUILayout.EndScrollView();
                if(GUILayout.Button(IsConnected?"Continue walking — microphone stays live":"Return to the room",buttonStyle))ClosePanel();
                GUILayout.EndArea();
            }
            finally{GUI.enabled=true;GUI.matrix=previous;}
        }
        private void DrawWeatherControls()
        {
            GUILayout.Space(10);GUILayout.Label(skyCycle.Summary,textStyle);
            GUILayout.Label("Weather changes slowly: clear → cloudy → gentle rain → clearing.",smallStyle);
            int mode=GUILayout.SelectionGrid((int)skyCycle.weatherMode,WeatherTabs,4,buttonStyle);
            if(mode!=(int)skyCycle.weatherMode)skyCycle.SetWeather((WellnessSkyCycle.WeatherMode)mode);
            GUILayout.Label("R switches to manual rain / clear. Choose Auto to resume the cycle.",smallStyle);
            GUILayout.Space(10);
            skyCycle.timeRuns=GUILayout.Toggle(skyCycle.timeRuns," Advance day and night automatically");
            GUILayout.Label("Time of day · "+skyCycle.Clock,textStyle);
            float nextHour=GUILayout.HorizontalSlider(skyCycle.Hour,0,23.99f);
            if(Mathf.Abs(nextHour-skyCycle.Hour)>.01f)skyCycle.SetHour(nextHour);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Morning",buttonStyle))skyCycle.SetHour(8);
            if(GUILayout.Button("Noon",buttonStyle))skyCycle.SetHour(12);
            if(GUILayout.Button("Sunset",buttonStyle))skyCycle.SetHour(18);
            if(GUILayout.Button("Night",buttonStyle))skyCycle.SetHour(0);
            GUILayout.EndHorizontal();
            GUILayout.Label("Full day: "+skyCycle.dayMinutes.ToString("0")+" real minutes",smallStyle);
            skyCycle.dayMinutes=Mathf.Round(GUILayout.HorizontalSlider(skyCycle.dayMinutes,4,30));
            GUILayout.Label("Weather loop: "+skyCycle.weatherMinutes.ToString("0")+" real minutes",smallStyle);
            skyCycle.weatherMinutes=Mathf.Round(GUILayout.HorizontalSlider(skyCycle.weatherMinutes,2,20));
            if(rain!=null){GUILayout.Label("Rain strength",smallStyle);rain.intensity=GUILayout.HorizontalSlider(rain.intensity,0,1);}
            if(skyCycle.cloudDeck!=null)
            {
                GUILayout.Label("Cloud drift · "+skyCycle.cloudDeck.windMetresPerSecond.ToString("0.0")+" m/s · independent of day speed",smallStyle);
                skyCycle.cloudDeck.windMetresPerSecond=GUILayout.HorizontalSlider(skyCycle.cloudDeck.windMetresPerSecond,0,4);
            }
            GUILayout.Space(12);GUILayout.Label("No lightning, volumetric clouds or extra shadow lights. The room keeps its warm lighting after dark.",smallStyle);
            if(IsConnected || IsBusy)
            {
                GUILayout.Label(status,smallStyle);
                if(GUILayout.Button("End conversation",buttonStyle))_=StopAsync();
            }
        }
    }
}
