using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheLastWatch.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
using static TherapyGame.Editor.TherapyFishingRefinementChecks;
namespace TherapyGame.Editor
{
    public static class TherapyFishingRefinementRenders
    {
        public static void RunBatch()
        {
            var routine=Run();
            void Tick(){try{if(routine.MoveNext()){EditorApplication.QueuePlayerLoopUpdate();return;}EditorApplication.update-=Tick;(routine as IDisposable)?.Dispose();EditorApplication.Exit(0);}catch(Exception e){EditorApplication.update-=Tick;(routine as IDisposable)?.Dispose();File.WriteAllText(Output+"RefinementRenderCheck.txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}}
            EditorApplication.update+=Tick;
        }
        static void Save(RenderTexture rt,string name)
        {
            var old=RenderTexture.active;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();Require(image.GetPixels32().Count(p=>p.r+p.g+p.b>110)>1000,"Empty image: "+name);File.WriteAllBytes(Output+name+".png",image.EncodeToPNG());}
            finally{RenderTexture.active=old;Object.DestroyImmediate(image);}
        }
        static Mesh Quad(Rect r,Vector2[] uv=null)
        {
            float x=r.x-640,y=360-r.y;var mesh=new Mesh{hideFlags=HideFlags.HideAndDontSave};mesh.vertices=new[]{new Vector3(x,y-r.height,0),new Vector3(x+r.width,y-r.height,0),new Vector3(x+r.width,y,0),new Vector3(x,y,0)};
            mesh.uv=uv??new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,1,2,0,2,3};return mesh;
        }
        static void AddMesh(GameObject root,Mesh mesh,Material material,List<Object> disposable)
        {var obj=new GameObject("HUD preview draw"){hideFlags=HideFlags.HideAndDontSave,layer=29};obj.transform.SetParent(root.transform,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;disposable.Add(mesh);disposable.Add(material);}
        static List<string> Lines(FishingPaint p)
        {
            var lines=new List<string>();foreach(var line in p.Content.Split('\n'))
            {
                if(!p.Style.wordWrap){lines.Add(line);continue;}string current="";
                foreach(string word in line.Split(' ')){string candidate=current.Length==0?word:current+" "+word;if(current.Length>0&&p.Style.CalcSize(new GUIContent(candidate)).x>p.Rect.width){lines.Add(current);current=word;}else current=candidate;}lines.Add(current);
            }
            return lines;
        }
        static void DrawText(GameObject root,FishingPaint p,Shader shader,int order,List<Object> disposable)
        {
            var lines=Lines(p);var verts=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();int size=p.Style.fontSize;var font=p.Style.font;
            float totalHeight=lines.Count*size*1.15f,top=360-p.Rect.y-(p.Rect.height-totalHeight)*.5f;
            foreach(string line in lines)
            {
                float width=p.Style.CalcSize(new GUIContent(line)).x,x=p.Rect.x-640;
                if(p.Style.alignment==TextAnchor.MiddleCenter)x+=(p.Rect.width-width)*.5f;
                float baseline=top-size*.87f;
                foreach(char ch in line)
                {
                    if(!font.GetCharacterInfo(ch,out var c,size,p.Style.fontStyle))continue;int start=verts.Count;
                    verts.Add(new Vector3(x+c.minX,baseline+c.minY,0));verts.Add(new Vector3(x+c.maxX,baseline+c.minY,0));verts.Add(new Vector3(x+c.maxX,baseline+c.maxY,0));verts.Add(new Vector3(x+c.minX,baseline+c.maxY,0));
                    uv.Add(c.uvBottomLeft);uv.Add(c.uvBottomRight);uv.Add(c.uvTopRight);uv.Add(c.uvTopLeft);tris.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});x+=c.advance;
                }
                top-=size*1.15f;
            }
            var mesh=new Mesh{hideFlags=HideFlags.HideAndDontSave};mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);
            var material=new Material(shader){hideFlags=HideFlags.HideAndDontSave,renderQueue=3000+order};material.SetFloat("_Mode",2);material.SetColor("_Color",p.Color);material.mainTexture=font.material.mainTexture;AddMesh(root,mesh,material,disposable);
        }
        static void Paint(GameObject root,List<FishingPaint> ops,Texture background,List<Object> disposable)
        {
            var shader=Shader.Find("Hidden/Therapy/Fishing Refinement Preview");Require(shader!=null&&!ShaderUtil.ShaderHasError(shader),"UI preview shader failed.");
            if(background!=null)ops.Insert(0,new FishingPaint{Rect=new Rect(0,0,1280,720),Color=Color.white,Texture=background});
            foreach(var p in ops)if(p.Content!=null)p.Style.font.RequestCharactersInTexture(p.Content,p.Style.fontSize,p.Style.fontStyle);
            int order=0;foreach(var p in ops)
            {
                if(p.Content!=null){DrawText(root,p,shader,order++,disposable);continue;}
                var material=new Material(shader){hideFlags=HideFlags.HideAndDontSave,renderQueue=3000+order++};material.SetColor("_Color",p.Color);material.SetVector("_Size",new Vector4(p.Rect.width,p.Rect.height,0,0));material.SetFloat("_Radius",p.Radius);material.SetFloat("_Stroke",p.Stroke);
                Rect rect=p.Rect;if(p.Texture!=null){material.SetFloat("_Mode",1);material.mainTexture=p.Texture;float factor=Mathf.Min(rect.width/p.Texture.width,rect.height/p.Texture.height);float w=p.Texture.width*factor,h=p.Texture.height*factor;rect=new Rect(rect.center.x-w*.5f,rect.center.y-h*.5f,w,h);}
                AddMesh(root,Quad(rect),material,disposable);
            }
        }
        static IEnumerator Run()
        {
            Require(!EditorApplication.isPlaying,"Do not enter Play mode.");EditorSceneManager.OpenScene("Assets/TherapyGame/Scenes/TherapyRoom.unity");var source=Object.FindAnyObjectByType<WellnessFishing>();yield return null;
            var world=new GameObject("Fishing render camera"){hideFlags=HideFlags.HideAndDontSave};var background=new RenderTexture(1280,720,24);background.Create();var uiTexture=new RenderTexture(1280,720,24);uiTexture.Create();
            var fixture=new GameObject("Fishing UI fixture"){hideFlags=HideFlags.HideAndDontSave};fixture.transform.position=new Vector3(10000,10000,10000);
            var uiRoot=new GameObject("Fishing painted HUD"){hideFlags=HideFlags.HideAndDontSave};uiRoot.transform.position=new Vector3(20000,20000,20000);var resources=new List<Object>();
            try
            {
                var camera=world.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.Skybox;camera.nearClipPlane=.02f;camera.farClipPlane=180;camera.fieldOfView=62;camera.aspect=1280f/720;camera.targetTexture=background;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                var water=WellnessFishing.WaterCenter(source.pond);camera.transform.position=water+new Vector3(0,2,-source.pond.pondRadii.y*1.15f);camera.transform.LookAt(water+Vector3.up*.2f);
                Physics.SyncTransforms();Require(WellnessFishing.FindLanding(source.pond,camera.transform.position,camera.transform.forward,null,new RaycastHit[64],out var landing),"Render camera has no valid open-water landing.");camera.transform.LookAt(landing+Vector3.up*.45f);
                var capture=uiRoot.AddComponent<Camera>();capture.enabled=false;capture.orthographic=true;capture.orthographicSize=360;capture.aspect=1280f/720;capture.clearFlags=CameraClearFlags.SolidColor;capture.backgroundColor=new Color(.05f,.09f,.12f);capture.nearClipPlane=.1f;capture.farClipPlane=20;capture.cullingMask=1<<29;capture.targetTexture=uiTexture;capture.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                // Camera sits behind the mesh canvas. Keep the source scene untouched.
                var canvas=new GameObject("HUD canvas"){hideFlags=HideFlags.HideAndDontSave};canvas.transform.SetParent(uiRoot.transform,false);canvas.transform.localPosition=Vector3.forward*10;
                var f=fixture.AddComponent<WellnessFishing>();f.catchLibrary=source.Library;f.chat=source.chat;Prepare(f,SnapshotInventory());
                using(var views=new KoiFishingViews(fixture.transform,fixture.transform,source.Library))
                using(var caught=new KoiFishingViews(world.transform,camera.transform,source.Library))
                {
                    Set(f,"views",views);caught.BeginCatch(KoiCatch.Create("genesis",1),landing);
                    foreach(float t in new[]{.08f,.28f,.65f,1.1f,2.3f}){caught.PoseCatch(t);yield return null;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=background});Save(background,"Catch-motion-"+t.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));}
                    caught.HideCatch();yield return null;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=background});
                    for(int scene=0;scene<6;scene++)
                    {
                        Call(f,"ResetCollectionView");Set(f,"selected",0);string method="DrawInventory",name="Collection-all";
                        if(scene==1){Set(f,"page",1);Set(f,"selected",6);name="Collection-page-2";}
                        if(scene==2){Set(f,"filterMenuOpen",true);name="Collection-filters";}
                        if(scene==3){Set(f,"journal",true);Set(f,"page",1);Set(f,"journalSelected",23);name="Species-journal";}
                        if(scene==4){var round=new KoiFishingRound(1);while(round.State!=KoiFishingRound.Phase.Bite)round.Step(.02f,false,false);round.Step(.02f,true,true);Set(f,"round",round);Set(f,"hookedEntry",KoiFishingLoot.ForVariety("genesis"));method="DrawRound";name="Minimal-reeling-HUD";}
                        if(scene==5){Set(f,"lastCatch",KoiCatch.Create("genesis",1));Set(f,"catchLiftTime",2.3f);Set(f,"newDiscovery",true);method="DrawCatch";name="Catch-result";caught.BeginCatch(KoiCatch.Create("genesis",1),landing);caught.PoseCatch(2.3f);yield return null;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=background});}
                        views.SelectPreview(scene==3?KoiCatch.Create("genesis",1):Get<KoiCatchInventory>(f,"inventory").Fish[Get<int>(f,"selected")]);for(int i=0;i<3;i++){views.TickPreview(.05f);yield return null;}
                        Paint(canvas,Capture(f,method),background,resources);yield return null;RenderPipeline.SubmitRenderRequest(capture,new RenderPipeline.StandardRequest{destination=uiTexture});Save(uiTexture,name);
                        foreach(Transform child in canvas.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);foreach(var obj in resources)Object.DestroyImmediate(obj);resources.Clear();
                    }
                    Set(f,"views",null);Set(f,"round",null);
                }
                File.WriteAllText(Output+"RefinementRenderCheck.txt","PASS: five actual Unity catch-animation keyframes and six previews using the runtime HUD draw list, actual font glyphs and spinning koi preview textures. Offscreen Edit-mode rendering, no Play mode or camera/microphone activation. Live mouse/controller feel and audio remain manual checks.\n");Debug.Log("KOI_REFINEMENT_RENDER_CHECKS_PASSED");
            }
            finally{foreach(var obj in resources)Object.DestroyImmediate(obj);Object.DestroyImmediate(uiRoot);Object.DestroyImmediate(fixture);Object.DestroyImmediate(world);background.Release();uiTexture.Release();Object.DestroyImmediate(background);Object.DestroyImmediate(uiTexture);}
        }
    }
}
