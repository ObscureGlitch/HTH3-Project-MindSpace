using System;
using System.IO;
using System.Linq;
using TheLastWatch.UI;
using TheLastWatch.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TherapyGame.Editor
{
    public static class TherapyFishingClickCastChecks
    {
        const string Scene="Assets/TherapyGame/Scenes/TherapyRoom.unity",Report="Assets/TherapyGame/Documentation/FishingClickCastCheck.txt";
        const string Output="D:/Hack the Hill/Deliverables/FishingClickCast/Checks/";
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
        static KoiFishingRound At(float fraction)
        {
            var round=new KoiFishingRound(1);float target=KoiFishingCastMotion.Duration*fraction;
            while(round.ElapsedSeconds+1e-5f<target)round.Step(Mathf.Min(1f/120,target-round.ElapsedSeconds),false,false);
            return round;
        }
        static string CheckMotion()
        {
            var rest=KoiFishingCastMotion.Sample(0);var back=KoiFishingCastMotion.Sample(KoiFishingCastMotion.Release);var forward=KoiFishingCastMotion.Sample(KoiFishingCastMotion.Forward);var finish=KoiFishingCastMotion.Sample(1);
            Require(Quaternion.Angle(rest.Rotation,back.Rotation)>60&&Quaternion.Angle(back.Rotation,forward.Rotation)>100,"Cast lacks a distinct wind-up and forward flick.");
            Require(Vector3.Distance(rest.Position,finish.Position)<1e-5f&&Quaternion.Angle(rest.Rotation,finish.Rotation)<.01f,"Follow-through never returns to a stable grip.");
            var water=new Vector3(0,-1.6f,4);var releaseTip=back.Position+back.Rotation*(Vector3.up*1.45f);Vector3 previous=default;
            for(int i=0;i<=600;i++)
            {
                float t=i/600f;var pose=KoiFishingCastMotion.Sample(t);var tip=pose.Position+pose.Rotation*(Vector3.up*1.45f);
                var end=KoiFishingCastMotion.FloatPosition(tip,releaseTip,water,pose);
                Require(Finite(pose.Position)&&Finite(end)&&pose.Flight>=0&&pose.Flight<=1,"Animation produced an invalid pose or trajectory.");
                if(t<=KoiFishingCastMotion.Release)Require(Vector3.Distance(end,tip)<1e-5f,"Float launched before the wind-up completed.");
                if(i>0)Require(Vector3.Distance(previous,end)<.1f,"Float jumps at a casting phase boundary.");
                previous=end;
            }
            Require(Vector3.Distance(previous,water)<1e-5f,"Float does not land at the requested water point.");
            Require(Finite(KoiFishingCastMotion.Sample(float.NaN).Position),"Invalid cast time corrupted the view.");
            foreach(float hz in new[]{30f,60f,120f})
            {
                var round=new KoiFishingRound(1);float elapsed=0;
                while(round.State==KoiFishingRound.Phase.Casting){round.Step(1/hz,false,false);elapsed+=1/hz;Require(elapsed<2,"Casting never finishes.");}
                Require(Mathf.Abs(elapsed-KoiFishingCastMotion.Duration)<1/hz+.01f,"Cast duration changes with frame rate.");
            }
            Require(KoiFishingCastMotion.CanBeginCast(true,true,false,false,false,false),"Fresh gameplay click cannot cast.");
            Require(!KoiFishingCastMotion.CanBeginCast(false,true,false,false,false,false),"Holding the mouse repeats casting.");
            Require(!KoiFishingCastMotion.CanBeginCast(true,false,false,false,false,false),"Unfocused/unready gameplay accepts a cast.");
            Require(!KoiFishingCastMotion.CanBeginCast(true,true,true,false,false,false),"UI input lock accepts a cast.");
            Require(!KoiFishingCastMotion.CanBeginCast(true,true,false,true,false,false),"Released menu cursor accepts a cast.");
            Require(!KoiFishingCastMotion.CanBeginCast(true,true,false,false,true,false),"Seated player accepts a cast.");
            Require(!KoiFishingCastMotion.CanBeginCast(true,true,false,false,false,true),"Photo camera/menu/inventory/active round accepts a cast.");
            string source=File.ReadAllText("Assets/TherapyGame/Runtime/WellnessFishing.cs");
            Require(source.Contains("mouse.leftButton.wasPressedThisFrame")&&!source.Contains("fKey")&&!source.Contains("press F"),"Installed fishing still uses F or lacks the fresh-click binding.");
            return "PASS: distinct wind-up, forward flick and follow-through; attached float before release, continuous arc and exact water landing; 30/60/120 Hz timing; fresh-click, focus, UI-lock, cursor, seating and other-activity guards. F fishing binding removed.\n";
        }
        static string CheckViewCleanup(KoiPondLibrary library)
        {
            int meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length,materials=Resources.FindObjectsOfTypeAll<Material>().Length;
            var temp=new GameObject("Temporary casting geometry check"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                using(var view=new KoiFishingViews(temp.transform,temp.transform,library))
                {
                    foreach(float time in new[]{0f,.42f,.66f,1f})view.PoseRod(At(time),new Vector3(0,-1.6f,4),time*KoiFishingCastMotion.Duration);
                    Require(temp.GetComponentsInChildren<Collider>(true).Length==0,"Animation created colliders.");
                    Require(temp.GetComponentsInChildren<Camera>(true).Length==0,"Animation created a continuous extra camera.");
                    Require(temp.GetComponentsInChildren<Renderer>(true).All(r=>r.shadowCastingMode==ShadowCastingMode.Off),"Animation casts costly or distracting world shadows.");
                    Require(temp.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Casting palm")&&temp.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Casting sleeve (view only)"),"Player grip/sleeve missing.");
                    view.HideRod();Require(temp.GetComponentsInChildren<Renderer>().Length==0,"Cancelling did not hide casting visuals.");
                }
                Require(temp.transform.childCount==0,"Casting left runtime objects behind after disposal.");
                Require(Resources.FindObjectsOfTypeAll<Mesh>().Length==meshes&&Resources.FindObjectsOfTypeAll<Material>().Length==materials,"Casting leaked owned meshes/materials.");
            }
            finally{UnityEngine.Object.DestroyImmediate(temp);}
            return "PASS: hand, sleeve, rod, float, line and ripple are temporary; no colliders, shadows or extra camera; cancel hides visuals, disposal releases owned objects/meshes/materials.\n";
        }
        public static void VerifyBatch()
        {
            try
            {
                Require(!EditorApplication.isPlaying,"Checks must stay in Edit mode.");EditorSceneManager.OpenScene(Scene);
                var fishing=EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WellnessFishing>(true)).Single();
                string checks=CheckMotion()+CheckViewCleanup(fishing.pond.library);
                File.WriteAllText(Report,"PASS: click-to-fish and first-person cast verified.\n"+checks+"Controls: left click cast; Space hook/reel; I koi collection; Esc cancel/close.\nExisting catch saves were not accessed. Camera, microphone and AI sessions were not activated.\nPending: manual Play input and UI check.\n");Debug.Log("KOI_CLICK_CAST_VERIFIED: "+Report);
                TherapyFishingSetup.VerifyBatch();
            }
            catch(Exception error){File.WriteAllText(Report,"FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
        }
        public static void PreviewBatch()
        {
            try
            {
                EditorSceneManager.OpenScene(Scene);var fishing=EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WellnessFishing>(true)).Single();
                var temp=new GameObject("Temporary first-person casting preview"){hideFlags=HideFlags.HideAndDontSave};temp.transform.position=Vector3.one*10000;
                var target=new RenderTexture(640,360,16,RenderTextureFormat.ARGB32){hideFlags=HideFlags.HideAndDontSave};target.Create();var image=new Texture2D(640,360,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};
                try
                {
                    var camera=temp.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.34f,.49f,.59f);camera.fieldOfView=65;camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.aspect=640f/360;camera.targetTexture=target;
                    var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.volumeLayerMask=0;
                    using(var view=new KoiFishingViews(temp.transform,temp.transform,fishing.pond.library))
                    {
                        var times=new[]{0f,.42f,.66f,1f};
                        for(int i=0;i<times.Length;i++)
                        {
                            view.PoseRod(At(times[i]),temp.transform.TransformPoint(new Vector3(0,-1.6f,4)),times[i]*KoiFishingCastMotion.Duration);
                            foreach(var child in temp.GetComponentsInChildren<Transform>(true))child.gameObject.layer=30;
                            RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});var prior=RenderTexture.active;
                            try{RenderTexture.active=target;image.ReadPixels(new Rect(0,0,640,360),0,0);image.Apply();File.WriteAllBytes(Output+"CastFrame"+i+".png",image.EncodeToPNG());}
                            finally{RenderTexture.active=prior;}
                        }
                    }
                }
                finally{target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(temp);}
                Debug.Log("KOI_CLICK_CAST_PREVIEW: four first-person casting keyframes rendered.");EditorApplication.Exit(0);
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}
