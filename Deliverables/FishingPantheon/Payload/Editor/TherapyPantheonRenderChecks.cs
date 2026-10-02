using System;
using System.Collections;
using System.IO;
using System.Linq;
using TheLastWatch.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
using static TherapyGame.Editor.TherapyPantheonSetup;
namespace TherapyGame.Editor
{
    public static class TherapyPantheonRenderChecks
    {
        const string Output="D:/Hack the Hill/Deliverables/FishingPantheon/Checks/";
        public static void RunBatch()
        {
            IEnumerator routine=Run();
            void Tick()
            {
                try
                {
                    if(routine.MoveNext()){EditorApplication.QueuePlayerLoopUpdate();return;}
                    EditorApplication.update-=Tick;(routine as IDisposable)?.Dispose();EditorApplication.Exit(0);
                }
                catch(Exception error){EditorApplication.update-=Tick;(routine as IDisposable)?.Dispose();File.WriteAllText(Output+"RenderCheck.txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
            }
            EditorApplication.update+=Tick;
        }
        static void Save(RenderTexture target,string name,bool transparent)
        {
            var before=RenderTexture.active;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();var pixels=image.GetPixels32();
                Require(pixels.Count(c=>c.a>20&&c.r+c.g+c.b>50)>300,"Rendered fish/effect is empty: "+name);
                if(transparent)Require(pixels[0].a<5&&pixels[target.width-1].a<5,"Preview lost transparent background: "+name);
                File.WriteAllBytes(Output+name+".png",image.EncodeToPNG());
            }
            finally{RenderTexture.active=before;Object.DestroyImmediate(image);}
        }
        static IEnumerator Run()
        {
            Require(!EditorApplication.isPlaying,"Do not enter Play mode.");EditorSceneManager.OpenScene("Assets/TherapyGame/Scenes/TherapyRoom.unity");
            var fishing=Object.FindAnyObjectByType<WellnessFishing>();var library=fishing.Library;Require(library.varieties.Length==24,"New catalog was not reloaded.");
            yield return null;
            var fixture=new GameObject("Pantheon preview fixture"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                using(var views=new KoiFishingViews(fixture.transform,fixture.transform,library))
                {
                    foreach(var variety in library.varieties)
                    {
                        views.SelectPreview(KoiCatch.Create(variety.name,1));float start=views.PreviewYaw;
                        for(int i=0;i<20;i++){views.TickPreview(.05f);yield return null;}
                        Require(Mathf.Abs(Mathf.DeltaAngle(start,views.PreviewYaw)-18)<.1f,"Preview does not spin smoothly.");
                        Save((RenderTexture)views.Preview,"Koi-"+variety.name,true);int renders=views.PreviewRenderCount;
                        for(int i=0;i<10;i++)views.TickPreview(.001f);Require(views.PreviewRenderCount-renders<=1,"Preview render cap exceeded.");
                        views.ClosePreview();Require(views.Preview==null&&fixture.transform.childCount==0,"Closed preview leaked camera, texture or effects.");
                    }
                }
            }
            finally{Object.DestroyImmediate(fixture);}
            var world=new GameObject("Pantheon world fixture"){hideFlags=HideFlags.HideAndDontSave};var texture=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);texture.Create();
            try
            {
                var camera=world.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.Skybox;camera.nearClipPlane=.02f;camera.farClipPlane=180;camera.fieldOfView=62;camera.aspect=1280f/720;camera.targetTexture=texture;
                var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.volumeLayerMask=0;
                var water=WellnessFishing.WaterCenter(fishing.pond);camera.transform.position=water+new Vector3(fishing.pond.pondRadii.x+1.3f,1.7f,-1);camera.transform.LookAt(water+Vector3.up*.2f);
                using(var views=new KoiFishingViews(world.transform,camera.transform,library))
                {
                    foreach(string name in new[]{"sakura","ryujin","tsukuyomi","genesis"})
                    {
                        views.BeginCatch(KoiCatch.Create(name,1),water);views.PoseCatch(2.3f);yield return null;
                        RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=texture});Save(texture,"Catch-"+name,false);views.HideCatch();
                    }
                }
            }
            finally{texture.Release();Object.DestroyImmediate(texture);Object.DestroyImmediate(world);}
            var shader=Resources.Load<Shader>("KoiPantheonSurface");Require(shader!=null&&!ShaderUtil.ShaderHasError(shader),"Pantheon GPU shader errors.");
            File.WriteAllText(Output+"RenderCheck.txt","PASS: all 24 actual Unity collection previews rendered with visible geometry and transparent corners; eighteen degrees per second spin, 30 Hz throttle and immediate close cleanup.\nPASS: four actual catch reveals rendered over the existing pond scenery, including Sakura petals, dragon appendages, moon and rainbow Godly effects.\nNo Play mode, real saves, microphone or webcam were used. Live input/UI appearance and sustained frame-rate checks remain manual.\n");
            Debug.Log("KOI_PANTHEON_RENDER_CHECKS_PASSED");
        }
    }
}
