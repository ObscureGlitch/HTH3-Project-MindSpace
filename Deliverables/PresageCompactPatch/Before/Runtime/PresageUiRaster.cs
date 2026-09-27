using System;

namespace TheLastWatch.UI
{
    // Pure pixel geometry shared by Unity and the visual regression check.
    // Coordinates are top-down; RGBA rows are stored bottom-up for Texture2D.
    public static class PresageUiRaster
    {
        public const int GuideSize = 400, TraceWidth = 400, TraceHeight = 112;
        private static byte[] cachedWave;

        public static byte[] Guide()
        {
            var pixels = new byte[GuideSize * GuideSize * 4];
            double px = 285, py = 139;
            for (int i = 1; i <= 192; i++)
            {
                double angle = i * Math.PI * 2 / 192;
                double x = 200 + 85 * Math.Cos(angle), y = 139 + 101 * Math.Sin(angle);
                Line(pixels, GuideSize, GuideSize, px, py, x, y, 2.1, 255, 255, 255, 1);
                px = x; py = y;
            }
            // Continuous collar and two curved shoulders: no polygon corners or chest dashes.
            Curve(pixels, 24, 374, 45, 303, 77, 271, 125, 250);
            Curve(pixels, 125, 250, 168, 280, 232, 280, 275, 250);
            Curve(pixels, 275, 250, 323, 271, 355, 303, 376, 374);
            return pixels;
        }

        private static void Curve(byte[] pixels, double ax, double ay, double bx, double by,
            double cx, double cy, double dx, double dy)
        {
            double px = ax, py = ay;
            for (int i = 1; i <= 64; i++)
            {
                double t = i / 64.0, u = 1 - t;
                double x = u*u*u*ax + 3*u*u*t*bx + 3*u*t*t*cx + t*t*t*dx;
                double y = u*u*u*ay + 3*u*u*t*by + 3*u*t*t*cy + t*t*t*dy;
                Line(pixels, GuideSize, GuideSize, px, py, x, y, 2.1, 255, 255, 255, 1);
                px = x; py = y;
            }
        }

        public static void Pulse(byte[] pixels, double phase, double bpm, bool usable)
        {
            if (pixels == null || pixels.Length != TraceWidth * TraceHeight * 4)
                throw new ArgumentException("Unexpected pulse texture size.", "pixels");
            // Keep the grid without a signal, but never invent a resting trace.
            for (int y = 0; y < TraceHeight; y++) for (int x = 0; x < TraceWidth; x++)
            {
                double glow = Math.Max(0, 1 - Math.Abs(y - 60) / 65.0);
                bool grid = x % 20 == 0 || y % 20 == 0;
                bool major = x % 100 == 0 || y == 60;
                int p = ((TraceHeight - 1 - y) * TraceWidth + x) * 4;
                pixels[p] = (byte)(5 + (grid ? 3 : 0));
                pixels[p+1] = (byte)(19 + glow * 13 + (grid ? major ? 15 : 7 : 0));
                pixels[p+2] = (byte)(27 + glow * 20 + (grid ? major ? 21 : 10 : 0));
                pixels[p+3] = 255;
            }
            if (!usable || double.IsNaN(bpm) || double.IsInfinity(bpm) || bpm <= 0) return;
            // CPU reference for visual tests. The game scrolls these same cached pixels on the GPU.
            byte[] wave = PulseWave();
            for (int x = 0; x < TraceWidth; x++)
            {
                double t = phase + (x / (double)TraceWidth - 1) * 3 * bpm / 60;
                double sourceX = (t - Math.Floor(t)) * TraceWidth - .5;
                int left = (int)Math.Floor(sourceX);
                double fraction = sourceX - left;
                int a = (left + TraceWidth) % TraceWidth, b = (a + 1) % TraceWidth;
                for (int y = 0; y < TraceHeight; y++)
                {
                    int p = (y * TraceWidth + x) * 4;
                    double alpha = (wave[(y*TraceWidth+a)*4+3] * (1-fraction)
                        + wave[(y*TraceWidth+b)*4+3] * fraction) / 255;
                    pixels[p] = (byte)(pixels[p] + (80 - pixels[p]) * alpha);
                    pixels[p+1] = (byte)(pixels[p+1] + (224 - pixels[p+1]) * alpha);
                    pixels[p+2] = (byte)(pixels[p+2] + (241 - pixels[p+2]) * alpha);
                }
            }
        }

        // A seamless transparent tile: one decorative beat, not a measured ECG.
        public static byte[] PulseWave()
        {
            if (cachedWave != null) return cachedWave;
            var pixels = new byte[TraceWidth * TraceHeight * 4];
            // Keep transparent RGB cyan as well, avoiding dark fringes with bilinear filtering.
            for (int p = 0; p < pixels.Length; p += 4)
            { pixels[p] = 80; pixels[p+1] = 224; pixels[p+2] = 241; }
            for (int pass = 0; pass < 3; pass++)
            {
                double radius = pass == 0 ? 6 : pass == 1 ? 3 : 1.65;
                double opacity = pass == 0 ? .08 : pass == 1 ? .20 : .95;
                double previousY = WaveY(0);
                for (int x = 1; x <= TraceWidth; x++)
                {
                    double y = WaveY(x / (double)TraceWidth);
                    Line(pixels, TraceWidth, TraceHeight, x - 1, previousY, x, y,
                        radius, 80, 224, 241, opacity, .25);
                    previousY = y;
                }
            }
            cachedWave = pixels;
            return pixels;
        }

        private static double WaveY(double phase)
        {
            double t = phase - Math.Floor(phase);
            double wave = .12 * Bell(t, .18, .038) - .15 * Bell(t, .36, .016)
                + .94 * Bell(t, .40, .018) - .28 * Bell(t, .445, .022) + .23 * Bell(t, .65, .065);
            return 65 - wave * 48;
        }

        private static double Bell(double t, double center, double width)
        { double z = (t - center) / width; return Math.Exp(-.5 * z * z); }

        private static void Line(byte[] pixels, int width, int height, double ax, double ay,
            double bx, double by, double radius, byte red, byte green, byte blue, double opacity, double xScale = 1)
        {
            int left = Math.Max(0, (int)Math.Floor(Math.Min(ax, bx) - (radius + 1) / xScale));
            int right = Math.Min(width - 1, (int)Math.Ceiling(Math.Max(ax, bx) + (radius + 1) / xScale));
            int top = Math.Max(0, (int)Math.Floor(Math.Min(ay, by) - radius - 1));
            int bottom = Math.Min(height - 1, (int)Math.Ceiling(Math.Max(ay, by) + radius + 1));
            double dx = (bx - ax) * xScale, dy = by - ay, length = dx * dx + dy * dy;
            for (int y = top; y <= bottom; y++) for (int x = left; x <= right; x++)
            {
                double t = length == 0 ? 0 : Math.Max(0, Math.Min(1, ((x+.5-ax)*xScale*dx + (y+.5-ay)*dy)/length));
                double ex = (x+.5-ax)*xScale-t*dx, ey = y+.5-ay-t*dy;
                double a = Math.Max(0, Math.Min(1, radius + .5 - Math.Sqrt(ex*ex + ey*ey))) * opacity;
                if (a <= 0) continue;
                int p = ((height - 1 - y) * width + x) * 4;
                if (pixels[p+3] < 255)
                {
                    pixels[p] = red; pixels[p+1] = green; pixels[p+2] = blue;
                    pixels[p+3] = Math.Max(pixels[p+3], (byte)(a * 255));
                }
                else
                {
                    pixels[p] = (byte)(pixels[p] + (red - pixels[p]) * a);
                    pixels[p+1] = (byte)(pixels[p+1] + (green - pixels[p+1]) * a);
                    pixels[p+2] = (byte)(pixels[p+2] + (blue - pixels[p+2]) * a);
                }
            }
        }
    }
}
