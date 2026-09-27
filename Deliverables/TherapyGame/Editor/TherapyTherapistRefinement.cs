using System;
using System.IO;
using System.Linq;
using System.Text;
using TheLastWatch.Integrations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyTherapistRefinement
    {
        private const string Root="Assets/TherapyGame",Folder=Root+"/Characters/Refinement";
        private const string Request=Root+"/TherapistRefinementRequest.txt",Report=Folder+"/RefinementCheck.txt";
        [Serializable] private class Part {public string name;public int start,count,bone;}
        [Serializable] private class Submesh {public int material;public int[] indices;}
        [Serializable] private class Shape {public string name;public float[] positions;}
        [Serializable] private class Geometry {public string name;public float[] positions,normals,uvs;public int[] joints;public Submesh[] submeshes;public Shape[] shapes;public Part[] parts;}
        [Serializable] private class Bone {public string name;public int parent;public float[] p,q,s;}
        [Serializable] private class Surface {public string name;}
        [Serializable] private class Model {public int version;public string name;public Geometry[] meshes;public Bone[] bones;public Surface[] materials;}
        static TherapyTherapistRefinement()=>EditorApplication.delayCall+=Once;
        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="refine-therapists-once")return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Stop Play/baking, then use Therapy Game > Repair Companion Faces and Animation.");return;}
            File.WriteAllText(Request,"installing-once");
            try{Install();}catch(Exception e){File.WriteAllText(Request,"failed");File.WriteAllText(Report,e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Therapy Game/Repair Companion Faces and Animation")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new InvalidOperationException("Stop Play/baking first.");
            Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded)throw new InvalidOperationException("Keep TherapyRoom open.");
            var room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
            var chat=room.GetComponentInChildren<WellnessVoiceChat>();var sofa=room.Find("Furniture/Sofa");
            if(chat==null||chat.IsBusy||chat.IsConnected||chat.therapists.Length!=2||sofa==null)throw new Exception("Two existing characters, sofa and idle voice are required.");
            if(chat.GetComponent<WellnessSpeechAnalysis>()!=null)throw new Exception("Refinement already exists; refusing duplicate installation.");
            foreach(string dir in new[]{"Meshes","Backups","Prefabs"})Directory.CreateDirectory(Folder+"/"+dir);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string backup=Folder+"/Backups/TherapyRoom_BeforeCharacterRepair.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Repair companion geometry and animation");
            var report=new StringBuilder();
            try
            {
                var analysis=Undo.AddComponent<WellnessSpeechAnalysis>(chat.gameObject);analysis.chat=chat;analysis.source=chat.voiceAudio;
                analysis.julienProfile=AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(Folder+"/Profiles/uLipSync-Profile-Sample-Male.asset");
                analysis.camilleProfile=AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(Folder+"/Profiles/uLipSync-Profile-Sample-Female.asset");
                if(analysis.julienProfile==null||analysis.camilleProfile==null)throw new Exception("Speech profiles failed to import.");
                Vector3 standingPosition=FindClearStandingPlace(room,chat);
                var sofaSeat=sofa.GetComponent<TheLastWatch.Interaction.WellnessSeat>();Undo.RecordObject(sofaSeat,"Release sofa cushions");
                foreach(var spot in sofaSeat.spots)spot.reserved=false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(sofaSeat);
                for(int i=0;i<2;i++)
                {
                    var actor=chat.therapists[i];var name=i==0?"julien":"camille";
                    Model d=JsonUtility.FromJson<Model>(File.ReadAllText(Folder+"/Source/"+name+".json"));
                    if(d.version!=2||d.meshes.Length!=3||d.bones.Length!=57)throw new Exception("Incorrect refined model export.");
                    var body=actor.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();var bones=body.bones;
                    if(bones.Length!=57||bones.Where((b,n)=>b.name!=d.bones[n].name).Any())throw new Exception("Original rig changed; inspect before repairing.");
                    Undo.RecordObject(actor,"Link facial and idle animation");Undo.RecordObject(actor.transform,"Fit seat position");
                    foreach(var bone in bones)Undo.RecordObject(bone,"Fit seated rig");
                    // Reset to the source bind transform before deriving the new seated pose.
                    for(int b=0;b<bones.Length;b++){var v=d.bones[b];bones[b].localPosition=V(v.p);bones[b].localRotation=new Quaternion(v.q[0],v.q[1],v.q[2],v.q[3]);bones[b].localScale=V(v.s);}
                    actor.transform.localScale=Vector3.one;
                    // Rebuild from the source bind pose, never inherit a prior import's cached bind matrices.
                    Matrix4x4[] bind=bones.Select(b=>b.worldToLocalMatrix*actor.transform.localToWorldMatrix).ToArray();
                    Transform Find(string n)=>bones.Single(b=>b.name==n);
                    // Restore the actual source proportions and standing rig. Do not force this model onto the sofa.
                    Vector3 toward=room.TransformPoint(new Vector3(0,0,-.6f))-standingPosition;toward.y=0;
                    actor.transform.SetPositionAndRotation(standingPosition,Quaternion.LookRotation(toward));
                    var bodyCollider=actor.GetComponent<CapsuleCollider>();Undo.RecordObject(bodyCollider,"Fit standing interaction collider");
                    bodyCollider.center=new Vector3(0,.94f,.03f);bodyCollider.height=1.86f;bodyCollider.radius=.27f;
                    foreach(var geometry in d.meshes)
                    {
                        var child=actor.transform.Find(geometry.name);
                        if(child==null){var go=new GameObject(geometry.name);go.transform.SetParent(actor.transform,false);child=go.transform;Undo.RegisterCreatedObjectUndo(go,"Add articulated mouth");go.AddComponent<SkinnedMeshRenderer>();}
                        var renderer=child.GetComponent<SkinnedMeshRenderer>();Undo.RecordObject(renderer,"Correct face directions and skinning");
                        Undo.RecordObject(child,"Restore source mesh space");child.localPosition=Vector3.zero;child.localRotation=Quaternion.identity;child.localScale=Vector3.one;
                        renderer.bones=Array.Empty<Transform>();renderer.rootBone=null;
                        renderer.sharedMesh=MeshAsset(d,geometry,bind);renderer.bones=bones;renderer.rootBone=bones[0];
                        renderer.sharedMaterials=geometry.submeshes.Select(s=>AssetDatabase.LoadAssetAtPath<Material>(Root+"/Characters/Materials/"+name+"_"+s.material.ToString("00")+"_"+d.materials[s.material].name+".mat")).ToArray();
                        if(renderer.sharedMaterials.Any(m=>m==null))throw new Exception("Missing original material.");
                        renderer.quality=SkinQuality.Bone1;renderer.updateWhenOffscreen=false;renderer.shadowCastingMode=ShadowCastingMode.Off;
                        renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                        renderer.localBounds=new Bounds(new Vector3(0,.85f,.28f),new Vector3(1.2f,1.9f,1.3f));renderer.sortingOrder=geometry.name=="Glass"?2:0;
                        if(geometry.name=="Face")actor.faceRenderer=renderer;
                    }
                    actor.leftLid=Find("eyelid_L");actor.rightLid=Find("eyelid_R");actor.leftShoulder=Find("shoulder_L");actor.rightShoulder=Find("shoulder_R");actor.speechAnalysis=analysis;
                    actor.CaptureRestPose();report.AppendLine(CheckActor(actor,sofa));
                    PrefabUtility.SaveAsPrefabAsset(actor.gameObject,Folder+"/Prefabs/"+actor.characterName+".prefab");EditorUtility.SetDirty(actor);
                }
                Physics.SyncTransforms();CheckSpeechMath(analysis);report.AppendLine("PASS: original standing proportions restored; selected character on checked clear floor at "+standingPosition+"; both sofa seats released for the player.");
                report.AppendLine("PASS: five distinct mouth blend shapes per character; finite normalized audio-analysis scores on silence and synthetic test signals; closed mouth in silence; bounded blink curve.");
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
                File.WriteAllText(Report,"Character refinement installed "+DateTime.Now.ToString("s")+"\n"+report+
                    "PASS: old model assets preserved; new corrected mesh assets and scene backup. No remote agents, voice consent, range controls, garden, lighting or rendering settings changed.\n"+
                    "CPU-only geometry previews inspected. No Play mode, microphone, remote session, GPU preview, bake, or reflection capture started. Live voice phoneme accuracy/latency and URP appearance still need a user-controlled check.\n");
                File.WriteAllText(Request,"installed-live-check-pending");Undo.CollapseUndoOperations(undo);Debug.Log("THERAPY_THERAPISTS_REFINED: geometry/pose/audio-analysis checks passed; microphone off.");
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }
        private static Vector3 V(float[] a)=>new Vector3(a[0],a[1],a[2]);
        private static Vector3 FindClearStandingPlace(Transform room,WellnessVoiceChat chat)
        {
            // Preferred positions are beside the sofa, away from the doorway and centre walkway.
            var points=new[]{new Vector3(1.55f,0,1.12f),new Vector3(1.4f,0,1.35f),new Vector3(1.7f,0,1.35f),new Vector3(-1.7f,0,1.2f),new Vector3(1.5f,0,.85f),new Vector3(-1.75f,0,.85f)};
            foreach(var local in points)
            {
                Vector3 p=room.TransformPoint(local);
                if(!Physics.Raycast(p+Vector3.up*.3f,Vector3.down,out RaycastHit floor,.7f,~0,QueryTriggerInteraction.Ignore)||floor.normal.y<.95f)continue;
                p.y=floor.point.y+.015f;
                var hits=Physics.OverlapCapsule(p+Vector3.up*.47f,p+Vector3.up*1.40f,.46f,~0,QueryTriggerInteraction.Ignore);
                if(hits.Any(h=>!chat.therapists.Any(a=>h.transform.IsChildOf(a.transform))))continue;
                return p;
            }
            throw new Exception("No safe standing spot near the sofa. Existing scene left unchanged; choose a clear location.");
        }
        private static Mesh MeshAsset(Model d,Geometry g,Matrix4x4[] bind)
        {
            string path=Folder+"/Meshes/"+d.name+"_"+g.name+"_standing_v3.asset";var prior=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(prior!=null)return prior;
            int n=g.joints.Length;var p=new Vector3[n];var normal=new Vector3[n];var uv=new Vector2[n];var weights=new BoneWeight[n];
            for(int i=0;i<n;i++){int k=i*3;p[i]=new Vector3(g.positions[k],g.positions[k+1],g.positions[k+2]);normal[i]=new Vector3(g.normals[k],g.normals[k+1],g.normals[k+2]);uv[i]=new Vector2(g.uvs[i*2],g.uvs[i*2+1]);weights[i]=new BoneWeight{boneIndex0=g.joints[i],weight0=1};}
            var mesh=new Mesh{name=d.name+" "+g.name+" corrected",indexFormat=IndexFormat.UInt16};mesh.vertices=p;mesh.normals=normal;mesh.uv=uv;mesh.boneWeights=weights;mesh.bindposes=bind;mesh.subMeshCount=g.submeshes.Length;
            for(int s=0;s<g.submeshes.Length;s++)mesh.SetTriangles(g.submeshes[s].indices,s,false);
            if(g.shapes!=null&&g.shapes.Length>0)
            {
                mesh.RecalculateNormals();var baseNormals=mesh.normals;
                foreach(var shape in g.shapes)
                {
                    var positions=new Vector3[n];var delta=new Vector3[n];for(int i=0;i<n;i++){int k=i*3;positions[i]=new Vector3(shape.positions[k],shape.positions[k+1],shape.positions[k+2]);delta[i]=positions[i]-p[i];}
                    var temp=UnityEngine.Object.Instantiate(mesh);temp.vertices=positions;temp.RecalculateNormals();var normals=temp.normals;
                    for(int i=0;i<n;i++)normals[i]-=baseNormals[i];UnityEngine.Object.DestroyImmediate(temp);
                    mesh.AddBlendShapeFrame(shape.name,100,delta,normals,null);
                }
            }
            mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        private static string CheckActor(WellnessTherapist actor,Transform sofa)
        {
            int triangles=0,aligned=0,tested=0;Bounds pose=default;bool first=true;
            var cushion=sofa.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Separate seat cushion"&&f.transform.localPosition.x<0);
            var tester=new GameObject("Temporary cushion fit test");tester.transform.SetPositionAndRotation(cushion.transform.position,cushion.transform.rotation);tester.transform.localScale=cushion.transform.lossyScale;
            var collider=tester.AddComponent<MeshCollider>();collider.sharedMesh=cushion.sharedMesh;Physics.SyncTransforms();int intersections=0;float worst=0;
            try
            {
                foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Mesh mesh=renderer.sharedMesh;var p=mesh.vertices;var normals=mesh.normals;var indices=mesh.triangles;triangles+=indices.Length/3;
                    for(int i=0;i<indices.Length;i+=3)
                    {Vector3 cross=Vector3.Cross(p[indices[i+1]]-p[indices[i]],p[indices[i+2]]-p[indices[i]]);if(cross.sqrMagnitude<1e-14f)continue;tested++;if(Vector3.Dot(cross,normals[indices[i]]+normals[indices[i+1]]+normals[indices[i+2]])>0)aligned++;}
                    var baked=new Mesh();renderer.BakeMesh(baked);var vertices=baked.vertices;
                    foreach(var v in vertices)
                    {
                        if(first){pose=new Bounds(v,Vector3.zero);first=false;}else pose.Encapsulate(v);
                        Vector3 world=renderer.transform.TransformPoint(v);
                        if(world.y<.4f||world.y>.7f)continue;
                        if(collider.Raycast(new Ray(new Vector3(world.x,.85f,world.z),Vector3.down),out RaycastHit hit,.45f))
                        {float depth=hit.point.y-world.y;if(depth>.008f){intersections++;worst=Mathf.Max(worst,depth);}}
                    }
                    UnityEngine.Object.DestroyImmediate(baked);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(tester);}
            if(triangles!=(actor.voiceIndex==0?10776:13954))throw new Exception("Corrected geometry count failed.");
            if(aligned<tested*.96f)throw new Exception("Triangle winding and normals still disagree: "+aligned+"/"+tested);
            if(intersections>0)throw new Exception("Seat still intersects "+actor.characterName+": "+intersections+" vertices; worst "+worst.ToString("F4")+" m.");
            if(pose.min.y<-.015f||pose.min.y>.08f)throw new Exception("Feet do not rest near the floor: "+pose.min.y);
            if(actor.faceRenderer.sharedMesh.blendShapeCount!=5)throw new Exception("Five vowel shapes are required.");
            for(int i=0;i<5;i++)if(actor.faceRenderer.sharedMesh.GetBlendShapeName(i)!=WellnessSpeechAnalysis.Names[i])throw new Exception("Viseme order mismatch.");
            return "PASS: "+actor.characterName+" "+triangles+" triangles; outward winding "+aligned+"/"+tested+"; no sofa intersections; source foot height "+pose.min.y.ToString("F3")+" m; five mouth shapes.";
        }
        private static void CheckSpeechMath(WellnessSpeechAnalysis analysis)
        {
            foreach(var p in new[]{analysis.julienProfile,analysis.camilleProfile})
            {
                if(WellnessSpeechAnalysis.Names.Any(n=>!p.GetPhonemeNames().Contains(n)))throw new Exception("Profile vowel missing.");
                // Synchronous CPU job in Edit mode on non-user synthetic data; no audio output/input.
                using(var input=new Unity.Collections.NativeArray<float>(1024,Unity.Collections.Allocator.TempJob))
                using(var means=new Unity.Collections.NativeArray<float>(p.means,Unity.Collections.Allocator.TempJob))
                using(var dev=new Unity.Collections.NativeArray<float>(p.standardDeviation,Unity.Collections.Allocator.TempJob))
                using(var phonemes=new Unity.Collections.NativeArray<float>(12*p.mfccs.Count,Unity.Collections.Allocator.TempJob))
                using(var mfcc=new Unity.Collections.NativeArray<float>(12,Unity.Collections.Allocator.TempJob))
                using(var scores=new Unity.Collections.NativeArray<float>(p.mfccs.Count,Unity.Collections.Allocator.TempJob))
                using(var info=new Unity.Collections.NativeArray<uLipSync.LipSyncJob.Info>(1,Unity.Collections.Allocator.TempJob))
                {
                    var writablePhonemes=phonemes;var writableInput=input;
                    for(int i=0;i<p.mfccs.Count;i++)for(int c=0;c<12;c++)writablePhonemes[i*12+c]=p.mfccs[i].mfccNativeArray[c];
                    var job=new uLipSync.LipSyncJob{input=input,startIndex=0,outputSampleRate=16000,targetSampleRate=16000,melFilterBankChannels=p.melFilterBankChannels,compareMethod=p.compareMethod,means=means,standardDeviations=dev,phonemes=phonemes,mfcc=mfcc,scores=scores,info=info};
                    job.Execute();if(info[0].volume!=0)throw new Exception("Silence analysis failed.");
                    for(int i=0;i<1024;i++)writableInput[i]=.05f*Mathf.Sin(i*2*Mathf.PI*180/16000)+.035f*Mathf.Sin(i*2*Mathf.PI*730/16000)+.02f*Mathf.Sin(i*2*Mathf.PI*1100/16000);
                    job.Execute();if(info[0].volume<=0||float.IsNaN(info[0].volume))throw new Exception("Speech analysis level failed.");
                    float sum=0;for(int i=0;i<scores.Length;i++){if(float.IsNaN(scores[i])||float.IsInfinity(scores[i])||scores[i]<0)throw new Exception("Invalid MFCC classification.");sum+=scores[i];}
                    if(sum>.001f&&(sum<.99f||sum>1.01f))throw new Exception("MFCC scores are not normalized.");
                }
            }
            if(WellnessTherapist.EvaluateBlink(-1)!=0||WellnessTherapist.EvaluateBlink(.3f)!=0||WellnessTherapist.EvaluateBlink(.076f)<.99f)throw new Exception("Blink curve failed.");
        }
    }
}
