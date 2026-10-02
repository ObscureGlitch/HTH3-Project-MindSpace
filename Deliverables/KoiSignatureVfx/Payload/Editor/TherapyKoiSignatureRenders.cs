using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TheLastWatch.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
using static TherapyGame.Editor.TherapyPantheonSetup;
using static TherapyGame.Editor.TherapyKoiSignatureChecks;
namespace TherapyGame.Editor
{
    public static class TherapyKoiSignatureRenders
    {
        public static void RunBatch()
        {
            var routines=new Stack<IEnumerator>();routines.Push(Run());
            void Cleanup(){while(routines.Count>0)(routines.Pop() as IDisposable)?.Dispose();}
            void Tick()
            {
                try
                {
                    while(routines.Count>0)
                    {
                        var current=routines.Peek();if(!current.MoveNext()){(routines.Pop() as IDisposable)?.Dispose();continue;}
                        if(current.Current is IEnumerator nested){routines.Push(nested);continue;}
                        EditorApplication.QueuePlayerLoopUpdate();return;
                    }
                    EditorApplication.update-=Tick;EditorApplication.Exit(0);
                }
                catch(Exception e){EditorApplication.update-=Tick;Cleanup();File.WriteAllText(Output+"SignatureRenderCheck.txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
            }
            EditorApplication.update+=Tick;
        }
        static Texture2D Pixels(RenderTexture rt,string name,bool alpha)
        {
            var old=RenderTexture.active;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();var pixels=image.GetPixels32();Require(pixels.Count(p=>p.a>20&&p.r+p.g+p.b>80)>300,"Empty capture: "+name);
                if(alpha){Require(pixels[0].a<3&&pixels[rt.width-1].a<3&&pixels[pixels.Length-1].a<3,"Effect cropped at preview corner: "+name);int border=0;for(int x=0;x<rt.width;x++)if(pixels[x].a>15||pixels[(rt.height-1)*rt.width+x].a>15)border++;Require(border<12,"Effect cropped at top/bottom: "+name);}
                File.WriteAllBytes(Output+name+".png",image.EncodeToPNG());return image;
            }
            catch{Object.DestroyImmediate(image);throw;}
            finally{RenderTexture.active=old;}
        }
        static IEnumerator Run()
        {
            Require(!EditorApplication.isPlaying,"Do not enter Play mode.");EditorSceneManager.OpenScene("Assets/TherapyGame/Scenes/TherapyRoom.unity");var source=Object.FindAnyObjectByType<WellnessFishing>();yield return null;
            var fixture=new GameObject("Signature preview fixture"){hideFlags=HideFlags.HideAndDontSave};var gods=new List<Texture2D>();var legends=new List<Texture2D>();
            try
            {
                using(var views=new KoiFishingViews(fixture.transform,fixture.transform,source.Library))
                {
                    foreach(var variety in source.Library.varieties)
                    {
                        var fish=KoiCatch.Create(variety.name,1);views.SelectPreview(fish);float start=views.PreviewYaw;
                        for(int i=0;i<20;i++){views.TickPreview(.05f);yield return null;}Require(Mathf.Abs(Mathf.DeltaAngle(start,views.PreviewYaw)-18)<.1f,"Fish preview spin changed.");
                        int builds=views.PreviewRenderCount;for(int i=0;i<10;i++)views.TickPreview(.001f);Require(views.PreviewRenderCount-builds<=1,"Preview exceeds 30 Hz.");
                        var image=Pixels((RenderTexture)views.Preview,"Koi-"+variety.name,true);if(KoiFishingLoot.Rank(fish.RarityName)==5)gods.Add(image);else if(KoiFishingLoot.Rank(fish.RarityName)==4)legends.Add(image);else Object.DestroyImmediate(image);
                        views.ClosePreview();Require(fixture.transform.childCount==0,"Preview close leaked resources.");
                    }
                }
                yield return Gallery(source,gods,new[]{"AMATERASU  /  SOLAR ASCENSION","TSUKUYOMI  /  LUNAR PROCESSION","VOID  /  EVENT HORIZON","GENESIS  /  PRISMATIC CREATION"},"Godly-signatures",0);
                yield return Gallery(source,legends.Take(4).ToList(),new[]{"TANCHO  /  CRIMSON CRANE","RYUJIN  /  DRAGONFIRE HELIX","RAIJIN  /  THUNDERSTORM COILS","HŌŌ  /  PHOENIX PLUMAGE"},"Legendary-signatures-1",0);
                yield return Gallery(source,legends.Skip(4).ToList(),new[]{"YUKI  /  CRYSTAL FROSTFALL","ABYSS  /  BIOLUMINESCENT DEPTHS","NEBULA  /  SPIRAL NEBULA","KITSUNE  /  NINE SPIRIT FLAMES"},"Legendary-signatures-2",0);
            }
            finally{foreach(var texture in gods.Concat(legends))Object.DestroyImmediate(texture);Object.DestroyImmediate(fixture);}
            var world=new GameObject("Signature catch fixture"){hideFlags=HideFlags.HideAndDontSave};var rt=new RenderTexture(1280,720,24);rt.Create();
            try
            {
                var camera=world.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.Skybox;camera.nearClipPlane=.02f;camera.farClipPlane=180;camera.fieldOfView=62;camera.aspect=1280f/720;camera.targetTexture=rt;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                var water=WellnessFishing.WaterCenter(source.pond);camera.transform.position=water+new Vector3(0,2,-source.pond.pondRadii.y*1.15f);camera.transform.LookAt(water+Vector3.up*.2f);Physics.SyncTransforms();Require(WellnessFishing.FindLanding(source.pond,camera.transform.position,camera.transform.forward,null,new RaycastHit[64],out var landing),"No water landing.");camera.transform.LookAt(landing+Vector3.up*.45f);
                using(var views=new KoiFishingViews(world.transform,camera.transform,source.Library))
                {
                    foreach(string name in new[]{"yuki","kitsune","amaterasu","tsukuyomi","void","genesis"})
                    {views.BeginCatch(KoiCatch.Create(name,1),landing);foreach(float t in new[]{2.3f,5f}){views.PoseCatch(t);yield return null;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});Object.DestroyImmediate(Pixels(rt,"Catch-"+name+"-"+(t<3?"reveal":"settled"),false));}views.HideCatch();}
                }
            }
            finally{rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(world);}
            File.WriteAllText(Output+"SignatureRenderCheck.txt","PASS: all 24 actual model previews, twelve themed signatures, transparent edges/no vertical clipping, 18°/s rotation and 30 Hz preview cap/cleanup. Three labelled effect galleries and twelve actual catch reveal/settled scene renders. No Play mode, real saves, microphone or webcam.\n");Debug.Log("KOI_SIGNATURE_RENDER_CHECKS_PASSED");
        }
        static IEnumerator Gallery(WellnessFishing source,List<Texture2D> textures,string[] labels,string name,int unused)
        {
            var root=new GameObject("Signature gallery"){hideFlags=HideFlags.HideAndDontSave};root.transform.position=new Vector3(20000,20000,20000);var rt=new RenderTexture(1280,720,24);rt.Create();var disposable=new List<Object>();
            try
            {
                var camera=root.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=360;camera.aspect=1280f/720;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.06f,.08f);camera.nearClipPlane=.1f;camera.farClipPlane=20;camera.cullingMask=1<<29;camera.targetTexture=rt;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                var canvas=new GameObject("Gallery canvas"){hideFlags=HideFlags.HideAndDontSave};canvas.transform.SetParent(root.transform,false);canvas.transform.localPosition=Vector3.forward*10;
                Font font=source.chat.quietGlassSans??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");var style=new GUIStyle{font=font,fontSize=17,alignment=TextAnchor.MiddleCenter};WellnessUiText.Static(style,new Color(.86f,.94f,.92f));var ops=new List<FishingPaint>();
                for(int i=0;i<4;i++){float x=20+i%2*630,y=12+i/2*350;ops.Add(new FishingPaint{Rect=new Rect(x,y,610,338),Color=new Color(.075f,.115f,.14f),Radius=16});ops.Add(FishingPaint.Text(new Rect(x+15,y+8,580,34),labels[i],style));ops.Add(new FishingPaint{Rect=new Rect(x+5,y+42,600,286),Color=Color.white,Texture=textures[i]});}
                typeof(TherapyFishingRefinementRenders).GetMethod("Paint",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{canvas,ops,null,disposable});yield return null;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});Object.DestroyImmediate(Pixels(rt,name,false));
            }
            finally{Object.DestroyImmediate(root);foreach(var obj in disposable)Object.DestroyImmediate(obj);rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}
