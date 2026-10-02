using UnityEngine;
using UnityEngine.AI;
using TheLastWatch.Interaction;

namespace TheLastWatch.Integrations
{
    [DefaultExecutionOrder(-50), DisallowMultipleComponent]
    public sealed class WellnessCompanionMovement : MonoBehaviour
    {
        public enum TravelMode { Explore, Follow, Stay }
        public WellnessTherapist actor;
        public WellnessCompanionNavigation navigation;
        public NavMeshAgent agent;
        public WellnessDoor door;
        public TravelMode mode=TravelMode.Explore;
        public float walkSpeed=1.25f,followSpeed=1.85f;
        public int firstDestination;
        public float Speed {get;private set;}
        public bool IsMoving => Speed>.06f;
        public string Status => !Ready?"Waiting for a safe path":yieldingDoor?"Making room for the door":mode==TravelMode.Stay?"Staying here":
            waitingIndoors?(door!=null&&door.AllowsCompanionPassage?"Waiting for you to be outside for more than 5 seconds":"Staying in the room · door closed or closing"):
            blocked?"Waiting for the path to clear":
            mode==TravelMode.Follow?"Following you":IsMoving?"Exploring the garden":"Taking a moment";
        private readonly RaycastHit[] hits=new RaycastHit[48];
        private readonly WellnessCompanionRoomPolicy roomPolicy=new WellnessCompanionRoomPolicy();
        private NavMeshPath path;
        private int destination;
        private float nextPlan,restUntil,attentionUntil,blockedSince;
        private bool following,blocked,hadDestination;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private bool initialized,spawnReady,waitingIndoors;
        private bool yieldingDoor;
        private CapsuleCollider body;
        private float nextYieldPlan;
        private bool Ready=>spawnReady&&agent!=null&&agent.enabled&&agent.isOnNavMesh&&navigation!=null&&navigation.Ready;

        private void OnEnable()
        {
            if(!Application.isPlaying)return;
            path=new NavMeshPath();
            initialPosition=transform.position;initialRotation=transform.rotation;initialized=true;
            destination=firstDestination;nextPlan=0;restUntil=Time.time+2;attentionUntil=0;
            following=blocked=hadDestination=false;Speed=0;
            yieldingDoor=false;body=GetComponent<CapsuleCollider>();
            roomPolicy.Reset();waitingIndoors=true;spawnReady=false;
            // Navigation data is registered before agents are enabled, including inactive companions.
            if(navigation!=null&&navigation.Ready&&agent!=null&&actor!=null&&actor.chat!=null&&actor.chat.roomSpace!=null)
            {
                agent.enabled=false;
                Vector3 indoor=actor.chat.roomSpace.TransformPoint(navigation.localIndoorSpawn);
                if(NavMesh.SamplePosition(indoor,out var start,.6f,agent.areaMask)&&actor.chat.IsInsideRoom(start.position))
                {
                    // Also handles older scenes where Camille was saved outdoors.
                    transform.position=start.position+Vector3.up*agent.baseOffset;
                    agent.updatePosition=false;agent.updateRotation=false;agent.enabled=true;
                    spawnReady=agent.isOnNavMesh;
                    if(spawnReady)agent.nextPosition=transform.position;
                }
            }
        }

        public void SetMode(TravelMode value)
        {
            mode=value;following=false;hadDestination=false;nextPlan=0;restUntil=Time.time+.5f;
            attentionUntil=0;Stop();
        }
        public void RequestAttention(){attentionUntil=Time.time+12;Stop();}
        public void RequestDoorClearance(WellnessDoor source)
        {
            if(source!=door||!Ready||body==null||yieldingDoor)return;
            yieldingDoor=true;nextYieldPlan=0;hadDestination=following=blocked=false;
            Stop();agent.ResetPath();
        }

        // Search short, straight walks on the existing navigation surface. Keep
        // the companion on its current side of the doorway, including when shut.
        public bool TryFindDoorClearance(Vector3 origin,out Vector3 target)
        {
            target=origin;
            if(body==null)body=GetComponent<CapsuleCollider>();
            if(door==null||agent==null||actor==null||actor.chat==null||body==null)return false;
            var chat=actor.chat;bool inside=chat.IsInsideRoom(origin);
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            if(!NavMesh.SamplePosition(origin,out var start,.3f,filter))return false;
            Vector3 away=origin-door.transform.position;away.y=0;
            if(away.sqrMagnitude<.001f)away=Vector3.forward;else away.Normalize();
            for(int ring=0;ring<4;ring++)for(int direction=0;direction<16;direction++)
            {
                float distance=ring==0?.6f:ring==1?1f:ring==2?1.6f:2.2f;
                Vector3 candidate=origin+Quaternion.Euler(0,direction*22.5f,0)*away*distance;
                if(!NavMesh.SamplePosition(candidate,out var hit,.25f,filter))continue;
                Vector3 root=hit.position+Vector3.up*agent.baseOffset;
                if(chat.IsInsideRoom(root)!=inside||door.BlocksCompanionSwing(body,root-transform.position))continue;
                if(chat.player!=null&&Vector3.Distance(root,chat.player.transform.position)<.85f)continue;
                if(NavMesh.Raycast(start.position,hit.position,out _,filter))continue;
                Vector3 delta=root-origin;delta.y=0;
                if(!StepClearFrom(origin,delta,true))continue;
                // The room footprint is convex, but exterior points can straddle
                // its corners. Sample the segment to forbid crossing a shut door.
                bool sameSide=true;int samples=Mathf.CeilToInt(delta.magnitude/.15f);
                for(int i=1;i<samples;i++)
                    if(chat.IsInsideRoom(Vector3.Lerp(origin,root,i/(float)samples))!=inside){sameSide=false;break;}
                if(!sameSide)continue;
                target=root;return true;
            }
            return false;
        }
        public static bool ShouldFollow(bool wasFollowing,float distance)=>distance>(wasFollowing?1.9f:3f);
        public static bool ShouldPause(TravelMode mode,bool userInterface,bool attention,bool nearby,bool conversing)
            =>userInterface||mode==TravelMode.Stay||attention||(mode==TravelMode.Explore&&(nearby||conversing));

        private void Stop()
        {
            Speed=0;
            if(Ready){agent.isStopped=true;agent.nextPosition=transform.position;}
        }
        private void Update()
        {
            if(!Application.isPlaying||!Ready||actor==null||actor.chat==null){Speed=0;return;}
            WellnessVoiceChat chat=actor.chat;
            if(chat.player==null){Stop();return;}
            Vector3 player=chat.player.transform.position;
            if(chat.roomSpace==null){Stop();return;}
            bool playerInside=chat.IsInsideRoom(player),inside=chat.IsInsideRoom(transform.position);
            bool doorAllows=door!=null&&door.AllowsCompanionPassage;
            roomPolicy.Observe(playerInside,Application.isFocused&&!chat.IsPanelOpen,Mathf.Min(Time.deltaTime,.25f));
            waitingIndoors=inside&&!roomPolicy.CanLeave(doorAllows);
            // Do not park where the companion can hide the door interaction or
            // block its next opening. A moving companion may traverse an open
            // entrance; a closed entrance remains clear on both sides.
            if(!yieldingDoor&&door!=null&&door.isActiveAndEnabled&&(!doorAllows||!IsMoving)&&door.BlocksCompanionSwing(body,Vector3.zero))
                RequestDoorClearance(door);
            if(yieldingDoor)
            {
                if(door==null||!door.isActiveAndEnabled||!door.BlocksCompanionSwing(body,Vector3.zero))
                {
                    // Hold the clear spot until the swing completes rather than
                    // resuming Follow/Explore and walking straight back into it.
                    if(door!=null&&door.isActiveAndEnabled&&door.IsMoving){Stop();return;}
                    yieldingDoor=false;agent.ResetPath();hadDestination=following=blocked=false;nextPlan=0;
                    Stop();return;
                }
                // Yielding takes priority over Stay, attention and the indoor
                // wait, but still pauses with menus, focus loss and game time.
                if(!Application.isFocused||chat.IsPanelOpen||Time.deltaTime<=0){Stop();return;}
                if(Time.time>=nextYieldPlan)
                {
                    nextYieldPlan=Time.time+.75f;
                    agent.ResetPath();hadDestination=false;
                    if(!TryFindDoorClearance(transform.position,out var retreat)||!Plan(retreat,.25f)){Stop();return;}
                }
                agent.speed=walkSpeed;
                Advance(inside,false);return;
            }
            if(waitingIndoors)
            {
                Stop();agent.ResetPath();hadDestination=following=blocked=false;nextPlan=0;
                if(playerInside)Face(player-transform.position);
                return;
            }
            float distance=Vector3.Distance(player,transform.position);
            bool selected=chat.SelectedIndex==actor.voiceIndex;
            bool conversing=selected&&(chat.IsConnected||chat.IsBusy);
            bool nearby=distance<2.7f&&chat.HasClearVoicePath(actor);
            if(!Application.isFocused||ShouldPause(mode,chat.IsPanelOpen,Time.time<attentionUntil,nearby,conversing))
            {
                Stop();if(nearby||conversing)Face(player-transform.position);return;
            }

            if(mode==TravelMode.Follow)
            {
                following=ShouldFollow(following,distance);
                if(!following){Stop();Face(player-transform.position);return;}
                agent.speed=followSpeed;
                if(Time.time>=nextPlan)
                {
                    nextPlan=Time.time+.45f;
                    // Keep the selected companion slightly to one side, not on the player's heels.
                    Vector3 target=player-chat.player.transform.forward*1.8f+
                        chat.player.transform.right*(actor.voiceIndex==0?-.7f:.7f);
                    if(!Plan(target,1.3f)&&!Plan(player,.7f)){agent.ResetPath();hadDestination=false;Stop();blocked=true;return;}
                }
            }
            else
            {
                agent.speed=walkSpeed;
                if(Time.time<restUntil){Stop();return;}
                if(hadDestination&&!agent.pathPending&&agent.remainingDistance<.4f)
                {
                    agent.ResetPath();hadDestination=false;restUntil=Time.time+4+actor.voiceIndex;Stop();return;
                }
                if((!hadDestination||!agent.hasPath)&&Time.time>=nextPlan)
                {
                    nextPlan=Time.time+1;
                    Vector3[] stops=navigation.destinations;
                    for(int attempt=0;attempt<stops.Length;attempt++)
                    {
                        Vector3 goal=stops[destination++%stops.Length];
                        if(Vector3.Distance(goal,transform.position)>2&&Plan(goal,.8f))break;
                    }
                    if(!hadDestination){Stop();return;}
                }
            }

            Advance(inside,doorAllows);
        }

        private void Advance(bool inside,bool doorAllows)
        {
            var chat=actor.chat;
            if(!agent.hasPath||agent.pathPending){Stop();return;}
            agent.isStopped=false;
            Vector3 next=agent.nextPosition,delta=next-transform.position;
            Vector3 horizontal=new Vector3(delta.x,0,delta.z);
            bool crossingClosedDoor=inside!=chat.IsInsideRoom(next)&&!doorAllows;
            if(crossingClosedDoor||(horizontal.sqrMagnitude>.000001f&&!StepClear(horizontal)))
            {
                if(!blocked)blockedSince=Time.time;
                blocked=true;Stop();
                if(mode==TravelMode.Explore&&Time.time-blockedSince>3)
                {agent.ResetPath();hadDestination=false;nextPlan=Time.time+.5f;blocked=false;}
                return;
            }
            blocked=false;
            transform.position=next;
            Speed=Time.deltaTime>0?horizontal.magnitude/Time.deltaTime:0;
            if(horizontal.sqrMagnitude>.000001f)Face(horizontal);
            else if(mode==TravelMode.Follow)Face(chat.player.transform.position-transform.position);
        }

        private bool Plan(Vector3 target,float radius)
        {
            if(path==null)path=new NavMeshPath();
            if(!NavMesh.SamplePosition(target,out NavMeshHit hit,radius,agent.areaMask))return false;
            if(door!=null&&!door.AllowsCompanionPassage&&actor!=null&&actor.chat!=null&&
                actor.chat.IsInsideRoom(transform.position)!=actor.chat.IsInsideRoom(hit.position))return false;
            if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
            hadDestination=agent.SetPath(path);return hadDestination;
        }
        private bool StepClear(Vector3 delta)
            =>StepClearFrom(transform.position,delta,yieldingDoor);
        private bool StepClearFrom(Vector3 origin,Vector3 delta,bool allowDoorEscape=false)
        {
            // The navmesh handles steps up to .22 m; start the body sweep above those low risers.
            Vector3 bottom=origin+Vector3.up*.55f,top=origin+Vector3.up*1.55f;
            int count=Physics.CapsuleCastNonAlloc(bottom,top,.25f,delta.normalized,hits,delta.magnitude+.07f,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)
            {
                if(hits[i].collider.transform.IsChildOf(transform))continue;
                // A leaf can already overlap the companion when a saved/open
                // door or a close command catches it. Permit only movement that
                // reduces that overlap; never ignore a new door hit, walls or
                // the player. Otherwise every escape cast starts blocked at 0 m.
                if(allowDoorEscape&&hits[i].distance<=.001f&&door!=null&&hits[i].collider==door.leaf&&
                    MovesOutOfDoor(origin,delta))continue;
                // Walkable slopes are handled by the baked navigation surface.
                if(hits[i].normal.y>.65f)continue;
                return false;
            }
            return true;
        }
        private bool MovesOutOfDoor(Vector3 origin,Vector3 delta)
        {
            if(body==null||door==null||door.leaf==null||delta.sqrMagnitude<.000001f)return false;
            var leaf=door.leaf;
            if(!Physics.ComputePenetration(body,origin,transform.rotation,leaf,leaf.transform.position,leaf.transform.rotation,
                out var direction,out float depth)||Vector3.Dot(delta.normalized,direction)<.05f)return false;
            return !Physics.ComputePenetration(body,origin+delta,transform.rotation,leaf,leaf.transform.position,leaf.transform.rotation,
                out _,out float nextDepth)||nextDepth<depth-.000001f;
        }
        private void Face(Vector3 toward)
        {
            toward.y=0;if(toward.sqrMagnitude<.001f)return;
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(toward),180*Time.deltaTime);
        }
        private void OnDisable()
        {
            Speed=0;
            roomPolicy.Reset();spawnReady=false;waitingIndoors=true;yieldingDoor=false;
            if(agent!=null)agent.enabled=false;
            if(initialized){transform.SetPositionAndRotation(initialPosition,initialRotation);initialized=false;}
        }
    }
}
