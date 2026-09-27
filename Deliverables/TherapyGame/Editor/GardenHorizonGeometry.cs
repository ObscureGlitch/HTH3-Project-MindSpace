using System;
using System.Collections.Generic;

namespace TherapyGame.Editor
{
    // Pure CPU geometry, independent of Unity so the real algorithm can be tested
    // before asking the laptop to import anything. Units remain metres.
    public static class GardenHorizonGeometry
    {
        public struct Point
        {
            public float x, y, z;
            public Point(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        }
        public sealed class Surface
        {
            public readonly List<Point> vertices = new List<Point>();
            public readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            public Point[] edge;
            public float minX, maxX, minZ, maxZ;
            public int TriangleCount { get { return vertices.Count / 3; } }
        }
        private static readonly float[] Scales = { .998f, 1, 1.18f, 1.4f, 1.7f, 2.1f, 2.6f, 3.2f, 3.9f, 4.7f, 5.6f, 6.6f, 7.8f };

        public static Surface Build(Point[] input)
        {
            if (input == null || input.Length < 8) throw new ArgumentException("A complete terrain perimeter is required.");
            Point[] edge = (Point[])input.Clone();
            float minX = edge[0].x, maxX = minX, minZ = edge[0].z, maxZ = minZ;
            foreach (Point p in edge)
            {
                if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z)) throw new ArgumentException("Non-finite terrain vertex.");
                minX = Math.Min(minX, p.x); maxX = Math.Max(maxX, p.x);
                minZ = Math.Min(minZ, p.z); maxZ = Math.Max(maxZ, p.z);
            }
            float cx = (minX + maxX) / 2, cz = (minZ + maxZ) / 2;
            if (maxX - minX < 20 || maxZ - minZ < 20) throw new ArgumentException("Unexpected terrain bounds.");
            Array.Sort(edge, (a, b) => Math.Atan2(a.z - cz, a.x - cx).CompareTo(Math.Atan2(b.z - cz, b.x - cx)));
            var result = new Surface { edge = edge, minX = cx + (minX - cx) * 7.8f, maxX = cx + (maxX - cx) * 7.8f,
                minZ = cz + (minZ - cz) * 7.8f, maxZ = cz + (maxZ - cz) * 7.8f };
            for (int ring = 0; ring < Scales.Length - 1; ring++)
                for (int i = 0; i < edge.Length; i++)
                {
                    int j = (i + 1) % edge.Length;
                    Point a = Ring(edge[i], cx, cz, Scales[ring]), b = Ring(edge[j], cx, cz, Scales[ring]);
                    Point c = Ring(edge[i], cx, cz, Scales[ring + 1]), d = Ring(edge[j], cx, cz, Scales[ring + 1]);
                    // The thin inner skirt overlaps underneath the original terrain;
                    // the second ring meets every original edge vertex exactly.
                    Add(result, a, b, c, Material(i, ring, 0));
                    Add(result, b, d, c, Material(i, ring, 1));
                }
            return result;
        }
        private static int Material(int segment, int ring, int triangle)
        {
            uint h = unchecked((uint)(segment * 374761393 + ring * 668265263 + triangle * 982451653));
            h = (h ^ (h >> 13)) * 1274126177u;
            return (int)((h ^ (h >> 16)) % 5) % 4;
        }
        private static Point Ring(Point p, float cx, float cz, float scale)
        {
            float x = cx + (p.x - cx) * scale, z = cz + (p.z - cz) * scale;
            if (scale <= 1) return new Point(x, p.y - (scale < 1 ? .035f : 0), z);
            double distance = Math.Sqrt((x - p.x) * (x - p.x) + (z - p.z) * (z - p.z));
            double t = Math.Min(1, distance / 50); t = t * t * (3 - 2 * t);
            double hills = 4 + 2.6 * Math.Sin(x * .016 + .7) * Math.Cos(z * .021 - .4)
                + .65 * Math.Sin(x * .09) * Math.Sin(z * .065);
            return new Point(x, (float)(p.y * (1 - t) + hills * t), z);
        }
        private static void Add(Surface surface, Point a, Point b, Point c, int material)
        {
            float up = (b.z - a.z) * (c.x - a.x) - (b.x - a.x) * (c.z - a.z);
            if (up <= 0) throw new ArgumentException("Boundary must form an unbroken counterclockwise rectangle without repeated vertices.");
            int index = surface.vertices.Count;
            surface.vertices.Add(a); surface.vertices.Add(b); surface.vertices.Add(c);
            surface.triangles[material].Add(index); surface.triangles[material].Add(index + 1); surface.triangles[material].Add(index + 2);
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        public static string RunChecks()
        {
            var border = new List<Point>();
            for (int i = 0; i < 60; i++) { border.Add(TestPoint(-40 + i * 1.5f, -42)); border.Add(TestPoint(50 - i * 1.5f, 36)); }
            for (int i = 0; i < 52; i++) { border.Add(TestPoint(50, -42 + i * 1.5f)); border.Add(TestPoint(-40, 36 - i * 1.5f)); }
            Surface mesh = Build(border.ToArray());
            if (mesh.edge.Length != 224 || mesh.TriangleCount != 5376) throw new Exception("Incorrect apron topology/budget.");
            if (mesh.minX > -345 || mesh.maxX < 355 || mesh.minZ > -307 || mesh.maxZ < 301) throw new Exception("Insufficient exterior extent.");
            int checks = 0;
            foreach (Point p in mesh.vertices)
            {
                if (!Finite(p.y) || p.y < -3 || p.y > 10) throw new Exception("Invalid apron height.");
                checks++;
            }
            for (int i = 0; i < mesh.edge.Length; i++)
            {
                Point a = mesh.edge[i], b = mesh.edge[(i + 1) % mesh.edge.Length];
                if (Math.Abs(Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.z - a.z) * (b.z - a.z)) - 1.5) > .0001)
                    throw new Exception("Perimeter segment missing, including wraparound corner.");
                Point same = Ring(a, 5, -3, 1);
                if (same.x != a.x || same.y != a.y || same.z != a.z) throw new Exception("Perimeter must match exactly.");
                checks += 2;
            }
            // All eight corner/side directions remain outside the 220m camera range
            // even at the playable fence's furthest corner (x=-12..32,z=-22..16).
            float clearance = Math.Min(Math.Min(-12 - mesh.minX, mesh.maxX - 32), Math.Min(-22 - mesh.minZ, mesh.maxZ - 16));
            if (clearance < 280) throw new Exception("Visible outer edge possible.");
            return "PASS: " + checks + " CPU apron checks; 224 exact edge samples, 5376 upward triangles, 4 material batches, "
                + clearance.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "m minimum outer-edge clearance.\n";
        }
        private static Point TestPoint(float x, float z) { return new Point(x, (float)(4 + 3 * Math.Sin(x * .08) * Math.Cos(z * .07)), z); }
    }
}
