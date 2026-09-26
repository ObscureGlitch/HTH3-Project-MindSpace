using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastWatch.Environment
{
    /// <summary>Restrained distance haze, scoped to this camera instead of the whole project.</summary>
    [ExecuteAlways, RequireComponent(typeof(Camera)), DisallowMultipleComponent]
    public sealed class WellnessAtmosphere : MonoBehaviour
    {
        [Range(0f, .08f)] public float density = 0f;
        public Color hazeColor = new Color(.74f, .83f, .81f, 1f);
        public bool hazeEnabled = true;
        public bool gardenTransition;
        public float outdoorDensity = .009f;
        public Color outdoorHazeColor = new Color(.74f, .83f, .81f, 1);
        private Camera _camera;
        private bool _applied, _oldFog;
        private FogMode _oldMode;
        private Color _oldColor;
        private float _oldDensity, _oldStart, _oldEnd;
        public static float OutdoorBlend(Vector3 position) => Mathf.SmoothStep(0, 1,
            Mathf.Clamp01(Mathf.Max(Mathf.Abs(position.x) - 3.6f, Mathf.Abs(position.z) - 3.1f) / 2.5f));

        public void SampleFog(Vector3 position, out Color color, out float amount)
        {
            // The weather cycle updates this color at night too. Never switch to an
            // unrelated orange screen tint just because the viewer crossed the door.
            color = gardenTransition ? outdoorHazeColor : hazeColor;
            amount = gardenTransition ? Mathf.Lerp(density, outdoorDensity, OutdoorBlend(position)) : density;
        }
        private void OnEnable()
        {
            _camera = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            Restore();
        }
        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camera || !hazeEnabled || _applied) return;
            _oldFog = RenderSettings.fog; _oldMode = RenderSettings.fogMode;
            _oldColor = RenderSettings.fogColor; _oldDensity = RenderSettings.fogDensity;
            _oldStart = RenderSettings.fogStartDistance; _oldEnd = RenderSettings.fogEndDistance;
            _applied = true;
            SampleFog(camera.transform.position, out Color color, out float amount);
            RenderSettings.fog = amount > .00001f; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = color;
            RenderSettings.fogDensity = amount;
        }
        private void EndCamera(ScriptableRenderContext context, Camera camera) { if (camera == _camera) Restore(); }
        private void Restore()
        {
            if (!_applied) return;
            RenderSettings.fog = _oldFog; RenderSettings.fogMode = _oldMode;
            RenderSettings.fogColor = _oldColor; RenderSettings.fogDensity = _oldDensity;
            RenderSettings.fogStartDistance = _oldStart; RenderSettings.fogEndDistance = _oldEnd;
            _applied = false;
        }
    }
}
