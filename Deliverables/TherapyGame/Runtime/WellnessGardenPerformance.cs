using UnityEngine;

namespace TheLastWatch.Environment
{
    // Active only in Play mode; restore the host project's frame pacing on exit.
    [DisallowMultipleComponent]
    public sealed class WellnessGardenPerformance : MonoBehaviour
    {
        private int previousRate, previousVsync;
        private void OnEnable()
        {
            previousRate = Application.targetFrameRate; previousVsync = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 30;
        }
        private void OnDisable()
        {
            if (Application.targetFrameRate == 30) Application.targetFrameRate = previousRate;
            if (QualitySettings.vSyncCount == 0) QualitySettings.vSyncCount = previousVsync;
        }
    }
}
