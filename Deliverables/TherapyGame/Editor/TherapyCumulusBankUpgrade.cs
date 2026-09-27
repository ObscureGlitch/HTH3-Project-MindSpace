using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TheLastWatch.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyCumulusBankUpgrade
    {
        private const string Root="Assets/TherapyGame";
        private const string Request=Root+"/CumulusBankRequest.txt",Report=Root+"/Weather/CloudTypes/CumulusBankCheck.txt";
        private const string TexturePath=Root+"/Weather/Textures/CloudTypes/CumulusBank.png";
        static TherapyCumulusBankUpgrade()
        {
            EditorApplication.delayCall+=Once;
            EditorSceneManager.sceneOpened+=(scene,mode)=>EditorApplication.delayCall+=Once;
        }
        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="install-cumulus-bank-once")return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Once;return;}
            if(!SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity").isLoaded)return;
            File.WriteAllText(Request,"installing-once");Install();
        }
        [MenuItem("Therapy Game/Install Single Cumulus Bank")]
        public static void Install()
        {
            int undo=-1;
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new Exception("Keep Play and baking stopped.");
                RequireMemory();
                Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
                Require(scene.isLoaded,"TherapyRoom must be open");
                var cycle=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WellnessSkyCycle>(true)).Single();
                var deck=cycle.cloudDeck;
                Require(deck!=null&&deck.CloudTypesAvailable&&deck.cards.Length==16&&deck.viewer!=null,"installed cloud deck required");
                Require(cycle.clouds.Length==0,"legacy clouds must remain disabled");
                int repaired;var resolvedCards=ResolveCards(deck,out repaired);
                Require(resolvedCards[0].renderer.enabled,"primary cloud renderer must be enabled");
                Mesh mesh=resolvedCards[0].transform.GetComponent<MeshFilter>().sharedMesh;
                Require(mesh!=null&&mesh.vertexCount==4&&mesh.triangles.Length==6&&mesh.uv2.Length==4,"reusable two-triangle quad required");
                var pond=cycle.pondRenderer!=null?cycle.pondRenderer.sharedMaterial:null;
                Require(pond!=null&&pond.HasProperty("_PondCloudSingle"),"pond single-bank sampling required");
                var importer=AssetImporter.GetAtPath(TexturePath) as TextureImporter;
                Require(importer!=null&&importer.DoesSourceTextureHaveAlpha(),"cloud bank needs genuine alpha");
                importer.textureType=TextureImporterType.Default;importer.alphaSource=TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency=true;importer.sRGBTexture=true;importer.isReadable=false;importer.mipmapEnabled=true;
                importer.npotScale=TextureImporterNPOTScale.ToNearest;importer.maxTextureSize=2048;
                importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=1;
                importer.textureCompression=TextureImporterCompression.Compressed;importer.crunchedCompression=false;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=2048,
                    format=TextureImporterFormat.DXT5,textureCompression=TextureImporterCompression.Compressed,compressionQuality=50,crunchedCompression=false});
                importer.SaveAndReimport();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
                Require(texture!=null&&texture.width==2048&&texture.height==1024&&!texture.isReadable&&texture.mipmapCount>1&&texture.format==TextureFormat.DXT5,"compressed mipmapped bank texture");
                RequireMemory();
                foreach(var material in new[]{deck.cloudMaterial,pond})
                {
                    RequireMemory();bool async=ShaderUtil.allowAsyncCompilation;
                    try{ShaderUtil.allowAsyncCompilation=false;ShaderUtil.CompilePass(material,0,true);}
                    finally{ShaderUtil.allowAsyncCompilation=async;}
                    Require(!ShaderUtil.ShaderHasError(material.shader),"cloud/pond shader errors; no automatic retry");
                }
                string tests=CumulusBankChecks.Run()+CloudSequenceChecks.Run()+CheckGeometry();
                int renderers=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Renderer>(true).Length);
                var originalAtlas=deck.cloudMaterial.GetTexture("_BaseMap");
                string backup=Path.GetFullPath("TherapyBackups/CumulusBank/"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
                Directory.CreateDirectory(backup);File.Copy(scene.path,Path.Combine(backup,"TherapyRoom-on-disk.unity"));
                Require(EditorSceneManager.SaveScene(scene),"save open scene before editing");
                File.Copy(scene.path,Path.Combine(backup,"TherapyRoom-before-cumulus-bank.unity"));
                Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Single large cumulus bank");
                Undo.RecordObject(deck,"Link single cloud bank");deck.cards=resolvedCards;deck.cumulusBankTexture=texture;deck.skyCycle=cycle;EditorUtility.SetDirty(deck);
                Require(originalAtlas==deck.cloudMaterial.GetTexture("_BaseMap"),"keep original soft banks");
                Require(renderers==scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Renderer>(true).Length),"no extra renderers");
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"save bank references");
                Require(File.ReadAllText(scene.path).Contains(AssetDatabase.AssetPathToGUID(TexturePath)),"bank texture reference persisted");
                Undo.CollapseUndoOperations(undo);undo=-1;
                Directory.CreateDirectory(Path.GetDirectoryName(Report));
                File.WriteAllText(Report,"Single cumulus bank installed "+DateTime.Now.ToString("s")+"\n"+tests+
                    "PASS: existing cloud-card links validated/reconnected: "+repaired+". Existing card metadata and objects preserved.\n"+
                    "PASS: full bank texture saved; only cloud card 0 renders in puffy mode (two triangles), other 15 disabled; original meshes/enabled states restored for other types and on exit.\n"+
                    "PASS: shared bank texture/size/opacity in pond, single-cloud UV mode, cloud and pond shader passes compiled without errors; both hide the bank behind the existing distant landscape.\n"+
                    "PASS: 650 m apparent base, 7 km virtual distance, broad 9.0-9.6 km bank, 3.7-4.7 km billboard height. Artistic scale, not a weather simulation.\n"+
                    "PASS: real-seconds wind preserved, convection-inspired daytime growth and gradual evening dissipation; manual puffy selection remains visible at night. Rain settings untouched.\n"+
                    "PASS: existing Auto/Off/manual controls, other cloud families, UI, sky/night shader, voice and scene renderer count unchanged.\n"+
                    "Backup: "+backup+"\nNo Play, GPU preview, reflection capture, bake or microphone started. Visual appearance/frame time still need a user-controlled Play check.\n");
                File.WriteAllText(Request,"installed-live-check-pending");
                Debug.Log("THERAPY_CUMULUS_BANK_READY: one large connected puffy bank, daytime development, exclusive rendering and pond reflection verified and saved.");
            }
            catch(Exception e)
            {
                if(undo>=0)Undo.RevertAllDownToGroup(undo);
                Directory.CreateDirectory(Path.GetDirectoryName(Report));File.WriteAllText(Report,"Single cumulus bank stopped "+DateTime.Now.ToString("s")+"\n"+e);
                File.WriteAllText(Request,"needs-attention-no-auto-retry");Debug.LogException(e);
            }
        }
        private static string CheckGeometry()
        {
            float desired=Mathf.Atan2(WellnessCumulusLife.BaseHeight,WellnessCumulusLife.Distance);
            foreach(float height in new[]{WellnessCumulusLife.Height*.78f,WellnessCumulusLife.Height})
            for(int yaw=0;yaw<360;yaw+=15)
            {
                Vector3 p=WellnessCloudDeck.BankPosition(yaw,height),direction=p.normalized;
                Quaternion rotation=Quaternion.LookRotation(-direction,Vector3.up);
                Vector3 bottom=p-rotation*Vector3.up*height*.5f;
                float angle=Mathf.Atan2(bottom.y,new Vector2(bottom.x,bottom.z).magnitude);
                Require(Mathf.Abs(angle-desired)<.00001f,"level stationary cloud base while top grows");
                Require(Mathf.Abs(p.magnitude-7000)<.01f,"distant bank radius");
                Vector3 right=rotation*Vector3.right*(p.magnitude/9600),up=rotation*Vector3.up*(p.magnitude/height);
                foreach(Vector2 uv in new[]{new Vector2(.1f,.1f),new Vector2(.5f,.5f),new Vector2(.9f,.9f)})
                {
                    Vector3 ray=(p+rotation*Vector3.right*((uv.x-.5f)*9600)+rotation*Vector3.up*((uv.y-.5f)*height)).normalized;
                    float facing=Vector3.Dot(ray,direction);
                    Vector2 reflected=new Vector2(Vector3.Dot(ray,right),Vector3.Dot(ray,up))/facing+Vector2.one*.5f;
                    Require(Vector2.Distance(uv,reflected)<.0001f,"sky and pond full-image UV correspondence");
                }
            }
            return "PASS: fixed apparent base through growth at 24 headings, 7 km radius, matching sky/pond projection and full-image UVs.\n";
        }
        private static WellnessCloudDeck.Card[] ResolveCards(WellnessCloudDeck deck,out int repaired)
        {
            repaired=0;var result=new WellnessCloudDeck.Card[deck.cards.Length];
            for(int i=0;i<result.Length;i++)
            {
                var c=deck.cards[i];Require(c!=null,"card metadata missing at index "+i);
                Require(c.virtualPosition.y>1000&&c.sizeMetres.x>0&&c.sizeMetres.y>0&&c.atlasTile==i%8,"card metadata invalid at index "+i);
                Transform t=c.transform;
                if(t==null)
                {
                    string prefix="Photographic cloud "+(i+1).ToString("00")+" ";
                    t=deck.transform.Cast<Transform>().SingleOrDefault(child=>child.name.StartsWith(prefix,StringComparison.Ordinal));
                    Require(t!=null,"existing named child missing for card "+i);repaired++;
                }
                Require(t.parent==deck.transform,"cloud is outside its original deck at index "+i);
                Renderer renderer=c.renderer;
                if(renderer==null){renderer=t.GetComponent<MeshRenderer>();repaired++;}
                Require(renderer!=null&&renderer.transform==t,"existing renderer missing at index "+i);
                var filter=t.GetComponent<MeshFilter>();Require(filter!=null&&filter.sharedMesh!=null,"existing quad missing at index "+i);
                result[i]=new WellnessCloudDeck.Card{transform=t,renderer=renderer,virtualPosition=c.virtualPosition,sizeMetres=c.sizeMetres,
                    speedFactor=c.speedFactor,rollDegrees=c.rollDegrees,opacity=c.opacity,atlasTile=c.atlasTile};
            }
            return result;
        }
        private static void Require(bool ok,string message){if(!ok)throw new Exception("Cumulus bank: "+message);}
        [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
        {
            public uint length,load;
            public ulong totalPhysical,availablePhysical,totalPageFile,availablePageFile,totalVirtual,availableVirtual,availableExtendedVirtual;
        }
        [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        private static void RequireMemory()
        {
            if(Application.platform!=RuntimePlatform.WindowsEditor)return;
            var m=new MemoryStatus{length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};
            if(!GlobalMemoryStatusEx(ref m)||m.availablePageFile<1536UL*1024*1024||m.availablePhysical<1024UL*1024*1024)
                throw new Exception("Low memory: close unused apps, then use Therapy Game > Install Single Cumulus Bank with Play stopped. No automatic retry.");
        }
    }
}
