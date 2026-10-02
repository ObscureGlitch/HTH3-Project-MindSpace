using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TheLastWatch.Environment;
using TheLastWatch.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace TherapyGame.Editor
{
    public static class TherapyRareFishVfxChecks
    {
        const string Output="D:/Hack the Hill/Deliverables/RareFishVfx/Checks/";
        static readonly string[] Varieties={"asagi","ogon","tancho","kigoi"};
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        public static void RunBatch()
        {
            Directory.CreateDirectory(Output);EditorSceneManager.OpenScene("Assets/TherapyGame/Scenes/TherapyRoom.unity");
            IEnumerator frames=Run();
            void Advance()
            {
                try
                {
                    if(frames.MoveNext()){EditorApplication.QueuePlayerLoopUpdate();return;}
                    EditorApplication.update-=Advance;File.WriteAllText(Output+"Result.txt","PASS: rare fish effects, regression checks and GPU captures completed.");EditorApplication.Exit(0);
                }
                catch(Exception error)
                {
                    EditorApplication.update-=Advance;(frames as IDisposable)?.Dispose();
                    File.WriteAllText(Output+"Result.txt","FAILED: "+error);Debug.LogException(error);EditorApplication.Exit(1);
                }
            }
            EditorApplication.update+=Advance;
        }
        static IEnumerator Run()
        {
            var fishing=Object.FindAnyObjectByType<WellnessFishing>();Require(fishing!=null&&fishing.pond!=null,"Installed fishing system missing.");
            Shader shader=Resources.Load<Shader>("KoiRarityAura");Require(shader!=null&&!ShaderUtil.ShaderHasError(shader),"VFX shader import failed.");
            var report=new List<string>{"PASS: rare fish VFX checks."};
            foreach(string method in new[]{"CheckRarity","CheckLegacySaves","CheckViews"})
            {
                var check=typeof(TherapyFishingPolishChecks).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static);
                report.Add((string)check.Invoke(null,method=="CheckLegacySaves"?null:new object[]{fishing.pond.library}));
            }
            var fixture=new GameObject("Rare-fish VFX checks"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                Require(KoiRarityVfx.Create(fixture.transform,KoiCatch.Create("kohaku",1),1,KoiRarityVfx.Presentation.Catch)==null,"Common fish acquired rare effects.");
                Require(KoiRarityVfx.Create(fixture.transform,KoiCatch.Create("showa",1),1,KoiRarityVfx.Presentation.Catch)==null,"Uncommon fish acquired rare effects.");
                int lastCount=0;
                foreach(string variety in Varieties)
                {
                    var fish=KoiCatch.Create(variety,1);fish.rarity=null; // legacy record path
                    using(var effect=KoiRarityVfx.Create(fixture.transform,fish,1,KoiRarityVfx.Presentation.Catch))
                    {
                        Require(effect!=null&&effect.Rank==KoiFishingLoot.ForVariety(variety).Rank,"Rarity mapping, including legacy catches.");
                        foreach(float time in new[]{0,.3f,.8f,1.8f,2.15f,2.7f,4.5f,30,600})
                        {
                            effect.Pose(Vector3.zero,Quaternion.identity,time);
                            var mesh=effect.Root.GetComponent<MeshFilter>().sharedMesh;
                            Require(effect.VertexCount>0&&effect.VertexCount<=KoiRarityVfx.MaxQuads*4,"Geometry budget exceeded.");
                            Require(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&mesh.bounds.Contains(v)),"Nonfinite or uncullable VFX geometry.");
                            Require(mesh.colors.All(c=>float.IsFinite(c.a)&&c.a>=0&&c.a<=1),"Invalid effect opacity.");
                        }
                        Require(effect.VertexCount>lastCount,"Rarer fish should gain richer effects.");lastCount=effect.VertexCount;
                        int before=effect.BuildCount;effect.Pose(Vector3.one,Quaternion.identity,600.005f);Require(effect.BuildCount==before,"VFX exceeded 30 Hz update cap.");
                        effect.SetVisible(false);effect.Pose(Vector3.zero,Quaternion.identity,601);Require(effect.BuildCount==before,"Hidden effects still update.");effect.SetVisible(true);
                        Require(effect.Root.GetComponentsInChildren<Renderer>().Length==1&&effect.Root.GetComponentsInChildren<Light>().Length==0&&
                            effect.Root.GetComponentsInChildren<Camera>().Length==0&&effect.Root.GetComponentsInChildren<Collider>().Length==0,"One draw and no extra light/camera/physics budget.");
                        report.Add(fish.RarityName+" / "+KoiRarityVfx.Signature(fish.RarityName)+": "+effect.VertexCount+" vertices; one mesh renderer; updates at most 30 Hz.");
                    }
                    Require(fixture.transform.childCount==0,"Effect disposal leaked a GameObject.");
                }
                using(var effect=KoiRarityVfx.Create(fixture.transform,KoiCatch.Create("kigoi",1),1,KoiRarityVfx.Presentation.Catch))
                {
                    for(int i=0;i<120;i++)effect.Pose(Vector3.zero,Quaternion.identity,i/30f);
                    long allocations=GC.GetAllocatedBytesForCurrentThread();var watch=System.Diagnostics.Stopwatch.StartNew();
                    for(int i=0;i<3000;i++)effect.Pose(Vector3.zero,Quaternion.identity,10+i/30f);
                    watch.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-allocations;
                    report.Add("Godly analytic VFX CPU microbenchmark: "+(watch.Elapsed.TotalMilliseconds/3000).ToString("0.0000")+" ms/update, "+bytes+" managed bytes over 3,000 updates (includes stopwatch); not gameplay FPS or GPU timing.");
                    Require(bytes<2048,"Steady-state effects allocate managed memory each update.");
                }
                using(var views=new KoiFishingViews(fixture.transform,fixture.transform,fishing.pond.library))
                {
                    for(int i=0;i<12;i++)
                    {
                        views.Equip(KoiCatch.Create(Varieties[i%4],1));views.TickHeld(.1f);
                        Require(fixture.GetComponentsInChildren<Renderer>().Count(r=>r.sharedMaterial.shader==shader)==1,"Swapping equipped fish stacks VFX.");
                        views.ShowHeld(false);Require(fixture.GetComponentsInChildren<Renderer>().Length==0,"Hidden held fish leaves floating VFX.");views.ShowHeld(true);
                        views.Equip(null);Require(fixture.transform.childCount==0,"Putting fish away leaks effects.");
                    }
                }
                report.Add("PASS: common/uncommon remain plain; all four rare tiers and legacy records resolve; catch/equip/hide/swap/disposal restore cleanly; models and rarity odds unchanged.");
            }
            finally{Object.DestroyImmediate(fixture);}
            File.WriteAllLines(Output+"Verification.txt",report);
            yield return null;

            // Use the same transparent, throttled inventory render as gameplay.
            var previewFixture=new GameObject("Rare-fish collection preview fixture"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                using(var views=new KoiFishingViews(previewFixture.transform,previewFixture.transform,fishing.pond.library))
                {
                    foreach(string variety in Varieties)
                    {
                        var fish=KoiCatch.Create(variety,1);views.SelectPreview(fish);
                        for(int frame=0;frame<36;frame++){views.TickPreview(.1f);yield return null;}
                        var target=views.Preview as RenderTexture;Require(target!=null,"Selected fish preview missing.");
                        Save(target,"Collection-"+fish.RarityName,true);
                        int before=views.PreviewRenderCount;
                        for(int sample=0;sample<10;sample++)views.TickPreview(.001f);
                        Require(views.PreviewRenderCount-before<=1,"Preview render cap lost.");
                        views.ClosePreview();Require(views.Preview==null&&previewFixture.transform.childCount==0,"Closing collection leaks effects or camera.");
                    }
                }
            }
            finally{Object.DestroyImmediate(previewFixture);}

            var sceneFixture=new GameObject("Rare-fish world preview fixture"){hideFlags=HideFlags.HideAndDontSave};
            var targetTexture=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);targetTexture.Create();
            try
            {
                var eyeObject=new GameObject("Temporary catch camera"){hideFlags=HideFlags.HideAndDontSave};eyeObject.transform.SetParent(sceneFixture.transform,false);
                var camera=eyeObject.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.Skybox;camera.nearClipPlane=.02f;camera.farClipPlane=180;camera.fieldOfView=62;camera.aspect=1280f/720;camera.targetTexture=targetTexture;
                var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.volumeLayerMask=0;
                Vector3 water=WellnessFishing.WaterCenter(fishing.pond);
                camera.transform.position=water+new Vector3(fishing.pond.pondRadii.x+1.3f,1.7f,-1.0f);camera.transform.LookAt(water+Vector3.up*.2f);
                using(var views=new KoiFishingViews(sceneFixture.transform,camera.transform,fishing.pond.library))
                {
                    foreach(string variety in Varieties)
                    {
                        var fish=KoiCatch.Create(variety,1);views.BeginCatch(fish,water);
                        foreach(float moment in new[]{2.25f,5.2f})
                        {
                            views.PoseCatch(moment);
                            for(int frame=0;frame<4;frame++){yield return null;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=targetTexture});}
                            Save(targetTexture,fish.RarityName+(moment<3?"-Reveal":"-Signature"),false);
                        }
                        views.HideCatch();views.HideRod();
                    }
                }
                Require(sceneFixture.GetComponentsInChildren<MeshRenderer>().Length==0,"Catch cleanup leaked effects.");
            }
            finally{targetTexture.Release();Object.DestroyImmediate(targetTexture);Object.DestroyImmediate(sceneFixture);}
            Require(!ShaderUtil.ShaderHasError(shader),"GPU effect shader compilation failed.");
            File.AppendAllText(Output+"Verification.txt","\nPASS: all four collection and world catch tiers rendered on GPU; transparent preview corners; bounded preview cadence; no remaining catch/equip/preview effects after cleanup.\n");
        }
        static void Save(RenderTexture target,string name,bool alpha)
        {
            var prior=RenderTexture.active;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
                if(alpha)
                {
                    Require(image.GetPixel(0,0).a<.02f&&image.GetPixel(target.width-1,target.height-1).a<.02f,"Collection background lost transparency.");
                    Require(image.GetPixels().Any(c=>c.a>.9f),"Collection lost visible fish.");
                    for(int x=0;x<target.width;x++)Require(image.GetPixel(x,0).a<.02f&&image.GetPixel(x,target.height-1).a<.02f,"VFX crown or orbit clipped by preview frame.");
                }
                File.WriteAllBytes(Output+name+".png",image.EncodeToPNG());
            }
            finally{RenderTexture.active=prior;Object.DestroyImmediate(image);}
        }
    }
}
