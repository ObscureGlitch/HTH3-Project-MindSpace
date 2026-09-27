using System;
using System.IO;
using System.Linq;
using System.Text;
using TheLastWatch.Integrations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class CompanionRoamingValidation
    {
        private const string Root="Assets/TherapyGame",Folder=Root+"/Characters/Roaming";
        private const string Request=Root+"/CompanionRoamingValidationRequest.txt";
        static CompanionRoamingValidation()
        {
            EditorApplication.delayCall+=Once;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Once;};
        }
        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="verify-roaming-once")return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Once;return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.WriteAllText(Request,"checking");
            try{Verify();File.WriteAllText(Request,"passed");}
            catch(Exception e){File.WriteAllText(Request,"failed");File.WriteAllText(Folder+"/RoamingValidation.txt",e.ToString());Debug.LogException(e);}
        }

        [MenuItem("Therapy Game/Verify Roaming Companions")]
        public static void Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Verify in Edit mode.");
            Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            var room=scene.GetRootGameObjects().Single(go=>go.name=="TherapyRoom");
            var chat=room.GetComponentInChildren<WellnessVoiceChat>(true);
            var navigation=room.GetComponentInChildren<WellnessCompanionNavigation>(true);
            if(chat==null||chat.roomSpace==null||navigation==null||navigation.data==null||!chat.proximityConversations)
                throw new InvalidOperationException("Roaming configuration is incomplete.");
            var report=new StringBuilder();
            VerifyExclusiveSelection();
            if(!chat.showOnlySelected||chat.therapists.Where((actor,i)=>actor!=null&&actor.gameObject.activeSelf!=(i==chat.SelectedIndex)).Any())
            {
                Undo.RecordObject(chat,"Show only the selected companion");
                foreach(var actor in chat.therapists)if(actor!=null)Undo.RecordObject(actor.gameObject,"Apply companion selection");
                chat.RefreshCompanionVisibility();
                EditorUtility.SetDirty(chat);EditorSceneManager.MarkSceneDirty(scene);
            }
            if(chat.therapists.Count(actor=>actor!=null&&actor.gameObject.activeSelf)!=1)
                throw new InvalidOperationException("Exactly one companion must be active.");
            report.AppendLine("PASS: only the selected companion is active; selection regression checks passed for Julien, Camille, switching back, and an invalid selection with the legacy both-visible flag.");
            NavMeshDataInstance probe=NavMesh.AddNavMeshData(navigation.data);
            try
            {
                // Sample navigation directly; a downward ray through a standing model hits its head.
                if(!NavMesh.SamplePosition(chat.roomSpace.TransformPoint(navigation.localIndoorSpawn),out var indoor,.8f,NavMesh.AllAreas)||!chat.IsInsideRoom(indoor.position))
                    throw new InvalidOperationException("Indoor spawn is not on the navigation surface.");
                foreach(var actor in chat.therapists)
                {
                    var agent=actor.GetComponent<NavMeshAgent>();
                    Vector3 start=indoor.position+Vector3.up*(agent!=null?agent.baseOffset:.025f);
                    if(Vector3.Distance(actor.transform.position,start)>.001f)
                    {
                        Undo.RecordObject(actor.transform,"Start either companion inside the room");
                        actor.transform.position=start;EditorUtility.SetDirty(actor.transform);EditorSceneManager.MarkSceneDirty(scene);
                    }
                }
                if(!navigation.destinations.Any(p=>Vector3.Distance(p,indoor.position)<.5f))
                {
                    Undo.RecordObject(navigation,"Include return visits to the room");
                    navigation.destinations=navigation.destinations.Concat(new[]{indoor.position}).ToArray();
                    EditorUtility.SetDirty(navigation);EditorSceneManager.MarkSceneDirty(scene);
                }
                var route=new NavMeshPath();int pairs=0;
                foreach(Vector3 a in navigation.destinations)foreach(Vector3 b in navigation.destinations)
                {
                    if(a==b)continue;
                    if(!NavMesh.CalculatePath(a,b,NavMesh.AllAreas,route)||route.status!=NavMeshPathStatus.PathComplete)
                        throw new InvalidOperationException("No complete route from "+a+" to "+b);
                    foreach(var p in route.corners)
                    {
                        if(p.x<-11.5f||p.x>31.5f||p.z<-21.5f||p.z>15.5f)throw new InvalidOperationException("Path crosses map bounds.");
                        if(p.x>8.4f&&p.x<21.6f&&p.z>-10.6f&&p.z<.6f)throw new InvalidOperationException("Path enters the pond exclusion.");
                    }
                    pairs++;
                }
                report.AppendLine("PASS: "+pairs+" directed routes between "+navigation.destinations.Length+" destinations, including return visits to the room; all routes stay inside the map and outside the pond.");
                foreach(var actor in chat.therapists)
                {
                    var movement=actor.movement;var agent=actor.GetComponent<NavMeshAgent>();
                    if(actor.gameObject.activeSelf!=(actor.voiceIndex==chat.SelectedIndex)||movement==null||agent==null||movement.agent!=agent||movement.actor!=actor||movement.navigation!=navigation||movement.door!=chat.entranceDoor)
                        throw new InvalidOperationException("Broken movement references for "+actor.characterName);
                    if(agent.enabled||agent.radius!=.29f||agent.height!=1.9f||agent.autoTraverseOffMeshLink)
                        throw new InvalidOperationException("Unexpected agent settings for "+actor.characterName);
                    if(actor.bodyAnimation==null||actor.bodyAnimation["Walk"]==null||actor.faceRenderer.sharedMesh.blendShapeCount!=5)
                        throw new InvalidOperationException("Walking animation or live mouth shapes missing.");
                    if(!chat.IsInsideRoom(actor.transform.position)||!NavMesh.SamplePosition(actor.transform.position,out var spawn,.2f,NavMesh.AllAreas))
                        throw new InvalidOperationException("Companion spawn is off navigation.");
                    report.AppendLine("PASS: "+actor.characterName+" has correct selection visibility, a valid floor spawn, linked movement/agent/navigation/door, supplied Walk animation and five live vowel shapes.");
                }
                if(chat.entranceDoor.companions.Length!=2||chat.entranceDoor.companions.Any(c=>c==null))
                    throw new InvalidOperationException("Door protection is missing companion colliders.");
                // Existing consent object is exercised without creating a provider or accessing a device.
                var consent=new WellnessVoiceConsent();
                if(consent.CanConnect(true,true,false))throw new InvalidOperationException("Consent unexpectedly granted.");
                consent.Choose(false);if(consent.CanConnect(true,true,false))throw new InvalidOperationException("Voice-off path did not hold.");
                consent.Choose(true);consent.Pause();if(consent.CanConnect(true,true,false))throw new InvalidOperationException("Pause did not hold.");
                consent.Resume();consent.SetMuted(true);if(consent.CanConnect(true,true,false))throw new InvalidOperationException("Mute did not hold.");
                var policy=new WellnessCompanionRoomPolicy();
                policy.Observe(false,true,5);
                if(policy.CanLeave(true))throw new InvalidOperationException("Departure allowed before more than five seconds.");
                policy.Observe(false,true,.1f);
                if(!policy.CanLeave(true)||policy.CanLeave(false))throw new InvalidOperationException("Open-door departure gate failed.");
                policy.Observe(true,false,0);
                if(policy.CanLeave(true)||WellnessCompanionRoomPolicy.DoorAllowsPassage(false,105))throw new InvalidOperationException("Return or closing-door gate failed.");
                report.AppendLine("PASS: both choices start indoors, five-second departure gate, return reset, immediate closing-door stop, both door safety colliders, voice off, pause and mute behavior.");
            }
            finally{if(probe.valid)probe.Remove();}
            if(scene.isDirty&&!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save the room return destination.");
            File.WriteAllText(Folder+"/RoamingValidation.txt",report+"Edit-mode verification only; no microphone or provider session was used.\n");
            Debug.Log("THERAPY_COMPANION_ROAMING_VERIFIED: "+report);
        }

        private static void VerifyExclusiveSelection()
        {
            // Isolated inactive fixtures: no provider, microphone, scene actors or Play mode involved.
            var fixture=new GameObject("Companion selection verification");fixture.SetActive(false);
            var settings=ScriptableObject.CreateInstance<WellnessVoiceSettings>();
            try
            {
                var chat=fixture.AddComponent<WellnessVoiceChat>();
                settings.agents=new[]{new WellnessVoiceSettings.Agent(),new WellnessVoiceSettings.Agent()};
                chat.settings=settings;chat.showOnlySelected=false;
                chat.therapists=new WellnessTherapist[2];
                for(int i=0;i<2;i++)
                {
                    var go=new GameObject("Selection fixture "+i);go.transform.SetParent(fixture.transform);
                    chat.therapists[i]=go.AddComponent<WellnessTherapist>();chat.therapists[i].voiceIndex=i;
                }
                var select=typeof(WellnessVoiceChat).GetMethod("SelectCharacter",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                foreach(int choice in new[]{0,1,0,2})
                {
                    select.Invoke(chat,new object[]{choice});
                    int expected=choice==2?0:choice;
                    if(!chat.showOnlySelected||chat.SelectedIndex!=expected||chat.therapists.Where((actor,i)=>actor.gameObject.activeSelf!=(i==expected)).Any())
                        throw new InvalidOperationException("Exclusive companion selection failed for choice "+choice);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(fixture);UnityEngine.Object.DestroyImmediate(settings);}
        }
    }
}
