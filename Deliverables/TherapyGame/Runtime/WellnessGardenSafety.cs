using TheLastWatch.Player;
using UnityEngine;

namespace TheLastWatch.Environment
{
    [RequireComponent(typeof(CharacterController)), DisallowMultipleComponent]
    public sealed class WellnessGardenSafety : MonoBehaviour
    {
        private CharacterController controller;
        private WellnessExplorer explorer;
        private Vector3 safe;
        private void Awake() { controller = GetComponent<CharacterController>(); explorer = GetComponent<WellnessExplorer>(); safe = transform.position; }
        private void LateUpdate()
        {
            if (explorer != null && (explorer.IsSeated || explorer.IsTransitioning)) return;
            Vector3 p = transform.position;
            if (p.y < -2.5f || p.x < -13 || p.x > 33 || p.z < -23 || p.z > 17)
            {
                bool enabled = controller.enabled; controller.enabled = false;
                transform.position = safe + Vector3.up * .08f; controller.enabled = enabled;
            }
            else if (controller.enabled && controller.isGrounded) safe = p;
        }
    }
}
