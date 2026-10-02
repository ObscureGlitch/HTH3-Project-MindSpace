using System;

namespace TheLastWatch.Environment
{
    // Pure motion math: deterministic tests need neither Unity nor a graphics device.
    public sealed class KoiSwimMotion
    {
        readonly Random random;
        readonly double radius, rx, rz, direction, phaseOffset;
        double angle, timer, speed, targetSpeed, finPhase;
        bool resting;
        double PoseAngle => angle + phaseOffset;
        public double X => rx * radius * Math.Cos(PoseAngle);
        public double Z => rz * radius * Math.Sin(PoseAngle);
        public double YawDegrees => Math.Atan2(-rx * Math.Sin(PoseAngle) * direction, rz * Math.Cos(PoseAngle) * direction) * 180 / Math.PI;
        public double Speed => speed;
        public double FinPhase => finPhase;
        public bool Resting => resting;

        public KoiSwimMotion(int seed, double lane, double radiusX, double radiusZ, double phaseOffset = 0)
        {
            random = new Random(seed); radius = lane; rx = radiusX; rz = radiusZ;
            this.phaseOffset = phaseOffset;
            angle = random.NextDouble() * Math.PI * 2;
            direction = random.Next(2) == 0 ? -1 : 1;
            speed = targetSpeed = .16 + random.NextDouble() * .13;
            timer = 3 + random.NextDouble() * 12;
            finPhase = (random.NextDouble() * Math.PI * 2 + phaseOffset) % (Math.PI * 2);
        }
        public void Step(double dt)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt <= 0) return;
            dt = Math.Min(dt, .1); // No teleport when the game stalls or regains focus.
            timer -= dt;
            if (timer <= 0)
            {
                resting = !resting;
                targetSpeed = resting ? .012 : .16 + random.NextDouble() * .13;
                timer = resting ? 2.5 + random.NextDouble() * 4 : 8 + random.NextDouble() * 12;
            }
            speed += (targetSpeed - speed) * (1 - Math.Exp(-dt * 1.2));
            // Integrate the shared route angle, not each member's offset position.
            // Three- and four-fish groups then keep their spacing around the ellipse.
            double a = rx * Math.Sin(angle), b = rz * Math.Cos(angle);
            angle = (angle + direction * speed * dt / (radius * Math.Sqrt(a * a + b * b))) % (Math.PI * 2);
            finPhase = (finPhase + dt * (.65 + speed * 3.8) * Math.PI * 2) % (Math.PI * 2);
        }
    }
}
