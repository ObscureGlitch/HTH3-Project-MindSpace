using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TheLastWatch.Environment;
using TheLastWatch.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace TherapyGame.Editor
{
    public static class TherapyPantheonSetup
    {
        public const string Asset="Assets/TherapyGame/Resources/PantheonKoiLibrary.asset";
        const string Source="Assets/TherapyGame/Editor/PantheonSource/Pantheon.bytes";
        const string Original="Assets/TherapyGame/Exterior/KoiPond/KoiLibrary.asset";
        const string Scene="Assets/TherapyGame/Scenes/TherapyRoom.unity";
        public const string Report="Assets/TherapyGame/Documentation/FishingPantheonCheck.txt";
        public static void Require(bool test,string message){if(!test)throw new InvalidOperationException("Koi pantheon: "+message);}
        static string String(BinaryReader reader){int n=reader.ReadInt32();Require(n>0&&n<=128,"Invalid source string.");return Encoding.UTF8.GetString(reader.ReadBytes(n));}
        static Vector3 Vector(BinaryReader reader)=>new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
        static Vector3[] Vectors(BinaryReader reader,int count){var values=new Vector3[count];for(int i=0;i<count;i++){values[i]=Vector(reader);Require(float.IsFinite(values[i].x)&&float.IsFinite(values[i].y)&&float.IsFinite(values[i].z),"Non-finite source vertex.");}return values;}
        static KoiPondLibrary Build()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Stay in Edit mode.");
            using(var reader=new BinaryReader(File.OpenRead(Source)))
            {
                Require(String(reader)=="KPN1","Invalid optimized source header.");string hash=String(reader);
                var existing=AssetDatabase.LoadAssetAtPath<KoiPondLibrary>(Asset);
                if(existing!=null){Require(existing.sourceSha256==hash&&existing.varieties.Length==24,"Existing derivative differs. Inspect it before replacing.");return existing;}
                var original=AssetDatabase.LoadAssetAtPath<KoiPondLibrary>(Original);Require(original!=null&&original.varieties.Length==8,"Original eight koi are required.");
                var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/TherapyGame/Resources/KoiPantheonSurface.shader");Require(shader!=null,"Pantheon shader missing.");
                var library=ScriptableObject.CreateInstance<KoiPondLibrary>();library.name="MindSpace koi collection (24 varieties)";library.material=original.material;library.sourceSha256=hash;
                var varieties=new List<KoiPondLibrary.Variety>(original.varieties);
                int fishCount=reader.ReadInt32();Require(fishCount==16,"Expected sixteen supplied koi.");
                AssetDatabase.CreateAsset(library,Asset);
                try
                {
                    var material=new Material(shader){name="Supplied pantheon markings and emission"};AssetDatabase.AddObjectToAsset(material,library);
                    for(int f=0;f<fishCount;f++)
                    {
                        string name=String(reader);Require(!varieties.Any(v=>v.name==name),"Duplicate fish name.");int partCount=reader.ReadInt32();Require(partCount>=4&&partCount<=12,"Invalid part count.");
                        var variety=new KoiPondLibrary.Variety{name=name,parts=new KoiPondLibrary.Part[partCount]};
                        for(int p=0;p<partCount;p++)
                        {
                            string part=String(reader);var pivot=Vector(reader);int count=reader.ReadInt32();Require(count>0&&count<60000,"Invalid vertex budget.");
                            var vertices=Vectors(reader,count);var normals=Vectors(reader,count);var rgb=Vectors(reader,count);var emission=Vectors(reader,count);var colors=new Color[count];var surface=new List<Vector2>(count);
                            for(int i=0;i<count;i++)colors[i]=new Color(rgb[i].x,rgb[i].y,rgb[i].z,1);
                            for(int i=0;i<count;i++)surface.Add(new Vector2(reader.ReadSingle(),reader.ReadSingle()));
                            int indexCount=reader.ReadInt32();Require(indexCount>0&&indexCount%3==0&&indexCount<=150000,"Invalid triangle budget.");var indices=new int[indexCount];
                            for(int i=0;i<indexCount;i++){indices[i]=reader.ReadInt32();Require(indices[i]>=0&&indices[i]<count,"Invalid triangle index.");}
                            var mesh=new Mesh{name=name+" "+part};mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.SetUVs(0,surface);mesh.SetUVs(1,new List<Vector3>(emission));mesh.triangles=indices;mesh.RecalculateBounds();
                            AssetDatabase.AddObjectToAsset(mesh,library);variety.parts[p]=new KoiPondLibrary.Part{name=part,mesh=mesh,pivot=pivot,material=material};
                        }
                        varieties.Add(variety);
                    }
                    Require(reader.BaseStream.Position==reader.BaseStream.Length,"Unexpected trailing source data.");library.varieties=varieties.ToArray();EditorUtility.SetDirty(library);AssetDatabase.SaveAssetIfDirty(library);
                    AssetDatabase.ImportAsset(Asset,ImportAssetOptions.ForceUpdate);return AssetDatabase.LoadAssetAtPath<KoiPondLibrary>(Asset);
                }
                catch{AssetDatabase.DeleteAsset(Asset);throw;}
            }
        }
        [MenuItem("Therapy Game/Install Koi Pantheon")]
        public static void InstallFromMenu(){try{Build();Verify();Debug.Log("KOI_PANTHEON_INSTALLED");}catch(Exception e){Debug.LogException(e);}}
        public static void InstallBatch(){try{Build();Verify();Debug.Log("KOI_PANTHEON_INSTALLED");EditorApplication.Exit(0);}catch(Exception e){File.WriteAllText(Report,"FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void VerifyBatch(){try{Verify();EditorApplication.Exit(0);}catch(Exception e){File.WriteAllText(Report,"FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}}
        public static KoiPondLibrary Verify()
        {
            Require(!EditorApplication.isPlaying,"No Play mode validation.");EditorSceneManager.OpenScene(Scene);
            var root=EditorSceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="TherapyRoom");var fishing=root.GetComponent<WellnessFishing>();
            var library=AssetDatabase.LoadAssetAtPath<KoiPondLibrary>(Asset);var original=AssetDatabase.LoadAssetAtPath<KoiPondLibrary>(Original);
            Require(fishing!=null&&fishing.Library==library&&library!=null&&library.varieties.Length==24,"Live fishing does not use the new catalog.");
            Require(fishing.pond.library==original&&original.varieties.Length==8&&fishing.pond.randomizeCount,"Ambient koi population was replaced.");
            for(int i=0;i<8;i++)Require(library.varieties[i].name==original.varieties[i].name&&library.varieties[i].parts.Select(p=>p.mesh).SequenceEqual(original.varieties[i].parts.Select(p=>p.mesh)),"Original koi references changed.");
            Require(!WellnessFishing.ModalOpen&&root.GetComponentsInChildren<Camera>(true).All(c=>!c.name.Contains("Preview")),"Transient fishing state was saved in scene.");
            int triangles=0;foreach(var variety in library.varieties.Skip(8))
            {
                Require(variety.parts.Length>=4&&variety.parts.Length<=12,"Too many mesh draws per catch.");int count=0;
                foreach(var p in variety.parts){Require(p.mesh!=null&&p.material!=null&&p.mesh.vertexCount<60000,"Missing mesh/material.");Require(p.mesh.colors.Length==p.mesh.vertexCount,"Supplied vertex markings lost.");count+=p.mesh.triangles.Length/3;Require(!ShaderUtil.ShaderHasError(p.material.shader),"Fish shader import errors.");}
                Require(count<21000,"Single detailed catch exceeds triangle budget.");triangles+=count;
            }
            string checks=TherapyPantheonChecks.CheckOdds(library)+TherapyPantheonChecks.CheckRounds()+TherapyPantheonChecks.CheckSaves()+TherapyPantheonChecks.CheckViews(library);
            File.WriteAllText(Report,"PASS: 16 supplied fish added to separate 24-koi catch catalog; original eight mesh references and 15-30 ambient school retained.\nDetailed model triangles across all 16: "+triangles+". Only displayed catches, held fish and one preview instantiate them.\n"+checks+"No real player saves, webcam, microphone, network or AI services were accessed. No Play mode was entered.\nPending: hands-on Play input, UI readability and sustained frame-rate measurement.\n");
            Debug.Log("KOI_PANTHEON_CHECKS_PASSED");return library;
        }
    }
}
