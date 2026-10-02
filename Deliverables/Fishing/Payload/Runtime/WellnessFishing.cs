using System;
using System.IO;
using TheLastWatch.Environment;
using TheLastWatch.Player;
using TheLastWatch.Integrations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastWatch.UI
{
    [DefaultExecutionOrder(-800),DisallowMultipleComponent]
    public sealed class WellnessFishing : MonoBehaviour
    {
        public WellnessExplorer player;
        public WellnessVoiceChat chat;
        public WellnessMainMenu mainMenu;
        public WellnessKoiPond pond;
        public static WellnessFishing Instance {get;private set;}
        public static bool ModalOpen=>Instance!=null&&(Instance.inventoryOpen||Instance.round!=null);
        public static bool OwnsShortcuts=>ModalOpen||consumedFrame==Time.frameCount;
        public static bool HideGameplayHud=>ModalOpen;
        static int consumedFrame=-1,generation;
        int initialized=-1,page,selected=-1;
        bool inventoryOpen,ownsLock,requestInventory,canCast;
        float aimClock,toastUntil;
        string toast;
        Vector3 landing;
        KoiFishingRound round;
        KoiCatchInventory inventory;
        KoiCatch lastCatch,pendingCatch;
        KoiFishingViews views;
        System.Random random;
        readonly RaycastHit[] waterHits=new RaycastHit[64];
        WellnessQuietGlassTheme ui;
        GUIStyle light,hint;
        public int CatchCount=>inventory==null?0:inventory.Fish.Count;
        public string SaveDirectory=>Path.Combine(Application.persistentDataPath,"MindSpaceFishing");
        bool Available=>player!=null&&player.isActiveAndEnabled&&player.ViewCamera!=null&&pond!=null&&pond.library!=null&&
            chat!=null&&chat.isActiveAndEnabled&&(mainMenu==null||!mainMenu.IsShowing)&&!player.IsTransitioning;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){Instance=null;consumedFrame=-1;unchecked{generation++;}}
        void OnEnable(){if(Application.isPlaying)EnsureSession();}
        bool EnsureSession()
        {
            if(Instance!=null&&Instance!=this){enabled=false;return false;}Instance=this;
            if(initialized==generation&&inventory!=null)return true;
            CloseModal();views?.Dispose();views=null;initialized=generation;lastCatch=pendingCatch=null;
            random=new System.Random(Guid.NewGuid().GetHashCode());inventory=new KoiCatchInventory(SaveDirectory);
            try{inventory.Reload();}catch(Exception){Notice("Your koi inventory could not be read. Check access to the local save folder.");}
            return true;
        }
        void EnsureViews()
        {if(views==null&&player!=null&&player.ViewCamera!=null){views=new KoiFishingViews(transform,player.ViewCamera.transform,pond.library);views.Equip(inventory.Equipped);}}
        public static Vector3 WaterCenter(WellnessKoiPond pond)
        {var center=pond.pondCenter;if(pond.waterSurface!=null)center.y=pond.waterSurface.bounds.center.y;return center;}
        public static bool FindLanding(WellnessKoiPond pond,Vector3 eye,Vector3 forward,Transform ignore,RaycastHit[] hits,out Vector3 target)
        {
            target=default;if(pond==null)return false;var center=WaterCenter(pond);
            for(int option=0;option<5;option++)
            {
                if(!KoiFishingWater.Candidate(eye,forward,center,pond.pondRadii,option,out var candidate))continue;
                // Reject the bridge deck, rocks, roofs and other dry landing spots.
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
            if(!Available){CloseModal();views?.HideRod();views?.ShowHeld(false);canCast=false;return;}
            EnsureViews();
            if(chat.HasOwnPanelOpen||WellnessPhotoCamera.OwnsShortcuts||player.TheatreMode)
            {CloseModal();views?.ShowHeld(false);canCast=false;return;}
            if(requestInventory){requestInventory=false;OpenInventory();}
            if(views!=null)views.ShowHeld(!ModalOpen&&!WellnessPhotoCamera.HideGameplayHud);
            if(!Application.isFocused)return;
            var key=Keyboard.current;if(key==null)return;
            if(inventoryOpen)
            {
                if(key.escapeKey.wasPressedThisFrame||key.iKey.wasPressedThisFrame)CloseModal();
                else if(key.leftArrowKey.wasPressedThisFrame)Select(Mathf.Max(0,selected-1));
                else if(key.rightArrowKey.wasPressedThisFrame)Select(Mathf.Min(CatchCount-1,selected+1));
                return;
            }
            if(round!=null)
            {
                if(key.escapeKey.wasPressedThisFrame||key.fKey.wasPressedThisFrame){CloseModal();return;}
                round.Step(Time.unscaledDeltaTime,key.spaceKey.isPressed,key.spaceKey.wasPressedThisFrame);
                if(round.State==KoiFishingRound.Phase.Caught&&lastCatch==null)
                {
                    var variety=pond.library.varieties[random.Next(pond.library.varieties.Length)].name;
                    lastCatch=pendingCatch=KoiCatch.Create(variety,Mathf.Lerp(.90f,1.08f,(float)random.NextDouble()));TrySaveCatch();
                }
                views.PoseRod(round,landing,Time.unscaledTime);
                if(round.Finished&&key.iKey.wasPressedThisFrame){CloseModal();OpenInventory();return;}
                if(round.Finished&&key.enterKey.wasPressedThisFrame)CloseModal();
                return;
            }
            if(player.IsUiInputBlocked||player.CursorReleased){canCast=false;return;}
            if(key.iKey.wasPressedThisFrame){OpenInventory();return;}
            if(player.IsSeated){canCast=false;return;}
            aimClock-=Time.unscaledDeltaTime;
            if(aimClock<=0){aimClock=.15f;canCast=FindLanding(pond,player.ViewCamera.transform.position,player.ViewCamera.transform.forward,player.transform,waterHits,out landing);}
            if(key.fKey.wasPressedThisFrame)
            {
                if(pendingCatch!=null){Notice("A catch still needs saving. Open I and choose Retry save.");return;}
                if(CatchCount>=KoiCatchInventory.Capacity){Notice("Your koi inventory is full.");return;}
                if(!FindLanding(pond,player.ViewCamera.transform.position,player.ViewCamera.transform.forward,player.transform,waterHits,out landing)){Notice("Stand beside the pond or on the bridge and face open water.");return;}
                round=new KoiFishingRound(random.Next());lastCatch=null;Lock();views.PoseRod(round,landing,Time.unscaledTime);
            }
        }
        void Lock(){player.SetUiInputBlocked(true);ownsLock=true;consumedFrame=Time.frameCount;}
        void CloseModal()
        {
            if(inventoryOpen||round!=null)consumedFrame=Time.frameCount;
            inventoryOpen=false;round?.Cancel();round=null;views?.ClosePreview();views?.HideRod();
            if(ownsLock&&player!=null){player.SetUiInputBlocked((chat!=null&&chat.HasOwnPanelOpen)||WellnessPhotoCamera.GalleryOpen||(mainMenu!=null&&mainMenu.IsShowing));ownsLock=false;}
            selected=-1;
        }
        public void OpenInventoryFromMenu(){chat.ClosePanel();requestInventory=true;}
        public void OpenInventory()
        {
            if(!Available||chat.HasOwnPanelOpen||WellnessPhotoCamera.OwnsShortcuts||player.IsUiInputBlocked||round!=null)return;
            EnsureViews();inventoryOpen=true;page=0;Lock();Select(CatchCount>0?0:-1);
        }
        void Select(int index)
        {selected=index;views?.SelectPreview(index>=0&&index<CatchCount?inventory.Fish[index]:null);if(index>=0)page=index/6;}
        void TrySaveCatch()
        {
            if(pendingCatch==null)return;
            try{inventory.Save(pendingCatch);round?.TryClaim();pendingCatch=null;Notice("Koi caught and saved  ·  I to view or equip");}
            catch(Exception){Notice("Catch not saved yet. Retry saving in your koi inventory.");}
        }
        void Equip(string id)
        {try{inventory.Equip(id);views.Equip(inventory.Equipped);Notice(id==null?"Koi put away.":"Koi equipped  ·  I to change or put away");}catch(Exception){Notice("Could not save the equipped choice.");}}
        void Notice(string message){toast=message;toastUntil=Time.unscaledTime+6;}
        public static string FishName(KoiCatch fish)=>fish==null?"Koi":System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(fish.variety)+" koi";
        void EnsureTheme()
        {
            if(ui!=null)return;ui=new WellnessQuietGlassTheme(chat.quietGlassSerif,chat.quietGlassSans);
            light=new GUIStyle(ui.Small){fontSize=19};WellnessUiText.Static(light,new Color(.98f,.97f,.90f));
            hint=new GUIStyle(ui.Hint){wordWrap=true};
        }
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
                {ui.Rounded(new Rect(405,580,470,64),new Color(.06f,.10f,.08f,.88f),16);GUI.Label(new Rect(425,591,430,28),"F  Cast your line     I  Koi inventory",light);}
                if(Time.unscaledTime<toastUntil&&!ModalOpen)
                {ui.Rounded(new Rect(230,519,820,45),new Color(.06f,.10f,.08f,.92f),13);GUI.Label(new Rect(251,528,780,29),toast,light);}
            }
            finally{GUI.matrix=matrix;GUI.depth=depth;GUI.color=color;}
        }
        void DrawRound()
        {
            Rect panel=new Rect(383,158,514,452);ui.Surface(panel);
            GUI.Label(new Rect(412,181,455,57),round.Finished?(round.State==KoiFishingRound.Phase.Caught?"A koi to keep":"The koi slipped away"):"A quiet cast",new GUIStyle(ui.Title){fontSize=38});
            if(round.State==KoiFishingRound.Phase.Reeling)
            {
                GUI.Label(new Rect(414,256,246,75),"Keep the koi inside the green bar.",new GUIStyle(ui.Body){wordWrap=true});
                GUI.Label(new Rect(414,352,236,83),"Hold or tap Space to lift the bar.\nRelease Space to let it fall.",hint);
                Rect track=new Rect(716,251,61,253);ui.Rounded(track,new Color(.22f,.34f,.32f,.22f),15);
                ui.Rounded(new Rect(track.x+4,track.y+track.height*(1-round.Bar-KoiFishingRound.BarWidth),track.width-8,track.height*KoiFishingRound.BarWidth),round.InBar?new Color(.50f,.72f,.40f):new Color(.67f,.73f,.53f),9);
                float y=track.y+track.height*(1-round.Fish);ui.Rounded(new Rect(track.x+13,y-7,32,14),new Color(.91f,.49f,.27f),7);
                ui.Rounded(new Rect(798,251,14,253),new Color(.20f,.27f,.21f,.18f),7);
                ui.Rounded(new Rect(798,504-253*round.Progress,14,Mathf.Max(5,253*round.Progress)),WellnessQuietGlassTheme.Sage,7);
                GUI.Label(new Rect(414,464,230,35),Mathf.RoundToInt(round.Progress*100)+"% reeled in",ui.Small);
            }
            else
            {
                string text=round.State==KoiFishingRound.Phase.Casting?"Casting into the pond…":round.State==KoiFishingRound.Phase.Waiting?"Enjoy the water. Watch for a bite.":round.State==KoiFishingRound.Phase.Bite?"A bite! Press Space to hook the koi.":
                    round.State==KoiFishingRound.Phase.Caught?FishName(lastCatch)+"\n"+(lastCatch!=null?lastCatch.lengthCm.ToString("0.0")+" cm  ·  "+lastCatch.weightKg.ToString("0.00")+" kg":"")+"\n"+(pendingCatch==null?"Saved in your koi inventory.":"Not saved yet. Open I to retry."):"No worries. Cast again whenever you like.";
                GUI.Label(new Rect(414,275,450,150),text,new GUIStyle(ui.Body){wordWrap=true});
                if(round.State==KoiFishingRound.Phase.Bite)ui.Rounded(new Rect(415,449,430,7),new Color(.62f,.70f,.46f),4);
                if(round.Finished&&ui.Action(new Rect(415,477,220,49),"Back to the pond",true)){CloseModal();return;}
                if(round.State==KoiFishingRound.Phase.Caught&&ui.Action(new Rect(651,477,211,49),"View your koi")){CloseModal();OpenInventory();return;}
            }
            GUI.Label(new Rect(416,551,446,30),"Esc or F  Cancel / return to the pond",ui.Hint);
        }
        void DrawInventory()
        {
            WellnessQuietGlassTheme.Fill(new Rect(-2000,-2000,5280,4720),new Color(.025f,.05f,.04f,.50f));Rect panel=new Rect(88,45,1104,631);ui.Surface(panel);
            if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&!panel.Contains(Event.current.mousePosition)){CloseModal();Event.current.Use();return;}
            GUI.Label(new Rect(119,64,780,57),"Your koi collection",ui.Title);
            GUI.Label(new Rect(122,128,850,29),CatchCount+" koi caught  ·  Saved locally  ·  I or Esc to close",ui.Hint);
            if(ui.Action(new Rect(1034,79,126,43),"Close")){CloseModal();return;}
            ui.Line(120,168,1037);
            if(CatchCount==0)
            {GUI.Label(new Rect(234,251,730,55),"Your first koi is waiting in the pond.",ui.Body);GUI.Label(new Rect(257,319,700,65),"Stand at the water or on the bridge, face the pond and press F.\nHook a bite with Space, then keep the koi in the green bar.",hint);}
            int start=page*6,end=Mathf.Min(start+6,CatchCount);
            for(int index=start;index<end;index++)
            {
                int slot=index-start;var fish=inventory.Fish[index];Rect card=new Rect(120+(slot%2)*310,189+(slot/2)*121,294,107);ui.Card(card);
                if(index==selected)ui.Outline(card,WellnessQuietGlassTheme.Sage,18);
                GUI.Label(new Rect(card.x+17,card.y+15,260,34),FishName(fish),ui.Small);
                GUI.Label(new Rect(card.x+17,card.y+53,258,30),fish.lengthCm.ToString("0.0")+" cm  ·  "+fish.weightKg.ToString("0.00")+" kg"+(fish.id==inventory.EquippedId?"  ·  Held":""),ui.Hint);
                if(GUI.Button(card,GUIContent.none,ui.TextButton)){Select(index);return;}
            }
            if(selected>=0&&selected<CatchCount)
            {
                var fish=inventory.Fish[selected];Rect detail=new Rect(758,189,399,357);ui.Card(detail);
                if(views.Preview!=null)GUI.DrawTexture(new Rect(774,205,367,206),views.Preview,ScaleMode.ScaleToFit);
                GUI.Label(new Rect(777,423,366,35),FishName(fish),ui.Body);
                GUI.Label(new Rect(777,463,366,31),fish.lengthCm.ToString("0.0")+" cm  ·  "+fish.weightKg.ToString("0.00")+" kg",ui.Small);
                if(ui.Action(new Rect(776,492,363,43),fish.id==inventory.EquippedId?"Put koi away":"Equip this koi",true))Equip(fish.id==inventory.EquippedId?null:fish.id);
            }
            ui.Line(120,571,1037);
            if(page>0&&ui.Action(new Rect(121,594,138,44),"Previous")){page--;Select(page*6);return;}
            GUI.Label(new Rect(290,603,435,30),CatchCount==0?"Koi only, using your pond's fish models":"Page "+(page+1)+" of "+Mathf.CeilToInt(CatchCount/6f),ui.Hint);
            if(end<CatchCount&&ui.Action(new Rect(625,594,110,44),"Next")){Select((page+1)*6);return;}
            if(pendingCatch!=null&&ui.Action(new Rect(777,594,360,44),"Retry saving your catch",true)){TrySaveCatch();Select(CatchCount>0?0:-1);}
            else GUI.Label(new Rect(777,603,369,30),"Equipped koi appears in your hand.",ui.Hint);
        }
        void OnDisable()
        {
            CloseModal();views?.Dispose();views=null;ui?.Dispose();ui=null;requestInventory=canCast=false;
            if(Instance==this)Instance=null;
        }
    }
}
