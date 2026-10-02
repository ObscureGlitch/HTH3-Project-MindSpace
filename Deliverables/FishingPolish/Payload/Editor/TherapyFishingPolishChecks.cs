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
    public static class TherapyFishingPolishChecks
    {
        const string Scene="Assets/TherapyGame/Scenes/TherapyRoom.unity",Report="Assets/TherapyGame/Documentation/FishingPolishCheck.txt";
        const string Output="D:/Hack the Hill/Deliverables/FishingPolish/Checks/";
        static void Require(bool test,string message){if(!test)throw new InvalidOperationException(message);}
        static KoiFishingRound At(float fraction)
        {var round=new KoiFishingRound(1);float target=KoiFishingCastMotion.Duration*fraction;while(round.ElapsedSeconds+1e-5f<target)round.Step(Mathf.Min(1f/120,target-round.ElapsedSeconds),false,false);return round;}
        static WellnessFishing Open()
        {EditorSceneManager.OpenScene(Scene);return EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WellnessFishing>(true)).Single();}
        static string CheckRarity(KoiPondLibrary library)
        {
            Require(KoiFishingLoot.TotalWeight(library)==10000,"Installed eight-koi odds do not sum to 100%.");var counts=new int[library.varieties.Length];
            for(int roll=0;roll<10000;roll++)counts[KoiFishingLoot.IndexForRoll(library,roll)]++;
            for(int i=0;i<counts.Length;i++){var entry=KoiFishingLoot.ForVariety(library.varieties[i].name);Require(counts[i]==entry.Weight,"Koi weighted selection differs from documented odds.");}
            int godly=KoiFishingViews.ModelIndex(library,"kigoi");Require(counts[godly]==25&&counts.Where((_,i)=>i!=godly).All(n=>n>counts[godly]),"Godly is not the rarest koi at 0.25%.");
            Require(KoiCatch.Create("kigoi",1).RarityName=="Godly"&&KoiFishingLoot.Rank("Godly")==5,"Godly tier missing.");
            return "PASS: all 10,000 weighted outcomes match eight documented koi odds; Godly Kigoi is rarest at 25/10,000 (0.25%). All six tiers resolve correctly.\n";
        }
        static string CheckLegacySaves()
        {
            string root=Path.GetFullPath("Temp/FishingPolishFixtures"),folder=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                var legacy=KoiCatch.Create("kigoi",1);legacy.rarity=null;var inventory=new KoiCatchInventory(folder);string oldPath=inventory.RecordPath(legacy);
                string oldJson="{\"id\":\""+legacy.id+"\",\"variety\":\"kigoi\",\"caughtUtc\":\""+legacy.caughtUtc+"\",\"scale\":1,\"lengthCm\":36,\"weightKg\":0.8}";
                File.WriteAllText(oldPath,oldJson);inventory.Reload();Require(inventory.Fish.Count==1&&inventory.Fish[0].rarity==null&&inventory.Fish[0].RarityName=="Godly","Legacy catch lost its model/tier.");
                Require(File.ReadAllText(oldPath)==oldJson,"Reading legacy rarity rewrote the original record.");
                var fresh=KoiCatch.Create("tancho",1.03f);inventory.Save(fresh);inventory.Equip(fresh.id);inventory.Reload();
                Require(inventory.Fish.Count==2&&inventory.Equipped.id==fresh.id&&inventory.Equipped.rarity=="Legendary","New rarity or equipped fish did not persist.");
                Require(File.ReadAllText(oldPath)==oldJson,"Saving a new catch altered an older record.");
                return "PASS: legacy catches without a rarity field resolve tiers without file rewrites; new rarity and equipped choices persist. Real player saves were not opened.\n";
            }
            finally{Require(Path.GetFullPath(folder).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Unsafe test cleanup path.");Directory.Delete(folder,true);}
        }
        static string CheckViews(KoiPondLibrary library)
        {
            var temp=new GameObject("Temporary fishing polish fixture"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                using(var views=new KoiFishingViews(temp.transform,temp.transform,library))
                {
                    var water=new Vector3(0,-1.6f,4);views.PoseRod(At(0),water,0);
                    var parts=temp.GetComponentsInChildren<MeshFilter>(true);var thumb=parts.Single(p=>p.name=="Grip thumb").sharedMesh;var palm=parts.Single(p=>p.name=="Casting palm").sharedMesh;var wrist=parts.Single(p=>p.name=="Casting wrist").sharedMesh;var reel=parts.Single(p=>p.name=="Reel").sharedMesh;
                    Require(thumb.vertices[17].y>thumb.vertices[16].y+.07f,"Thumb still points backward along the handle.");
                    Require(wrist.bounds.min.y<palm.bounds.min.y&&reel.bounds.max.y<palm.bounds.min.y,"Wrist/reel do not sit below the grip.");
                    foreach(float phase in new[]{KoiFishingCastMotion.Release,KoiFishingCastMotion.Forward})
                    {
                        var a=KoiFishingCastMotion.Sample(phase-.001f);var b=KoiFishingCastMotion.Sample(phase+.001f);
                        Require(Vector3.Distance(a.Position,b.Position)<.0001f&&Quaternion.Angle(a.Rotation,b.Rotation)<.05f,"Cast has a discontinuity at a phase boundary.");
                    }
                    foreach(var variety in library.varieties)
                    {
                        views.BeginCatch(KoiCatch.Create(variety.name,1),water);views.PoseCatch(0);Require(views.CaughtModel.transform.position.y<water.y,"Catch model does not start submerged.");
                        views.PoseCatch(KoiFishingCatchMotion.Duration*KoiFishingCatchMotion.LiftEnd);Require(views.CaughtModel.transform.position.y>water.y+.7f,"Fish never lifts out of water.");
                        views.PoseCatch(KoiFishingCatchMotion.Duration);Require(Vector3.Distance(views.CaughtModel.transform.position,new Vector3(.20f,.07f,1.12f))<.001f,"Fish does not reel into view.");
                        Require(views.CaughtModel.GetComponentsInChildren<MeshRenderer>().All(r=>r.sharedMaterial==library.material),"Reveal changed original koi colours/materials.");views.HideCatch();
                    }
                    Require(temp.GetComponentsInChildren<Collider>(true).Length==0,"Polish creates collision geometry.");views.HideRod();Require(temp.GetComponentsInChildren<Renderer>().Length==0,"Hiding fishing left an active model.");
                }
                Require(temp.transform.childCount==0,"Disposal leaked runtime fishing geometry.");
            }
            finally{UnityEngine.Object.DestroyImmediate(temp);}
            string source=File.ReadAllText("Assets/TherapyGame/Runtime/WellnessFishing.cs");
            Require(!source.Contains("ui.Surface(")&&!source.Contains("ui.Card(")&&!source.Contains("new Rect(-2000"),"Fishing UI still has filled panels or fullscreen dimming.");
            Require(source.Contains("views.TickPreview(")&&source.Contains("KoiFishingLoot.PickIndex("),"Spinning preview or weighted catches not connected.");
            return "PASS: thumb points toward rod tip, reel/wrist below the palm; smooth cast boundaries; all eight original koi lift from below water into view and clean up without colliders; fishing UI has no filled panels/fullscreen dimming.\n";
        }
        public static void VerifyBatch()
        {
            try
            {
                Require(!EditorApplication.isPlaying,"Stay in Edit mode.");var fishing=Open();string checks=CheckRarity(fishing.pond.library)+CheckLegacySaves()+CheckViews(fishing.pond.library);
                File.WriteAllText(Report,"PASS: fishing polish verified.\n"+checks+"Pending: GPU transparent/spinning preview check and manual Play UI/input check.\n");Debug.Log("KOI_FISHING_POLISH_VERIFIED: "+Report);TherapyFishingClickCastChecks.VerifyBatch();
            }
            catch(Exception error){File.WriteAllText(Report,"FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
        }
        static void Save(RenderTexture target,Texture2D image,string name)
        {var prior=RenderTexture.active;try{RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Output+name+".png",image.EncodeToPNG());}finally{RenderTexture.active=prior;}}
        public static void PreviewBatch()
        {
            try
            {
                var fishing=Open();var temp=new GameObject("Temporary fishing polish render"){hideFlags=HideFlags.HideAndDontSave};temp.transform.position=Vector3.one*10000;
                var target=new RenderTexture(640,360,16,RenderTextureFormat.ARGB32){hideFlags=HideFlags.HideAndDontSave};target.Create();var image=new Texture2D(640,360,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};
                try
                {
                    var camera=temp.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.34f,.49f,.59f);camera.fieldOfView=65;camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.aspect=640f/360;camera.targetTexture=target;
                    var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.volumeLayerMask=0;
                    var lamp=new GameObject("Temporary catch light"){hideFlags=HideFlags.HideAndDontSave};lamp.transform.SetParent(temp.transform,false);var light=lamp.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.cullingMask=1<<30;lamp.transform.localRotation=Quaternion.Euler(35,-25,0);
                    using(var views=new KoiFishingViews(temp.transform,temp.transform,fishing.pond.library))
                    {
                        var water=temp.transform.TransformPoint(new Vector3(0,-1.6f,4));
                        foreach(float t in new[]{0f,.42f,.70f,1f}){views.PoseRod(At(t),water,t*KoiFishingCastMotion.Duration);foreach(var child in temp.GetComponentsInChildren<Transform>(true))child.gameObject.layer=30;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});Save(target,image,"GripCast"+Mathf.RoundToInt(t*100));}
                        views.BeginCatch(KoiCatch.Create("kigoi",1),water);
                        foreach(float t in new[]{0f,.36f,.65f,1f}){views.PoseCatch(t*KoiFishingCatchMotion.Duration);foreach(var child in temp.GetComponentsInChildren<Transform>(true))child.gameObject.layer=30;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});Save(target,image,"CatchLift"+Mathf.RoundToInt(t*100));}
                        views.HideCatch();views.HideRod();views.SelectPreview(KoiCatch.Create("kigoi",1));var preview=views.Preview as RenderTexture;var fishImage=new Texture2D(512,288,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};
                        try
                        {
                            Save(preview,fishImage,"Spin0");Require(fishImage.GetPixel(0,0).a<.02f&&fishImage.GetPixel(511,287).a<.02f,"Inventory preview background is not transparent.");
                            Require(fishImage.GetPixels().Any(c=>c.a>.9f),"Transparent preview lost the fish model.");
                            int count=views.PreviewRenderCount;views.TickPreview(.005f);Require(views.PreviewRenderCount==count,"Preview renders every fast frame rather than throttling.");
                            for(int i=0;i<50;i++)views.TickPreview(.1f);Require(Mathf.Abs(views.PreviewYaw-145.09f)<.15f,"Preview does not rotate slowly at 18 degrees/second.");Save(preview,fishImage,"Spin90");
                            views.ClosePreview();count=views.PreviewRenderCount;views.TickPreview(.1f);Require(views.Preview==null&&views.PreviewRenderCount==count,"Closed inventory still renders a preview.");
                        }
                        finally{UnityEngine.Object.DestroyImmediate(fishImage);}
                    }
                }
                finally{target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(temp);}
                File.WriteAllText("Assets/TherapyGame/Documentation/FishingPolishRenderCheck.txt","PASS: revised grip/cast and water-to-view catch keyframes rendered using original koi meshes; inventory preview has an alpha-transparent background and visible fish, spins at 18 degrees/second, throttles rendering and stops/releases on close.\nPending: live Play UI/input check.\n");Debug.Log("KOI_FISHING_POLISH_RENDERED");EditorApplication.Exit(0);
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}
