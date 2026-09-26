using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastWatch.Environment
{
    // Runs only in Play mode. Leaves the host project's saved graphics settings alone.
    public sealed class WellnessScenePipeline : MonoBehaviour
    {
        public RenderPipelineAsset pipeline;
        private RenderPipelineAsset _previous;
        private void OnEnable()
        {
            _previous = QualitySettings.renderPipeline;
            if (pipeline != null) QualitySettings.renderPipeline = pipeline;
        }
        private void OnDisable()
        {
            if (QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = _previous;
        }
    }
}
