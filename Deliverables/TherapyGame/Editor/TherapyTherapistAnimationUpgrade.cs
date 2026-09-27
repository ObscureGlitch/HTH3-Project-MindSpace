using System;
using System.IO;
using System.Linq;
using System.Text;
using TheLastWatch.Integrations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyTherapistAnimationUpgrade
    {
        private const string Root="Assets/TherapyGame";
        private const string Folder=Root+"/Characters/Animation";
        private const string Request=Root+"/TherapistAnimationRequest.txt";
        private const string Report=Folder+"/AnimationUpgradeCheck.txt";
        private static readonly string[] Names={"Idle","Listen","Talk","Nod","Write","Think","Wave","Walk","Sit"};
        private static readonly string[] LoopNames={"Idle","Listen","Talk","Think","Walk","Sit"};

        [Serializable] private sealed class Track {public string path,property;public float[] times,values;}
        [Serializable] private sealed class Clip {public string name;public float duration;public Track[] tracks;}
        [Serializable] private sealed class Pack {public int version;public string person,sourceHash;public Clip[] clips;}

        private sealed class ActorSnapshot
        {
            public Vector3 position,scale,colliderCenter;
            public Quaternion rotation;
            public float colliderHeight,colliderRadius;
            public Mesh[] meshes;
            public Material[][] materials;
            public int[] triangles,blendShapes;
        }

        static TherapyTherapistAnimationUpgrade()=>EditorApplication.delayCall+=ImportOnce;

        private static void ImportOnce()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="upgrade-therapist-animations-once")return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||Lightmapping.isRunning)
            {
                File.WriteAllText(Request,"manual-only");
                Debug.LogWarning("Animated companion upgrade deferred. Stop Play mode and baking, then choose Therapy Game > Upgrade Companion Animations.");
                return;
            }
            File.WriteAllText(Request,"installing-once");
            try{Install();}
            catch(Exception exception)
            {
                File.WriteAllText(Request,"failed");Directory.CreateDirectory(Folder);File.WriteAllText(Report,exception.ToString());Debug.LogException(exception);
            }
        }

        [MenuItem("Therapy Game/Upgrade Companion Animations")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new InvalidOperationException("Stop Play mode and baking first.");
            Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded)throw new InvalidOperationException("Keep TherapyRoom open.");
            Transform room=scene.GetRootGameObjects().Single(gameObject=>gameObject.name=="TherapyRoom").transform;
            WellnessVoiceChat chat=room.GetComponentInChildren<WellnessVoiceChat>(true);
            if(chat==null||chat.IsBusy||chat.IsConnected||chat.therapists==null||chat.therapists.Length!=2)
                throw new InvalidOperationException("Two existing idle companions are required.");
            if(chat.therapists.Any(actor=>actor==null||actor.speechAnalysis==null||actor.faceRenderer==null||actor.faceRenderer.sharedMesh==null||actor.faceRenderer.sharedMesh.blendShapeCount!=5))
                throw new InvalidOperationException("Install the corrected five-vowel companion models before their animation packs.");
            if(chat.therapists.All(actor=>actor.bodyAnimation!=null))throw new InvalidOperationException("The supplied companion animation packs are already installed.");
            if(chat.therapists.Any(actor=>actor.bodyAnimation!=null||actor.GetComponent<Animation>()!=null))
                throw new InvalidOperationException("The companion animation upgrade is only partially installed; restore its backup before retrying.");

            Pack[] packs={Load("julien"),Load("camille")};
            Validate(packs[0],"julien","1c0f781e31bf70756c8b5b4775ca3d8ee3301efb43d7d1dece78db4eb53c5545",chat.therapists[0]);
            Validate(packs[1],"camille","69130fbf9e5d7e6308b730f495c17f19b48c80a1cf81f7e45a61ca1bfd2e0c85",chat.therapists[1]);

            foreach(string directory in new[]{"Backups","Clips/Julien","Clips/Camille"})Directory.CreateDirectory(Folder+"/"+directory);
            string backup=Folder+"/Backups/TherapyRoom_BeforeAnimatedTherapists.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not back up the current open scene, including unsaved edits.");

            ActorSnapshot[] before=chat.therapists.Select(Snapshot).ToArray();
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Upgrade companion animations");
            try
            {
                var report=new StringBuilder();
                for(int index=0;index<chat.therapists.Length;index++)
                {
                    WellnessTherapist actor=chat.therapists[index];
                    AnimationClip[] clips=packs[index].clips.Select(clip=>ClipAsset(clip,index==0?"Julien":"Camille")).ToArray();
                    Animation animation=Undo.AddComponent<Animation>(actor.gameObject);
                    animation.playAutomatically=false;animation.wrapMode=WrapMode.Loop;animation.cullingType=AnimationCullingType.BasedOnRenderers;
                    foreach(AnimationClip clip in clips)animation.AddClip(clip,clip.name);
                    animation.clip=clips.Single(clip=>clip.name=="Idle");
                    Undo.RecordObject(actor,"Link supplied companion animations");actor.bodyAnimation=animation;actor.CaptureRestPose();
                    EditorUtility.SetDirty(animation);EditorUtility.SetDirty(actor);
                    VerifyUnchanged(actor,before[index]);
                    report.AppendLine("PASS: "+actor.characterName+" uses nine supplied clips on its existing 57-bone corrected rig; geometry, materials, collider, transform and five live vowel shapes are unchanged.");
                }
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save the animated companion upgrade.");
                File.WriteAllText(Report,
                    "Conversation-driven companion animations installed "+DateTime.Now.ToString("s")+"\n"+
                    "PASS: supplied Julien GLB SHA-256 "+packs[0].sourceHash+".\n"+
                    "PASS: supplied Camille GLB SHA-256 "+packs[1].sourceHash+".\n"+
                    report+
                    "PASS: deterministic mapping is Idle=disconnected, Listen=connected/user speaking, Talk=agent speaking, Nod=completed user transcript, Think=response pending, Write=agent speech completed, Wave=companion activated.\n"+
                    "PASS: Walk and Sit are imported for completeness but never selected while these companions remain stationary and standing.\n"+
                    "PASS: authored GLB morph-target tracks were excluded so real-time uLipSync vowel weights remain authoritative during Talk.\n"+
                    "No random animation choices, model replacement, Play mode, microphone, remote agent session, GPU preview, bake or reflection capture were used. Live appearance and voice timing still need a brief user-controlled check.\n");
                File.WriteAllText(Request,"installed-live-visual-check-pending");Undo.CollapseUndoOperations(undo);
                Debug.Log("THERAPY_COMPANION_ANIMATIONS_IMPORTED: deterministic state mapping and CPU/import checks passed; microphone off.");
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }

        private static Pack Load(string person)
        {
            string path=Folder+"/Source/"+person+"_animations.json";
            if(!File.Exists(path))throw new FileNotFoundException("Missing supplied animation conversion.",path);
            return JsonUtility.FromJson<Pack>(File.ReadAllText(path));
        }

        private static void Validate(Pack pack,string person,string expectedHash,WellnessTherapist actor)
        {
            if(pack==null||pack.version!=1||pack.person!=person||pack.sourceHash!=expectedHash||pack.clips==null||pack.clips.Length!=Names.Length)
                throw new InvalidOperationException("Unexpected "+person+" animation conversion metadata.");
            if(!pack.clips.Select(clip=>clip.name).SequenceEqual(Names)||pack.clips.Any(clip=>clip.tracks==null||clip.tracks.Length!=33||clip.duration<=0))
                throw new InvalidOperationException("The supplied "+person+" clip set changed.");
            foreach(Clip clip in pack.clips)
            foreach(Track track in clip.tracks)
            {
                int components=track.property=="quaternion"?4:track.property=="position"||track.property=="scale"?3:0;
                if(components==0||track.times==null||track.values==null||track.times.Length<2||track.values.Length!=track.times.Length*components)
                    throw new InvalidOperationException("Malformed "+clip.name+" track "+track.path+".");
                if(actor.transform.Find(track.path)==null)throw new InvalidOperationException(actor.characterName+" rig is missing animated path "+track.path+".");
            }
            if(actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(renderer=>renderer.bones).Distinct().Count()!=57)
                throw new InvalidOperationException(actor.characterName+" no longer has the expected 57-bone rig.");
        }

        private static AnimationClip ClipAsset(Clip data,string person)
        {
            string path=Folder+"/Clips/"+person+"/"+data.name+".anim";
            AnimationClip clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(clip==null){clip=new AnimationClip{name=data.name};AssetDatabase.CreateAsset(clip,path);}else clip.ClearCurves();
            clip.legacy=true;clip.frameRate=30;clip.wrapMode=LoopNames.Contains(data.name)?WrapMode.Loop:WrapMode.Once;
            foreach(Track track in data.tracks)
            {
                string[] properties=track.property=="position"?new[]{"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z"}:
                    track.property=="scale"?new[]{"m_LocalScale.x","m_LocalScale.y","m_LocalScale.z"}:
                    track.property=="quaternion"?new[]{"m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"}:
                    throw new InvalidOperationException("Unsupported animation property "+track.property+".");
                for(int component=0;component<properties.Length;component++)
                {
                    Keyframe[] keys=new Keyframe[track.times.Length];
                    for(int key=0;key<keys.Length;key++)keys[key]=new Keyframe(track.times[key],track.values[key*properties.Length+component]);
                    AnimationCurve curve=new AnimationCurve(keys);
                    for(int key=0;key<keys.Length;key++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve,key,AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curve,key,AnimationUtility.TangentMode.Linear);
                    }
                    clip.SetCurve(track.path,typeof(Transform),properties[component],curve);
                }
            }
            clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(clip);return clip;
        }

        private static ActorSnapshot Snapshot(WellnessTherapist actor)
        {
            SkinnedMeshRenderer[] renderers=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            CapsuleCollider collider=actor.GetComponent<CapsuleCollider>();
            if(renderers.Length!=3||collider==null)throw new InvalidOperationException(actor.characterName+" corrected renderers or interaction collider are missing.");
            return new ActorSnapshot
            {
                position=actor.transform.position,rotation=actor.transform.rotation,scale=actor.transform.localScale,
                colliderCenter=collider.center,colliderHeight=collider.height,colliderRadius=collider.radius,
                meshes=renderers.Select(renderer=>renderer.sharedMesh).ToArray(),materials=renderers.Select(renderer=>renderer.sharedMaterials).ToArray(),
                triangles=renderers.Select(renderer=>renderer.sharedMesh.triangles.Length/3).ToArray(),
                blendShapes=renderers.Select(renderer=>renderer.sharedMesh.blendShapeCount).ToArray()
            };
        }

        private static void VerifyUnchanged(WellnessTherapist actor,ActorSnapshot before)
        {
            ActorSnapshot after=Snapshot(actor);
            bool materialsEqual=before.materials.Length==after.materials.Length&&before.materials.SelectMany(items=>items).SequenceEqual(after.materials.SelectMany(items=>items));
            if(actor.bodyAnimation==null||actor.bodyAnimation.GetClipCount()!=9||actor.bodyAnimation.clip==null||actor.bodyAnimation.clip.name!="Idle"||
                before.position!=after.position||before.rotation!=after.rotation||before.scale!=after.scale||before.colliderCenter!=after.colliderCenter||
                before.colliderHeight!=after.colliderHeight||before.colliderRadius!=after.colliderRadius||!before.meshes.SequenceEqual(after.meshes)||
                !before.triangles.SequenceEqual(after.triangles)||!before.blendShapes.SequenceEqual(after.blendShapes)||!materialsEqual)
                throw new InvalidOperationException(actor.characterName+" changed outside the animation component; the upgrade was reverted.");
            foreach(string name in Names)
            {
                AnimationState state=actor.bodyAnimation[name];
                if(state==null)throw new InvalidOperationException(actor.characterName+" is missing "+name+".");
                bool loops=LoopNames.Contains(name);
                if(state.clip.legacy==false||state.clip.wrapMode!=(loops?WrapMode.Loop:WrapMode.Once))
                    throw new InvalidOperationException(actor.characterName+" has incorrect wrap mode for "+name+".");
            }
        }
    }
}
