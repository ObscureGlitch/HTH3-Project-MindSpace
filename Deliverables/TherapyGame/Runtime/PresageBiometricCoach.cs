using System;
using System.Globalization;
using UnityEngine;

namespace TheLastWatch.Integrations
{
    [DisallowMultipleComponent]
    public sealed class PresageBiometricCoach : MonoBehaviour
    {
        public const string AgentPolicy =
            "Private MindSpace sensor context is available with the player's consent. Treat all measurements as approximate general-wellness signals, never as a diagnosis or proof of anxiety, panic, deception, or danger. Refer to stable trends relative to this player's own session baseline, not population norms. When gentle guidance is requested, speak slowly and give one concrete step at a time: orient to the room, release jaw and shoulders, then invite comfortable breathing with a slightly longer exhale and no forced breath holds. Ask how it feels before continuing. Tell the player to stop if dizzy or uncomfortable. For severe pain, trouble breathing, fainting, or immediate danger, advise contacting local emergency help rather than continuing an exercise.";

        public PresageBiometricProvider provider;
        public WellnessVoiceChat chat;
        [Range(0, 100)] public float minimumConfidence = 60;
        [Range(4, 30)] public int baselineSamples = 8;
        [Range(.4f, 1)] public float elevatedThreshold = .68f;
        [Range(8, 60)] public float sustainedSeconds = 18;
        [Range(45, 300)] public float coachingCooldownSeconds = 120;
        [Range(3, 30)] public float contextIntervalSeconds = 8;

        public bool BaselineReady => PulseBaselineReady || BreathingBaselineReady;
        public float Activation { get; private set; }
        public float BaselinePulse => baselinePulse;
        public float BaselineBreathing => baselineBreathing;

        private int pulseBaselineSamples, breathingBaselineSamples;
        private float baselinePulse, baselineBreathing;
        private bool hasPulseBaseline, hasBreathingBaseline;
        private float elevatedSince = -1, lastGuidanceAt = -1000, lastContextAt = -1000;

        private void Update()
        {
            // Lost framing, pause, or stale data breaks the sustained-change window.
            if (provider == null || !provider.HasFreshSample)
            {
                elevatedSince = -1;
                Activation = 0;
            }
        }

        private bool PulseBaselineReady => hasPulseBaseline && pulseBaselineSamples >= baselineSamples;
        private bool BreathingBaselineReady => hasBreathingBaseline && breathingBaselineSamples >= baselineSamples;

        private void OnEnable()
        {
            ResetSession();
            if (provider != null) provider.SampleUpdated += OnSample;
        }

        private void OnDisable()
        {
            if (provider != null) provider.SampleUpdated -= OnSample;
            ResetSession();
        }

        public void ResetSession()
        {
            pulseBaselineSamples = breathingBaselineSamples = 0;
            baselinePulse = baselineBreathing = 0;
            hasPulseBaseline = hasBreathingBaseline = false;
            Activation = 0;
            elevatedSince = -1;
            lastGuidanceAt = lastContextAt = -1000;
        }

        private void OnSample(PresageBiometricSample sample)
        {
            bool pulse = sample.PulseIsUsable(minimumConfidence);
            bool breathing = sample.BreathingIsUsable(minimumConfidence);
            if (!pulse && !breathing) { elevatedSince = -1; Activation = 0; return; }

            if (pulse && !PulseBaselineReady)
            {
                baselinePulse = RunningAverage(baselinePulse, sample.PulseBpm, pulseBaselineSamples);
                pulseBaselineSamples++;
                hasPulseBaseline = true;
            }
            if (breathing && !BreathingBaselineReady)
            {
                baselineBreathing = RunningAverage(baselineBreathing, sample.BreathingRate, breathingBaselineSamples);
                breathingBaselineSamples++;
                hasBreathingBaseline = true;
            }

            if (!BaselineReady)
            {
                Activation = 0;
            }
            else
            {
                Activation = CalculateActivation(sample, baselinePulse, PulseBaselineReady, baselineBreathing, BreathingBaselineReady, minimumConfidence);
                // Slowly follow ordinary session drift, but do not absorb a sustained elevated period into the baseline.
                if (Activation < .45f)
                {
                    if (pulse && PulseBaselineReady) baselinePulse = Mathf.Lerp(baselinePulse, sample.PulseBpm, .025f);
                    if (breathing && BreathingBaselineReady) baselineBreathing = Mathf.Lerp(baselineBreathing, sample.BreathingRate, .025f);
                }
            }

            float now = Time.unscaledTime;
            if (chat != null && chat.IsConnected && now - lastContextAt >= contextIntervalSeconds)
            {
                chat.TrySendBiometricContext(BuildContext(sample));
                lastContextAt = now;
            }

            if (!BaselineReady || Activation < elevatedThreshold)
            {
                elevatedSince = -1;
                return;
            }
            if (elevatedSince < 0) elevatedSince = now;
            if (provider == null || !provider.ProactiveGuidance || chat == null || !chat.IsConnected ||
                now - elevatedSince < sustainedSeconds || now - lastGuidanceAt < coachingCooldownSeconds) return;

            chat.TrySendBiometricContext(BuildContext(sample));
            if (chat.TryRequestBiometricGuidance(
                "The player opted into proactive support and has a sustained, confidence-filtered increase from their own session baseline. Offer a calm 45-to-90-second grounding exercise now. Use the private sensor trend only to pace the guidance; do not announce exact numbers unless the player asks, and do not claim to know their emotion."))
            {
                lastGuidanceAt = now;
                elevatedSince = -1;
            }
        }

        private string BuildContext(PresageBiometricSample sample)
        {
            string pulse = sample.PulseIsUsable(minimumConfidence)
                ? sample.PulseBpm.ToString("0.0", CultureInfo.InvariantCulture) + " bpm (confidence " + sample.PulseConfidence.ToString("0", CultureInfo.InvariantCulture) + "%)"
                : "not currently reliable";
            string breathing = sample.BreathingIsUsable(minimumConfidence)
                ? sample.BreathingRate.ToString("0.0", CultureInfo.InvariantCulture) + "/min (confidence " + sample.BreathingConfidence.ToString("0", CultureInfo.InvariantCulture) + "%)"
                : "not currently reliable";
            string hrv = sample.HasHrv && sample.HrvConfidence >= minimumConfidence
                ? "RMSSD " + sample.HrvRmssd.ToString("0.0", CultureInfo.InvariantCulture) + " ms, SDNN " + sample.HrvSdnn.ToString("0.0", CultureInfo.InvariantCulture) + " ms"
                : "not currently reliable";
            string baseline = BaselineReady
                ? " Session baseline: pulse " + (PulseBaselineReady ? baselinePulse.ToString("0.0", CultureInfo.InvariantCulture) : "calibrating") +
                  ", breathing " + (BreathingBaselineReady ? baselineBreathing.ToString("0.0", CultureInfo.InvariantCulture) : "calibrating") +
                  ". Relative activation score " + Activation.ToString("0.00", CultureInfo.InvariantCulture) + " (0-1 heuristic, not a clinical score)."
                : " Baseline is still calibrating; do not interpret changes yet.";
            return "Private consented wellness-camera update. Pulse: " + pulse + ". Breathing: " + breathing + ". HRV: " + hrv + "." + baseline +
                   " Apply the previously supplied wellness-safety policy when using this context.";
        }

        public static float CalculateActivation(PresageBiometricSample sample, float pulseBaseline,
            bool hasPulseBaseline, float breathingBaseline, bool hasBreathingBaseline, float confidenceFloor)
        {
            float sum = 0;
            int count = 0;
            if (hasPulseBaseline && pulseBaseline > 0 && sample.PulseIsUsable(confidenceFloor))
            {
                sum += Mathf.Clamp01((sample.PulseBpm - pulseBaseline) / Mathf.Max(12, pulseBaseline * .18f));
                count++;
            }
            if (hasBreathingBaseline && breathingBaseline > 0 && sample.BreathingIsUsable(confidenceFloor))
            {
                sum += Mathf.Clamp01((sample.BreathingRate - breathingBaseline) / Mathf.Max(4, breathingBaseline * .35f));
                count++;
            }
            return count == 0 ? 0 : sum / count;
        }

        private static float RunningAverage(float average, float value, int count) =>
            count <= 0 ? value : average + (value - average) / (count + 1);
    }
}
