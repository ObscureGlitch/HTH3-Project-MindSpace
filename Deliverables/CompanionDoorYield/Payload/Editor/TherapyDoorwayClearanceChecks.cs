using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using TheLastWatch.Interaction;
using TheLastWatch.Integrations;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyDoorwayClearanceChecks
    {
        const string Root="Assets/TherapyGame";
        const string Request=Root+"/DoorwayClearanceRequest.txt";
        const string Report=Root+"/Documentation/DoorwayClearanceCheck.txt";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static TherapyDoorwayClearanceChecks()
        {
            EditorApplication.delayCall+=Once;
            EditorSceneManager.sceneOpened+=(scene,mode)=>EditorApplication.delayCall+=Once;
        }
        static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="verify-companion-door-clearance")return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Once;return;}
            if(EditorSceneManager.GetActiveScene().name!="TherapyRoom"||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.WriteAllText(Request,"checking");
            Verify();
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static object Get(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
        static void Tick(WellnessDoor door)=>typeof(WellnessDoor).GetMethod("TickMotion",Flags).Invoke(door,new object[]{.02f});

        public static void VerifyBatch()
        {
            EditorSceneManager.OpenScene(Root+"/Scenes/TherapyRoom.unity");
            Verify();
            EditorApplication.Exit(File.Exists(Report)&&File.ReadAllText(Report).StartsWith("PASS:")?0:1);
        }

        [MenuItem("Therapy Game/Verify Doorway Clearance")]
        public static void Verify()
        {
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Stop Play mode before checking doorway clearance.");
                var scene=EditorSceneManager.GetActiveScene();
                Require(scene.name=="TherapyRoom","Open TherapyRoom first.");
                var room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom");
                var chat=room.GetComponentInChildren<WellnessVoiceChat>(true);
                var door=room.GetComponentInChildren<WellnessDoor>(true);
                var navigation=room.GetComponentInChildren<WellnessCompanionNavigation>(true);
                Require(chat!=null&&door!=null&&door.leaf!=null&&navigation!=null&&navigation.data!=null,"Missing existing doorway/navigation references.");
                foreach(var actor in chat.therapists)
                    Require(actor!=null&&actor.movement!=null&&actor.movement.door==door&&door.companions.Contains(actor.GetComponent<CapsuleCollider>()),"Companion cannot receive doorway clearance requests.");
                CheckDoorCommands();
                int retreats=CheckRetreats(chat,door,navigation);
                Directory.CreateDirectory(Path.GetDirectoryName(Report));
                File.WriteAllText(Report,"PASS: actual door controller queues blocked opening and closing, retains motion while waiting, accepts reversal, resumes and finishes when the companion clears, and still rejects a player in the swing. Disabled companion colliders are ignored.\n"+
                    "PASS: "+retreats+" doorway positions have a short clear retreat on the existing navmesh, on the same side of the entrance, outside the complete door swing.\n"+
                    "PASS: both companion collider/movement/door references linked. Existing saved scene and navigation asset retained.\n"+
                    "Runtime movement, Stay/Follow modes and timing still require a Play-mode check.\nVerified "+DateTime.Now.ToString("s")+"\n");
                File.WriteAllText(Request,"verified");Debug.Log("COMPANION_DOOR_CLEARANCE_VERIFIED: "+Report);
            }
            catch(Exception error)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Report));File.WriteAllText(Report,"FAILED\n"+error);
                File.WriteAllText(Request,"needs-attention");Debug.LogException(error);
            }
        }

        static void CheckDoorCommands()
        {
            var root=new GameObject("Door clearance regression fixture"){hideFlags=HideFlags.HideAndDontSave};
            root.transform.position=new Vector3(2000,0,2000);
            try
            {
                var leafObject=new GameObject("Leaf");leafObject.transform.SetParent(root.transform,false);
                var leaf=leafObject.AddComponent<BoxCollider>();leaf.center=DoorSwingGeometry.Center;leaf.size=DoorSwingGeometry.Size;
                var playerObject=new GameObject("Player");playerObject.transform.SetParent(root.transform,false);
                var player=playerObject.AddComponent<CharacterController>();player.center=Vector3.up;player.height=2;player.radius=.3f;
                playerObject.transform.localPosition=new Vector3(-2,0,3);
                var companionObject=new GameObject("Companion");companionObject.transform.SetParent(root.transform,false);
                var companion=companionObject.AddComponent<CapsuleCollider>();companion.center=Vector3.up;companion.height=1.9f;companion.radius=.29f;
                companionObject.transform.localPosition=new Vector3(.6f,0,-.4f);
                var door=root.AddComponent<WellnessDoor>();door.leaf=leaf;door.player=player;door.companions=new[]{companion};
                Set(door,"initialRotation",Quaternion.identity);Set(door,"leafCenter",leaf.center);Set(door,"leafHalfSize",leaf.size*.5f);Set(door,"initialized",true);
                Require(door.Toggle().Contains("companion")&&door.WaitingForCompanion&&(bool)Get(door,"moving"),"Blocked opening was rejected rather than queued.");
                Tick(door);Require((bool)Get(door,"moving"),"Waiting cancelled the door command.");
                door.Toggle();Require(!(bool)Get(door,"targetOpen"),"Pending door command could not reverse.");
                door.Toggle();Require((bool)Get(door,"targetOpen"),"Pending opening could not be restored.");
                companionObject.transform.localPosition=new Vector3(3,0,3);
                Tick(door);
                Require(!door.WaitingForCompanion&&(float)Get(door,"angle")>0,"Door did not resume after clearance.");
                for(int frame=0;frame<100;frame++)Tick(door);
                Require(Mathf.Abs((float)Get(door,"angle")-door.openAngle)<.01f&&!door.IsMoving,"Door did not finish its queued opening.");
                companionObject.transform.localPosition=new Vector3(.6f,0,-.4f);
                Require(door.Toggle().Contains("companion")&&door.WaitingForCompanion&&!((bool)Get(door,"targetOpen")),"Blocked closing was not queued.");
                Tick(door);Require(door.IsMoving,"Waiting cancelled the close command.");
                companionObject.transform.localPosition=new Vector3(3,0,3);
                for(int frame=0;frame<100;frame++)Tick(door);
                Require(Mathf.Abs((float)Get(door,"angle"))<.01f&&!door.IsMoving,"Door did not finish its queued closing.");
                door.Toggle();for(int frame=0;frame<100;frame++)Tick(door);
                playerObject.transform.localPosition=new Vector3(.6f,0,-.4f);
                bool target=(bool)Get(door,"targetOpen");
                Require(door.Toggle().Contains("Step")&&(bool)Get(door,"targetOpen")==target,"Player swing protection was lost.");
                playerObject.transform.localPosition=new Vector3(-2,0,3);
                companionObject.transform.localPosition=new Vector3(.6f,0,-.4f);companion.enabled=false;
                Set(door,"targetOpen",false);Set(door,"angle",0f);
                Require(door.Toggle().Contains("Opening")&&!door.WaitingForCompanion,"Inactive companion still blocked the door.");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }

        static int CheckRetreats(WellnessVoiceChat chat,WellnessDoor door,WellnessCompanionNavigation navigation)
        {
            var actor=chat.therapists.First(a=>a!=null&&a.gameObject.activeInHierarchy);
            var movement=actor.movement;var body=actor.GetComponent<CapsuleCollider>();
            var center=Get(door,"leafCenter");var half=Get(door,"leafHalfSize");
            NavMeshDataInstance probe=NavMesh.AddNavMeshData(navigation.data);
            try
            {
                Set(door,"leafCenter",door.transform.InverseTransformPoint(door.leaf.transform.TransformPoint(door.leaf.center)));
                Vector3 scale=door.leaf.transform.lossyScale,hinge=door.transform.lossyScale;
                Set(door,"leafHalfSize",Vector3.Scale(door.leaf.size*.5f,new Vector3(scale.x/hinge.x,scale.y/hinge.y,scale.z/hinge.z)));
                int tested=0,inside=0,outside=0;
                Vector3[] positions={new Vector3(-2.43f,.025f,-3.08f),new Vector3(-2.43f,.025f,-3.65f),new Vector3(-2.8f,.025f,-3.85f),new Vector3(-2.2f,.025f,-3.95f)};
                Physics.SyncTransforms();
                foreach(var local in positions)
                {
                    Vector3 origin=chat.roomSpace.TransformPoint(local);
                    if(!door.BlocksCompanionSwing(body,origin-actor.transform.position))continue;
                    Require(movement.TryFindDoorClearance(origin,out var target),"No safe retreat from doorway position "+local+"\n"+DescribeRetreat(origin,movement,door,body));
                    bool wasInside=chat.IsInsideRoom(origin);
                    Require(chat.IsInsideRoom(target)==wasInside&&!door.BlocksCompanionSwing(body,target-actor.transform.position),"Retreat crosses the door or remains in its swing.");
                    tested++;if(wasInside)inside++;else outside++;
                }
                Require(tested>=2&&inside>0&&outside>0,"Doorway retreat coverage needs both sides of the entrance.");return tested;
            }
            finally{probe.Remove();Set(door,"leafCenter",center);Set(door,"leafHalfSize",half);}
        }

        static string DescribeRetreat(Vector3 origin,WellnessCompanionMovement movement,WellnessDoor door,CapsuleCollider body)
        {
            var filter=new NavMeshQueryFilter{agentTypeID=movement.agent.agentTypeID,areaMask=movement.agent.areaMask};
            if(!NavMesh.SamplePosition(origin,out var start,.3f,filter))return "Origin has no navmesh within .3 m.";
            var result=new System.Text.StringBuilder("Navmesh origin: "+start.position+"\n");
            Vector3 away=origin-door.transform.position;away.y=0;away.Normalize();
            foreach(float distance in new[]{.6f,1f,1.6f,2.2f})for(int direction=0;direction<16;direction++)
            {
                Vector3 candidate=origin+Quaternion.Euler(0,direction*22.5f,0)*away*distance;
                if(!NavMesh.SamplePosition(candidate,out var hit,.25f,filter))continue;
                Vector3 root=hit.position+Vector3.up*movement.agent.baseOffset;
                if(movement.actor.chat.IsInsideRoom(root)!=movement.actor.chat.IsInsideRoom(origin)||door.BlocksCompanionSwing(body,root-movement.transform.position))continue;
                Vector3 delta=root-origin;delta.y=0;
                var blockers=Physics.CapsuleCastAll(origin+Vector3.up*.55f,origin+Vector3.up*1.55f,.25f,delta.normalized,delta.magnitude+.07f,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>!h.collider.transform.IsChildOf(movement.transform)&&h.normal.y<=.65f).Select(h=>h.collider.name+" @ "+h.distance);
                result.AppendLine("Candidate "+root+" navBlocked="+NavMesh.Raycast(start.position,hit.position,out _,filter)+" blockers="+string.Join(", ",blockers));
            }
            return result.ToString();
        }
    }
}
