using UnityEngine;

namespace TheLastWatch.Environment
{
    [DisallowMultipleComponent]
    public sealed class WellnessButterfly : MonoBehaviour
    {
        public Transform leftWing, rightWing;
        public Vector3 home;
        public float radius = 1, phase;
        [Range(5, 12)] public float flapHz = 7.8f;
        [Range(.2f, .8f)] public float orbitSpeed = .38f;
        public void Sample(float seconds)
        {
            float a = seconds * orbitSpeed + phase;
            transform.localPosition = home + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a * 2.3f) * .12f, Mathf.Sin(a) * radius * .72f);
            // The authored head and forewings point along local -Z, not +Z.
            // Follow the actual ellipse tangent, including its unequal X/Z radii.
            float yaw = Mathf.Atan2(Mathf.Sin(a), -.72f * Mathf.Cos(a)) * Mathf.Rad2Deg;
            transform.localRotation = Quaternion.Euler(0, yaw, 0);
            float flap = (.18f + Mathf.Sin(seconds * flapHz * Mathf.PI * 2 + phase) * 1.15f) * Mathf.Rad2Deg;
            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0, 0, flap);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0, 0, -flap);
        }
        private void Update() => Sample(Time.time);
    }
}
