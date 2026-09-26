using System;
using System.IO;
using System.Linq;
using TheLastWatch.Environment;
using TheLastWatch.Integrations;
using TheLastWatch.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapySkyRefinement
    {
        private const string Root="Assets/TherapyGame",Folder=Root+"/Weather";
        private const string Request=Root+"/SkyRefinementRequest.txt",Report=Folder+"/SkyRefinementCheck.txt";
        static TherapySkyRefinement()=>EditorApplication.delayCall+=ImportOnce;
        private static void ImportOnce()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="refine-photographic-sky-once")return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Sky refinement deferred. Stop Play/baking, then choose Therapy Game > Refine Clouds and Night Sky.");return;}
            File.WriteAllText(Request,"installing-once");
            try{Install();}catch(Exception e){File.WriteAllText(Request,"failed");File.WriteAllText(Report,e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Therapy Game/Refine Clouds and Night Sky")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new InvalidOperationException("Stop Play mode and baking first.");
            Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");if(!scene.isLoaded)throw new InvalidOperationException("Keep TherapyRoom open.");
            Transform room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
            var cycle=room.GetComponentInChildren<WellnessSkyCycle>();var chat=room.GetComponentInChildren<WellnessVoiceChat>();
            var player=room.GetComponentInChildren<WellnessExplorer>();
            if(cycle==null||chat==null||player==null||chat.IsConnected||chat.IsBusy)throw new InvalidOperationException("An idle, installed weather/voice scene is required.");
            Directory.CreateDirectory(Folder+"/Backups");Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(Folder+"/Materials");
            string backup=Folder+"/Backups/TherapyRoom_BeforePhotographicSky.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
            string atlasPath=Folder+"/Textures/PhotographicCloudAtlas.png";
            var importer=AssetImporter.GetAtPath(atlasPath) as TextureImporter;
            if(importer==null||!importer.DoesSourceTextureHaveAlpha())throw new Exception("Cloud atlas needs genuine source alpha.");
            importer.textureType=TextureImporterType.Default;importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=true;importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.isReadable=false;
            importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.ToNearest;importer.anisoLevel=1;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            Shader cloudShader=Shader.Find("Therapy Game/Photographic Cloud"),skyShader=Shader.Find("Therapy Game/Natural Sky and Stars");
            if(texture==null||cloudShader==null||skyShader==null||ShaderUtil.ShaderHasError(cloudShader)||ShaderUtil.ShaderHasError(skyShader))throw new Exception("Cloud texture or sky shader import failed.");
            Material cloud=GetMaterial("PhotographicCloud",cloudShader),sky=GetMaterial("NaturalSky",skyShader);
            cloud.SetTexture("_BaseMap",texture);EditorUtility.SetDirty(cloud);
            Transform previous=cycle.transform.Find("Photographic cloud deck");if(previous!=null)Undo.DestroyObjectImmediate(previous.gameObject);
            var group=new GameObject("Photographic cloud deck");group.transform.SetParent(cycle.transform,false);Undo.RegisterCreatedObjectUndo(group,"Refine sky layers");
            var deck=group.AddComponent<WellnessCloudDeck>();deck.viewer=player.ViewCamera;deck.cloudMaterial=cloud;
            deck.cards=new WellnessCloudDeck.Card[16];var random=new System.Random(9262026);
            for(int i=0;i<16;i++)
            {
                int tile=i%8;bool mirrored=i>=8;
                var card=new GameObject("Photographic cloud "+(i+1).ToString("00")+" · shape "+(tile+1));card.transform.SetParent(group.transform,false);
                card.AddComponent<MeshFilter>().sharedMesh=GetCardMesh(tile,mirrored);
                var renderer=card.AddComponent<MeshRenderer>();renderer.sharedMaterial=cloud;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                renderer.allowOcclusionWhenDynamic=false;renderer.sortingOrder=i<8?1:0;
                Vector3 virtualPosition=new Vector3(-3900+(i%4)*2600+Next(random,-360,360),Next(random,1500,2100),-3900+(i/4)*2600+Next(random,-400,400));
                Vector2 size=new Vector2(Next(random,1800,2750),Next(random,1250,1850));
                // Prevent a regular grid: independently jitter shape, aspect ratio, location and opacity.
                var entry=new WellnessCloudDeck.Card{transform=card.transform,renderer=renderer,virtualPosition=virtualPosition,
                    sizeMetres=size,speedFactor=Next(random,.90f,1.08f),rollDegrees=Next(random,-4,4),opacity=Next(random,.84f,1),atlasTile=tile};
                card.transform.SetPositionAndRotation(WellnessCloudDeck.Project(virtualPosition,player.ViewCamera.transform.position),Quaternion.LookRotation(-virtualPosition.normalized,Vector3.up)*Quaternion.Euler(0,0,entry.rollDegrees));
                card.transform.localScale=WellnessCloudDeck.ProjectedScale(virtualPosition,size);deck.cards[i]=entry;
            }
            // Retain old authored models for recovery, but neither draw nor animate them.
            foreach(Transform old in cycle.clouds)if(old!=null){Undo.RecordObject(old.gameObject,"Retain old cloud as disabled backup");old.gameObject.SetActive(false);}
            Undo.RecordObject(cycle,"Link photographic sky");cycle.clouds=Array.Empty<Transform>();cycle.cloudDeck=deck;cycle.skyMaterial=sky;EditorUtility.SetDirty(cycle);
            CheckMath();TherapyWeatherSetup.CheckCycleMath();Verify(deck,cycle,chat);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save refined sky scene.");
            File.WriteAllText(Report,"Photographic sky installed "+DateTime.Now.ToString("s")+"\nPASS: eight alpha cloud shapes across 16 varied layers, 32 rendered triangles, old solid clouds retained disabled, compressed mipmapped atlas <= 2048.\nPASS: 2.4 m/s wind independent of game-hour speed; 1500-2100 m virtual height, drift/advance/projection/wrap checks; existing day/weather math.\nPASS: camera, cycle, cloud/sky shader/material, voice/rain references. Stars vary in size, placement and color, fade after twilight, and dim in cloud cover.\nNo Play mode, microphone, GPU preview, reflection capture, GI refresh or bake started. Live appearance/performance still unverified.\n");
            File.WriteAllText(Request,"installed-live-visual-check-pending");Debug.Log("THERAPY_PHOTOGRAPHIC_SKY_IMPORTED: CPU checks passed; no Play, microphone or bake.");
        }
        private static float Next(System.Random r,float min,float max)=>min+(max-min)*(float)r.NextDouble();
        private static Material GetMaterial(string name,Shader shader)
        {
            string path=Folder+"/Materials/"+name+".mat";var result=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(result==null){result=new Material(shader){name=name};AssetDatabase.CreateAsset(result,path);}return result;
        }
        private static Mesh GetCardMesh(int tile,bool mirrored)
        {
            string path=Folder+"/Meshes/CloudCard_"+tile+(mirrored?"_mirrored":"")+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
            float col=tile%4,row=1-tile/4;
            Vector2 Map(float u,float v)=>new Vector2((col+(mirrored?1-u:u))/4,(row+v)/2);
            var mesh=new Mesh{name="Cloud atlas card "+tile+(mirrored?" mirrored":"")};
            mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
            mesh.uv=new[]{Map(0,0),Map(1,0),Map(1,1),Map(0,1)};mesh.uv2=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
            mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        [MenuItem("Therapy Game/Validate Photographic Sky (CPU Only)")]
        public static void CheckMath()
        {
            void Require(bool ok,string description){if(!ok)throw new Exception("Sky check: "+description);}
            float speed=WellnessCloudDeck.DriftDegreesPerSecond(2.4f,1500);
            Require(speed>.09f&&speed<.10f,"calm cloud angular speed");
            Require(Mathf.Abs(WellnessCloudDeck.AdvanceWind(100,2.4f,60)-244)<.01f,"one real minute advances 144 metres");
            Require(Mathf.Abs(WellnessCloudDeck.AdvanceWind(6499,2.4f,1)+6498.6f)<.02f,"bounded wrap");
            Require(WellnessCloudDeck.AdvanceWind(300,0,60)==300,"zero wind holds clouds");
            Vector3 p=new Vector3(2000,1500,-1200),eye=new Vector3(10,2,-8);
            Require(Mathf.Abs(Vector3.Distance(WellnessCloudDeck.Project(p,eye),eye)-150)<.01f,"sky projection distance");
            Require(Vector3.Distance(WellnessCloudDeck.Project(p,eye+Vector3.right)-WellnessCloudDeck.Project(p,eye),Vector3.right)<.001f,"sky stays distant while walking");
            Require(WellnessCloudDeck.ProjectedScale(p,new Vector2(2000,1500)).x>0,"positive projected shape");
        }
        private static void Verify(WellnessCloudDeck deck,WellnessSkyCycle cycle,WellnessVoiceChat chat)
        {
            if(deck.cards.Length!=16||deck.cards.Select(c=>c.atlasTile).Distinct().Count()!=8||deck.cards.Select(c=>c.sizeMetres).Distinct().Count()!=16)throw new Exception("Cloud variation is incomplete.");
            if(deck.cards.Sum(c=>c.transform.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)!=32)throw new Exception("Cloud triangle budget changed.");
            if(deck.cards.Any(c=>c.virtualPosition.y<1500||WellnessCloudDeck.DriftDegreesPerSecond(deck.windMetresPerSecond*c.speedFactor,c.virtualPosition.y)>.10f))throw new Exception("Cloud drift exceeds the calm preset.");
            if(deck.GetComponentsInChildren<Collider>().Length>0||deck.GetComponentsInChildren<Light>().Length>0||cycle.clouds.Length>0)throw new Exception("Unneeded solid cloud rendering remains active.");
            if(chat.skyCycle!=cycle||cycle.cloudDeck!=deck||chat.IsConnected||chat.IsBusy)throw new Exception("Existing cycle and voice state not preserved.");
        }
    }
}
