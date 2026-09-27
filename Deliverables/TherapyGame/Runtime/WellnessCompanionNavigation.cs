using UnityEngine;
using UnityEngine.AI;

namespace TheLastWatch.Integrations
{
    [DefaultExecutionOrder(-200)]
    public sealed class WellnessCompanionNavigation : MonoBehaviour
    {
        public NavMeshData data;
        public Vector3[] destinations = new Vector3[0];
        // Shared safe floor position in room-local space; only one model is active at a time.
        public Vector3 localIndoorSpawn = new Vector3(1.55f,.02f,1.12f);
        private NavMeshDataInstance instance;
        public bool Ready => instance.valid;

        private void OnEnable()
        {
            if(Application.isPlaying&&data!=null&&!instance.valid)instance=NavMesh.AddNavMeshData(data);
        }
        private void OnDisable(){if(instance.valid)instance.Remove();}
    }
}
