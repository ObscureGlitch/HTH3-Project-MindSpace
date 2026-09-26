using UnityEngine;

namespace TheLastWatch.Environment
{
    public sealed class WellnessBreathingOrb : MonoBehaviour
    {
        [SerializeField] private float cycleSeconds = 10f;
        [SerializeField] private float expansion = 0.12f;
        private Vector3 _restScale;
        public bool IsBreathing { get; private set; } = true;
        private void Awake() => _restScale = transform.localScale;
        public void Toggle() => IsBreathing = !IsBreathing;
        private void Update()
        {
            float wave = IsBreathing ? (1f - Mathf.Cos(Time.time * Mathf.PI * 2f / cycleSeconds)) * 0.5f : 0f;
            transform.localScale = _restScale * (1f + wave * expansion);
        }
    }
}
