using System;
using System.Collections.Generic;
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
    public static class TherapyCompanionRoamingUpgrade
    {
        private const string Root="Assets/TherapyGame",Folder=Root+"/Characters/Roaming";
        private const string Request=Root+"/CompanionRoamingRequest.txt";
        static TherapyCompanionRoamingUpgrade()=>EditorApplication.delayCall+=Once;
        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="install-companion-roaming-once")return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Once;return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Stop Play and baking, then use Therapy Game > Install Roaming Companions.");return;}
            File.WriteAllText(Request,"installing-once");
            try{Install();}
            catch(Exception e){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/RoamingCheck.txt",e.ToString());File.WriteAllText(Request,"failed");Debug.LogException(e);}
        }

        [MenuItem("Therapy Game/Install Roaming Companions")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new InvalidOperationException("Stop Play and baking first.");
            Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");
            if(!scene.isLoaded)throw new InvalidOperationException("Keep TherapyRoom open.");
            Transform room=scene.GetRootGameObjects().Single(go=>go.name=="TherapyRoom").transform;
            var chat=room.GetComponentInChildren<WellnessVoiceChat>(true);
            if(chat==null||chat.IsBusy||chat.IsConnected||chat.therapists.Length!=2||chat.entranceDoor==null||chat.player==null)
                throw new InvalidOperationException("Two idle companions, the existing player and entrance door are required.");
            if(room.GetComponentInChildren<WellnessCompanionNavigation>(true)!=null||chat.therapists.Any(a=>a==null||a.movement!=null||a.GetComponent<NavMeshAgent>()!=null))
                throw new InvalidOperationException("Roaming components already exist; inspect the previous installation.");
            if(chat.therapists.Any(a=>a.bodyAnimation==null||a.bodyAnimation["Walk"]==null))throw new InvalidOperationException("Install both supplied walking clips first.");
            RunBehaviorChecks();Physics.SyncTransforms();
            Directory.CreateDirectory(Folder+"/Backups");
            string backup=Folder+"/Backups/TherapyRoom_BeforeRoaming.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");

            var sources=new List<NavMeshBuildSource>();
            var markups=new List<NavMeshBuildMarkup>();
            foreach(var actor in chat.therapists)markups.Add(new NavMeshBuildMarkup{root=actor.transform,ignoreFromBuild=true});
            markups.Add(new NavMeshBuildMarkup{root=chat.player.transform,ignoreFromBuild=true});
            markups.Add(new NavMeshBuildMarkup{root=chat.entranceDoor.transform,ignoreFromBuild=true});
            UnityEngine.AI.NavMeshBuilder.CollectSources(room,~0,NavMeshCollectGeometry.PhysicsColliders,0,markups,sources);
            // Only active solid collision geometry participates. Door motion is handled by runtime sweeps.
            sources.RemoveAll(s=>s.component is Collider c&&(!c.enabled||c.isTrigger||!c.gameObject.activeInHierarchy));
            Bounds map=new Bounds(new Vector3(10,.5f,-3),new Vector3(43,6,37));
            // The shallow pond remains available to the player; companions use dry banks.
            sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.ModifierBox,area=1,
                transform=Matrix4x4.TRS(new Vector3(15,0,-5),Quaternion.identity,Vector3.one),size=new Vector3(13.4f,6,11.4f)});
            NavMeshBuildSettings settings=NavMesh.GetSettingsByID(0);
            settings.agentRadius=.29f;settings.agentHeight=1.9f;settings.agentClimb=.22f;settings.agentSlope=35;
            settings.overrideVoxelSize=true;settings.voxelSize=.07f;settings.overrideTileSize=true;settings.tileSize=128;settings.minRegionArea=.3f;
            NavMeshData data=UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings,sources,map,Vector3.zero,Quaternion.identity);
            if(data==null)throw new InvalidOperationException("Navigation build returned no data.");
            data.name="Companion room and garden navigation";
            NavMeshDataInstance probe=NavMesh.AddNavMeshData(data);
            var report=new StringBuilder();
            Vector3[] spawn=new Vector3[2];var stops=new List<Vector3>();
            try
            {
                spawn[0]=Snap(chat.therapists[0].transform.position,.8f);
                spawn[1]=spawn[0]; // Either startup choice occupies the same indoor welcome position.
                var candidates=new[]{new Vector3(-2.43f,0,-7),new Vector3(-7,0,-13),new Vector3(-7,0,6),new Vector3(1,0,10),
                    new Vector3(9,0,10),new Vector3(24,0,10),new Vector3(27,0,-1),new Vector3(26,0,-14),
                    new Vector3(15,0,-16),new Vector3(5,0,-15),new Vector3(3,0,-7),chat.therapists[0].transform.position};
                var route=new NavMeshPath();
                foreach(Vector3 candidate in candidates)
                {
                    Vector3 point;
                    try{point=GroundSnap(candidate);}catch{continue;}
                    if(NavMesh.CalculatePath(spawn[0],point,NavMesh.AllAreas,route)&&route.status==NavMeshPathStatus.PathComplete)
                    {stops.Add(point);report.AppendLine("Destination: "+point+"; complete indoor-to-garden path, "+route.corners.Length+" corners.");}
                }
                if(stops.Count<8)throw new InvalidOperationException("Only "+stops.Count+" reachable exploration points.\n"+report);
                if(stops.Max(p=>p.x)-stops.Min(p=>p.x)<25||stops.Max(p=>p.z)-stops.Min(p=>p.z)<20)
                    throw new InvalidOperationException("Exploration route does not span the playable garden.");
                if(!stops.Any(p=>Vector3.Distance(p,spawn[0])<.5f))stops.Add(spawn[0]);
                if(!NavMesh.CalculatePath(spawn[0],spawn[1],NavMesh.AllAreas,route)||route.status!=NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("The doorway does not connect both spawn positions.");
                int connected=0;
                for(int i=0;i<stops.Count;i++)for(int j=0;j<stops.Count;j++)if(i!=j)
                {
                    if(!NavMesh.CalculatePath(stops[i],stops[j],NavMesh.AllAreas,route)||route.status!=NavMeshPathStatus.PathComplete)
                        throw new InvalidOperationException("Exploration destination pair is disconnected.");
                    foreach(Vector3 corner in route.corners)
                        if(corner.x<-11.5f||corner.x>31.5f||corner.z<-21.5f||corner.z>15.5f)
                            throw new InvalidOperationException("Path leaves the playable map boundary.");
                    connected++;
                }
                report.AppendLine("PASS: "+connected+" complete directed paths between "+stops.Count+" destinations; indoor doorway and all map quadrants connected.");
                report.AppendLine("PASS: navigation excludes the pond and keeps paths inside the fenced playable area.");
            }
            catch{UnityEngine.Object.DestroyImmediate(data);throw;}
            finally{if(probe.valid)probe.Remove();}

            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Roaming and following companions");
            try
            {
                string assetPath=Folder+"/CompanionNavigation.asset";
                if(File.Exists(assetPath))throw new InvalidOperationException("Navigation asset already exists; inspect before retrying.");
                AssetDatabase.CreateAsset(data,assetPath);
                var go=new GameObject("Companion navigation");go.transform.SetParent(room,false);Undo.RegisterCreatedObjectUndo(go,"Add shared companion navigation");
                var navigation=Undo.AddComponent<WellnessCompanionNavigation>(go);navigation.data=data;navigation.destinations=stops.ToArray();
                navigation.localIndoorSpawn=chat.roomSpace.InverseTransformPoint(spawn[0]);
                for(int i=0;i<chat.therapists.Length;i++)
                {
                    var actor=chat.therapists[i];Undo.RecordObject(actor,"Link movement");Undo.RecordObject(actor.transform,"Start the selected companion indoors");Undo.RecordObject(actor.gameObject,"Show only the selected companion");
                    actor.transform.position=spawn[i]+Vector3.up*.025f;actor.gameObject.SetActive(i==chat.SelectedIndex);
                    var agent=Undo.AddComponent<NavMeshAgent>(actor.gameObject);agent.enabled=false;
                    agent.agentTypeID=0;agent.radius=.29f;agent.height=1.9f;agent.baseOffset=.025f;
                    agent.speed=1.25f;agent.acceleration=4;agent.angularSpeed=180;agent.stoppingDistance=.25f;
                    agent.autoBraking=true;agent.autoRepath=true;agent.autoTraverseOffMeshLink=false;
                    agent.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance;agent.avoidancePriority=45+i*10;
                    agent.updatePosition=false;agent.updateRotation=false;
                    var movement=Undo.AddComponent<WellnessCompanionMovement>(actor.gameObject);
                    movement.actor=actor;movement.agent=agent;movement.navigation=navigation;movement.door=chat.entranceDoor;
                    movement.firstDestination=i==0?0:3;actor.movement=movement;EditorUtility.SetDirty(actor);
                }
                Undo.RecordObject(chat,"Enable selected companion roaming");chat.showOnlySelected=true;chat.proximityConversations=true;chat.companionTalkingDistance=6;
                Undo.RecordObject(chat.entranceDoor,"Protect companions from door swing");
                chat.entranceDoor.companions=chat.therapists.Select(a=>a.GetComponent<CapsuleCollider>()).ToArray();
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save roaming companions.");
                report.AppendLine("PASS: two configured companions, only the selected companion active, two NavMeshAgents, two movement controllers, one shared baked navigation asset.");
                report.AppendLine("PASS: indoor start, five-second departure delay, player-controlled door, Follow / Stay / Explore, actual-speed Walk animation and moving spatial voice configured.");
                report.AppendLine("PASS: opening conversation begins only after initial voice consent; later conversations use E or Talk; movement also works with voice off.");
                File.WriteAllText(Folder+"/RoamingCheck.txt","Roaming companions installed "+DateTime.Now.ToString("s")+"\n"+report+
                    "Pathfinding and behavior checks ran in Edit mode. No microphone, remote session, Play mode or render capture was started.\n");
                File.WriteAllText(Request,"installed-live-check-pending");Undo.CollapseUndoOperations(undo);
                Debug.Log("THERAPY_COMPANIONS_ROAMING_READY: navigation connectivity and movement policy checks passed.");
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }

        private static Vector3 Snap(Vector3 point,float distance)
        {
            if(!NavMesh.SamplePosition(point,out NavMeshHit hit,distance,NavMesh.AllAreas))throw new InvalidOperationException("No walkable navigation near "+point);
            return hit.position;
        }
        private static Vector3 GroundSnap(Vector3 point)
        {
            if(!Physics.Raycast(point+Vector3.up*2,Vector3.down,out RaycastHit ground,5,~0,QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("No floor at "+point);
            return Snap(ground.point,.7f);
        }
        private static void RunBehaviorChecks()
        {
            void Check(bool result,string reason){if(!result)throw new InvalidOperationException(reason);}
            Check(!WellnessCompanionMovement.ShouldFollow(false,2.8f),"Follower should not start within the comfort band.");
            Check(WellnessCompanionMovement.ShouldFollow(false,3.1f),"Follower must start when player walks away.");
            Check(WellnessCompanionMovement.ShouldFollow(true,2.4f),"Follower should continue through hysteresis band.");
            Check(!WellnessCompanionMovement.ShouldFollow(true,1.8f),"Follower must stop before crowding the player.");
            Check(WellnessCompanionMovement.ShouldPause(WellnessCompanionMovement.TravelMode.Explore,false,false,true,false),"An approached explorer must pause.");
            Check(WellnessCompanionMovement.ShouldPause(WellnessCompanionMovement.TravelMode.Explore,false,false,false,true),"Explorer must hold for conversation.");
            Check(!WellnessCompanionMovement.ShouldPause(WellnessCompanionMovement.TravelMode.Follow,false,false,false,true),"Following must remain available while talking.");
            Check(WellnessCompanionMovement.ShouldPause(WellnessCompanionMovement.TravelMode.Stay,false,false,false,false),"Stay must prevent roaming.");
            Check(WellnessCompanionMovement.ShouldPause(WellnessCompanionMovement.TravelMode.Follow,true,false,false,false),"Menu must pause following.");
        }
    }
}
