using UnityEngine;

namespace TheLastWatch.Environment
{
    // Active only in Play mode; restore the host project's frame pacing on exit.
    [DisallowMultipleComponent]
    public sealed class WellnessGardenPerformance : MonoBehaviour
    {
        [Range(60, 240)] public int targetFrameRate = 120;
        private int previousRate, previousVsync, appliedRate;
        private bool applied;
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            previousRate = Application.targetFrameRate; previousVsync = QualitySettings.vSyncCount;
            appliedRate = Mathf.Clamp(targetFrameRate, 60, 240);
            // Desktop players follow the display refresh rate without tearing.
            // The editor ignores vSync, so also give its Game view a sensible ceiling.
            QualitySettings.vSyncCount = 1; Application.targetFrameRate = appliedRate;
            applied = true;
        }
        private void OnDisable()
        {
            if (!applied) return;
            if (Application.targetFrameRate == appliedRate) Application.targetFrameRate = previousRate;
            if (QualitySettings.vSyncCount == 1) QualitySettings.vSyncCount = previousVsync;
            applied = false;
        }
    }
}
