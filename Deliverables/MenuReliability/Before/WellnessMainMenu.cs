using System;
using System.Collections.Generic;
using TheLastWatch.Audio;
using TheLastWatch.Environment;
using TheLastWatch.Integrations;
using TheLastWatch.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastWatch.UI
{
    // Uses the existing camera/world. No screenshot, extra camera, capture pass,
    // scene clone, microphone or external session belongs to the title screen.
    [DefaultExecutionOrder(-10000),DisallowMultipleComponent]
    public sealed class WellnessMainMenu : MonoBehaviour
    {
        public Transform room;
        public WellnessExplorer player;
        public WellnessVoiceChat chat;
        public WellnessHud hud;
        public WellnessSkyCycle sky;
        public Font serif,sans;
        public Vector3 localEye=new Vector3(24,2.7f,-5);
        public Vector3 localLook=new Vector3(-1,3.6f,-10);
        [Range(35,70)] public float fieldOfView=48;
        [Range(0,24)] public float openingHour=1.5f;
        [Range(4,12)] public float titleDayMinutes=6;
        private readonly List<Behaviour> held=new List<Behaviour>();
        private readonly List<bool> wasEnabled=new List<bool>();
        private Camera view;
        private Vector3 oldCameraPosition;
        private Quaternion oldCameraRotation;
        private float oldFieldOfView,oldHour,oldDayMinutes,oldTimeScale;
        private bool oldTimeRuns,active,captured,animateBackground=true,moveCamera;
        private WellnessSkyCycle.WeatherMode oldWeather;
        private WellnessMainMenuFlow flow;
        private float clock,reveal;
        private Vector2 lastPointer;
        private bool pointerKnown;
        private GUIStyle brand,subtitle,action,body,small,heading,toggle,slider,thumb;
        private Texture2D shade;
        private Vector2 creditScroll;
        private static readonly string[] Actions={"Enter MindSpace","Settings","How to play","Credits","Exit"};
        private static readonly Color Ivory=new Color(.98f,.94f,.83f),Sage=new Color(.70f,.84f,.57f);
        public bool IsShowing=>active;
        private void OnEnable()
        {
            if(!Application.isPlaying||room==null||player==null||chat==null||sky==null||player.ViewCamera==null)return;
            flow=new WellnessMainMenuFlow();clock=reveal=0;pointerKnown=false;active=captured=true;
            view=player.ViewCamera;oldCameraPosition=view.transform.localPosition;oldCameraRotation=view.transform.localRotation;oldFieldOfView=view.fieldOfView;
            oldHour=sky.hour;oldDayMinutes=sky.dayMinutes;oldTimeRuns=sky.timeRuns;oldWeather=sky.weatherMode;oldTimeScale=Time.timeScale;
            held.Clear();wasEnabled.Clear();
            Hold(chat);Hold(hud);Hold(player);Hold(chat.biometricCoach);Hold(chat.biometrics);
            // OnEnable of chat cannot run until Enter restores it. Consent is never
            // inferred from a menu visit, a settings action or a prior play session.
            if(chat.biometrics!=null)chat.biometrics.SetConsent(false,false);
            sky.hour=openingHour;sky.dayMinutes=titleDayMinutes;sky.timeRuns=true;sky.weatherMode=WellnessSkyCycle.WeatherMode.Clear;
            Time.timeScale=1;Pose();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        private void Hold(Behaviour component)
        {
            if(component==null)return;held.Add(component);wasEnabled.Add(component.enabled);component.enabled=false;
        }
        private void Update()
        {
            if(!Application.isPlaying)return;
            if(!active){reveal=Mathf.MoveTowards(reveal,0,Time.unscaledDeltaTime*2);return;}
            float dt=Application.isFocused?Mathf.Min(Time.unscaledDeltaTime,.1f):0;
            clock+=dt;
            if(flow.Tick(dt)){Restore(true);reveal=1;return;}
            if(flow.Entering||!Application.isFocused)return;
            UpdatePointerSelection();
            var key=Keyboard.current;if(key==null)return;
            if(key.escapeKey.wasPressedThisFrame){flow.Back();return;}
            if(flow.Current!=WellnessMainMenuFlow.Page.Home)return;
            if(key.upArrowKey.wasPressedThisFrame)flow.Move(-1);
            if(key.downArrowKey.wasPressedThisFrame||key.tabKey.wasPressedThisFrame)flow.Move(key.shiftKey.isPressed?-1:1);
            if(key.enterKey.wasPressedThisFrame||key.numpadEnterKey.wasPressedThisFrame)Activate(flow.Selection);
        }
        private void UpdatePointerSelection()
        {
            var mouse=Mouse.current;
            if(mouse==null){pointerKnown=false;return;}
            Vector2 pointer=mouse.position.ReadValue();
            bool moved=!pointerKnown||(pointer-lastPointer).sqrMagnitude>.01f;
            pointerKnown=true;lastPointer=pointer;
            // A parked mouse must not steal selection back after arrow/Tab input.
            // Select ignores gaps, subpages and the Enter transition.
            if(moved)flow.Select(WellnessMainMenuFlow.OptionAt(pointer.x,pointer.y,Screen.width,Screen.height));
        }
        private void LateUpdate()
        {
            if(!active||!Application.isPlaying)return;
            Pose();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            sky.timeRuns=animateBackground;
            // Title-only aurora composition, applied to runtime material instances.
            // Gameplay's normal 45% nightly lottery resumes immediately on entry.
            FeatureAurora(RenderSettings.skybox,sky.skyMaterial);
            if(sky.pondRenderer!=null)FeatureAurora(sky.pondRenderer.sharedMaterial,null);
        }
        private void Pose()
        {
            if(view==null)return;
            Vector3 offset=moveCamera?new Vector3(Mathf.Sin(clock*.045f)*.10f,Mathf.Sin(clock*.035f)*.025f,0):Vector3.zero;
            Vector3 eye=room.TransformPoint(localEye+offset),target=room.TransformPoint(localLook);
            view.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye,room.up));view.fieldOfView=fieldOfView;
        }
        private void FeatureAurora(Material material,Material source)
        {
            if(material==null||material==source||!material.HasProperty("_NightEffects")||!material.HasProperty("_AuroraShape"))return;
            // Only affect transient material clones, never saved sky/pond assets.
            if((material.hideFlags&HideFlags.DontSave)==0)return;
            Vector4 effects=material.GetVector("_NightEffects");
            effects.y=sky.auroraBorealis?.70f:0;effects.w=view.transform.eulerAngles.y*Mathf.Deg2Rad;
            material.SetVector("_NightEffects",effects);material.SetVector("_AuroraShape",new Vector4(.12f,1.25f,.36f,84));
        }
        private void Restore(bool enter)
        {
            if(!captured)return;active=false;captured=false;
            if(view!=null){view.transform.localPosition=oldCameraPosition;view.transform.localRotation=oldCameraRotation;view.fieldOfView=oldFieldOfView;}
            if(sky!=null)
            {
                sky.dayMinutes=oldDayMinutes;sky.timeRuns=oldTimeRuns;sky.weatherMode=oldWeather;
                if(!enter)sky.SetHour(oldHour);
            }
            Time.timeScale=oldTimeScale;
            // Restore player before chat. Its OnEnable displays the original
            // affirmative consent screen and blocks movement until a choice.
            for(int i=held.Count-1;i>=0;i--)if(held[i]!=null)held[i].enabled=wasEnabled[i];
            held.Clear();wasEnabled.Clear();
        }
        private void OnDisable(){Restore(false);if(shade!=null)Destroy(shade);shade=null;}
        private void Activate(int index)
        {
            if(index==0)flow.Enter();
            else flow.Open(index==1?WellnessMainMenuFlow.Page.Settings:index==2?WellnessMainMenuFlow.Page.Help:index==3?WellnessMainMenuFlow.Page.Credits:WellnessMainMenuFlow.Page.Exit);
        }
        private void Styles()
        {
            if(brand!=null)return;
            brand=Label(serif,100,Ivory);subtitle=Label(sans,22,new Color(.8f,.84f,.84f));
            action=Label(serif,36,Ivory);action.alignment=TextAnchor.MiddleLeft;action.padding=new RectOffset(50,12,0,0);
            heading=Label(serif,40,Ivory);body=Label(sans,25,Ivory);body.wordWrap=true;
            small=Label(sans,19,new Color(.77f,.81f,.79f));small.wordWrap=true;
            toggle=new GUIStyle(GUI.skin.toggle){font=sans,fontSize=23,padding=new RectOffset(28,3,3,3)};WellnessUiText.Static(toggle,Ivory);
            slider=new GUIStyle{fixedHeight=10,margin=new RectOffset(),padding=new RectOffset()};thumb=new GUIStyle{fixedWidth=24,fixedHeight=24};
            shade=new Texture2D(128,1,TextureFormat.RGBA32,false,true){name="Title readability gradient",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            for(int i=0;i<128;i++){float t=i/127f;shade.SetPixel(i,0,new Color(.016f,.032f,.055f,Mathf.Lerp(.74f,0,Mathf.SmoothStep(0,1,t))));}
            shade.Apply(false,true);
        }
        private static GUIStyle Label(Font font,int size,Color color)=>WellnessUiText.Static(new GUIStyle{font=font,fontSize=size,normal={textColor=color},richText=false,clipping=TextClipping.Clip},color);
        private static void Fill(Rect rect,Color color){Color old=GUI.color;GUI.color*=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        private void OnGUI()
        {
            if(!Application.isPlaying||(!active&&reveal<=0))return;
            int depth=GUI.depth;var matrix=GUI.matrix;Color color=GUI.color;bool enabled=GUI.enabled;
            try
            {
                GUI.depth=-10000;GUI.matrix=Matrix4x4.identity;
                if(active)
                {
                    Styles();GUI.DrawTexture(new Rect(0,0,Screen.width*.70f,Screen.height),shade);
                    float scale=Mathf.Min(Screen.width/1672f,Screen.height/941f);
                    GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1672*scale)*.5f,(Screen.height-941*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
                    GUI.enabled=!flow.Entering;
                    GUI.Label(new Rect(92,144,560,130),"MindSpace",brand);
                    GUI.Label(new Rect(97,268,590,36),"C O U N S E L I N G   C E N T E R",subtitle);
                    for(int i=0;i<Actions.Length;i++)
                    {
                        Rect r=new Rect(93,338+i*71,390,70);
                        // The hit target never grows/moves with the visual pulse.
                        if(Button(r,string.Empty,action)&&flow.Current==WellnessMainMenuFlow.Page.Home){flow.Select(i);Activate(i);}
                        DrawOption(r,i);
                    }
                    GUI.Label(new Rect(40,885,600,32),"Voice is optional",small);
                    if(flow.Current!=WellnessMainMenuFlow.Page.Home)Panel();
                    GUI.matrix=Matrix4x4.identity;
                    if(flow.Entering)Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.015f,.025f,.035f,Mathf.SmoothStep(0,1,flow.Fade)));
                }
                else Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.015f,.025f,.035f,reveal));
            }
            finally{GUI.depth=depth;GUI.matrix=matrix;GUI.color=color;GUI.enabled=enabled;}
        }
        private static bool Button(Rect rect,string text,GUIStyle style)
        {return GUI.Button(rect,text,style);}
        private void DrawOption(Rect r,int index)
        {
            bool selected=index==flow.Selection&&flow.Current==WellnessMainMenuFlow.Page.Home;
            Matrix4x4 previous=GUI.matrix;
            try
            {
                if(selected)
                {
                    Vector3 center=new Vector3(r.center.x,r.center.y,0);
                    GUI.matrix=previous*Matrix4x4.Translate(center)*Matrix4x4.Scale(new Vector3(flow.SelectionScale,flow.SelectionScale,1))*Matrix4x4.Translate(-center);
                    WellnessUiPrimitives.Round(r,Sage,15,1.6f);
                    WellnessUiPrimitives.Round(new Rect(r.x+17,r.y+14,8,42),Sage,4);
                }
                GUI.Label(r,Actions[index],action);
            }
            finally{GUI.matrix=previous;}
        }
        private void Panel()
        {
            Rect r=new Rect(690,172,840,610);
            WellnessUiPrimitives.Round(r,new Color(.025f,.052f,.073f,.95f),22);
            WellnessUiPrimitives.Round(r,new Color(.7f,.79f,.70f,.28f),22,1);
            string title=flow.Current==WellnessMainMenuFlow.Page.Settings?"Make yourself comfortable":flow.Current==WellnessMainMenuFlow.Page.Help?"How to play":flow.Current==WellnessMainMenuFlow.Page.Credits?"Made with care":"Leave MindSpace?";
            GUI.Label(new Rect(732,208,748,64),title,heading);
            if(Button(new Rect(1316,719,168,40),"Esc · Back",small))flow.Back();
            switch(flow.Current)
            {
                case WellnessMainMenuFlow.Page.Settings:Settings();break;
                case WellnessMainMenuFlow.Page.Help:
                    GUI.Label(new Rect(732,304,735,350),"WASD  ·  Walk through the room and garden\nMouse  ·  Look around\nE  ·  Interact, sit, open doors, talk to a companion\nSpace  ·  Stand up\nEsc  ·  Settings and pause menu\nJ  ·  Show or hide the gentle checklist\nV / T  ·  Voice controls / type a message",body);
                    GUI.Label(new Rect(732,645,705,60),"After entering, choose whether to enable voice. Camera access is optional and has its own consent.",small);break;
                case WellnessMainMenuFlow.Page.Credits:
                    var music=BackgroundMusicPlayer.Instance;
                    creditScroll=GUI.BeginScrollView(new Rect(732,297,748,380),creditScroll,new Rect(0,0,710,430));
                    GUI.Label(new Rect(0,0,705,425),"MindSpace Counseling Center\nA quiet space to explore at your own pace.\n\nFonts  ·  Source Serif 4 (Adobe) and Carlito\nMaterials  ·  Poly Haven (CC0)\nVoice companions  ·  ElevenLabs\nLip sync  ·  uLipSync\n\nNow playing\n"+(music!=null?music.TrackTitle+"\n"+music.TrackArtist:"Room ambience"),body);GUI.EndScrollView();break;
                case WellnessMainMenuFlow.Page.Exit:
                    GUI.Label(new Rect(732,321,720,90),"Your quiet space will be here when you return.",body);
                    if(Action(new Rect(732,457,308,62),"Stay here"))flow.Back();
                    if(Action(new Rect(1072,457,308,62),"Exit game"))chat.ExitFromMenu();break;
            }
        }
        private void Settings()
        {
            var music=BackgroundMusicPlayer.Instance;
            if(music!=null)music.Volume=Volume("Music",309,music.Volume);
            OutdoorNatureAmbience.MasterVolume=Volume("Nature sounds",371,OutdoorNatureAmbience.MasterVolume);
            animateBackground=GUI.Toggle(new Rect(732,437,650,36),animateBackground,"Day and night cycle in the menu",toggle);
            moveCamera=GUI.Toggle(new Rect(732,483,650,36),moveCamera,"Gentle camera drift (optional)",toggle);
            if(hud!=null)hud.showCaptions=GUI.Toggle(new Rect(732,529,650,36),hud.showCaptions,"Conversation captions in game",toggle);
            string[] names={"Morning","Noon","Sunset","Night"};float[] hours={8,12,18,1.5f};
            for(int i=0;i<4;i++)if(Action(new Rect(732+i*181,588,168,48),names[i]))sky.SetHour(hours[i]);
            GUI.Label(new Rect(732,656,680,47),"Microphone and camera remain off here. Permissions are chosen after entering.",small);
        }
        private float Volume(string label,float y,float value)
        {
            GUI.Label(new Rect(732,y-5,250,40),label,body);
            Rect r=new Rect(998,y,375,30);float next=GUI.HorizontalSlider(r,value,0,1,slider,thumb);
            WellnessUiPrimitives.Round(new Rect(r.x,r.y+10,r.width,8),new Color(.6f,.67f,.65f,.28f),4);
            float x=r.x+12+(r.width-24)*next;
            WellnessUiPrimitives.Round(new Rect(r.x,r.y+10,x-r.x,8),Sage,4);WellnessUiPrimitives.Round(new Rect(x-11,r.y+3,22,22),Sage,11);
            GUI.Label(new Rect(1400,y-4,88,40),Mathf.RoundToInt(next*100)+"%",body);return next;
        }
        private bool Action(Rect r,string label)
        {
            WellnessUiPrimitives.Round(r,new Color(.67f,.8f,.60f,.55f),12,1);
            var prior=body.alignment;body.alignment=TextAnchor.MiddleCenter;bool clicked=Button(r,label,body);body.alignment=prior;return clicked;
        }
    }
}
