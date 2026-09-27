using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ElevenLabs.Agents;
using ElevenLabs.Protocol;
using TheLastWatch.Player;
using TheLastWatch.Environment;
using TheLastWatch.Interaction;
using TheLastWatch.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastWatch.Integrations
{
    // Uses the same public agents and Conversation API as Hackathon26's TalkingBox.
    // A visible, affirmative per-run choice is required before any microphone session.
    [DisallowMultipleComponent]
    public sealed class WellnessVoiceChat : MonoBehaviour
    {
        public WellnessVoiceSettings settings;
        public WellnessExplorer player;
        public WellnessRain rain;
        public AudioSource voiceAudio;
        public WellnessSkyCycle skyCycle;
        public WellnessTherapist[] therapists = Array.Empty<WellnessTherapist>();
        [HideInInspector] public float hearingDistance = 3; // Legacy installer field; range now follows the room/door.
        public Transform roomSpace;
        public WellnessDoor entranceDoor;
        public Bounds localRoom = new Bounds(new Vector3(0,1.5f,0), new Vector3(7.2f,3.4f,6.2f));
        public Vector3 localDoorway = new Vector3(-2.43f,1.45f,-3.25f);
        [Range(2,10)] public float outdoorHearingDistance = 6;
        public bool showOnlySelected = true;
        public WellnessHud hud;
        public bool IsConnected => conversation != null && conversation.Status == Status.Connected;
        public bool IsPanelOpen => panelOpen || consentScreen;
        public bool HasVoiceConsent => permission.Granted;
        public bool IsMuted => muted;
        public string StatusText => status;
        public string GuideName => selected>=0&&selected<therapists.Length&&therapists[selected]!=null?therapists[selected].characterName:"Your guide";
        public string MicrophoneBadge => changingMute?"Updating microphone…":closing?"Stopping microphone…":
            IsConnected?(muted?"Mic muted · M unmute":"Mic live · M mute · X pause"):connecting?"Mic starting · X cancels":
            permission.ListeningRequested?(muted?"Mic muted · M unmute":"Mic off · waiting for range"):"Mic off · V voice controls";
        public WellnessCaptions Captions {get;} = new WellnessCaptions();
        public bool IsInTalkingZone => reachable;
        public event Action UserCaptionReceived;
        public bool IsBusy => connecting || closing;
        private Conversation conversation;
        private CancellationTokenSource startup;
        private int generation, selected;
        private bool panelOpen, connecting, closing, changingMute, consentScreen = true;
        private WellnessVoiceConsent permission = new WellnessVoiceConsent();
        private bool muted => permission.Muted;
        private float nextConnect, nextReachCheck, acousticDistance, doorDistance, opening, outputGain;
        private bool reachable, listenerIndoors;
        private string reachReason = "Checking room voice range…";
        private float connectedAt;
        private string status = "Microphone off", typedMessage = "";
        private Vector2 scroll, panelScroll, consentScroll;
        private int panelTab;
        private bool focusTypedInput;
        private float userVoiceUntil;
        private static readonly string[] PanelTabs={"Companions","Sky & weather","Display & audio"};
        private static readonly string[] WeatherTabs={"Auto","Clear","Cloudy","Rain"};
        private GUIStyle titleStyle, textStyle, smallStyle, buttonStyle;
        public GUIStyle ToggleStyle {get;private set;}
        private GUIStyle fieldStyle;
        private Texture2D panelTexture;
        private readonly List<Line> transcript = new List<Line>();
        private readonly ConcurrentQueue<Notice> notices = new ConcurrentQueue<Notice>();
        private Action<UserTranscriptArgs> userHandler;
        private Action<AgentResponseArgs> agentHandler;
        private Action<AgentChatResponsePartArgs> streamHandler;
        private Action<AudioResponseArgs> alignmentHandler;
        private Action<AgentResponseCorrectionArgs> correctionHandler;
        private Action<DisconnectionDetails> disconnectHandler;
        private Action<string> errorHandler;
        private Action<VadScoreArgs> vadHandler;
        private Action<InterruptionArgs> interruptionHandler;
        private sealed class Line { public string role, text; public int id; }
        private sealed class Notice
        {
            public int generation,id; public string role,text,partType;
            public string[] chars; public int[] starts,durations;
        }
        private readonly RaycastHit[] hearingHits = new RaycastHit[32];
        public int SelectedIndex => selected;
        public bool IsSpeakingWith(int index) => index==selected && IsConnected && conversation.Mode==Mode.Speaking;
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            permission = new WellnessVoiceConsent(); consentScreen = true; panelOpen = false;
            reachable = false; outputGain = 0; nextConnect = nextReachCheck = 0;
            Captions.Reset();userVoiceUntil=0;
            ConfigureVoiceAudio(); SyncUiLock();
        }
        private void Start() { SelectCharacter(selected); SyncUiLock(); }
        public void ConfigureVoiceAudio()
        {
            if (voiceAudio == null) return;
            voiceAudio.spatialBlend = 1; voiceAudio.dopplerLevel = 0;
            // Keep directional sound. SDK SetVolume applies our single acoustic-path falloff;
            // Unity's second distance falloff is neutral throughout the small voice region.
            voiceAudio.rolloffMode = AudioRolloffMode.Linear;
            voiceAudio.minDistance = 100; voiceAudio.maxDistance = 101;
            voiceAudio.mute = true; // Output stays silent until a consented, reachable session is ready.
        }
        private void SyncUiLock()
        {
            bool blocked = panelOpen || consentScreen;
            if (player != null) player.SetUiInputBlocked(blocked);
            if (blocked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        public void OpenFor(int index)
        {
            OpenPanel(); panelTab=0;
            if(IsBusy||IsConnected){if(index!=selected)status="End the current conversation before switching companions.";return;}
            SelectCharacter(index);
        }
        private void SelectCharacter(int index)
        {
            if(IsBusy||IsConnected)return;
            if(settings==null||settings.agents==null||index<0||index>=settings.agents.Length)return;
            selected=index;
            nextReachCheck = 0;
            for(int i=0;i<therapists.Length;i++)if(therapists[i]!=null)therapists[i].gameObject.SetActive(!showOnlySelected||i==selected);
            WellnessTherapist actor=selected<therapists.Length?therapists[selected]:null;
            if(actor!=null&&voiceAudio!=null&&actor.voiceAnchor!=null)
            {
                voiceAudio.transform.SetPositionAndRotation(actor.voiceAnchor.position,actor.voiceAnchor.rotation);
            }
        }
        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if(voiceAudio!=null&&selected>=0&&selected<therapists.Length&&therapists[selected]!=null&&therapists[selected].voiceAnchor!=null)
            {
                Transform anchor = therapists[selected].voiceAnchor;
                Vector3 position = !listenerIndoors && roomSpace != null ? roomSpace.TransformPoint(localDoorway) : anchor.position;
                voiceAudio.transform.SetPositionAndRotation(position, anchor.rotation);
                float target = reachable && permission.Granted && Application.isFocused ?
                    WellnessVoiceAcoustics.Gain(acousticDistance, listenerIndoors, opening, doorDistance, outdoorHearingDistance) : 0;
                // Immediately silence a closed doorway/out-of-range source; smooth ordinary movement.
                outputGain = target <= 0 ? 0 : Mathf.MoveTowards(outputGain, target, Time.unscaledDeltaTime * 1.8f);
                voiceAudio.mute = !IsConnected || !reachable || !permission.Granted || !Application.isFocused;
                if (IsConnected) conversation.SetVolume(outputGain);
            }
        }
        // Whole room, or a clear outdoor path through the actual open entrance. Fail closed if unconfigured.
        public bool CanHearSelected(out string reason)
        {
            reason="Voice range is not configured.";
            if(roomSpace==null||entranceDoor==null||!entranceDoor.isActiveAndEnabled||selected<0||selected>=therapists.Length||therapists[selected]==null||player==null||player.ViewCamera==null)return false;
            WellnessTherapist actor=therapists[selected];
            if(!actor.isActiveAndEnabled||actor.voiceAnchor==null)return false;
            Vector3 listener = roomSpace.InverseTransformPoint(player.ViewCamera.transform.position);
            Vector3 speaker = roomSpace.InverseTransformPoint(actor.voiceAnchor.position);
            opening = entranceDoor.VoiceOpening;
            doorDistance = Vector3.Distance(listener, localDoorway);
            bool clear = true;
            if (!localRoom.Contains(listener) && opening > .02f && doorDistance < outdoorHearingDistance)
            {
                Vector3 origin = roomSpace.TransformPoint(localDoorway);
                Vector3 delta = player.ViewCamera.transform.position - origin;
                int count = Physics.RaycastNonAlloc(origin,delta.normalized,hearingHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
                clear = count < hearingHits.Length;
                for(int i=0;i<count;i++)
                {
                    Transform hit=hearingHits[i].collider.transform;
                    if(hit.IsChildOf(player.transform)||hit.IsChildOf(actor.transform))continue;
                    clear = false; break;
                }
            }
            bool canHear = WellnessVoiceAcoustics.Evaluate(localRoom,listener,speaker,localDoorway,opening,
                outdoorHearingDistance,clear,out listenerIndoors,out acousticDistance);
            reason = canHear ? (listenerIndoors ? "Room-wide voice · distance changes volume." : "Outdoor voice through the open door · fades with distance.") :
                opening <= .02f ? "Outside voice is off while the door is closed. Return inside or open it." :
                "Return to the room or within "+outdoorHearingDistance.ToString("0")+" m of the open doorway, with a clear path to it.";
            return canHear;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (consentScreen) SyncUiLock();
            if (Time.unscaledTime >= nextReachCheck)
            { nextReachCheck=Time.unscaledTime+.1f; reachable=CanHearSelected(out reachReason); }
            // A closing door cuts exterior sound without waiting for the next reach sample.
            if (!listenerIndoors && entranceDoor != null && entranceDoor.VoiceOpening <= .02f) reachable=false;
            if((IsConnected||(connecting&&startup!=null&&!startup.IsCancellationRequested))&&!reachable)
                _=StopAsync("Out of hearing range — microphone off. Voice resumes when you return while voice is enabled.", false);
            if (!consentScreen && permission.CanConnect(reachable,Application.isFocused,IsBusy||conversation!=null) && Time.unscaledTime>=nextConnect)
            { nextConnect=Time.unscaledTime+2; _=StartAsync(); }
            var keyboard = Keyboard.current;
            // While editing a text field, letter keys belong to that field.
            if (!panelOpen && !consentScreen && keyboard != null)
            {
                if (keyboard.vKey.wasPressedThisFrame) OpenPanel();
                if (keyboard.f1Key.wasPressedThisFrame) OpenSettings();
                if (keyboard.tKey.wasPressedThisFrame) OpenTyping();
                if (keyboard.tabKey.wasPressedThisFrame) PauseFromHud();
                if (keyboard.rKey.wasPressedThisFrame)
                {
                    if(skyCycle!=null && skyCycle.isActiveAndEnabled)skyCycle.ToggleRain();
                    else if(rain!=null)rain.rainEnabled=!rain.rainEnabled;
                }
                if (keyboard.xKey.wasPressedThisFrame) _ = StopAsync();
                if (keyboard.mKey.wasPressedThisFrame && permission.Granted && !IsBusy) _ = ToggleMuteAsync();
            }
            else if (panelOpen && keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.f1Key.wasPressedThisFrame))
            { ClosePanel(); }
            for (int i=0; i<64 && notices.TryDequeue(out Notice notice); i++)
            {
                if (notice.generation != generation) continue;
                if (notice.role == "error") { _ = StopAsync("Connection ended. Check network and agent access."); continue; }
                if (notice.role == "end") { _ = StopAsync(); continue; }
                if (notice.role == "vad") { if(!muted)userVoiceUntil=Time.unscaledTime+.65f; continue; }
                if (notice.role == "interrupted") { Captions.Interrupt(Time.unscaledTime);continue; }
                if (notice.role == "stream") { Captions.Stream(notice.text,notice.partType,notice.id,Time.unscaledTime);continue; }
                if (notice.role == "alignment") { Captions.Align(notice.chars,notice.starts,notice.durations,notice.id,Time.unscaledTime);continue; }
                if (notice.role == "correction")
                {
                    Line line = transcript.FindLast(l => l.role == "Companion" && l.id == notice.id);
                    if (line != null) line.text = notice.text;
                    Captions.Correct(notice.text,notice.id,Time.unscaledTime);
                }
                else
                {
                    Line existing=notice.id==0?null:transcript.FindLast(l=>l.role==notice.role&&l.id==notice.id);
                    if(existing!=null)existing.text=notice.text;
                    else transcript.Add(new Line { role=notice.role, text=notice.text, id=notice.id });
                    if (transcript.Count>12) transcript.RemoveAt(0);
                    if(notice.role=="You"){Captions.User(notice.text,notice.id,Time.unscaledTime);UserCaptionReceived?.Invoke();}
                    else if(notice.role=="Companion")Captions.Agent(notice.text,notice.id,Time.unscaledTime);
                }
                scroll.y = float.MaxValue;
            }
            if (IsConnected && settings != null && Time.realtimeSinceStartup-connectedAt > settings.maximumSessionSeconds)
                _ = StopAsync("Session time limit reached. You can start a new conversation.");
            if (rain != null) rain.VoiceDucking = IsConnected || connecting;
            if(IsConnected)Captions.Tick(conversation.Mode==Mode.Speaking,Time.unscaledTime<userVoiceUntil,muted,Time.unscaledTime);
        }

        public void OpenPanel()
        {
            panelOpen = true;panelTab=0;
            SyncUiLock();
        }
        public void OpenSettings(){OpenPanel();panelTab=hud!=null?2:0;}
        public void OpenTyping(){OpenPanel();focusTypedInput=true;}
        public void PauseFromHud()=>_=StopAsync("Take your time. Voice paused — V → Resume voice when you're ready.");
        public void ClosePanel()
        {
            panelOpen = false;
            SyncUiLock();
        }
        private static string Limit(string value) => WellnessCaptions.Bounded(value);
        private void Queue(int session, string role, string text, int id = 0)
        {
            if (notices.Count<(role=="vad"?24:256)) notices.Enqueue(new Notice { generation=session, role=role, text=Limit(text), id=id });
        }
        private void Subscribe(Conversation session, int epoch)
        {
            userHandler = e => Queue(epoch,"You",e.UserTranscript,e.EventId);
            agentHandler = e => Queue(epoch,"Companion",e.AgentResponse,e.EventId);
            streamHandler = e =>
            {
                if(notices.Count<256)notices.Enqueue(new Notice {generation=epoch,role="stream",text=Limit(e.Text),partType=e.Type,id=e.EventId});
            };
            alignmentHandler = e =>
            {
                var a=e.Alignment;
                if(a==null||a.Chars==null||a.CharStartTimesMs==null||a.CharDurationsMs==null||notices.Count>=256)return;
                // Copy only bounded text/timing metadata. Never queue PCM/base64 audio.
                notices.Enqueue(new Notice {generation=epoch,role="alignment",id=e.EventId,
                    chars=a.Chars.Take(2048).Select(Limit).ToArray(),starts=a.CharStartTimesMs.Take(2048).ToArray(),durations=a.CharDurationsMs.Take(2048).ToArray()});
            };
            correctionHandler = e => Queue(epoch,"correction",e.CorrectedAgentResponse,e.EventId);
            disconnectHandler = _ => Queue(epoch,"end","");
            errorHandler = _ => Queue(epoch,"error",""); // No raw service payloads in UI or logs.
            vadHandler = e => {if(e.VadScore>.55)Queue(epoch,"vad","");};
            interruptionHandler = e => Queue(epoch,"interrupted","",e.EventId);
            session.UserTranscriptReceived += userHandler; session.AgentResponded += agentHandler;
            session.AgentChatResponsePartReceived += streamHandler;session.AudioReceived += alignmentHandler;
            session.AgentResponseCorrected += correctionHandler; session.Disconnected += disconnectHandler;
            session.ErrorOccurred += errorHandler;
            session.VadScoreUpdated += vadHandler;session.Interrupted += interruptionHandler;
        }
        private void Unsubscribe(Conversation session)
        {
            session.UserTranscriptReceived -= userHandler; session.AgentResponded -= agentHandler;
            session.AgentChatResponsePartReceived -= streamHandler;session.AudioReceived -= alignmentHandler;
            session.AgentResponseCorrected -= correctionHandler; session.Disconnected -= disconnectHandler;
            session.ErrorOccurred -= errorHandler;
            session.VadScoreUpdated -= vadHandler;session.Interrupted -= interruptionHandler;
        }

        private async Awaitable StartAsync()
        {
            if (!Application.isPlaying || consentScreen || !permission.CanConnect(true,Application.isFocused,IsBusy||conversation!=null)) return;
            if (settings == null || settings.agents == null || settings.agents.Length == 0)
            { permission.Pause(); status="No agent configured. Check the Voice Companions settings asset."; return; }
            selected=Mathf.Clamp(selected,0,settings.agents.Length-1);
            if(!CanHearSelected(out string rangeReason)){status=rangeReason;return;}
            if(voiceAudio==null){permission.Pause();status="Character audio is not configured.";return;}
            WellnessVoiceSettings.Agent agent=settings.agents[selected];
            if (agent == null || string.IsNullOrWhiteSpace(agent.agentId)) { permission.Pause();status="This companion has no agent ID."; return; }
#if !UNITY_WEBGL || UNITY_EDITOR
            if (Microphone.devices.Length == 0) { permission.Pause();status="No microphone available. Check Windows microphone permissions, then Resume voice."; return; }
#endif
            connecting=true; voiceAudio.mute=true; transcript.Clear(); typedMessage="";Captions.Reset();userVoiceUntil=0;
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
                if (cancel.IsCancellationRequested || this == null || !isActiveAndEnabled || generation != epoch ||
                    !permission.Granted || !permission.ListeningRequested || !Application.isFocused || !CanHearSelected(out _))
                { await opened.EndSession(); opened=null; return; }
                conversation=opened; Subscribe(opened,epoch); opened.SetVolume(0);
                connectedAt=Time.realtimeSinceStartup; status="Microphone live — speaking with "+agent.displayName;
            }
            catch (OperationCanceledException)
            { if (this != null && epoch==generation) { permission.Pause(); status="Connection cancelled or timed out. Microphone off — Resume voice to retry."; } }
            catch (Exception)
            {
                if (opened != null) { try { await opened.EndSession(); } catch { } }
                if (this != null && epoch==generation) { permission.Pause();conversation=null; status="Could not connect. Check internet, microphone permission and agent access, then Resume voice."; }
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
        private async Awaitable StopAsync(string finalStatus="Voice paused — microphone off. Use V → Resume voice when ready.", bool pauseListening=true)
        {
            if (pauseListening) permission.Pause();
            ++generation; startup?.Cancel();
            Conversation current=conversation; conversation=null;
            if(voiceAudio!=null)voiceAudio.mute=true;
            outputGain=0; if (current!=null) current.SetVolume(0);
            transcript.Clear(); typedMessage=""; while(notices.TryDequeue(out _)) { }
            Captions.Reset();userVoiceUntil=0;
            if (rain != null) rain.VoiceDucking=false;
            if (current==null) { status=connecting?"Cancelling connection…":finalStatus; return; }
            closing=true; status="Ending conversation…"; Unsubscribe(current);
            try { await current.EndSession(); }
            catch { finalStatus="Session closed. Check microphone permissions if the device stays busy."; }
            finally { if(this != null){closing=false;status=finalStatus;} }
        }
        private async Awaitable ToggleMuteAsync()
        {
            Conversation current=conversation; if(closing||changingMute||connecting||!permission.Granted)return;
            if(current==null){permission.SetMuted(!muted);status=muted?"Voice muted — microphone off":"Voice unmuted";return;}
            changingMute=true;
            bool next=!muted;
            permission.SetMuted(next); // Preserve the user's mute intent even if range changes during the await.
            try { await current.SetMicMuted(next); if(ReferenceEquals(current,conversation)){status=muted?"Muted — no microphone audio sent":"Microphone live";} }
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
        private void OnDisable()
        {
            consentScreen=false; ClosePanel(); _=StopAsync();
            permission=new WellnessVoiceConsent(); // Never carry consent into a later play run.
        }
        private void OnDestroy() { if(panelTexture!=null)Destroy(panelTexture); }

        private void Styles()
        {
            if(panelTexture!=null)return;
            panelTexture=new Texture2D(1,1);panelTexture.SetPixel(0,0,hud!=null?new Color(.94f,.925f,.885f,.97f):new Color(.10f,.16f,.15f,.97f));panelTexture.Apply();
            ToggleStyle=new GUIStyle(GUI.skin.toggle){wordWrap=true,fontSize=14};fieldStyle=new GUIStyle(GUI.skin.textField){fontSize=16};
            if(hud!=null)
            {
                var ui=hud.Theme;titleStyle=ui.Title;textStyle=ui.Text;smallStyle=ui.Small;buttonStyle=ui.Button;
                ToggleStyle.normal.textColor=ToggleStyle.onNormal.textColor=ToggleStyle.hover.textColor=ToggleStyle.onHover.textColor=WellnessUiTheme.Ink;
                ToggleStyle.focused.textColor=ToggleStyle.onFocused.textColor=ToggleStyle.active.textColor=ToggleStyle.onActive.textColor=WellnessUiTheme.Ink;
                fieldStyle.normal.textColor=fieldStyle.focused.textColor=fieldStyle.hover.textColor=WellnessUiTheme.Ink;
                fieldStyle.normal.background=fieldStyle.focused.background=fieldStyle.hover.background=ui.Button.normal.background;
                fieldStyle.border=new RectOffset(16,16,16,16);fieldStyle.padding=new RectOffset(10,10,7,7);return;
            }
            textStyle=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true,richText=false,normal={textColor=new Color(.93f,.92f,.85f)}};
            titleStyle=new GUIStyle(textStyle){fontSize=25,fontStyle=FontStyle.Bold};
            smallStyle=new GUIStyle(textStyle){fontSize=13};
            buttonStyle=new GUIStyle(GUI.skin.button){fontSize=15,wordWrap=true,padding=new RectOffset(10,10,8,8)};
        }
        private void OnGUI()
        {
            if(!Application.isPlaying)return;Styles();Matrix4x4 previous=GUI.matrix;int previousDepth=GUI.depth;GUI.depth=-20;
            float scale=Mathf.Max(.3f,Mathf.Min(Screen.width/1280f,Screen.height/720f));GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            float width=Screen.width/scale,height=Screen.height/scale;
            try
            {
                if(consentScreen){DrawConsent(width,height);return;}
                if(hud==null)
                {
                GUI.DrawTexture(new Rect(width-390,24,366,98),panelTexture);
                string badge = changingMute ? "UPDATING MIC · X pauses voice" : closing ? "STOPPING MICROPHONE…" :
                    IsConnected ? (muted?"MIC MUTED · M unmute · X pause":"MIC LIVE · M mute · X pause") :
                    connecting ? "MIC STARTING · X cancels" : permission.ListeningRequested ?
                    (muted?"VOICE MUTED · M unmute":"VOICE READY · waiting for range · X pause") : "MIC OFF · V voice controls · R rain";
                GUI.Label(new Rect(width-374,34,338,28),badge,smallStyle);
                GUI.Label(new Rect(width-374,61,338,24),skyCycle!=null?skyCycle.Summary:rain!=null&&rain.rainEnabled?"Gentle rain on":"Rain off",smallStyle);
                if(GUI.Button(new Rect(width-374,86,338,27),panelOpen?"Return to game (voice unchanged)":"Voice & weather controls",buttonStyle))
                {if(panelOpen)ClosePanel();else OpenPanel();}
                }
                if(!panelOpen)return;
                Rect panel=new Rect((width-600)/2,(height-632)/2,600,632);
                if(hud!=null)hud.Theme.Card(panel);else GUI.DrawTexture(panel,panelTexture);
                GUILayout.BeginArea(new Rect(panel.x+20,panel.y+16,panel.width-40,panel.height-32));
                GUILayout.Label(panelTab==2?"Your quiet space":panelTab==1?"A sky that changes gently":"A voice beside you",titleStyle);
                if(skyCycle!=null)panelTab=GUILayout.SelectionGrid(panelTab,PanelTabs,3,buttonStyle);
                if(panelTab==2&&hud!=null)
                {
                    panelScroll=GUILayout.BeginScrollView(panelScroll);hud.DrawSettings();GUILayout.EndScrollView();
                    if(GUILayout.Button("Return to the room",buttonStyle))ClosePanel();GUILayout.EndArea();return;
                }
                if(panelTab==1 && skyCycle!=null)
                {
                    panelScroll=GUILayout.BeginScrollView(panelScroll);
                    DrawWeatherControls();GUILayout.EndScrollView();
                    if(GUILayout.Button("Return to the garden",buttonStyle))ClosePanel();
                    GUILayout.EndArea();return;
                }
                panelScroll=GUILayout.BeginScrollView(panelScroll);
                GUILayout.Label("AI companions · not clinicians or emergency services",smallStyle);
                GUI.enabled=!IsBusy&&!IsConnected;
                if(settings!=null&&settings.agents!=null&&settings.agents.Length>0)
                {
                    var names=new string[settings.agents.Length];for(int i=0;i<names.Length;i++)names[i]=settings.agents[i].displayName;
                    int choice=GUILayout.SelectionGrid(selected,names,Mathf.Min(2,names.Length),buttonStyle);
                    if(choice!=selected)SelectCharacter(choice);
                }
                GUI.enabled=true;GUILayout.Label(status,textStyle);
                if(!IsConnected&&!IsBusy)
                {
                    GUILayout.Label(reachReason,smallStyle);
                    if(!permission.Granted)
                    {
                        GUILayout.Label("Voice is off. You can explore without sending microphone audio.",smallStyle);
                        if(GUILayout.Button("Review voice permission",buttonStyle)){consentScreen=true;SyncUiLock();}
                    }
                    else
                    {
                        GUILayout.Label("Voice permission granted for this play session. No repeat consent is needed when changing companion or returning.",smallStyle);
                        if(permission.ListeningRequested)
                        {
                            if(GUILayout.Button("Pause voice",buttonStyle))_=StopAsync();
                            if(GUILayout.Button(muted?"Unmute voice":"Mute voice",buttonStyle))_=ToggleMuteAsync();
                        }
                        else if(GUILayout.Button("Resume voice",buttonStyle)){permission.Resume();nextConnect=0;}
                    }
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUI.enabled=IsConnected&&!closing;if(GUILayout.Button(muted?"Unmute":"Mute",buttonStyle))_=ToggleMuteAsync();GUI.enabled=true;
                    if(GUILayout.Button("Pause / end conversation",buttonStyle))_=StopAsync();
                    GUILayout.EndHorizontal();
                    GUILayout.Label("Talk anywhere in the room, or near the open entrance outside. Walking beyond that area stops the mic; returning reconnects while voice is enabled. M mutes; X pauses. Losing focus pauses voice until you Resume.",smallStyle);
                }
                if(permission.Granted && GUILayout.Button("Turn off voice permission for this session",buttonStyle))
                {permission.Choose(false);_=StopAsync("Voice permission removed — microphone off.");}
                scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(145));
                if(transcript.Count==0)GUILayout.Label("Conversation text appears here while connected.",smallStyle);
                foreach(Line line in transcript)GUILayout.Label((line.role=="Companion"&&settings!=null?settings.agents[selected].displayName:line.role)+": "+line.text,smallStyle);
                GUILayout.EndScrollView();
                GUI.enabled=IsConnected;GUILayout.BeginHorizontal();GUI.SetNextControlName("CompanionTypedMessage");typedMessage=GUILayout.TextField(typedMessage,1400,fieldStyle,GUILayout.Height(34));
                if(focusTypedInput&&IsConnected&&Event.current.type==EventType.Repaint){GUI.FocusControl("CompanionTypedMessage");focusTypedInput=false;}
                if(GUILayout.Button("Send",buttonStyle,GUILayout.Width(72)))SendText();GUILayout.EndHorizontal();GUI.enabled=true;
                if(IsConnected&&Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Return&&GUI.GetNameOfFocusedControl()=="CompanionTypedMessage")
                {SendText();Event.current.Use();}
                if(rain!=null && skyCycle==null)
                {
                    GUILayout.BeginHorizontal();rain.rainEnabled=GUILayout.Toggle(rain.rainEnabled," Gentle rain",ToggleStyle,GUILayout.Width(130));
                    GUILayout.Label("Intensity",smallStyle,GUILayout.Width(70));rain.intensity=GUILayout.HorizontalSlider(rain.intensity,0,1);GUILayout.EndHorizontal();
                }
                if(skyCycle!=null)GUILayout.Label("Clouds, time and rain: open the Sky & weather tab.",smallStyle);
                GUILayout.EndScrollView();
                if(GUILayout.Button("Return to the room (voice unchanged)",buttonStyle))ClosePanel();
                GUILayout.EndArea();
            }
            finally{GUI.enabled=true;GUI.matrix=previous;GUI.depth=previousDepth;}
        }
        private void DrawConsent(float width,float height)
        {
            GUI.DrawTexture(new Rect(0,0,width,height),panelTexture);
            Rect panel=new Rect((width-620)/2,(height-600)/2,620,600);
            GUILayout.BeginArea(panel);
            GUILayout.Label("Before you enter",titleStyle);
            GUILayout.Space(12);
            consentScroll=GUILayout.BeginScrollView(consentScroll);
            GUILayout.Label("Would you like to talk with the AI companions?",textStyle);
            GUILayout.Label("Voice is optional. Julien and Camille are AI companions, not clinicians or emergency services.",smallStyle);
            GUILayout.Space(12);
            GUILayout.Label("Enable voice sends your microphone audio and typed messages to ElevenLabs and the selected agent's configured services. This game does not save transcripts; the provider or agent owner may retain them. Avoid sharing sensitive information.",textStyle);
            GUILayout.Space(12);
            GUILayout.Label("Your choice lasts only for this play session. Voice starts when you enter hearing range and reconnects when you return: anywhere indoors, or within "+outdoorHearingDistance.ToString("0")+" m of the open front doorway outside. Closing the door cuts off outdoor chat.",textStyle);
            GUILayout.Label("A MIC LIVE indicator shows when listening. M mutes; X pauses. V opens controls to resume, choose a companion or withdraw permission. Leaving the game window pauses voice until you Resume. No microphone starts before you click Enable.",smallStyle);
            GUILayout.Space(14);
            if(settings!=null && settings.agents!=null && settings.agents.Length>0)
            {
                var names=new string[settings.agents.Length];for(int i=0;i<names.Length;i++)names[i]=settings.agents[i].displayName;
                int choice=GUILayout.SelectionGrid(selected,names,Mathf.Min(2,names.Length),buttonStyle);
                if(choice!=selected)SelectCharacter(choice);
            }
            GUILayout.EndScrollView();
            GUILayout.Space(10);
            if(GUILayout.Button("Enable voice & begin — I consent for this play session",buttonStyle))ChooseVoice(true);
            if(GUILayout.Button("Play without voice",buttonStyle))ChooseVoice(false);
            GUILayout.EndArea();
        }
        private void ChooseVoice(bool enable)
        {
            permission.Choose(enable);consentScreen=false;panelOpen=false;nextConnect=nextReachCheck=0;
            status=enable?"Voice enabled — waiting for hearing range.":"Voice off — enjoy the room and garden.";
            SyncUiLock();
        }
        private void DrawWeatherControls()
        {
            GUILayout.Space(10);GUILayout.Label(skyCycle.Summary,textStyle);
            GUILayout.Label("Weather changes slowly: clear → cloudy → gentle rain → clearing.",smallStyle);
            int mode=GUILayout.SelectionGrid((int)skyCycle.weatherMode,WeatherTabs,4,buttonStyle);
            if(mode!=(int)skyCycle.weatherMode)skyCycle.SetWeather((WellnessSkyCycle.WeatherMode)mode);
            GUILayout.Label("R switches to manual rain / clear. Choose Auto to resume the cycle.",smallStyle);
            GUILayout.Space(10);
            skyCycle.timeRuns=GUILayout.Toggle(skyCycle.timeRuns," Advance day and night automatically",ToggleStyle);
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
