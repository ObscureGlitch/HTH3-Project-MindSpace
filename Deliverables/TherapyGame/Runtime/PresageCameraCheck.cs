using System;

namespace TheLastWatch.Integrations
{
    // A fresh, uninterrupted check is required. Launching the SDK alone is not a pass.
    public sealed class PresageCameraCheck
    {
        public const double ConfirmationSeconds = 1.5;
        private double framesAt = -1000, validationAt = -1000, metricsAt = -1000, validSince = -1;
        public int ValidationCode { get; private set; } = -1;
        public bool PulseReliable { get; private set; }
        public bool BreathingReliable { get; private set; }
        public bool Running { get; private set; }
        public void Reset()
        {
            framesAt = validationAt = metricsAt = -1000; validSince = -1;
            ValidationCode = -1; PulseReliable = BreathingReliable = Running = false;
        }
        public void Frames(double now) { framesAt = now; }
        public void Validation(int code, double now)
        {
            if (code != 0 || ValidationCode != 0 || now - validationAt > 4) validSince = -1;
            ValidationCode = code; validationAt = now;
        }
        public void Metrics(bool pulseReliable, bool breathingReliable, double now)
        {
            PulseReliable = pulseReliable; BreathingReliable = breathingReliable;
            if (!pulseReliable || !breathingReliable) validSince = -1;
            metricsAt = now;
        }
        public void SetRunning(bool running) { Running = running; if (!running) validSince = -1; }
        public bool CameraWorking(double now) => now - framesAt <= 4;
        public bool PositionValid(double now) => ValidationCode == 0 && now - validationAt <= 4;
        public bool PulseReady(double now) => PulseReliable && now - metricsAt <= 4;
        public bool BreathingReady(double now) => BreathingReliable && now - metricsAt <= 4;
        public bool SignalsReliable(double now) => PulseReady(now) && BreathingReady(now);
        public void Tick(double now)
        {
            if (!Running || !CameraWorking(now) || !PositionValid(now) || !SignalsReliable(now)) validSince = -1;
            else if (validSince < 0) validSince = now;
        }
        public double Progress(double now) => validSince < 0 || !Running || !CameraWorking(now) || !PositionValid(now) || !SignalsReliable(now)
            ? 0 : Math.Min(1, Math.Max(0, (now - validSince) / ConfirmationSeconds));
        public bool Passed(double now) => Running && CameraWorking(now) && PositionValid(now) && SignalsReliable(now) && Progress(now) >= 1;
        public static string Guidance(int code)
        {
            switch (code)
            {
                case 0: return "Good position. Keep still and breathe comfortably while pulse and breathing settle.";
                case 1: return "Move into view so your whole face is visible. Uncover the lens and face the camera.";
                case 2: return "Only one person should be in view. Ask others to move out of the camera frame.";
                case 3: return "Move your face toward the centre of the camera view.";
                case 4: return "Adjust your distance so your entire face and upper chest fit in the view.";
                case 5: return "Add light in front of you. Avoid having a bright window behind you.";
                case 6: return "Reduce direct glare or strong light on your face.";
                case 7: return "Move back or tilt the camera slightly down until your shoulders and upper chest are visible.";
                case 10: return "The camera is adjusting its exposure. Hold your position for a moment.";
                case 11: return "Close other camera or video apps to improve the camera frame rate.";
                case 12: return "Rest the camera on a steady surface and keep your head and shoulders still.";
                case 13: return "Move a little farther from the camera.";
                case 14: return "Move a little closer while keeping your upper chest visible.";
                case 15: return "Lower your position or tilt the camera up slightly to centre your face.";
                case 16: return "Raise your position or tilt the camera down slightly to centre your face.";
                case 17: return "Face the camera straight on, with your head upright.";
                default: return "Sit facing the camera with your face, shoulders and upper chest visible.";
            }
        }
    }
}
