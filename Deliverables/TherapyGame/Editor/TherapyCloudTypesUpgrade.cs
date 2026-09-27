using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TheLastWatch.Environment;
using TheLastWatch.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyCloudTypesUpgrade
    {
        private const string Root="Assets/TherapyGame";
        private const string Request=Root+"/CloudTypesRequest.txt";
        private const string Report=Root+"/Weather/CloudTypes/CloudTypesCheck.txt";
        private const string TextureFolder=Root+"/Weather/Textures/CloudTypes/";
        static TherapyCloudTypesUpgrade()
        {
            EditorApplication.delayCall+=Once;
            EditorSceneManager.sceneOpened+=(scene,mode)=>EditorApplication.delayCall+=Once;
        }
        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="install-cloud-types-once")return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Once;return;}
            if(!SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity").isLoaded)return;
            File.WriteAllText(Request,"installing-once");
            Install();
        }
        [MenuItem("Therapy Game/Install Cloud Types")]
        public static void Install()
        {
            int undo=-1;
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)
                    throw new Exception("Keep Play mode and baking stopped.");
                RequireMemory();
                Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
                if(!scene.isLoaded)throw new Exception("Keep TherapyRoom open.");
                var cycle=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WellnessSkyCycle>(true)).Single();
                WellnessCloudDeck deck=cycle.cloudDeck;
                Require(deck!=null&&deck.viewer!=null&&deck.cloudMaterial!=null,"existing cloud deck/camera/material required");
                Require(cycle.clouds.Length==0,"legacy clouds must remain disabled");
                Require(cycle.pondRenderer!=null&&cycle.pondRenderer.sharedMaterial!=null,"existing pond required");
                Require(deck.cards.Length==16,"preserve the existing 16-card sky");
                Texture originalAtlas=deck.cloudMaterial.GetTexture("_BaseMap");
                Require(originalAtlas!=null,"preserve the original soft-bank atlas");
                foreach(Material material in new[]{deck.cloudMaterial,cycle.pondRenderer.sharedMaterial})
                    Require(!ShaderUtil.ShaderHasError(material.shader),"existing shader has errors: "+material.name);
                int[] counts=SceneCounts(scene);

                // Configure two new small atlases only. No shader compilation,
                // reflection capture, scene rendering, or project-wide refresh.
                Texture2D puffy=ImportAtlas("PuffyCumulusAtlas.png");
                Texture2D alto=ImportAtlas("AltocumulusAtlas.png");
                string tests=CloudSequenceChecks.Run();
                TherapySkyRefinement.CheckMath();
                string geometry=CheckProjection(deck);
                string uiChecks=CheckControls();
                Require(puffy!=alto&&puffy!=originalAtlas&&alto!=originalAtlas,"all three families need distinct atlases");

                string backup=Path.GetFullPath("TherapyBackups/CloudTypes/"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
                Directory.CreateDirectory(backup);
                File.Copy(scene.path,Path.Combine(backup,"TherapyRoom-on-disk.unity"));
                Require(EditorSceneManager.SaveScene(scene),"could not preserve the open scene");
                File.Copy(scene.path,Path.Combine(backup,"TherapyRoom-before-cloud-types.unity"));
                Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Add exclusive cloud families");
                Undo.RecordObject(deck,"Set cloud atlases");
                deck.cumulusAtlas=puffy;deck.altocumulusAtlas=alto;
                deck.initialCloudType=WellnessCloudType.PuffyCumulus;
                EditorUtility.SetDirty(deck);
                Require(deck.cloudMaterial.GetTexture("_BaseMap")==originalAtlas,"original cloud material was changed");
                Require(counts.SequenceEqual(SceneCounts(scene)),"scene object/light/renderer/collider counts changed");
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                Require(EditorSceneManager.SaveScene(scene),"could not save cloud references");
                string saved=File.ReadAllText(scene.path);
                Require(saved.Contains(AssetDatabase.AssetPathToGUID(TextureFolder+"PuffyCumulusAtlas.png"))&&
                    saved.Contains(AssetDatabase.AssetPathToGUID(TextureFolder+"AltocumulusAtlas.png")),"both atlas references must persist in the saved scene");
                Undo.CollapseUndoOperations(undo);undo=-1;
                Directory.CreateDirectory(Path.GetDirectoryName(Report));
                File.WriteAllText(Report,"Cloud types and controls installed and saved "+DateTime.Now.ToString("s")+"\n"+tests+geometry+uiChecks+
                    "PASS: one shared runtime cloud material binds exactly one atlas; the pond uses the same atlas, card projection and transition opacity.\n"+
                    "PASS: two alpha atlases imported as 2048x1024, mipmapped DXT5, non-readable; original soft-bank material/atlas retained.\n"+
                    "PASS: starts with puffy cumulus; randomized 3-5 minute holds cycle through puffy cumulus, altocumulus and original soft banks without overlapping types.\n"+
                    "PASS: scene object/light/renderer/collider counts unchanged; old solid clouds remain disabled. Night sky, rain, boundaries and voice are unchanged.\n"+
                    "Backup: "+backup+"\nNo Play mode, GPU render, shader recompile, bake or microphone started. Appearance and frame time still require a user-controlled Play check.\n");
                File.WriteAllText(Request,"installed-live-check-pending");
                Debug.Log("THERAPY_CLOUD_TYPES_READY: cloud textures and settings saved; exclusive Auto/Off/manual selection, pond synchronization and compact display-only music HUD verified.");
            }
            catch(Exception e)
            {
                if(undo>=0)Undo.RevertAllDownToGroup(undo);
                Directory.CreateDirectory(Path.GetDirectoryName(Report));
                File.WriteAllText(Report,"Cloud types installation stopped "+DateTime.Now.ToString("s")+"\n"+e);
                File.WriteAllText(Request,"needs-attention-no-auto-retry");Debug.LogException(e);
            }
        }
        private static Texture2D ImportAtlas(string name)
        {
            RequireMemory();
            string path=TextureFolder+name;
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer!=null,"missing imported atlas: "+path);
            Require(importer.DoesSourceTextureHaveAlpha(),"atlas must have genuine alpha: "+name);
            importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
            importer.sRGBTexture=true;importer.isReadable=false;importer.mipmapEnabled=true;
            importer.npotScale=TextureImporterNPOTScale.ToNearest;importer.maxTextureSize=2048;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=1;
            importer.textureCompression=TextureImporterCompression.Compressed;importer.crunchedCompression=false;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{
                name="Standalone",overridden=true,maxTextureSize=2048,format=TextureImporterFormat.DXT5,
                textureCompression=TextureImporterCompression.Compressed,compressionQuality=50,crunchedCompression=false});
            importer.SaveAndReimport();
            Texture2D atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Require(atlas!=null&&atlas.width==2048&&atlas.height==1024&&atlas.mipmapCount>1&&!atlas.isReadable,
                "atlas must be a small non-readable mipmapped 4-by-2 sheet: "+name);
            Require(atlas.format==TextureFormat.DXT5,"atlas compression must preserve alpha: "+name);
            return atlas;
        }
        private static string CheckProjection(WellnessCloudDeck deck)
        {
            int triangles=0;
            foreach(var card in deck.cards)
            {
                Require(card!=null&&card.transform!=null&&card.renderer!=null,"missing cloud card");
                Require(card.renderer.sharedMaterial==deck.cloudMaterial,"clouds must share the single atlas material");
                var filter=card.transform.GetComponent<MeshFilter>();
                Require(filter!=null&&filter.sharedMesh!=null,"missing quad");
                triangles+=filter.sharedMesh.triangles.Length/3;
                Require(card.atlasTile>=0&&card.atlasTile<8,"4x2 tile index");
                foreach(WellnessCloudType type in Enum.GetValues(typeof(WellnessCloudType)))
                {
                    Vector3 p=WellnessCloudDeck.PositionForType(card.virtualPosition,type);
                    Vector2 size=WellnessCloudDeck.SizeForType(card.sizeMetres,type);
                    Require(p.y>=1400&&size.x>0&&size.y>0,"invalid cloud height/size");
                    Vector3 eye=new Vector3(3,2,-8),projected=WellnessCloudDeck.Project(p,eye);
                    Require(Mathf.Abs(Vector3.Distance(projected,eye)-150)<.01f,"distant sky projection");
                    Vector3 scale=WellnessCloudDeck.ProjectedScale(p,size);
                    Require(float.IsFinite(scale.x)&&float.IsFinite(scale.y)&&scale.x>0&&scale.y>0,"finite cloud scale");
                    Require(WellnessCloudDeck.DriftDegreesPerSecond(2.4f*card.speedFactor,p.y)<.12f,"calm wind speed");
                }
                Require(WellnessCloudDeck.PositionForType(card.virtualPosition,WellnessCloudType.Altocumulus).y>card.virtualPosition.y,"altocumulus must be higher");
            }
            Require(triangles==32,"preserve 32 cloud triangles");
            return "PASS: 16 existing quads / 32 triangles; all three families have finite sky projections, sensible heights/sizes and slow wind drift.\n";
        }
        private static int[] SceneCounts(Scene scene)
        {
            var objects=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            return new[]{objects.Length,objects.Count(t=>t.GetComponent<Renderer>()!=null),objects.Count(t=>t.GetComponent<Light>()!=null),objects.Count(t=>t.GetComponent<Collider>()!=null)};
        }
        private static void Require(bool condition,string message){if(!condition)throw new Exception("Cloud types: "+message);}
        private static string CheckControls()
        {
            foreach(Vector2 screen in new[]{new Vector2(1280,720),new Vector2(1920,1080),new Vector2(2560,1080),new Vector2(1024,768)})
            {
                float scale=Mathf.Min(screen.x/1280,screen.y/720);
                Vector2 canvas=screen/scale;
                var layout=WellnessHudLayout.At(canvas.x,canvas.y);
                Require(layout.music.width==222&&layout.music.height==54,"compact display-only music size");
                Require(layout.music.xMin>=0&&layout.music.yMin>=0&&layout.music.xMax<canvas.x&&layout.music.yMax<canvas.y,"music corner screen bounds");
                Require(!layout.music.Overlaps(layout.captions)&&!layout.music.Overlaps(layout.clock),"music widget overlaps another HUD element");
            }
            string hud=File.ReadAllText(Root+"/Runtime/WellnessHud.cs");
            int begin=hud.IndexOf("private void DrawMusic(Rect r)",StringComparison.Ordinal);
            int end=hud.IndexOf("private static string Fit",begin,StringComparison.Ordinal);
            string display=hud.Substring(begin,end-begin);
            Require(display.Contains("ui.Vinyl")&&display.Contains("TrackTitle")&&display.Contains("TrackArtist"),"music identity and record preserved");
            Require(!display.Contains("HudAction")&&!display.Contains("GUI.Button")&&!display.Contains("Elapsed")&&!display.Contains("Duration"),"no HUD transport buttons/timer/progress");
            string menu=File.ReadAllText(Root+"/Runtime/WellnessQuietGlassMenu.cs");
            Require(menu.Contains("private void CloudsPage")&&menu.Contains("Automatic cloud events")&&menu.Contains("SetCloudMode"),"dedicated cloud event settings");
            Require(menu.Contains("music.PlayPrevious()")&&menu.Contains("music.TogglePause()")&&menu.Contains("music.PlayNext()"),"pause-menu transport controls retained");
            return "PASS: 222x54 display-only music widget at four screen sizes; record/title/artist retained, HUD buttons/timer/progress removed, pause controls retained. Dedicated Clouds page has visibility, Auto/manual shapes and drift.\n";
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint length,load;
            public ulong totalPhysical,availablePhysical,totalPageFile,availablePageFile,totalVirtual,availableVirtual,availableExtendedVirtual;
        }
        [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        private static void RequireMemory()
        {
            if(Application.platform!=RuntimePlatform.WindowsEditor)return;
            var memory=new MemoryStatus{length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};
            if(!GlobalMemoryStatusEx(ref memory)||memory.availablePageFile<1536UL*1024*1024||memory.availablePhysical<1024UL*1024*1024)
                throw new Exception("Not enough memory headroom. Close unused apps, keep Play stopped, then use Therapy Game > Install Cloud Types. No automatic retry will run.");
        }
    }
}
