using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TheLastWatch.Player;
using TheLastWatch.Integrations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace TheLastWatch.UI
{
    [DefaultExecutionOrder(-500),DisallowMultipleComponent]
    public sealed class WellnessPhotoCamera : MonoBehaviour
    {
        public WellnessExplorer player;
        public WellnessVoiceChat chat;
        public WellnessMainMenu mainMenu;
        public WellnessHud hud;
        public static WellnessPhotoCamera Instance {get;private set;}
        public static bool CameraActive=>Instance!=null&&Instance.cameraActive;
        public static bool GalleryOpen=>Instance!=null&&Instance.galleryOpen;
        public static bool HideGameplayHud=>CameraActive||GalleryOpen||(Instance!=null&&Instance.hideCaptureUi);
        public static bool OwnsShortcuts=>HideGameplayHud||escapeFrame==Time.frameCount;
        private static int escapeFrame=-1;
        private static int playGeneration;
        private int initializedGeneration=-1;
        private bool cameraActive,galleryOpen,capturing,hideCaptureUi,ownsInputLock,requestGallery;
        private Camera view;
        private float originalFov,zoom=1,toastUntil,flash;
        private string toast;
        private WellnessPhotoAlbum album;
        private int page,selected=-1;
        private Texture2D fullImage;
        private RenderTexture captureScreen,captureScaled,captureThumb;
        private Texture2D capturePicture,captureThumbnail;
        private AsyncGPUReadbackRequest pendingRead;
        private bool awaitingRead;
        private readonly Dictionary<int,Texture2D> thumbnails=new Dictionary<int,Texture2D>();
        private WellnessQuietGlassTheme ui;
        private GUIStyle light,titleLight,hintLight;
        public int PhotoCount=>album!=null?album.Photos.Count:0;
        public float Zoom=>zoom;
        public string PhotoDirectory=>album!=null?album.DirectoryPath:Path.Combine(Application.persistentDataPath,"MindSpacePhotos");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics(){Instance=null;escapeFrame=-1;unchecked{playGeneration++;}}
        private void OnEnable()
        {
            if(!Application.isPlaying)return;
            EnsureSession();
        }
        private bool EnsureSession()
        {
            if(Instance!=null&&Instance!=this){enabled=false;return false;}
            Instance=this;
            if(initializedGeneration==playGeneration&&album!=null)return true;
            ReleaseCapture();CloseGallery();CloseCamera();hideCaptureUi=capturing=requestGallery=false;
            initializedGeneration=playGeneration;
            toastUntil=flash=0;
            album=new WellnessPhotoAlbum(PhotoDirectory);
            try{album.Reload();}catch(Exception error){Notice("Photo folder unavailable: "+error.Message);}
            return true;
        }
        public static float ZoomFieldOfView(float original,float magnification)
            =>2*Mathf.Atan(Mathf.Tan(original*Mathf.Deg2Rad*.5f)/Mathf.Clamp(magnification,1,4))*Mathf.Rad2Deg;
        public static Vector2Int CaptureSize(int width,int height)
        {
            float scale=Mathf.Min(1,Mathf.Min(1920f/Mathf.Max(1,width),1080f/Mathf.Max(1,height)));
            return new Vector2Int(Mathf.Max(1,Mathf.RoundToInt(width*scale)),Mathf.Max(1,Mathf.RoundToInt(height*scale)));
        }
        private bool Available=>player!=null&&player.isActiveAndEnabled&&player.ViewCamera!=null&&chat!=null&&chat.isActiveAndEnabled&&
            (mainMenu==null||!mainMenu.IsShowing)&&!player.IsTransitioning;
        private void Update()
        {
            if(!Application.isPlaying||!EnsureSession())return;
            flash=Mathf.MoveTowards(flash,0,Time.unscaledDeltaTime*2.5f);
            if(!Available||WellnessFishing.OwnsShortcuts){CloseGallery();CloseCamera();return;}
            if(requestGallery){requestGallery=false;if(!chat.IsPanelOpen)OpenGallery();}
            if(galleryOpen)
            {
                if(chat.HasOwnPanelOpen){CloseGallery();return;}
                var key=Keyboard.current;if(key==null||!Application.isFocused)return;
                if(key.escapeKey.wasPressedThisFrame){escapeFrame=Time.frameCount;if(selected>=0)Select(-1);else CloseGallery();}
                else if(key.gKey.wasPressedThisFrame){escapeFrame=Time.frameCount;CloseGallery();}
                else if(selected>=0&&key.leftArrowKey.wasPressedThisFrame)Select(Mathf.Max(0,selected-1));
                else if(selected>=0&&key.rightArrowKey.wasPressedThisFrame)Select(Mathf.Min(PhotoCount-1,selected+1));
                return;
            }
            if(chat.IsPanelOpen||player.IsUiInputBlocked||player.TheatreMode){CloseCamera();return;}
            if(!Application.isFocused||player.CursorReleased)return;
            var keyboard=Keyboard.current;var mouse=Mouse.current;
            if(keyboard!=null&&keyboard.gKey.wasPressedThisFrame&&!capturing){OpenGallery();return;}
            if(keyboard!=null&&keyboard.cKey.wasPressedThisFrame&&!capturing)
            {if(cameraActive)CloseCamera();else OpenCamera();return;}
            if(!cameraActive)return;
            if(keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame)
            {escapeFrame=Time.frameCount;CloseCamera();return;}
            if(!capturing&&mouse!=null)
            {
                float wheel=mouse.scroll.ReadValue().y;
                if(Mathf.Abs(wheel)>.001f)zoom=Mathf.Clamp(zoom*Mathf.Pow(1.13f,Mathf.Clamp(wheel/120f,-3,3)),1,4);
            }
            if(view!=null)view.fieldOfView=ZoomFieldOfView(originalFov,zoom);
            if(!capturing&&((mouse!=null&&mouse.leftButton.wasPressedThisFrame)||(keyboard!=null&&keyboard.spaceKey.wasPressedThisFrame)))
                StartCoroutine(Capture());
        }
        public void OpenCamera()
        {
            if(!Available||chat.IsPanelOpen||player.IsUiInputBlocked||WellnessFishing.OwnsShortcuts)return;
            view=player.ViewCamera;originalFov=view.fieldOfView;zoom=1;cameraActive=true;
        }
        private void CloseCamera()
        {
            if(cameraActive&&view!=null)view.fieldOfView=originalFov;
            cameraActive=false;view=null;zoom=1;
        }
        // Called from pause settings: defer until the existing menu has released its lock.
        public void OpenGalleryFromMenu(){chat.ClosePanel();requestGallery=true;}
        public void OpenGallery()
        {
            if(!Available||capturing||chat.IsPanelOpen||player.IsUiInputBlocked||WellnessFishing.OwnsShortcuts)return;
            CloseCamera();ClearImages();page=0;selected=-1;
            try{album.Reload();}catch(Exception error){Notice("Could not read the photo inventory: "+error.Message);}
            galleryOpen=true;player.SetUiInputBlocked(true);ownsInputLock=true;
        }
        private void CloseGallery()
        {
            galleryOpen=false;
            if(ownsInputLock&&player!=null){player.SetUiInputBlocked(chat!=null&&chat.HasOwnPanelOpen);ownsInputLock=false;}
            ClearImages();selected=-1;
        }
        private void Notice(string message){toast=message;toastUntil=Time.unscaledTime+6;}
        private IEnumerator Capture()
        {
            if(album.Photos.Count>=WellnessPhotoAlbum.Capacity){Notice("Your album is full. Move photos out of the saved photo folder to make room.");yield break;}
            capturing=true;hideCaptureUi=true;
            float shutterZoom=zoom;var sky=chat.skyCycle;
            var record=new WellnessPhotoRecord{id=Guid.NewGuid().ToString("N"),capturedUtc=DateTime.UtcNow.ToString("o"),
                zoom=shutterZoom,hour=sky!=null?sky.Hour:12,weather=sky!=null?sky.Summary:"Your quiet space"};
            RenderTexture screen=null,scaled=null;Texture2D picture=null,thumbnail=null;
            try
            {
                // Hide all gameplay IMGUI for the complete captured frame. Capture
                // the finished URP frame, including water/sky/post processing.
                yield return new WaitForEndOfFrame();
                var size=CaptureSize(Screen.width,Screen.height);
                screen=captureScreen=RenderTexture.GetTemporary(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);
                scaled=captureScaled=RenderTexture.GetTemporary(size.x,size.y,0,RenderTextureFormat.ARGB32);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(screen);
                Graphics.Blit(screen,scaled);hideCaptureUi=false;
                picture=capturePicture=new Texture2D(size.x,size.y,TextureFormat.RGBA32,false);
                if(SystemInfo.supportsAsyncGPUReadback)
                {
                    pendingRead=AsyncGPUReadback.Request(scaled,0,TextureFormat.RGBA32);awaitingRead=true;
                    while(!pendingRead.done)yield return null;
                    awaitingRead=false;
                    if(pendingRead.hasError){Notice("The camera could not read this frame. Please try again.");yield break;}
                    picture.LoadRawTextureData(pendingRead.GetData<byte>());picture.Apply();
                }
                else ReadPixels(scaled,picture);
                var thumbSize=CaptureSize(size.x,size.y);float ratio=320f/Mathf.Max(thumbSize.x,thumbSize.y);
                thumbnail=captureThumbnail=new Texture2D(Mathf.Max(1,Mathf.RoundToInt(size.x*ratio)),Mathf.Max(1,Mathf.RoundToInt(size.y*ratio)),TextureFormat.RGB24,false);
                captureThumb=RenderTexture.GetTemporary(thumbnail.width,thumbnail.height,0,RenderTextureFormat.ARGB32);
                Graphics.Blit(picture,captureThumb);ReadPixels(captureThumb,thumbnail);
                record.width=size.x;record.height=size.y;
                if(TrySave(record,picture,thumbnail))
                {flash=.22f;Notice("Photo saved  ·  "+PhotoCount+" in your inventory  ·  G to view");}
            }
            finally
            {
                ReleaseCapture();
                hideCaptureUi=false;capturing=false;
            }
        }
        private void ReleaseCapture()
        {
            // Explicitly cover scene unload/disabled components as well as normal
            // completion, without releasing a target still used by GPU readback.
            if(awaitingRead&&!pendingRead.done)pendingRead.WaitForCompletion();awaitingRead=false;
            if(captureScreen!=null)RenderTexture.ReleaseTemporary(captureScreen);captureScreen=null;
            if(captureScaled!=null)RenderTexture.ReleaseTemporary(captureScaled);captureScaled=null;
            if(captureThumb!=null)RenderTexture.ReleaseTemporary(captureThumb);captureThumb=null;
            if(capturePicture!=null)Destroy(capturePicture);capturePicture=null;
            if(captureThumbnail!=null)Destroy(captureThumbnail);captureThumbnail=null;
        }
        private bool TrySave(WellnessPhotoRecord photo,Texture2D picture,Texture2D thumbnail)
        {
            try{album.Save(photo,picture.EncodeToPNG(),thumbnail.EncodeToJPG(85));return true;}
            catch(Exception error){Notice("Photo was not saved: "+error.Message);Debug.LogWarning("MindSpace photo save: "+error.Message);return false;}
        }
        private static void ReadPixels(RenderTexture source,Texture2D target)
        {
            var previous=RenderTexture.active;
            try{RenderTexture.active=source;target.ReadPixels(new Rect(0,0,source.width,source.height),0,0);target.Apply();}
            finally{RenderTexture.active=previous;}
        }
        private void ClearImages()
        {
            foreach(var image in thumbnails.Values)if(image!=null)Destroy(image);thumbnails.Clear();
            if(fullImage!=null)Destroy(fullImage);fullImage=null;
        }
        private Texture2D Load(WellnessPhotoRecord photo,bool thumbnail)
        {
            Texture2D result=null;
            try{result=new Texture2D(2,2,TextureFormat.RGB24,false);if(!result.LoadImage(album.ReadImage(photo,thumbnail),true))throw new IOException("Unreadable image.");return result;}
            catch(Exception){if(result!=null)Destroy(result);return null;}
        }
        private Texture2D Thumbnail(int index)
        {
            if(!thumbnails.TryGetValue(index,out var image)){image=Load(album.Photos[index],true);thumbnails[index]=image;}
            return image;
        }
        private void Select(int index)
        {
            if(fullImage!=null)Destroy(fullImage);fullImage=null;selected=index;
            if(index>=0&&index<PhotoCount)fullImage=Load(album.Photos[index],false);
        }
        private void OnGUI()
        {
            if(!Application.isPlaying||!Available||hideCaptureUi)return;
            if(!cameraActive&&!galleryOpen&&Time.unscaledTime>=toastUntil)return;
            EnsureTheme();var previous=GUI.matrix;int depth=GUI.depth;Color color=GUI.color;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);if(scale<=0)return;
            GUI.depth=-30;GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f),Quaternion.identity,new Vector3(scale,scale,1));
            try
            {
                if(galleryOpen)DrawGallery();else if(cameraActive)DrawCamera();
                if(Time.unscaledTime<toastUntil&&!galleryOpen)
                {ui.Rounded(new Rect(250,574,780,43),new Color(.06f,.10f,.085f,.9f),12);GUI.Label(new Rect(270,580,740,30),toast,hintLight);}
                if(flash>0)WellnessQuietGlassTheme.Fill(new Rect(0,0,1280,720),new Color(1,.99f,.94f,flash));
            }
            finally{GUI.matrix=previous;GUI.depth=depth;GUI.color=color;}
        }
        private void EnsureTheme()
        {
            if(ui!=null)return;
            ui=new WellnessQuietGlassTheme(chat.quietGlassSerif,chat.quietGlassSans);
            light=new GUIStyle(ui.Body){fontSize=19};WellnessUiText.Static(light,new Color(.98f,.96f,.88f));
            titleLight=new GUIStyle(light){fontSize=24};hintLight=new GUIStyle(light){fontSize=16,alignment=TextAnchor.MiddleCenter,wordWrap=true};
        }
        private void DrawCamera()
        {
            ui.Rounded(new Rect(40,30,292,54),new Color(.06f,.10f,.085f,.82f),14);
            GUI.Label(new Rect(60,43,250,30),"MindSpace  /  Photo camera",light);
            ui.Rounded(new Rect(1050,30,190,54),new Color(.06f,.10f,.085f,.82f),14);
            GUI.Label(new Rect(1067,43,168,30),PhotoCount+" photos  ·  G",light);
            var frame=new Rect(135,113,1010,438);Color ink=new Color(.98f,.96f,.88f,.70f);
            foreach(var corner in new[]{new Vector2(frame.x,frame.y),new Vector2(frame.xMax,frame.y),new Vector2(frame.x,frame.yMax),new Vector2(frame.xMax,frame.yMax)})
            {
                float sx=corner.x==frame.x?1:-1,sy=corner.y==frame.y?1:-1;
                WellnessQuietGlassTheme.Fill(new Rect(corner.x+(sx<0?-32:0),corner.y,32,2),ink);
                WellnessQuietGlassTheme.Fill(new Rect(corner.x,corner.y+(sy<0?-32:0),2,32),ink);
            }
            WellnessQuietGlassTheme.Fill(new Rect(633,332,14,1),new Color(1,1,1,.28f));
            WellnessQuietGlassTheme.Fill(new Rect(640,325,1,14),new Color(1,1,1,.28f));
            ui.Rounded(new Rect(440,631,400,52),new Color(.06f,.10f,.085f,.88f),18);
            GUI.Label(new Rect(455,641,370,31),capturing?"Saving your moment…":"Click or Space  ·  Capture   |   "+zoom.ToString("0.0")+"×",hintLight);
            GUI.Label(new Rect(35,641,390,30),"Wheel  Zoom     WASD  Move",hintLight);
            GUI.Label(new Rect(885,641,360,30),"C or Esc  Put camera away",hintLight);
        }
        private void DrawGallery()
        {
            WellnessQuietGlassTheme.Fill(new Rect(-2000,-2000,5280,4720),new Color(.03f,.055f,.045f,.56f));
            Rect panel=new Rect(85,44,1110,632);ui.Surface(panel);
            if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&!panel.Contains(Event.current.mousePosition))
            {escapeFrame=Time.frameCount;CloseGallery();Event.current.Use();return;}
            GUI.Label(new Rect(117,65,650,52),selected<0?"Your captured moments":"A moment to keep",ui.Title);
            GUI.Label(new Rect(121,120,700,28),"Photo inventory  ·  "+PhotoCount+" saved locally  ·  No uploads",ui.Hint);
            if(ui.Action(new Rect(1030,75,132,46),"Close")){CloseGallery();return;}
            ui.Line(118,161,1044);
            if(selected>=0){DrawPhoto();return;}
            if(PhotoCount==0)
            {
                ui.Card(new Rect(332,226,616,267));
                ui.Outline(new Rect(587,259,106,76),WellnessQuietGlassTheme.Sage,16);
                ui.Outline(new Rect(622,275,37,37),WellnessQuietGlassTheme.Sage,19);
                GUI.Label(new Rect(418,351,460,40),"Make room for a beautiful moment.",ui.Body);
                GUI.Label(new Rect(407,405,480,30),"Look around, zoom in and capture what you love.",ui.Hint);
                if(ui.Action(new Rect(481,518,318,54),"Take your first photo",true)){CloseGallery();OpenCamera();}
                return;
            }
            const int perPage=6;
            int start=page*perPage,end=Mathf.Min(start+perPage,PhotoCount);
            for(int index=start;index<end;index++)
            {
                int slot=index-start;Rect card=new Rect(118+(slot%3)*353,180+(slot/3)*205,338,191);
                ui.Card(card);Rect imageArea=new Rect(card.x+9,card.y+9,320,151);
                ui.Rounded(imageArea,new Color(.18f,.24f,.20f,.15f),8);
                var image=Thumbnail(index);
                if(image!=null)GUI.DrawTexture(imageArea,image,ScaleMode.ScaleToFit);
                else GUI.Label(imageArea,"Preview unavailable",ui.Small);
                GUI.Label(new Rect(card.x+15,card.y+163,310,24),PhotoDate(album.Photos[index]),ui.Hint);
                if(GUI.Button(card,GUIContent.none,ui.TextButton)){Select(index);return;}
            }
            ui.Line(118,600,1044);
            if(page>0&&ui.Action(new Rect(119,615,130,39),"Previous")){page--;ClearImages();}
            GUI.Label(new Rect(539,618,230,30),"Page "+(page+1)+" of "+Mathf.CeilToInt(PhotoCount/6f),ui.Small);
            if(end<PhotoCount&&ui.Action(new Rect(1032,615,130,39),"Next")){page++;ClearImages();}
        }
        private void DrawPhoto()
        {
            Rect photoRect=new Rect(122,180,1036,415);
            ui.Rounded(photoRect,new Color(.045f,.07f,.06f,.94f),14);
            if(fullImage!=null)GUI.DrawTexture(new Rect(131,189,1018,397),fullImage,ScaleMode.ScaleToFit);
            else GUI.Label(new Rect(481,348,450,40),"This photo could not be opened.",ui.Small);
            var record=album.Photos[selected];
            GUI.Label(new Rect(315,619,650,30),PhotoDate(record)+"  ·  "+record.zoom.ToString("0.0")+"× zoom  ·  "+record.width+" × "+record.height,ui.Hint);
            if(ui.Action(new Rect(120,615,172,41),"Back to album")){Select(-1);return;}
            bool old=GUI.enabled;GUI.enabled=selected>0;
            if(ui.Action(new Rect(982,615,79,41),"‹"))Select(selected-1);
            GUI.enabled=selected<PhotoCount-1;
            if(ui.Action(new Rect(1076,615,79,41),"›"))Select(selected+1);
            GUI.enabled=old;
        }
        private static string PhotoDate(WellnessPhotoRecord photo)
            =>DateTime.TryParse(photo.capturedUtc,out var date)?date.ToLocalTime().ToString("MMM d, yyyy  ·  HH:mm"):"Captured moment";
        private void OnDisable()
        {
            StopAllCoroutines();ReleaseCapture();CloseGallery();CloseCamera();hideCaptureUi=capturing=requestGallery=false;
            if(Instance==this)Instance=null;
            ui?.Dispose();ui=null;
        }
    }
}
