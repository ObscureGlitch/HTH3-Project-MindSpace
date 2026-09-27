using UnityEngine;

namespace TheLastWatch.Integrations
{
    // Memory-only, per play session. No PlayerPrefs, automatic permission grants or mic access.
    public sealed class WellnessVoiceConsent
    {
        public bool Decided { get; private set; }
        public bool Granted { get; private set; }
        public bool ListeningRequested { get; private set; }
        public bool Muted { get; private set; }
        public void Choose(bool enable)
        { Decided = true; Granted = enable; ListeningRequested = enable; Muted = false; }
        public void Pause() => ListeningRequested = false;
        public void Resume() { if (Granted) { ListeningRequested = true; Muted = false; } }
        public void SetMuted(bool value) => Muted = value;
        public bool CanConnect(bool inRange, bool focused, bool busy) =>
            Decided && Granted && ListeningRequested && !Muted && inRange && focused && !busy;
    }

    public static class WellnessVoiceAcoustics
    {
        public static bool Evaluate(Bounds room, Vector3 listener, Vector3 speaker, Vector3 doorway,
            float opening, float outdoorRange, bool exteriorPathClear, out bool indoors, out float distance)
        {
            indoors = room.Contains(listener);
            distance = Vector3.Distance(listener, speaker);
            if (indoors) return true; // Furniture is not a soundproof wall within this single room.
            float outsideDistance = Vector3.Distance(listener, doorway);
            distance = Vector3.Distance(speaker, doorway) + outsideDistance;
            // This house's entrance faces local -Z. Do not listen through the back/side walls.
            return opening > .02f && exteriorPathClear && listener.z <= room.min.z + .12f &&
                listener.y >= room.min.y - .5f && listener.y <= room.max.y &&
                outdoorRange > 0 && outsideDistance < outdoorRange;
        }

        public static float Gain(float distance, bool indoors, float opening, float doorDistance, float outdoorRange)
        {
            float falloff = 1f / (1f + .22f * Mathf.Max(0, distance - 1f));
            if (indoors) return .85f * falloff;
            float edge = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(outdoorRange - 1.5f, outdoorRange, doorDistance));
            return .85f * falloff * .85f * Mathf.Clamp01(opening) * edge;
        }
    }
}
