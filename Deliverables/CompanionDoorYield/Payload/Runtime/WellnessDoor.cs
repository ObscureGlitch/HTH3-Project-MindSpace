using UnityEngine;

namespace TheLastWatch.Interaction
{
    /// <summary>One existing hinged door; no physics simulation or extra render work.</summary>
    [DisallowMultipleComponent]
    public sealed class WellnessDoor : MonoBehaviour
    {
        public BoxCollider leaf;
        public CharacterController player;
        public CapsuleCollider[] companions=new CapsuleCollider[0];
        [Range(30, 140)] public float openAngle = 105;
        [Range(20, 120)] public float degreesPerSecond = 70;
        private Quaternion initialRotation;
        private Vector3 leafCenter, leafHalfSize;
        private float angle;
        private bool targetOpen, moving, initialized;
        private bool waitingForCompanion;
        public bool WaitingForCompanion => waitingForCompanion;
        public bool IsMoving => moving;
        public string Prompt => targetOpen ? "Close door" : "Open door";
        // A player's close command takes effect immediately, before the swing finishes.
        public bool AllowsCompanionPassage => initialized&&isActiveAndEnabled&&
            TheLastWatch.Integrations.WellnessCompanionRoomPolicy.DoorAllowsPassage(targetOpen,angle);
        // Actual opening, not the target: a door stopped by the player still transmits sound.
        public float VoiceOpening => OpeningForAngle(initialized ? angle : Mathf.DeltaAngle(0, transform.localEulerAngles.y));
        public static float OpeningForAngle(float degrees) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(5, 65, Mathf.Abs(degrees)));

        private void OnEnable()
        {
            if (!Application.isPlaying || leaf == null || player == null) return;
            initialRotation = transform.localRotation;
            angle = Mathf.Clamp(Mathf.DeltaAngle(0, transform.localEulerAngles.y), 0, openAngle);
            targetOpen = angle > openAngle * .5f; moving = false; initialized = true;
            leafCenter = transform.InverseTransformPoint(leaf.transform.TransformPoint(leaf.center));
            Vector3 relativeScale = leaf.transform.lossyScale;
            Vector3 hingeScale = transform.lossyScale;
            relativeScale = new Vector3(relativeScale.x / hingeScale.x, relativeScale.y / hingeScale.y, relativeScale.z / hingeScale.z);
            leafHalfSize = Vector3.Scale(leaf.size * .5f, relativeScale);
        }

        // Conservative upright-capsule vs rotated leaf test, also usable by CPU-only
        // installer checks. Include the handles and an extra stand-clear margin.
        public static bool BlocksLeaf(Vector3 playerCenter, float radius, float halfHeight,
            Vector3 center, Vector3 halfSize, float yaw)
        {
            Vector3 p = Quaternion.Euler(0, -yaw, 0) * playerCenter - center;
            if (Mathf.Abs(p.y) > halfSize.y + halfHeight + .04f) return false;
            float dx = Mathf.Max(Mathf.Abs(p.x) - halfSize.x - .04f, 0);
            float dz = Mathf.Max(Mathf.Abs(p.z) - halfSize.z - .09f, 0);
            return dx * dx + dz * dz <= (radius + .06f) * (radius + .06f);
        }

        private bool PlayerArcClear(float from, float to)
        {
            Vector3 world = player.transform.TransformPoint(player.center) - transform.position;
            Quaternion parentRotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            Vector3 local = Quaternion.Inverse(parentRotation) * world;
            float radius = player.radius * Mathf.Max(player.transform.lossyScale.x, player.transform.lossyScale.z);
            float halfHeight = player.height * player.transform.lossyScale.y * .5f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(to - from) / 2f));
            for (int i = 0; i <= steps; i++)
                if (BlocksLeaf(local, radius, halfHeight, leafCenter, leafHalfSize, Mathf.Lerp(from, to, i / (float)steps))) return false;
            return true;
        }

        private bool CompanionArcClear(float from, float to)
        {
            foreach(var companion in companions)
            {
                if(CompanionBlocksArc(companion,Vector3.zero,from,to))return false;
            }
            return true;
        }

        private bool CompanionBlocksArc(CapsuleCollider companion,Vector3 offset,float from,float to)
        {
            if(companion==null||!companion.enabled||!companion.gameObject.activeInHierarchy)return false;
            Quaternion parentRotation=transform.parent!=null?transform.parent.rotation:Quaternion.identity;
            Vector3 relative=Quaternion.Inverse(parentRotation)*(companion.transform.TransformPoint(companion.center)+offset-transform.position);
            float r=companion.radius*Mathf.Max(companion.transform.lossyScale.x,companion.transform.lossyScale.z);
            float h=companion.height*companion.transform.lossyScale.y*.5f;
            int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(to-from)/2f));
            for(int i=0;i<=steps;i++)
                if(BlocksLeaf(relative,r,h,leafCenter,leafHalfSize,Mathf.Lerp(from,to,i/(float)steps)))return true;
            return false;
        }

        // A retreat must clear the whole swing, so reopening is safe as well.
        public bool BlocksCompanionSwing(CapsuleCollider companion,Vector3 offset)
            =>CompanionBlocksArc(companion,offset,0,openAngle);

        private void RequestCompanionClearance()
        {
            foreach(var companion in companions)
                if(BlocksCompanionSwing(companion,Vector3.zero))
                {
                    var movement=companion.GetComponentInParent<TheLastWatch.Integrations.WellnessCompanionMovement>();
                    if(movement!=null)movement.RequestDoorClearance(this);
                }
        }

        public string Toggle()
        {
            if (!initialized) return "The door is not ready yet.";
            float destination = targetOpen ? 0 : openAngle;
            if (!PlayerArcClear(angle, destination)) return "Step a little clear of the door, then press E again.";
            targetOpen = !targetOpen; moving = true;
            waitingForCompanion=!CompanionArcClear(angle,destination);
            if(waitingForCompanion)
            {
                RequestCompanionClearance();
                return "Your companion is stepping clear of the door.";
            }
            return targetOpen ? "Opening the door." : "Closing the door.";
        }

        private void Update()
        {
            if (!initialized || !moving || !Application.isFocused) return;
            TickMotion(Mathf.Min(Time.deltaTime,.05f));
        }

        private void TickMotion(float deltaTime)
        {
            float destination = targetOpen ? openAngle : 0;
            if(waitingForCompanion)
            {
                if(!CompanionArcClear(angle,destination)){RequestCompanionClearance();return;}
                waitingForCompanion=false;
            }
            float next = Mathf.MoveTowards(angle, destination, degreesPerSecond * deltaTime);
            // Recheck every small step in case the player walks into the swing arc.
            if (!PlayerArcClear(angle, next)) { moving = false; return; }
            if(!CompanionArcClear(angle,next))
            {
                waitingForCompanion=true;RequestCompanionClearance();return;
            }
            angle = next; transform.localRotation = Quaternion.Euler(0, angle, 0);
            if (Mathf.Abs(angle - destination) < .01f) moving = false;
        }

        private void OnDisable()
        {
            if (initialized) transform.localRotation = initialRotation;
            initialized = moving = waitingForCompanion = false;
        }
    }
}
