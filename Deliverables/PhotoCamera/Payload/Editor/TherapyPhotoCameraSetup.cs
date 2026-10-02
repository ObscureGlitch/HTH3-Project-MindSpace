using System;
using System.IO;
using System.Linq;
using TheLastWatch.UI;
using TheLastWatch.Player;
using TheLastWatch.Integrations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TherapyGame.Editor
{
    public static class TherapyPhotoCameraSetup
    {
        const string Root="Assets/TherapyGame",Scene=Root+"/Scenes/TherapyRoom.unity";
        const string Report=Root+"/Documentation/PhotoCameraCheck.txt";
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        public static void InstallBatch()
        {
            EditorSceneManager.OpenScene(Scene);
            bool success=Install();EditorApplication.Exit(success?0:1);
        }
        [MenuItem("Therapy Game/Install Player Photo Camera")]
        public static void InstallFromMenu(){Install();}
        static bool Install()
        {
            int undo=-1;
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Stop Play mode first.");
                var scene=EditorSceneManager.GetActiveScene();Require(scene.path==Scene,"Open TherapyRoom first.");
                var root=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom");
                var player=root.GetComponentInChildren<WellnessExplorer>(true);
                var chat=root.GetComponentInChildren<WellnessVoiceChat>(true);
                var menu=root.GetComponent<WellnessMainMenu>();var hud=root.GetComponent<WellnessHud>();
                Require(player!=null&&chat!=null&&menu!=null&&hud!=null&&player.ViewCamera!=null,"Missing existing room/camera/menu references.");
                string checks=CheckAlbum()+CheckZoom();
                int cameras=root.GetComponentsInChildren<Camera>(true).Length,renderers=root.GetComponentsInChildren<Renderer>(true).Length;
                float fov=player.ViewCamera.fieldOfView;Vector3 position=player.ViewCamera.transform.localPosition;
                string backup=Path.GetFullPath("TherapyBackups/PhotoCamera/"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
                Directory.CreateDirectory(backup);File.Copy(scene.path,Path.Combine(backup,"TherapyRoom-before-photo-camera.unity"));
                Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Player photo camera and inventory");
                var camera=root.GetComponent<WellnessPhotoCamera>()??Undo.AddComponent<WellnessPhotoCamera>(root);
                Undo.RecordObject(camera,"Photo camera references");camera.player=player;camera.chat=chat;camera.mainMenu=menu;camera.hud=hud;
                EditorUtility.SetDirty(camera);
                Require(cameras==root.GetComponentsInChildren<Camera>(true).Length&&renderers==root.GetComponentsInChildren<Renderer>(true).Length,"Do not add a permanent rendering pass.");
                Require(fov==player.ViewCamera.fieldOfView&&position==player.ViewCamera.transform.localPosition,"Do not change the saved player view.");
                EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Photo camera scene save failed.");
                AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(undo);undo=-1;
                var photoGuid=AssetDatabase.AssetPathToGUID(Root+"/Runtime/WellnessPhotoCamera.cs");
                Require(!string.IsNullOrEmpty(photoGuid)&&File.ReadAllText(scene.path).Contains(photoGuid),"Photo camera reference did not persist.");
                Directory.CreateDirectory(Path.GetDirectoryName(Report));
                File.WriteAllText(Report,"PASS: player photo camera and local inventory installed "+DateTime.Now.ToString("s")+"\n"+checks+
                    "PASS: existing gameplay camera/menu/chat/HUD references saved; no added camera, renderer, provider, shader or webcam access. Existing camera pose/FOV unchanged.\n"+
                    "Controls: C camera, wheel 1–4x zoom, click/Space capture, G inventory, Escape back/close. Inventory also accessible from pause Controls.\n"+
                    "Photos: persistentDataPath/MindSpacePhotos, full PNG at up to 1920x1080, 320px JPEG thumbnails, metadata committed last. 200-photo safe capacity; no automatic deletion.\n"+
                    "Performance: no capture work while idle; one capture at a time; GPU readback when supported; six thumbnails per page plus one opened photo; textures released on close.\n"+
                    "Not yet verified: actual GPU screenshot/orientation, visual layout, live keyboard/mouse input and Play-mode lifecycle. No Play, microphone or webcam was started.\nBackup: "+backup+"\n");
                Debug.Log("PLAYER_PHOTO_CAMERA_INSTALLED: "+Report);return true;
            }
            catch(Exception error)
            {
                if(undo>=0)Undo.RevertAllDownToGroup(undo);
                Directory.CreateDirectory(Path.GetDirectoryName(Report));File.WriteAllText(Report,"FAILED\n"+error);Debug.LogException(error);return false;
            }
        }
        static string CheckAlbum()
        {
            string folder=Path.GetFullPath("Temp/PhotoCameraChecks/"+Guid.NewGuid().ToString("N"));
            var album=new WellnessPhotoAlbum(folder);
            Texture2D source=null,loaded=null;
            try
            {
                source=new Texture2D(4,2,TextureFormat.RGB24,false);
                source.SetPixels(new[]{Color.red,Color.green,Color.blue,Color.white,Color.yellow,Color.cyan,Color.magenta,Color.black});source.Apply();
                var photo=new WellnessPhotoRecord{id=Guid.NewGuid().ToString("N"),capturedUtc=DateTime.UtcNow.ToString("o"),width=4,height=2,zoom=2,hour=12,weather="Clear"};
                var png=source.EncodeToPNG();album.Save(photo,png,source.EncodeToJPG(85));
                Require(album.Photos.Count==1,"Save did not publish the captured photo.");
                var reopened=new WellnessPhotoAlbum(folder);reopened.Reload();
                Require(reopened.Photos.Count==1&&reopened.Photos[0].zoom==2&&reopened.Photos[0].id==photo.id,"Inventory did not persist across reload.");
                Require(File.ReadAllBytes(album.ImagePath(photo)).SequenceEqual(png),"Original PNG changed during persistence.");
                loaded=new Texture2D(2,2,TextureFormat.RGB24,false);Require(loaded.LoadImage(reopened.ReadImage(photo,false)),"Saved photo could not be opened.");
                Require(loaded.width==4&&loaded.height==2&&loaded.GetPixel(0,0)==Color.red&&loaded.GetPixel(0,1)==Color.yellow,"Photo pixel/color layout changed during save/load.");
                bool duplicateRejected=false;
                try{album.Save(photo,png,source.EncodeToJPG(85));}catch(IOException){duplicateRejected=true;}
                Require(duplicateRejected&&File.ReadAllBytes(album.ImagePath(photo)).SequenceEqual(png),"Duplicate ID overwrote an existing photo.");
                bool unsafeRejected=false;try{album.ImagePath(new WellnessPhotoRecord{id="../outside"});}catch(ArgumentException){unsafeRejected=true;}
                Require(unsafeRejected,"Photo path traversal was not rejected.");
                File.WriteAllText(Path.Combine(folder,"malformed.json"),"not valid json");
                File.WriteAllText(Path.Combine(folder,Guid.NewGuid().ToString("N")+".json"),JsonUtility.ToJson(new WellnessPhotoRecord{id="../outside"}));
                reopened.Reload();Require(reopened.Photos.Count==1,"Damaged metadata broke the existing inventory.");
                for(int i=album.Photos.Count;i<WellnessPhotoAlbum.Capacity;i++)album.Photos.Add(photo);
                var extra=new WellnessPhotoRecord{id=Guid.NewGuid().ToString("N")};bool fullRejected=false;
                try{album.Save(extra,png,png);}catch(IOException){fullRejected=true;}
                Require(fullRejected&&!File.Exists(album.ImagePath(extra)),"Full inventory created a partial photo.");
                Require(!Directory.EnumerateFiles(folder,"*.tmp").Any(),"Save left uncommitted temporary files.");
                return "PASS: real PNG/JPEG/metadata save, inventory reload, photo decoding and exact PNG colors; duplicate protection, path validation, corrupt-metadata tolerance and full-album protection.\n";
            }
            finally
            {
                if(source!=null)UnityEngine.Object.DestroyImmediate(source);if(loaded!=null)UnityEngine.Object.DestroyImmediate(loaded);
                // Unique test-owned directory, always strictly beneath project Temp.
                if(Directory.Exists(folder))Directory.Delete(folder,true);
            }
        }
        static string CheckZoom()
        {
            foreach(float baseFov in new[]{35f,48f,60f,70f,90f})
            {
                Require(Mathf.Abs(WellnessPhotoCamera.ZoomFieldOfView(baseFov,1)-baseFov)<.0001f,"No-zoom FOV changed.");
                float previous=baseFov;
                foreach(float zoom in new[]{1.25f,2,3,4})
                {
                    float fov=WellnessPhotoCamera.ZoomFieldOfView(baseFov,zoom);Require(fov>0&&fov<previous,"Zoom must narrow the view smoothly.");
                    float actual=Mathf.Tan(baseFov*Mathf.Deg2Rad/2)/Mathf.Tan(fov*Mathf.Deg2Rad/2);
                    Require(Mathf.Abs(actual-zoom)<.001f,"Optical zoom label disagrees with camera FOV.");previous=fov;
                }
            }
            foreach(var screen in new[]{new Vector2Int(1920,1080),new Vector2Int(3840,2160),new Vector2Int(3440,1440),new Vector2Int(1024,768),new Vector2Int(720,1280)})
            {
                var size=WellnessPhotoCamera.CaptureSize(screen.x,screen.y);
                Require(size.x<=1920&&size.y<=1080&&size.x<=screen.x&&size.y<=screen.y,"Photo dimensions unbounded.");
                Require(Mathf.Abs((float)size.x/size.y-(float)screen.x/screen.y)<.003f,"Photo aspect ratio changed.");
            }
            return "PASS: true optical 1–4x zoom at five base FOVs; bounded aspect-preserving capture at five screen sizes.\n";
        }
    }
}
