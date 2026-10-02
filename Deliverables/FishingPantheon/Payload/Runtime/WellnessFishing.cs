using System;
using System.IO;
using System.Collections.Generic;
using TheLastWatch.Environment;
using TheLastWatch.Player;
using TheLastWatch.Integrations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastWatch.UI
{
    [DefaultExecutionOrder(-800),DisallowMultipleComponent]
    public sealed partial class WellnessFishing : MonoBehaviour
    {
        public WellnessExplorer player;
        public WellnessVoiceChat chat;
        public WellnessMainMenu mainMenu;
        public WellnessKoiPond pond;
        public KoiPondLibrary catchLibrary;
        public bool fishingSounds=true;
        KoiPondLibrary loadedLibrary;
        public KoiPondLibrary Library
        {
            get
            {
                if(catchLibrary!=null)return catchLibrary;
                if(loadedLibrary==null)loadedLibrary=Resources.Load<KoiPondLibrary>("PantheonKoiLibrary");
                return loadedLibrary!=null?loadedLibrary:pond!=null?pond.library:null;
            }
        }
        public static WellnessFishing Instance {get;private set;}
        public static bool ModalOpen=>Instance!=null&&(Instance.inventoryOpen||Instance.round!=null);
        public static bool OwnsShortcuts=>ModalOpen||consumedFrame==Time.frameCount;
        public static bool HideGameplayHud=>ModalOpen;
        static int consumedFrame=-1,generation;
        int initialized=-1,page,selected=-1,allowCastAfterFrame=-1,journalSelected,filter=-1,sort,sessionCatches;
        bool inventoryOpen,ownsLock,requestInventory,requestCast,canCast,journal,newDiscovery,personalBest;
        float aimClock,toastUntil,catchLiftTime;
        string toast,focus;
        Vector3 landing;
        KoiFishingRound round;
        KoiCatchInventory inventory;
        readonly KoiFishingCollection collection=new KoiFishingCollection();
        readonly List<int> visible=new List<int>();
        KoiCatch lastCatch,pendingCatch;
        KoiFishingLootEntry hookedEntry;
        KoiFishingViews views;
        KoiFishingAudio sounds;
        System.Random random;
        readonly RaycastHit[] waterHits=new RaycastHit[64];
        WellnessQuietGlassTheme ui;
        GUIStyle light,hint,title38,wrapBody,buttonText,eyebrow,bigNumber,centered;
        readonly Dictionary<GUIStyle,GUIStyle> shadows=new Dictionary<GUIStyle,GUIStyle>();
        readonly Dictionary<string,GUIStyle> rarityStyles=new Dictionary<string,GUIStyle>();
        public int CatchCount=>inventory==null?0:inventory.Fish.Count;
        public string SaveDirectory=>Path.Combine(Application.persistentDataPath,"MindSpaceFishing");
        bool Available=>player!=null&&player.isActiveAndEnabled&&player.ViewCamera!=null&&pond!=null&&Library!=null&&
            chat!=null&&chat.isActiveAndEnabled&&(mainMenu==null||!mainMenu.IsShowing)&&!player.IsTransitioning;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){Instance=null;consumedFrame=-1;unchecked{generation++;}}
        void OnEnable(){if(Application.isPlaying)EnsureSession();}
        bool EnsureSession()
        {
            if(Instance!=null&&Instance!=this){enabled=false;return false;}Instance=this;
            if(initialized==generation&&inventory!=null)return true;
            CloseModal();views?.Dispose();views=null;sounds?.Dispose();sounds=null;loadedLibrary=null;
            initialized=generation;lastCatch=pendingCatch=null;allowCastAfterFrame=-1;focus=null;sessionCatches=0;requestCast=false;
            random=new System.Random(Guid.NewGuid().GetHashCode());inventory=new KoiCatchInventory(SaveDirectory);
            try{inventory.Reload();}catch(Exception){Notice("Your koi inventory could not be read. Check access to the local save folder.");}
            collection.Rebuild(inventory.Fish);return true;
        }
        void EnsureViews()
        {
            if(views!=null||player==null||player.ViewCamera==null)return;
            views=new KoiFishingViews(transform,player.ViewCamera.transform,Library);views.Equip(inventory.Equipped);
            sounds=new KoiFishingAudio(transform);
        }
        void Sound(int index){if(fishingSounds)sounds?.Play(index);}
        public static Vector3 WaterCenter(WellnessKoiPond pond)
        {var center=pond.pondCenter;if(pond.waterSurface!=null)center.y=pond.waterSurface.bounds.center.y;return center;}
        public static bool FindLanding(WellnessKoiPond pond,Vector3 eye,Vector3 forward,Transform ignore,RaycastHit[] hits,out Vector3 target)
        {
            target=default;if(pond==null)return false;var center=WaterCenter(pond);
            for(int option=0;option<5;option++)
            {
                if(!KoiFishingWater.Candidate(eye,forward,center,pond.pondRadii,option,out var candidate))continue;
                int count=Physics.RaycastNonAlloc(candidate+Vector3.up*6,Vector3.down,hits,6.1f,~0,QueryTriggerInteraction.Ignore);
                if(count==hits.Length)continue;bool dry=false;
                for(int i=0;i<count;i++)if((ignore==null||!hits[i].collider.transform.IsChildOf(ignore))&&hits[i].point.y>center.y+.13f){dry=true;break;}
                if(!dry){target=candidate;return true;}
            }
            return false;
        }
        void Update()
        {
            if(!Application.isPlaying||!EnsureSession())return;
            if(!Available){CloseModal();views?.HideRod();views?.ShowHeld(false);canCast=requestCast=false;return;}
            EnsureViews();
            if(chat.HasOwnPanelOpen||WellnessPhotoCamera.OwnsShortcuts||player.TheatreMode)
            {CloseModal();views?.ShowHeld(false);canCast=requestCast=false;return;}
            if(requestInventory){requestInventory=false;OpenInventory();}
            views.ShowHeld(!ModalOpen&&!WellnessPhotoCamera.HideGameplayHud);
            if(!Application.isFocused)return;
            views.TickHeld(Time.unscaledDeltaTime);
            var key=Keyboard.current;var mouse=Mouse.current;
            if(inventoryOpen)
            {
                views.TickPreview(Time.unscaledDeltaTime);
                if(key==null)return;
                if(key.escapeKey.wasPressedThisFrame||key.iKey.wasPressedThisFrame)CloseModal();
                else if(key.leftArrowKey.wasPressedThisFrame)MoveSelection(-1);
                else if(key.rightArrowKey.wasPressedThisFrame)MoveSelection(1);
                return;
            }
            if(round!=null)
            {
                if(key!=null&&key.escapeKey.wasPressedThisFrame){CloseModal();return;}
                var before=round.State;
                bool held=(key!=null&&key.spaceKey.isPressed)||(mouse!=null&&mouse.leftButton.isPressed);
                bool pressed=(key!=null&&key.spaceKey.wasPressedThisFrame)||(mouse!=null&&mouse.leftButton.wasPressedThisFrame);
                round.Step(Time.unscaledDeltaTime,held,pressed);
                if(before!=round.State)
                {if(round.State==KoiFishingRound.Phase.Bite)Sound(0);else if(round.State==KoiFishingRound.Phase.Reeling)Sound(round.PerfectHook?2:1);else if(round.State==KoiFishingRound.Phase.Escaped)Sound(3);}
                if(round.State==KoiFishingRound.Phase.Caught&&lastCatch==null)
                {
                    newDiscovery=collection.Count(hookedEntry.Variety)==0;
                    float size=Mathf.Lerp(.90f,1.08f,(float)random.NextDouble());
                    lastCatch=pendingCatch=KoiCatch.Create(hookedEntry.Variety,size);lastCatch.scored=true;lastCatch.accuracy=round.Accuracy;lastCatch.perfectHook=round.PerfectHook;
                    personalBest=lastCatch.lengthCm>collection.Best(lastCatch.variety)+.05f;TrySaveCatch();sessionCatches++;Sound(2);
                    catchLiftTime=0;views.BeginCatch(lastCatch,landing);
                }
                if(round.State==KoiFishingRound.Phase.Caught){catchLiftTime+=Mathf.Min(Time.unscaledDeltaTime,.1f);views.PoseCatch(catchLiftTime);}
                else views.PoseRod(round,landing,Time.unscaledTime);
                if(round.Finished&&key!=null&&key.iKey.wasPressedThisFrame){CloseModal();OpenInventory();return;}
                if(round.Finished&&key!=null&&key.enterKey.wasPressedThisFrame){QueueRecast();return;}
                return;
            }
            if(player.IsUiInputBlocked||player.CursorReleased){canCast=requestCast=false;return;}
            if(key!=null&&key.iKey.wasPressedThisFrame){OpenInventory();return;}
            if(player.IsSeated){canCast=requestCast=false;return;}
            aimClock-=Time.unscaledDeltaTime;
            if(aimClock<=0){aimClock=.15f;canCast=FindLanding(pond,player.ViewCamera.transform.position,player.ViewCamera.transform.forward,player.transform,waterHits,out landing);}
            if(requestCast){requestCast=false;BeginRound();return;}
            if(canCast&&KoiFishingCastMotion.CanBeginCast(mouse!=null&&mouse.leftButton.wasPressedThisFrame,
                Available&&Time.frameCount>allowCastAfterFrame,player.IsUiInputBlocked,player.CursorReleased,
                player.IsSeated,round!=null||inventoryOpen||chat.HasOwnPanelOpen||WellnessPhotoCamera.OwnsShortcuts||player.TheatreMode))BeginRound();
        }
        void BeginRound()
        {
            if(!Available||!Application.isFocused||player.IsSeated||player.IsUiInputBlocked||player.CursorReleased||chat.HasOwnPanelOpen||WellnessPhotoCamera.OwnsShortcuts||player.TheatreMode)return;
            if(pendingCatch!=null){Notice("A catch still needs saving. Open I and choose Retry save.");return;}
            if(CatchCount>=KoiCatchInventory.Capacity){Notice("Your koi inventory is full. All existing catches are safe.");return;}
            if(!FindLanding(pond,player.ViewCamera.transform.position,player.ViewCamera.transform.forward,player.transform,waterHits,out landing)){Notice("Stand beside the pond or on the bridge and face open water.");return;}
            hookedEntry=KoiFishingLoot.ForVariety(Library.varieties[KoiFishingLoot.PickIndex(Library,random,focus)].name);
            round=new KoiFishingRound(random.Next(),1,hookedEntry.Style,hookedEntry.Rank);lastCatch=null;catchLiftTime=0;Lock();views.PoseRod(round,landing,Time.unscaledTime);Sound(1);
        }
        void QueueRecast(){CloseModal();requestCast=true;}
        void Lock(){player.SetUiInputBlocked(true);ownsLock=true;consumedFrame=Time.frameCount;}
        void CloseModal()
        {
            if(inventoryOpen||round!=null){consumedFrame=Time.frameCount;allowCastAfterFrame=Time.frameCount+1;}
            inventoryOpen=false;round?.Cancel();round=null;views?.ClosePreview();views?.HideRod();views?.HideCatch();
            if(ownsLock&&player!=null){player.SetUiInputBlocked((chat!=null&&chat.HasOwnPanelOpen)||WellnessPhotoCamera.GalleryOpen||(mainMenu!=null&&mainMenu.IsShowing));ownsLock=false;}
            selected=-1;requestCast=false;
        }
        public void OpenInventoryFromMenu(){chat.ClosePanel();requestInventory=true;}
        public void OpenInventory()
        {
            if(!Available||chat.HasOwnPanelOpen||WellnessPhotoCamera.OwnsShortcuts||player.IsUiInputBlocked||round!=null)return;
            EnsureViews();inventoryOpen=true;page=0;journal=false;collection.Rebuild(inventory.Fish);RebuildVisible();Lock();Select(visible.Count>0?visible[0]:-1);
        }
        void RebuildVisible()
        {
            visible.Clear();for(int i=0;i<CatchCount;i++)if(filter<0||KoiFishingLoot.Rank(inventory.Fish[i].RarityName)==filter)visible.Add(i);
            if(sort!=0)visible.Sort((a,b)=>sort==1?KoiFishingLoot.Rank(inventory.Fish[b].RarityName).CompareTo(KoiFishingLoot.Rank(inventory.Fish[a].RarityName)):inventory.Fish[b].lengthCm.CompareTo(inventory.Fish[a].lengthCm));
            page=0;
        }
        void MoveSelection(int direction)
        {
            if(journal){journalSelected=Mathf.Clamp(journalSelected+direction,0,Library.varieties.Length-1);page=journalSelected/12;PreviewJournal();}
            else{int slot=visible.IndexOf(selected);slot=Mathf.Clamp(slot+direction,0,visible.Count-1);if(visible.Count>0){page=slot/6;Select(visible[slot]);}}
        }
        void Select(int index){selected=index;views?.SelectPreview(index>=0&&index<CatchCount?inventory.Fish[index]:null);}
        void PreviewJournal(){views?.SelectPreview(KoiCatch.Create(Library.varieties[journalSelected].name,1));}
        void TrySaveCatch()
        {
            if(pendingCatch==null)return;
            try{inventory.Save(pendingCatch);round?.TryClaim();pendingCatch=null;collection.Rebuild(inventory.Fish);Notice("Koi caught and saved  ·  I to view or equip");}
            catch(Exception){Notice("Catch not saved yet. Open I to retry. Keep this session open until it is saved.");}
        }
        void Equip(string id)
        {try{inventory.Equip(id);views.Equip(inventory.Equipped);Notice(id==null?"Koi put away.":"Koi equipped  ·  I to change or put away");Sound(1);}catch(Exception){Notice("Could not save the equipped choice.");}}
        void Notice(string message){toast=message;toastUntil=Time.unscaledTime+6;}
        public static string FishName(KoiCatch fish)=>fish==null?"Koi":KoiFishingLoot.DisplayName(fish.variety);
        void EnsureTheme()
        {
            if(ui!=null)return;ui=new WellnessQuietGlassTheme(chat.quietGlassSerif,chat.quietGlassSans);
            foreach(var style in new[]{ui.Title,ui.Body,ui.Small,ui.Hint,ui.TextButton})WellnessUiText.Static(style,new Color(.98f,.97f,.91f));
            light=new GUIStyle(ui.Small){fontSize=19};WellnessUiText.Static(light,new Color(.98f,.97f,.90f));
            hint=new GUIStyle(ui.Hint){wordWrap=true};title38=new GUIStyle(ui.Title){fontSize=38};wrapBody=new GUIStyle(ui.Body){wordWrap=true};
            buttonText=new GUIStyle(light){fontSize=18,alignment=TextAnchor.MiddleCenter};eyebrow=new GUIStyle(light){fontSize=13};
            bigNumber=new GUIStyle(title38){fontSize=58};centered=new GUIStyle(light){alignment=TextAnchor.MiddleCenter};
        }
        void Label(Rect rect,string text,GUIStyle style)
        {
            if(!shadows.TryGetValue(style,out var shadow)){shadow=new GUIStyle(style);WellnessUiText.Static(shadow,new Color(.025f,.04f,.035f,.95f));shadows.Add(style,shadow);}
            GUI.Label(new Rect(rect.x+1.5f,rect.y+1.5f,rect.width,rect.height),text,shadow);
            GUI.Label(new Rect(rect.x-.8f,rect.y-.8f,rect.width,rect.height),text,shadow);GUI.Label(rect,text,style);
        }
        bool Action(Rect rect,string text,bool primary=false)
        {
            bool hover=rect.Contains(Event.current.mousePosition);var color=primary?new Color(.74f,.94f,.60f,.95f):new Color(.97f,.97f,.90f,.55f);
            Rect border=rect;if(hover){color.a=1;border.x-=1;border.y-=1;border.width+=2;border.height+=2;}
            ui.Outline(border,color,12);bool clicked=GUI.Button(rect,GUIContent.none,ui.TextButton);Label(rect,text,buttonText);return clicked;
        }
        void RarityLabel(Rect rect,string rarity,int size=17)
        {
            string key=rarity+size;
            if(!rarityStyles.TryGetValue(key,out var style)){style=new GUIStyle(light){fontSize=size};WellnessUiText.Static(style,KoiFishingLoot.ColorFor(rarity));rarityStyles.Add(key,style);}
            Label(rect,rarity.ToUpperInvariant(),style);
        }
        void Line(float x,float y,float width)=>WellnessQuietGlassTheme.Fill(new Rect(x,y,width,1),new Color(.97f,.97f,.90f,.28f));
        void OnGUI()
        {
            if(!Application.isPlaying||!Available||WellnessPhotoCamera.HideGameplayHud||chat.HasOwnPanelOpen||player.TheatreMode)return;
            EnsureTheme();float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);if(scale<=0)return;
            var matrix=GUI.matrix;int depth=GUI.depth;var color=GUI.color;
            GUI.depth=-35;GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f),Quaternion.identity,new Vector3(scale,scale,1));
            try
            {
                if(inventoryOpen)DrawInventory();else if(round!=null)DrawRound();
                else if(!player.CursorReleased&&!player.IsUiInputBlocked&&canCast)
                {Label(new Rect(389,590,510,28),"Left click  Cast your line     I  Koi collection",light);if(focus!=null)Label(new Rect(389,623,510,26),"Seeking "+KoiFishingLoot.DisplayName(focus),hint);}
                if(Time.unscaledTime<toastUntil&&!ModalOpen)Label(new Rect(251,528,780,29),toast,light);
            }
            finally{GUI.matrix=matrix;GUI.depth=depth;GUI.color=color;}
        }
        void OnDisable()
        {
            CloseModal();views?.Dispose();views=null;sounds?.Dispose();sounds=null;ui?.Dispose();ui=null;shadows.Clear();rarityStyles.Clear();requestInventory=canCast=false;
            if(Instance==this)Instance=null;
        }
    }
}
