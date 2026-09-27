using System;
using UnityEngine;

namespace TheLastWatch.Interaction
{
    /// <summary>Selectable positions on one existing piece of furniture. No extra blocking colliders.</summary>
    [DisallowMultipleComponent]
    public sealed class WellnessSeat : MonoBehaviour
    {
        [Serializable]
        public sealed class Spot
        {
            public string label = "Seat";
            public Vector3 localBodyPosition = new Vector3(0, .04f, .10f);
            public float eyeHeight = 1.21f;
            public float localYaw;
            [Tooltip("Occupied by an AI companion, not available to the player.")]
            public bool reserved;
            public Vector3 localStandPosition = new Vector3(0, .04f, 1.15f);
        }

        public Spot[] spots = { new Spot() };
        public int Count => spots == null ? 0 : spots.Length;
        public bool Valid(int index) => isActiveAndEnabled && index >= 0 && index < Count && spots[index] != null && !spots[index].reserved;
        public string Label(int index) => Valid(index) ? spots[index].label : "Seat";
        public Vector3 BodyPosition(int index) => transform.TransformPoint(spots[index].localBodyPosition);
        public Vector3 AimPosition(int index) => BodyPosition(index) + transform.up * .62f;
        public Quaternion Facing(int index) => Quaternion.Euler(0, transform.eulerAngles.y + spots[index].localYaw, 0);
        public Vector3 StandPosition(int index) => transform.TransformPoint(spots[index].localStandPosition);

        public int NearestSpot(Vector3 hitPoint)
        {
            int best = -1; float nearest = float.PositiveInfinity;
            for (int i = 0; i < Count; i++)
            {
                if (!Valid(i)) continue;
                float distance = (hitPoint - AimPosition(i)).sqrMagnitude;
                if (distance < nearest) { nearest = distance; best = i; }
            }
            return best;
        }

        // Alternative exits are used only when the player's original approach point becomes blocked.
        public Vector3 ExitCandidate(int index, int candidate)
        {
            Vector3 p = spots[index].localStandPosition;
            if (candidate == 1) p.x -= .55f;
            if (candidate == 2) p.x += .55f;
            if (candidate == 3) p.z += .55f;
            return transform.TransformPoint(p);
        }

        private void OnDrawGizmosSelected()
        {
            for (int i = 0; i < Count; i++)
            {
                if (!Valid(i)) continue;
                Gizmos.color = new Color(.75f, .85f, .45f);
                Vector3 eye = BodyPosition(i) + Vector3.up * spots[i].eyeHeight;
                Gizmos.DrawWireSphere(eye, .09f); Gizmos.DrawLine(eye, eye + Facing(i) * Vector3.forward * .4f);
                Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(StandPosition(i), .24f);
            }
        }
    }
}
