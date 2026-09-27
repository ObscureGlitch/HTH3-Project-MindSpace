using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyWallArtVariationUpgrade
    {
        private const string Root="Assets/TherapyGame";
        private const string Request=Root+"/WallArtVariationRequest.txt";
        private const string Report=Root+"/WallArt/VariationCheck.txt";
        private const string ScenePath=Root+"/Scenes/TherapyRoom.unity";

        private readonly struct PrintSpec
        {
            public readonly string title,texture;
            public PrintSpec(string title,string texture){this.title=title;this.texture=texture;}
        }

        static TherapyWallArtVariationUpgrade()
        {
            EditorApplication.delayCall+=Once;
            EditorSceneManager.sceneOpened+=(scene,mode)=>EditorApplication.delayCall+=Once;
        }

        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="install-wall-art-variety-once")return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Once;return;}
            if(!SceneManager.GetSceneByPath(ScenePath).isLoaded)return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Stop Play/baking, then use Therapy Game > Install Varied Wall Art.");return;}
            File.WriteAllText(Request,"installing-once");
            try{Install();}
            catch(Exception exception)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Report));
                File.WriteAllText(Report,"Wall-art installation stopped: "+DateTime.Now.ToString("s")+"\n"+exception);
                File.WriteAllText(Request,"needs-attention-no-auto-retry");
                Debug.LogException(exception);
            }
        }

        [MenuItem("Therapy Game/Install Varied Wall Art")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new Exception("Keep Play and baking stopped.");
            Scene scene=SceneManager.GetSceneByPath(ScenePath);
            if(!scene.isLoaded)throw new Exception("Keep TherapyRoom open.");
            Transform[] artwork=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true))
                .Where(item=>item.name.StartsWith("Botanical artwork ",StringComparison.Ordinal)).ToArray();
            if(artwork.Length!=4)throw new Exception("Expected exactly four existing framed artworks; found "+artwork.Length+".");

            Transform original=artwork.Single(item=>item.name=="Botanical artwork 0");
            Transform[] replacements=artwork.Where(item=>item!=original).ToArray();
            Transform sideWall=replacements.Single(item=>Mathf.Abs(Mathf.DeltaAngle(0,item.localEulerAngles.y))>45);
            Transform[] frontWall=replacements.Where(item=>item!=sideWall).OrderBy(item=>item.localPosition.x).ToArray();
            if(frontWall.Length!=2)throw new Exception("Expected two flanking prints on the front wall.");

            InstallPrint(frontWall[0],new PrintSpec("Quiet Orbit","QuietOrbit.png"));
            InstallPrint(frontWall[1],new PrintSpec("Still Water","StillWater.png"));
            InstallPrint(sideWall,new PrintSpec("Balanced Stones","BalancedStones.png"));

            Transform[] generated=artwork.SelectMany(item=>Children(item)).Where(item=>item.name.StartsWith("Generated print - ",StringComparison.Ordinal)&&item.gameObject.activeSelf).ToArray();
            if(generated.Length!=3||generated.Select(item=>item.GetComponent<MeshRenderer>().sharedMaterial.mainTexture).Distinct().Count()!=3)
                throw new Exception("Expected three active generated prints with three distinct textures.");
            if(Children(original).Any(item=>item.name.StartsWith("Generated print - ",StringComparison.Ordinal)))
                throw new Exception("The original centre botanical must remain unchanged.");

            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new Exception("Unity could not save TherapyRoom.");
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report,"Varied wall art installed "+DateTime.Now.ToString("s")+"\n"+
                "PASS: four framed artworks remain; the larger centre botanical is unchanged.\n"+
                "PASS: Quiet Orbit, Still Water and Balanced Stones use three distinct generated portrait textures.\n"+
                "PASS: existing oak frames and warm paper mounts are preserved; superseded clay motifs are disabled, not deleted.\n"+
                "PASS: portrait aspect ratios are preserved inside each mount with mipmaps, sRGB colour and clamped edges.\n"+
                "No Play mode, bake, microphone, network session or new light was started. Final appearance still needs a brief live view.\n");
            File.WriteAllText(Request,"installed-live-check-pending");
            AssetDatabase.SaveAssets();
            Debug.Log("THERAPY_WALL_ART_READY: three generated prints installed; original centre botanical retained.");
        }

        private static void InstallPrint(Transform root,PrintSpec spec)
        {
            string texturePath=Root+"/WallArt/Textures/"+spec.texture;
            TextureImporter importer=AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if(importer==null)throw new Exception("Missing wall-art texture importer: "+texturePath);
            bool importChanged=!importer.sRGBTexture||!importer.mipmapEnabled||importer.wrapMode!=TextureWrapMode.Clamp||importer.maxTextureSize!=1024;
            importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=1024;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            if(importChanged)importer.SaveAndReimport();
            Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(texture==null||texture.width<=0||texture.height<=0)throw new Exception("Could not import "+texturePath);

            string materialDirectory=Root+"/WallArt/Materials";
            Directory.CreateDirectory(materialDirectory);
            string materialPath=materialDirectory+"/"+spec.texture.Replace(".png",".mat");
            Material material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)throw new Exception("URP Lit shader is unavailable.");
            if(material==null){material=new Material(shader){name=spec.title};AssetDatabase.CreateAsset(material,materialPath);}
            material.shader=shader;material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.04f);material.SetFloat("_Cull",0);
            material.doubleSidedGI=true;material.enableInstancing=true;EditorUtility.SetDirty(material);

            Transform paper=Children(root).Single(item=>item.name=="Warm paper mount");
            foreach(Transform child in Children(root))
                if(child.name=="Abstract clay sun"||child.name=="Botanical stem"||child.name=="Paper leaf")
                {Undo.RecordObject(child.gameObject,"Preserve original wall art");child.gameObject.SetActive(false);}

            Transform print=Children(root).FirstOrDefault(item=>item.name.StartsWith("Generated print - ",StringComparison.Ordinal));
            if(print==null)
            {
                GameObject created=GameObject.CreatePrimitive(PrimitiveType.Quad);
                Undo.RegisterCreatedObjectUndo(created,"Install generated wall art");
                print=created.transform;print.SetParent(root,false);
                Collider collider=created.GetComponent<Collider>();if(collider!=null)UnityEngine.Object.DestroyImmediate(collider);
            }
            print.name="Generated print - "+spec.title;print.gameObject.SetActive(true);
            float width=Mathf.Max(.1f,Mathf.Abs(paper.localScale.x)-.065f),height=Mathf.Max(.1f,Mathf.Abs(paper.localScale.y)-.065f);
            float aspect=texture.width/(float)texture.height;
            if(width/height>aspect)width=height*aspect;else height=width/aspect;
            print.localPosition=paper.localPosition+new Vector3(0,0,-.014f);print.localRotation=Quaternion.identity;
            print.localScale=new Vector3(width,height,1);
            MeshRenderer renderer=print.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
            EditorUtility.SetDirty(print.gameObject);
        }

        private static Transform[] Children(Transform parent)
        {
            Transform[] children=new Transform[parent.childCount];
            for(int index=0;index<children.Length;index++)children[index]=parent.GetChild(index);
            return children;
        }
    }
}
