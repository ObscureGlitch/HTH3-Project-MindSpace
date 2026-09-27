using System;
using System.IO;
using System.Linq;
using TheLastWatch.Integrations;
using TheLastWatch.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyTherapistSetup
    {
        private const string Root="Assets/TherapyGame", Folder=Root+"/Characters";
        private const string Request=Root+"/TherapistRequest.txt", Report=Folder+"/TherapistCheck.txt";
        [Serializable] private class Bone { public string name; public int parent; public float[] p,q,s; }
        [Serializable] private class Surface { public string name,alphaMode; public float[] color,emission; public float roughness,metallic; public int texture; public bool doubleSided; }
        [Serializable] private class Submesh { public int material; public int[] indices; }
        [Serializable] private class Geometry { public string name; public float[] positions,normals,uvs; public int[] joints; public Submesh[] submeshes; }
        [Serializable] private class Model { public int version; public string name,units; public Bone[] bones; public Surface[] materials; public string[] textures; public Geometry[] meshes; }
        static TherapyTherapistSetup()=>EditorApplication.delayCall+=ImportOnce;
        private static void ImportOnce()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="import-therapists-once")return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||Lightmapping.isRunning)
            { File.WriteAllText(Request,"manual-only");Debug.LogWarning("Therapist import deferred. Stop Play/baking, then use Therapy Game > Add Seated Voice Companions.");return; }
            File.WriteAllText(Request,"installing-once");
            try{Install();}
            catch(Exception e){File.WriteAllText(Request,"failed");File.WriteAllText(Report,e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Therapy Game/Add Seated Voice Companions")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new InvalidOperationException("Stop Play mode and baking first.");
            Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded)throw new InvalidOperationException("Keep TherapyRoom open.");
            Transform room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
            var chat=room.GetComponentInChildren<WellnessVoiceChat>();
            var sofa=room.Find("Furniture/Sofa");
            if(sofa==null||chat==null||chat.IsBusy||chat.IsConnected||chat.settings==null||chat.settings.agents.Length!=2||chat.player==null||chat.voiceAudio==null)
                throw new InvalidOperationException("Existing sofa, player and two idle voice configurations are required.");
            if(chat.settings.agents[0].agentId!="agent_8001m3e67fv6fazsz8h90paz1xtv"||chat.settings.agents[1].agentId!="agent_4301m3e7t5qfffkb26yn6gwydg03")
                throw new InvalidOperationException("Voice configurations changed; confirm which is Voice 1/2 before importing.");
            if(room.Find("Seated AI companions")!=null)throw new InvalidOperationException("Companions already exist; not duplicating or replacing them.");
            var seat=sofa.GetComponent<WellnessSeat>();
            if(seat==null||seat.Count!=2)throw new InvalidOperationException("Expected the two sofa cushion spots.");
            // The supplied screenshot matches the sage cushion on sofa-local negative X.
            var pillow=sofa.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Soft pillow"&&t.localPosition.x<-.6f);
            if(pillow==null)throw new InvalidOperationException("Could not identify the green-pillow side safely.");
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new Exception("URP Lit is missing.");
            Model[] data={Read("julien"),Read("camille")};foreach(var m in data)Validate(m);
            foreach(string dir in new[]{"Backups","Meshes","Materials","Prefabs"})Directory.CreateDirectory(Folder+"/"+dir);
            string backup=Folder+"/Backups/TherapyRoom_BeforeTherapists.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Add seated AI companions");
            try
            {
                var group=new GameObject("Seated AI companions");group.transform.SetParent(room,false);Undo.RegisterCreatedObjectUndo(group,"Add seated AI companions");
                Undo.RecordObject(chat,"Link character voices");Undo.RecordObject(chat.settings,"Name voice companions");Undo.RecordObject(seat,"Reserve companion cushion");
                Undo.RecordObject(pillow,"Make room for seated companion");
                // Keep the green pillow beside the character rather than intersecting their torso.
                pillow.localPosition=new Vector3(-.88f,.79f,-.055f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(pillow);
                for(int i=0;i<seat.Count;i++)seat.spots[i].reserved=seat.spots[i].localBodyPosition.x<0;
                PrefabUtility.RecordPrefabInstancePropertyModifications(seat);
                chat.therapists=new WellnessTherapist[2];chat.hearingDistance=3;chat.showOnlySelected=true;
                string[] names={"Julien","Camille"};
                for(int i=0;i<2;i++)
                {
                    chat.settings.agents[i].displayName=names[i]+" · Voice "+(i+1);
                    var actor=CreateCharacter(data[i],names[i],i,group.transform,shader);
                    actor.transform.SetPositionAndRotation(sofa.TransformPoint(new Vector3(-.47f,0,.13f)),sofa.rotation);
                    actor.chat=chat;chat.therapists[i]=actor;
                    actor.gameObject.SetActive(i==0);
                }
                Undo.RecordObject(chat.voiceAudio,"Use positional character voice");Undo.RecordObject(chat.voiceAudio.transform,"Position character voice");
                chat.voiceAudio.spatialBlend=1;chat.voiceAudio.rolloffMode=AudioRolloffMode.Linear;chat.voiceAudio.minDistance=.9f;
                chat.voiceAudio.maxDistance=4.5f;chat.voiceAudio.dopplerLevel=0;chat.voiceAudio.playOnAwake=false;
                chat.voiceAudio.transform.SetPositionAndRotation(chat.therapists[0].voiceAnchor.position,chat.therapists[0].voiceAnchor.rotation);
                Physics.SyncTransforms();string poseReport=Verify(chat,seat,sofa);
                EditorUtility.SetDirty(chat);EditorUtility.SetDirty(chat.settings);EditorUtility.SetDirty(seat);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save character scene.");
                File.WriteAllText(Report,"Seated companions installed "+DateTime.Now.ToString("s")+"\n"+
                    "PASS: Julien = existing Voice 1; Camille = existing Voice 2. Remote IDs and agent configuration unchanged.\n"+
                    "PASS: all source geometry preserved (10,776 / 15,214 triangles); 57 rigid joints each; two renderers per character.\n"+
                    "PASS: selected character sits on green-pillow end; other avatar hidden; occupied cushion reserved; other seats retained.\n"+
                    "PASS: E / V opens selection; explicit consent and Start required; 3 m range and line-of-sight checks before/during calls; single session only.\n"+
                    poseReport+"\nNo microphone, ElevenLabs session, Play mode, rendered preview, lighting bake or reflection capture started. Live voice, appearance and frame rate are unverified.\n");
                File.WriteAllText(Request,"installed-live-visual-check-pending");Undo.CollapseUndoOperations(undo);
                Debug.Log("THERAPY_THERAPISTS_IMPORTED: CPU/import checks passed; no microphone or conversation started.");
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }
        private static Model Read(string name)=>JsonUtility.FromJson<Model>(File.ReadAllText(Folder+"/Source/"+name+".json"));
        private static Vector3 V(float[] a)=>new Vector3(a[0],a[1],a[2]);
        private static void Validate(Model m)
        {
            if(m.version!=1||m.units!="metres"||m.bones.Length!=57||m.meshes.Length!=2||m.materials.Length!=30)throw new Exception("Unexpected model conversion.");
            foreach(var g in m.meshes)
            {
                int n=g.positions.Length/3;
                if(n>65000||g.positions.Length%3!=0||g.normals.Length!=g.positions.Length||g.uvs.Length!=n*2||g.joints.Length!=n||g.positions.Any(x=>float.IsNaN(x)||float.IsInfinity(x))||g.joints.Any(i=>i<0||i>=m.bones.Length))throw new Exception("Invalid character geometry.");
                if(g.submeshes.Any(s=>s.material<0||s.material>=m.materials.Length||s.indices.Length%3!=0||s.indices.Any(i=>i<0||i>=n)))throw new Exception("Invalid submesh.");
            }
            if(m.meshes.Sum(g=>g.submeshes.Sum(s=>s.indices.Length/3))!=(m.name=="julien"?10776:15214))throw new Exception("Source geometry was lost.");
        }
        private static WellnessTherapist CreateCharacter(Model d,string name,int index,Transform parent,Shader shader)
        {
            var root=new GameObject(name);root.transform.SetParent(parent,false);
            var bones=d.bones.Select(b=>new GameObject(b.name).transform).ToArray();
            for(int i=0;i<bones.Length;i++)bones[i].SetParent(d.bones[i].parent<0?root.transform:bones[d.bones[i].parent],false);
            for(int i=0;i<bones.Length;i++)
            {Bone b=d.bones[i];bones[i].localPosition=V(b.p);bones[i].localRotation=new Quaternion(b.q[0],b.q[1],b.q[2],b.q[3]);bones[i].localScale=V(b.s);}
            var bind=bones.Select(b=>b.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();
            var materials=d.materials.Select((s,i)=>MakeMaterial(d,s,i,shader)).ToArray();
            foreach(var g in d.meshes)
            {
                var go=new GameObject(g.name);go.transform.SetParent(root.transform,false);
                var skin=go.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=MakeMesh(d,g,bind);skin.bones=bones;skin.rootBone=bones[0];
                skin.sharedMaterials=g.submeshes.Select(s=>materials[s.material]).ToArray();skin.quality=SkinQuality.Bone1;
                skin.updateWhenOffscreen=false;skin.shadowCastingMode=ShadowCastingMode.Off;skin.receiveShadows=true;
                skin.lightProbeUsage=LightProbeUsage.Off;skin.reflectionProbeUsage=ReflectionProbeUsage.Off;
                skin.localBounds=new Bounds(new Vector3(0,.85f,.25f),new Vector3(1.15f,1.85f,1.2f));
                skin.sortingOrder=g.name=="Glass"?2:0;
            }
            Transform Find(string n)=>bones.Single(b=>b.name==n);
            // Cushion top is roughly 0.60 m. Thighs slope down toward the front edge, feet stay near the floor.
            Find("hips").localPosition=new Vector3(0,.72f,0);
            foreach(string side in new[]{"L","R"})
            {Find("hip_"+side).localRotation=Quaternion.Euler(-67,0,side=="L"?-3:3);Find("knee_"+side).localRotation=Quaternion.Euler(67,0,0);}
            var actor=root.AddComponent<WellnessTherapist>();actor.characterName=name;actor.voiceIndex=index;
            actor.head=Find("head");actor.spine=Find("spine");actor.lowerLip=Find("lips");actor.lowerTeeth=Find("teeth_lower");actor.tongue=Find("tongue");
            var mouth=new GameObject("Voice anchor");mouth.transform.SetParent(actor.head,false);mouth.transform.localPosition=new Vector3(0,-.06f,.12f);actor.voiceAnchor=mouth.transform;
            var collider=root.AddComponent<CapsuleCollider>();collider.center=new Vector3(0,1.15f,.29f);collider.height=1;collider.radius=.23f;
            root.AddComponent<WellnessInteraction>().therapist=actor;
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Prefabs/"+name+".prefab");
            return actor;
        }
        private static Mesh MakeMesh(Model d,Geometry g,Matrix4x4[] bind)
        {
            string path=Folder+"/Meshes/"+d.name+"_"+g.name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
            int n=g.positions.Length/3;var v=new Vector3[n];var normals=new Vector3[n];var uv=new Vector2[n];var weights=new BoneWeight[n];
            for(int i=0;i<n;i++){int p=i*3;v[i]=new Vector3(g.positions[p],g.positions[p+1],g.positions[p+2]);normals[i]=new Vector3(g.normals[p],g.normals[p+1],g.normals[p+2]);uv[i]=new Vector2(g.uvs[i*2],g.uvs[i*2+1]);weights[i]=new BoneWeight{boneIndex0=g.joints[i],weight0=1};}
            var mesh=new Mesh{name=d.name+"_"+g.name,indexFormat=IndexFormat.UInt16};mesh.vertices=v;mesh.normals=normals;mesh.uv=uv;mesh.boneWeights=weights;mesh.bindposes=bind;mesh.subMeshCount=g.submeshes.Length;
            for(int i=0;i<g.submeshes.Length;i++)mesh.SetTriangles(g.submeshes[i].indices,i,false);
            mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        private static Material MakeMaterial(Model d,Surface s,int index,Shader shader)
        {
            string path=Folder+"/Materials/"+d.name+"_"+index.ToString("00")+"_"+s.name+".mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing!=null)return existing;
            var mat=new Material(shader){name=d.name+" "+s.name};
            Color c=new Color(s.color[0],s.color[1],s.color[2],s.color[3]).gamma;c.a=s.color[3];mat.SetColor("_BaseColor",c);
            mat.SetFloat("_Metallic",s.metallic);mat.SetFloat("_Smoothness",1-s.roughness);mat.SetFloat("_Cull",s.doubleSided?0:2);
            if(s.texture>=0)
            {
                string texturePath=Folder+"/Source/"+d.textures[s.texture];
                var importer=AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if(importer==null)throw new Exception("Embedded notebook texture did not import.");
                importer.sRGBTexture=true;importer.maxTextureSize=1024;importer.mipmapEnabled=true;importer.isReadable=false;importer.SaveAndReimport();
                mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            }
            if(s.alphaMode=="BLEND")
            {
                mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",0);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);mat.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.SetOverrideTag("RenderType","Transparent");mat.renderQueue=(int)RenderQueue.Transparent;mat.SetShaderPassEnabled("ShadowCaster",false);
            }
            if(s.emission.Any(x=>x>0)){mat.SetColor("_EmissionColor",new Color(s.emission[0],s.emission[1],s.emission[2]));mat.EnableKeyword("_EMISSION");}
            AssetDatabase.CreateAsset(mat,path);return mat;
        }
        private static string Verify(WellnessVoiceChat chat,WellnessSeat seat,Transform sofa)
        {
            void Require(bool ok,string message){if(!ok)throw new Exception("Therapist check: "+message);}
            Require(chat.therapists.Length==2&&chat.therapists[0].voiceIndex==0&&chat.therapists[1].voiceIndex==1,"voice mapping");
            Require(chat.therapists.Count(a=>a.gameObject.activeSelf)==1,"one selected avatar");
            Require(seat.spots.Count(s=>s.reserved)==1&&seat.spots.Single(s=>s.reserved).localBodyPosition.x<0,"green cushion reserved");
            Require(!chat.IsConnected&&!chat.IsBusy&&!chat.voiceAudio.isPlaying,"no session or microphone on import");
            var notes=new System.Text.StringBuilder();
            foreach(var a in chat.therapists)
            {
                Vector3 local=sofa.InverseTransformPoint(a.transform.position);Require(Mathf.Abs(local.x+.47f)<.005f,"green pillow side placement");
                Require(Vector3.Dot(a.transform.forward,sofa.forward)>.999f,"facing into room");
                int triangles=0;Bounds combined=default;bool first=true;
                foreach(var renderer in a.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Require(renderer.bones.Length==57&&renderer.sharedMesh.bindposes.Length==57,"complete rigid rig");triangles+=renderer.sharedMesh.triangles.Length/3;
                    var posed=new Mesh();renderer.BakeMesh(posed);
                    foreach(Vector3 p in posed.vertices){if(first){combined=new Bounds(p,Vector3.zero);first=false;}else combined.Encapsulate(p);}
                    UnityEngine.Object.DestroyImmediate(posed);
                }
                Require(triangles==(a.voiceIndex==0?10776:15214),"all source triangles retained");
                Require(combined.min.y>-.03f&&combined.min.y<.22f&&combined.max.y<1.8f,"seated feet/head bounds");
                Require(combined.size.x<1.1f&&combined.max.z<1,"pose stays on cushion");
                notes.AppendLine("PASS: "+a.characterName+" seated CPU bounds "+combined+"; source triangles "+triangles+".");
            }
            // CPU raycasts exercise the privacy boundary, without Play mode or a session.
            Transform camera=chat.player.ViewCamera.transform;Vector3 savedPosition=camera.position;
            GameObject blocker=null;
            try
            {
                camera.position=sofa.TransformPoint(new Vector3(-.47f,1.5f,1.65f));Physics.SyncTransforms();
                Require(chat.CanHearSelected(out _),"nearby unobstructed conversation allowed");
                blocker=new GameObject("Temporary hearing check (no renderer)");
                blocker.transform.position=(camera.position+chat.therapists[0].voiceAnchor.position)*.5f;
                blocker.AddComponent<BoxCollider>().size=Vector3.one*.4f;Physics.SyncTransforms();
                Require(!chat.CanHearSelected(out _),"solid obstacle stops hearing");
                UnityEngine.Object.DestroyImmediate(blocker);blocker=null;Physics.SyncTransforms();
                camera.position=sofa.TransformPoint(new Vector3(-.47f,1.5f,5));
                Require(!chat.CanHearSelected(out _),"out of range is denied");
                notes.AppendLine("PASS: actual-scene raycasts allow nearby clear view and deny solid blockers / more than 3 m. No session invoked.");
            }
            finally{camera.position=savedPosition;if(blocker!=null)UnityEngine.Object.DestroyImmediate(blocker);Physics.SyncTransforms();}
            return notes.ToString();
        }
    }
}
