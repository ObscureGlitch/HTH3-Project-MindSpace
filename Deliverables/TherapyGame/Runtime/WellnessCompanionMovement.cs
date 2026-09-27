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
        public string Status => !Ready?"Waiting for a safe path":mode==TravelMode.Stay?"Staying here":
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
        private bool Ready=>spawnReady&&agent!=null&&agent.enabled&&agent.isOnNavMesh&&navigation!=null&&navigation.Ready;

        private void OnEnable()
        {
            if(!Application.isPlaying)return;
            path=new NavMeshPath();
            initialPosition=transform.position;initialRotation=transform.rotation;initialized=true;
            destination=firstDestination;nextPlan=0;restUntil=Time.time+2;attentionUntil=0;
            following=blocked=hadDestination=false;Speed=0;
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
            else if(mode==TravelMode.Follow)Face(player-transform.position);
        }

        private bool Plan(Vector3 target,float radius)
        {
            if(path==null)path=new NavMeshPath();
            if(!NavMesh.SamplePosition(target,out NavMeshHit hit,radius,agent.areaMask)||
                !agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
            hadDestination=agent.SetPath(path);return hadDestination;
        }
        private bool StepClear(Vector3 delta)
        {
            // The navmesh handles steps up to .22 m; start the body sweep above those low risers.
            Vector3 bottom=transform.position+Vector3.up*.55f,top=transform.position+Vector3.up*1.55f;
            int count=Physics.CapsuleCastNonAlloc(bottom,top,.25f,delta.normalized,hits,delta.magnitude+.07f,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)
            {
                if(hits[i].collider.transform.IsChildOf(transform))continue;
                // Walkable slopes are handled by the baked navigation surface.
                if(hits[i].normal.y>.65f)continue;
                return false;
            }
            return true;
        }
        private void Face(Vector3 toward)
        {
            toward.y=0;if(toward.sqrMagnitude<.001f)return;
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(toward),180*Time.deltaTime);
        }
        private void OnDisable()
        {
            Speed=0;
            roomPolicy.Reset();spawnReady=false;waitingIndoors=true;
            if(agent!=null)agent.enabled=false;
            if(initialized){transform.SetPositionAndRotation(initialPosition,initialRotation);initialized=false;}
        }
    }
}
