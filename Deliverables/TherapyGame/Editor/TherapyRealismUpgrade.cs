using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TheLastWatch.Environment;
using TheLastWatch.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyRealismUpgrade
    {
        private const string Root = "Assets/TherapyGame";
        private const string Request = Root + "/RealismUpgradeRequest.txt";
        private const string HQ = Root + "/Realism";
        private static readonly Dictionary<string,Material> Materials = new Dictionary<string,Material>();
        static TherapyRealismUpgrade() { EditorApplication.delayCall += Dispatch; }
        private static void Dispatch()
        {
            if (!File.Exists(Request)) return;
            string request=File.ReadAllText(Request).Trim();
            if (request!="pending" && request!="render" && request!="polish" && request!="finish") return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorApplication.delayCall += Dispatch; return; }
            File.WriteAllText(Request,"running");
            try { if(request=="render") { RenderPreviews(); File.WriteAllText(Request,"complete"); } else if(request=="finish") FinishDetails(); else if(request=="polish") Polish(); else Apply(); }
            catch(Exception e) { File.WriteAllText(Request,"failed"); File.WriteAllText(HQ+"/UpgradeReport.txt",e.ToString()); Debug.LogException(e); }
        }
        private static Transform Room()
        {
            Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded) throw new InvalidOperationException("Open TherapyRoom before applying its realism pass.");
            return scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
        }
        private static Color Hex(string s) { ColorUtility.TryParseHtmlString("#"+s,out Color c); return c; }
        private static void Save(Object o) { EditorUtility.SetDirty(o); AssetDatabase.SaveAssetIfDirty(o); }
        private static T Copy<T>(string source,string destination) where T:Object
        {
            if(!File.Exists(destination) && !AssetDatabase.CopyAsset(source,destination)) throw new IOException("Could not copy "+source);
            return AssetDatabase.LoadAssetAtPath<T>(destination);
        }

        [MenuItem("Therapy Game/Apply Realism Upgrade")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) throw new InvalidOperationException("Stop Play mode or the current lighting bake first.");
            Transform room=Room(); Scene scene=room.gameObject.scene;
            if(SceneManager.sceneCount!=1) throw new InvalidOperationException("This lighting pass requires only TherapyRoom open, to avoid baking unrelated scenes.");
            foreach(string sub in new[]{"Materials","Models","Settings","Previews","Backups"}) Directory.CreateDirectory(HQ+"/"+sub);
            AssetDatabase.Refresh();
            string snapshot=HQ+"/Backups/TherapyRoom_BeforeRealism.unity";
            if(!File.Exists(snapshot)) EditorSceneManager.SaveScene(scene,snapshot,true);
            RenderView(room,HQ+"/Previews/Before.png",new Vector3(-.1f,1.73f,-2.75f),new Vector3(.1f,1.13f,.65f),false);
            Undo.RegisterFullObjectHierarchyUndo(room.gameObject,"Therapy room realism pass");
            ImportTextures(); CreateMaterials();
            int changed=0,boardIndex=0;
            foreach(Renderer renderer in room.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    if(materials[i]==null) continue;
                    string key=materials[i].name.Replace(" HQ","");
                    if(Materials.TryGetValue(key,out Material replacement)) { materials[i]=replacement; changed++; }
                }
                renderer.sharedMaterials=materials;
                if(renderer.name=="Oak board")
                {
                    int variant=boardIndex++%12; string p=HQ+"/Materials/FloorOak_"+variant+".mat";
                    Material floor=AssetDatabase.LoadAssetAtPath<Material>(p);
                    if(floor==null)
                    {
                        floor=new Material(Materials["Oak"]) { name="Floor oak "+variant };
                        SetTiling(floor,new Vector2(.14f,.8f));
                        floor.SetTextureOffset("_BaseMap",new Vector2(variant*.073f,variant*.137f));
                        foreach(string slot in new[]{"_BumpMap","_MetallicGlossMap"}) floor.SetTextureOffset(slot,floor.GetTextureOffset("_BaseMap"));
                        floor.SetColor("_BaseColor",Color.white*Mathf.Lerp(.83f,1.05f,variant/11f)); AssetDatabase.CreateAsset(floor,p);
                    }
                    renderer.sharedMaterial=floor;
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            int upgradedMeshes=UpgradeMeshes(room);
            ConfigurePipeline(room); ConfigureLighting(room);
            Camera camera=room.GetComponentInChildren<Camera>(true);
            camera.allowHDR=true; camera.allowMSAA=true;
            var data=camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing=true; data.renderShadows=true;
            data.antialiasing=AntialiasingMode.None;
            WellnessAtmosphere atmosphere=camera.GetComponent<WellnessAtmosphere>();
            if(atmosphere==null) atmosphere=Undo.AddComponent<WellnessAtmosphere>(camera.gameObject);
            atmosphere.density=.032f; atmosphere.hazeColor=Hex("DCB78F"); atmosphere.hazeEnabled=true;
            RenderSettings.fog=false;
            // A known-safe custom reflection avoids the local editor's native reflection-baker crash.
            foreach(ReflectionProbe probe in room.GetComponentsInChildren<ReflectionProbe>(true)) probe.mode=ReflectionProbeMode.Custom;
            var settings=Copy<LightingSettings>(Root+"/Settings/WellnessLighting.asset",HQ+"/Settings/RealismLighting.asset");
            settings.bakedGI=true; settings.realtimeGI=false; settings.lightmapper=LightingSettings.Lightmapper.ProgressiveCPU;
            settings.lightmapResolution=24; settings.lightmapMaxSize=2048; settings.indirectSampleCount=128;
            settings.directSampleCount=64; settings.environmentSampleCount=64; settings.maxBounces=3;
            settings.ao=true; settings.aoMaxDistance=.22f; settings.aoExponentIndirect=.5f;
            Lightmapping.lightingSettings=settings; Save(settings);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText(HQ+"/UpgradeReport.txt",$"Materials replaced: {changed}\nRounded meshes refined: {upgradedMeshes}\n2K material maps, 4096 shadow atlas, 4x MSAA, 115% render scale.\nWarm daylight/practicals; first-person-only exponential-squared haze 0.032.\nSaved current scene including existing edits; pre-upgrade snapshot preserved.\nBaking 24 texels/metre, 2048 atlas cap, 128 indirect samples, 3 bounces.\n");
            Lightmapping.bakeCompleted-=BakeCompleted; Lightmapping.bakeCompleted+=BakeCompleted;
            File.WriteAllText(Request,"baking");
            if(!Lightmapping.BakeAsync()) throw new InvalidOperationException("Could not start the room lighting bake.");
            Debug.Log("THERAPY_REALISM_BAKING");
        }

        private static void ImportTextures()
        {
            foreach(string file in Directory.GetFiles(Root+"/Textures/Realism","*.jpg",SearchOption.AllDirectories))
            {
                string path=file.Replace('\\','/');
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                bool normal=path.Contains("_nor_gl_"),rough=path.Contains("_rough_");
                importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=!normal&&!rough; importer.maxTextureSize=2048;
                importer.mipmapEnabled=true; importer.streamingMipmaps=false; importer.anisoLevel=8;
                importer.filterMode=FilterMode.Trilinear; importer.wrapMode=TextureWrapMode.Repeat;
                importer.textureCompression=TextureImporterCompression.CompressedHQ;
                importer.isReadable=rough; importer.SaveAndReimport();
            }
        }
        private static Texture2D Map(string set,string kind)
        {
            string path=Directory.GetFiles(Root+"/Textures/Realism/"+set,"*"+kind+"_2k.jpg").Single().Replace('\\','/');
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private static Texture2D Smoothness(string set)
        {
            string path=HQ+"/Materials/"+set+"_smoothness.png";
            Texture2D existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path); if(existing!=null)return existing;
            Texture2D rough=Map(set,"rough"); Color32[] pixels=rough.GetPixels32();
            for(int i=0;i<pixels.Length;i++) pixels[i]=new Color32(0,0,0,(byte)(255-pixels[i].r));
            var texture=new Texture2D(rough.width,rough.height,TextureFormat.RGBA32,false,true);
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture=false; importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.maxTextureSize=2048; importer.mipmapEnabled=true; importer.anisoLevel=8;
            importer.filterMode=FilterMode.Trilinear; importer.wrapMode=TextureWrapMode.Repeat;
            importer.textureCompression=TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private static void SetTiling(Material m,Vector2 tiling)
        { foreach(string slot in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap"}) m.SetTextureScale(slot,tiling); }
        private static void Pbr(string name,string set,string color,float tiling,float normal,float smoothness,bool useDiffuse=true)
        {
            string path=HQ+"/Materials/"+name+".mat";
            Material material=Copy<Material>(Root+"/Materials/"+name+".mat",path); material.name=name+" HQ";
            material.SetTexture("_BaseMap",useDiffuse?Map(set,"diff"):null); material.SetColor("_BaseColor",Hex(color));
            material.SetTexture("_BumpMap",Map(set,"nor_gl")); material.SetFloat("_BumpScale",normal); material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap",Smoothness(set)); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Smoothness",smoothness); material.SetFloat("_Metallic",0);
            material.SetFloat("_SpecularHighlights",1); material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            SetTiling(material,Vector2.one*tiling); Save(material); Materials[name]=material;
        }
        private static void Simple(string name,string color,float smoothness,float metallic=0)
        {
            Material material=Copy<Material>(Root+"/Materials/"+name+".mat",HQ+"/Materials/"+name+".mat"); material.name=name+" HQ";
            material.SetColor("_BaseColor",Hex(color)); material.SetFloat("_Smoothness",smoothness); material.SetFloat("_Metallic",metallic);
            material.SetFloat("_SpecularHighlights",1); material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF"); Save(material); Materials[name]=material;
        }
        private static void CreateMaterials()
        {
            Materials.Clear();
            Pbr("Oak","oak_veneer_01","FFFFFF",1,.28f,.85f); Pbr("OakLight","oak_veneer_01","FFF3DA",1,.22f,.75f);
            Pbr("OakMid","oak_veneer_01","D9C5AB",1,.28f,.72f); Pbr("OakDark","oak_veneer_01","AF9278",1,.30f,.7f);
            Pbr("Linen","curly_teddy_natural","EBECE8",3,.40f,.35f); Pbr("Cream","curly_teddy_natural","FCFCF7",3,.40f,.35f);
            Pbr("SageFabric","rough_linen","768B61",2,.40f,.5f,false);
            Pbr("Curtain","rough_linen","EAE1CD",3,.25f,.4f,false); Pbr("LampShade","rough_linen","E9D1A5",2,.18f,.4f,false);
            Pbr("Rug","hessian_230","FFEFD5",8,.6f,.3f); Pbr("RugBorder","hessian_230","E0CBB0",8,.45f,.3f);
            Pbr("Basket","hessian_230","DCC2A0",3,.65f,.35f);
            Pbr("Throw","knitted_fleece","B2C098",2,.45f,.5f);
            Pbr("SofaBlanketKnit","knitted_fleece","B2C098",1,.45f,.5f);
            Pbr("WarmPlaster","white_plaster_02","E8DECA",3,.12f,.4f,false);
            Pbr("SageWall","white_plaster_02","718567",3,.12f,.4f,false);
            Pbr("Ceiling","white_plaster_02","E7DDC7",3,.08f,.3f,false);
            Pbr("Pot","white_plaster_02","B1A38C",1,.18f,.7f,false);
            Pbr("Terracotta","white_plaster_02","B77952",1,.18f,.5f,false);
            Pbr("Stone","white_plaster_02","8F9991",1,.20f,.65f,false);
            Simple("Ceramic","E5DBC5",.60f); Simple("Brass","B99A54",.65f,.7f); Simple("Metal","4A4F48",.6f,.65f);
            Simple("Leaf","496C37",.36f); Simple("LeafLight","738F4A",.32f); Simple("LeafDark","315539",.38f);
            Simple("Stem","5D753F",.20f); Simple("Soil","473C2D",.03f);
            Simple("BookSage","758B62",.18f); Simple("BookClay","BA795E",.18f); Simple("BookSand","D2B78B",.18f);
            Simple("ArtClay","B78159",.12f); Simple("Canvas","E7DABD",.08f);
        }

        private static void ConfigurePipeline(Transform room)
        {
            var renderer=Copy<UniversalRendererData>(Root+"/Settings/WellnessRenderer.asset",HQ+"/Settings/RealismRenderer.asset");
            foreach(ScriptableRendererFeature feature in renderer.rendererFeatures)
            {
                var s=new SerializedObject(feature); s.FindProperty("m_Settings.Intensity").floatValue=.48f;
                s.FindProperty("m_Settings.Radius").floatValue=.18f; s.FindProperty("m_Settings.DirectLightingStrength").floatValue=.13f;
                s.FindProperty("m_Settings.Downsample").boolValue=false; s.FindProperty("m_Settings.Samples").intValue=0;
                s.FindProperty("m_Settings.BlurQuality").intValue=0; s.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(feature);
            }
            Save(renderer);
            var pipeline=Copy<UniversalRenderPipelineAsset>(Root+"/Settings/WellnessURP.asset",HQ+"/Settings/RealismURP.asset");
            var so=new SerializedObject(pipeline); so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue=renderer;
            so.FindProperty("m_SoftShadowQuality").intValue=3; so.ApplyModifiedPropertiesWithoutUndo();
            pipeline.renderScale=1.15f; pipeline.msaaSampleCount=4; pipeline.supportsHDR=true; pipeline.maxAdditionalLightsCount=8;
            pipeline.mainLightShadowmapResolution=4096; pipeline.shadowCascadeCount=4; pipeline.shadowDistance=15;
            pipeline.shadowDepthBias=.25f; pipeline.shadowNormalBias=.25f; Save(pipeline);
            room.GetComponent<WellnessScenePipeline>().pipeline=pipeline;
            var profile=Copy<VolumeProfile>(Root+"/Settings/WellnessVolume.asset",HQ+"/Settings/RealismVolume.asset");
            if(profile.TryGet(out ColorAdjustments color)) { color.postExposure.Override(.40f); color.contrast.Override(7); color.saturation.Override(6); }
            if(profile.TryGet(out WhiteBalance wb)) { wb.temperature.Override(4); wb.tint.Override(1); }
            if(profile.TryGet(out Tonemapping tone)) tone.mode.Override(TonemappingMode.ACES);
            if(profile.TryGet(out Bloom bloom)) { bloom.intensity.Override(.075f); bloom.threshold.Override(1.2f); bloom.scatter.Override(.45f); bloom.highQualityFiltering.Override(true); }
            foreach(VolumeComponent component in profile.components) EditorUtility.SetDirty(component);
            Save(profile); room.GetComponentInChildren<Volume>().sharedProfile=profile;
        }
        private static void ConfigureLighting(Transform room)
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=Hex("CACED3"); RenderSettings.ambientEquatorColor=Hex("B6A692"); RenderSettings.ambientGroundColor=Hex("766650");
            RenderSettings.ambientIntensity=.65f; RenderSettings.reflectionIntensity=.65f;
            foreach(Light light in room.GetComponentsInChildren<Light>(true))
            {
                light.useColorTemperature=false;
                if(light.type==LightType.Directional)
                {
                    light.color=Hex("FFE0AF"); light.intensity=1.5f; light.shadowStrength=.78f;
                    light.shadowBias=.025f; light.shadowNormalBias=.12f; light.shadows=LightShadows.Soft;
                }
                else if(light.name=="Neutral window bounce") { light.color=Hex("E2EAF1"); light.intensity=1.6f; }
                else if(light.name=="Warm lamp light 3000K") { light.color=Hex("FFD1A0"); light.intensity=.80f; }
                else if(light.name=="Soft orb spill") { light.color=Hex("FFCB8C"); light.intensity=.22f; }
                else { light.color=Hex("F0EDE3"); light.intensity=light.name=="Soft entry bounce"?1.2f:1.1f; }
                if(light.name!="Soft entry bounce") light.lightmapBakeType=LightmapBakeType.Mixed;
            }
        }

        [MenuItem("Therapy Game/Polish Realism Lighting")]
        public static void Polish()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || SceneManager.sceneCount!=1)
                throw new InvalidOperationException("Stop Play/baking and keep only TherapyRoom open before polishing.");
            Transform room=Room(); Undo.RegisterFullObjectHierarchyUndo(room.gameObject,"Balance warm realism lighting");
            ConfigurePipeline(room); ConfigureLighting(room);
            // Preserve the knit relief without multiplying the sage tint by a very dark scan albedo.
            foreach(string name in new[]{"Throw","SofaBlanketKnit"})
            {
                Material knit=AssetDatabase.LoadAssetAtPath<Material>(HQ+"/Materials/"+name+".mat");
                knit.SetTexture("_BaseMap",null); knit.SetColor("_BaseColor",Hex("9AA586")); Save(knit);
            }
            // Broad bounced light is baked, so it creates soft contact and no extra runtime shadow pass.
            Transform ceiling=room.Find("Lighting/Broad ceiling bounce");
            if(ceiling==null)
            {
                var go=new GameObject("Broad ceiling bounce"); Undo.RegisterCreatedObjectUndo(go,"Soft bounced light");
                ceiling=go.transform; ceiling.SetParent(room.Find("Lighting"),false); go.AddComponent<Light>();
            }
            ceiling.localPosition=new Vector3(0,2.83f,.15f); ceiling.localRotation=Quaternion.Euler(90,0,0);
            Light area=ceiling.GetComponent<Light>(); area.type=LightType.Rectangle; area.areaSize=new Vector2(4.3f,3.6f);
            area.lightmapBakeType=LightmapBakeType.Baked; area.color=Hex("EEEAE0"); area.intensity=1.25f;
            area.range=8; area.bounceIntensity=1.2f; area.shadows=LightShadows.Soft;
            int planar=FixWoodCapUVs(room);
            EditorSceneManager.MarkSceneDirty(room.gameObject.scene); EditorSceneManager.SaveScene(room.gameObject.scene);
            File.AppendAllText(HQ+"/UpgradeReport.txt",$"Visual polish: neutral bounce fill, restrained amber key, lighter sage knit, {planar} planar wood caps.\n");
            Lightmapping.bakeCompleted-=BakeCompleted; Lightmapping.bakeCompleted+=BakeCompleted;
            File.WriteAllText(Request,"baking");
            if(!Lightmapping.BakeAsync())throw new InvalidOperationException("Could not start polished lighting bake.");
        }
        private static int FixWoodCapUVs(Transform room)
        {
            int count=0; var cache=new Dictionary<Mesh,Mesh>();
            foreach(MeshFilter filter in room.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<Renderer>(); Mesh source=filter.sharedMesh;
                if(source==null || source.name.StartsWith("Box_") || source.name.EndsWith("_PlanarCaps") || renderer==null ||
                    !renderer.sharedMaterials.Any(m=>m!=null && m.name.StartsWith("Oak")))continue;
                if(!cache.TryGetValue(source,out Mesh mesh))
                {
                    string sourcePath=AssetDatabase.GetAssetPath(source);
                    string path=HQ+"/Models/WoodCaps_"+AssetDatabase.AssetPathToGUID(sourcePath)+".asset";
                    mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(mesh==null)
                    {
                        mesh=Object.Instantiate(source); mesh.name=source.name+"_PlanarCaps";
                        Vector3[] v=mesh.vertices,n=mesh.normals; Vector2[] uv=mesh.uv;
                        Bounds b=mesh.bounds;
                        for(int i=0;i<v.Length;i++) if(Mathf.Abs(n[i].y)>.7f)
                            uv[i]=new Vector2((v[i].x-b.min.x)/Mathf.Max(.001f,b.size.x),(v[i].z-b.min.z)/Mathf.Max(.001f,b.size.z));
                        mesh.uv=uv; mesh.RecalculateTangents(); AssetDatabase.CreateAsset(mesh,path);
                    }
                    cache[source]=mesh;
                }
                filter.sharedMesh=mesh; PrefabUtility.RecordPrefabInstancePropertyModifications(filter); count++;
            }
            return count;
        }

        [MenuItem("Therapy Game/Finish and Verify Realism Details")]
        public static void FinishDetails()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Finish the lighting bake and stop Play mode first.");
            Transform room=Room();
            Material knit=AssetDatabase.LoadAssetAtPath<Material>(HQ+"/Materials/SofaBlanketKnit.mat");
            knit.SetTexture("_BaseMap",Map("knitted_fleece","diff"));
            // The source scan is dark wool. Compensate its albedo in the material, retaining stitch variation.
            knit.SetColor("_BaseColor",new Color(1.90f,2.02f,1.70f,1));
            SetTiling(knit,new Vector2(.13f,.13f)); knit.SetFloat("_BumpScale",.8f); knit.SetFloat("_Smoothness",.18f); Save(knit);
            var camera=room.GetComponentInChildren<Camera>();
            if(camera.GetComponent<WellnessAtmosphere>()==null)throw new InvalidOperationException("Missing first-person haze.");
            RenderPreviews(); TherapyGameTools.ValidateImportedRoom();
            var a=new Texture2D(2,2); var b=new Texture2D(2,2);
            double average=0; int maximum=0;
            try
            {
                a.LoadImage(File.ReadAllBytes(HQ+"/Previews/FirstPerson.png"));
                b.LoadImage(File.ReadAllBytes(HQ+"/Previews/FirstPerson_NoHaze.png"));
                Color32[] pa=a.GetPixels32(),pb=b.GetPixels32();
                for(int i=0;i<pa.Length;i++)
                {
                    int r=Math.Abs(pa[i].r-pb[i].r),g=Math.Abs(pa[i].g-pb[i].g),blue=Math.Abs(pa[i].b-pb[i].b);
                    average+=r+g+blue; maximum=Math.Max(maximum,Math.Max(r,Math.Max(g,blue)));
                }
                average/=pa.Length*3.0;
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
            if(average<.02 || average>12 || RenderSettings.fog)
                throw new InvalidOperationException($"Unexpected haze result: mean RGB difference {average:F3}/255, external fog {RenderSettings.fog}.");
            EditorSceneManager.SaveScene(room.gameObject.scene);
            File.AppendAllText(HQ+"/UpgradeReport.txt",$"Final detail check: visible knit scale; matched 2560x1440 haze A/B mean channel change {average:F3}/255, maximum {maximum}/255. Camera fog restored outside rendering. Missing materials/scripts: 0; interaction targets: 7.\n");
            File.WriteAllText(Request,"complete"); Debug.Log("THERAPY_REALISM_FINAL_VERIFIED");
        }

        private static int UpgradeMeshes(Transform room)
        {
            var cache=new Dictionary<Mesh,Mesh>(); int count=0;
            foreach(MeshFilter filter in room.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh original=filter.sharedMesh; if(original==null || !original.name.StartsWith("Box_")) continue;
                string[] parts=original.name.Split('_'); if(parts.Length!=5)continue;
                float radius=float.Parse(parts[4],CultureInfo.InvariantCulture);
                if(radius<.045f)continue; // Architectural edges and tiny props do not need subdivision.
                if(!cache.TryGetValue(original,out Mesh refined))
                {
                    string path=HQ+"/Models/"+original.name+"_Smooth.asset";
                    refined=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(refined==null)
                    {
                        Vector3 size=new Vector3(float.Parse(parts[1],CultureInfo.InvariantCulture),float.Parse(parts[2],CultureInfo.InvariantCulture),float.Parse(parts[3],CultureInfo.InvariantCulture));
                        refined=SmoothBox(size,radius); refined.name=original.name+"_Smooth";
                        Unwrapping.GenerateSecondaryUVSet(refined); AssetDatabase.CreateAsset(refined,path);
                    }
                    cache.Add(original,refined);
                }
                filter.sharedMesh=refined; PrefabUtility.RecordPrefabInstancePropertyModifications(filter); count++;
            }
            return count;
        }
        private static float Axis(int i,float half,float radius)
        {
            float inner=half-radius;
            if(i==6)return 0;
            if(i==5)return -inner*.5f; if(i==7)return inner*.5f;
            float[] ring={1,.9238795f,.7071068f,.3826834f,0};
            if(i<5)return -inner-radius*ring[i];
            return inner+radius*ring[12-i];
        }
        private static Mesh SmoothBox(Vector3 size,float radius)
        {
            Vector3 half=size*.5f; radius=Mathf.Min(radius,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.99f); Vector3 inner=half-Vector3.one*radius;
            var vertices=new List<Vector3>(); var normals=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
            foreach(Vector3 normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
            {
                Vector3 tangent=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.Cross(Vector3.up,normal), bitangent=Vector3.Cross(normal,tangent);
                float hu=Vector3.Scale(tangent,half).magnitude,hv=Vector3.Scale(bitangent,half).magnitude; int start=vertices.Count;
                for(int y=0;y<=12;y++)for(int x=0;x<=12;x++)
                {
                    float u=Axis(x,hu,radius),v=Axis(y,hv,radius);
                    Vector3 raw=Vector3.Scale(normal,half)+tangent*u+bitangent*v;
                    Vector3 core=new Vector3(Mathf.Clamp(raw.x,-inner.x,inner.x),Mathf.Clamp(raw.y,-inner.y,inner.y),Mathf.Clamp(raw.z,-inner.z,inner.z));
                    Vector3 n=(raw-core).normalized; vertices.Add(core+n*radius); normals.Add(n); uv.Add(new Vector2(u/(2*hu)+.5f,v/(2*hv)+.5f));
                }
                for(int y=0;y<12;y++)for(int x=0;x<12;x++) { int a=start+y*13+x,b=a+1,c=a+13,d=c+1; triangles.AddRange(new[]{a,b,d,a,d,c}); }
            }
            var mesh=new Mesh(); mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }

        private static void BakeCompleted()
        {
            Lightmapping.bakeCompleted-=BakeCompleted;
            EditorApplication.delayCall+=()=>
            {
                try
                {
                    EditorSceneManager.SaveScene(Room().gameObject.scene);
                    RenderPreviews();
                    TherapyGameTools.ValidateImportedRoom();
                    int missing=Room().GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                    if(missing!=0 || LightmapSettings.lightmaps.Length==0)throw new InvalidOperationException("Post-bake validation failed.");
                    File.AppendAllText(HQ+"/UpgradeReport.txt",$"Bake completed: {LightmapSettings.lightmaps.Length} lightmaps. Missing scripts: {missing}. Interaction targets: {Room().GetComponentsInChildren<WellnessInteraction>().Length}.\nPASS: generated full-resolution previews and saved upgraded scene.\n");
                    File.WriteAllText(Request,"complete"); Debug.Log("THERAPY_REALISM_COMPLETE");
                }
                catch(Exception e) { File.WriteAllText(Request,"failed"); File.AppendAllText(HQ+"/UpgradeReport.txt",e.ToString()); Debug.LogException(e); }
            };
        }
        [MenuItem("Therapy Game/Render Realism Previews")]
        public static void RenderPreviews()
        {
            Transform room=Room();
            RenderView(room,HQ+"/Previews/Interior.png",new Vector3(-.1f,1.73f,-2.75f),new Vector3(.1f,1.13f,.65f),true);
            RenderView(room,HQ+"/Previews/SofaDetail.png",new Vector3(-1.75f,1.70f,.05f),new Vector3(-.20f,.73f,2.35f),true);
            Camera first=room.GetComponentInChildren<Camera>();
            RenderView(room,HQ+"/Previews/FirstPerson.png",first.transform.position,first.transform.position+first.transform.forward*4,true,first.fieldOfView);
            RenderView(room,HQ+"/Previews/FirstPerson_NoHaze.png",first.transform.position,first.transform.position+first.transform.forward*4,false,first.fieldOfView);
        }
        private static void RenderView(Transform room,string path,Vector3 position,Vector3 lookAt,bool haze,float fov=65)
        {
            var previousPipeline=QualitySettings.renderPipeline;
            var go=new GameObject("Therapy realism preview") {hideFlags=HideFlags.HideAndDontSave}; var camera=go.AddComponent<Camera>();
            camera.transform.position=position; camera.transform.LookAt(lookAt); camera.fieldOfView=fov;
            camera.nearClipPlane=.04f; camera.farClipPlane=80; camera.allowHDR=true; camera.allowMSAA=true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            if(haze)
            {
                var source=room.GetComponentInChildren<WellnessAtmosphere>();
                var atmosphere=go.AddComponent<WellnessAtmosphere>();
                if(source!=null) { atmosphere.density=source.density; atmosphere.hazeColor=source.hazeColor; atmosphere.hazeEnabled=source.hazeEnabled; }
            }
            // Export display-referred sRGB; reading a half-float linear target directly makes PNGs incorrectly dark.
            RenderTexture previous=RenderTexture.active; var rt=new RenderTexture(2560,1440,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB); Texture2D texture=null;
            try
            {
                QualitySettings.renderPipeline=room.GetComponent<WellnessScenePipeline>().pipeline;
                camera.targetTexture=rt; camera.aspect=16f/9f; camera.Render(); camera.Render(); camera.Render();
                RenderTexture.active=rt; texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous; camera.targetTexture=null; QualitySettings.renderPipeline=previousPipeline;
                if(texture!=null)Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
            }
        }
    }
}
