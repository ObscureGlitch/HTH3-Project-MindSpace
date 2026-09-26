using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TheLastWatch.Editor
{
    // Small, reusable meshes. Dimensions are baked into vertices so bevels stay round.
    internal static class WellnessGeometry
    {
        internal static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();
        internal const string Folder = "Assets/_Project/Art/Models/Wellness";

        internal static Mesh RoundBox(Vector3 size, float radius)
        {
            string key = $"Box_{size.x:F3}_{size.y:F3}_{size.z:F3}_{radius:F3}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;
            Vector3 half = size * .5f;
            radius = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * .99f);
            Vector3 inner = half - Vector3.one * radius;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uvs = new List<Vector2>(); var triangles = new List<int>();
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            const int n = 6;
            foreach (Vector3 normal in axes)
            {
                Vector3 tangent = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.Cross(Vector3.up, normal);
                Vector3 bitangent = Vector3.Cross(normal, tangent);
                int start = vertices.Count;
                for (int y = 0; y <= n; y++) for (int x = 0; x <= n; x++)
                {
                    // Samples concentrate at the bevel instead of stretching a sphere.
                    float u = Sample(x, n), v = Sample(y, n);
                    Vector3 raw = Vector3.Scale(normal + tangent * u + bitangent * v, half);
                    Vector3 core = new Vector3(Mathf.Clamp(raw.x, -inner.x, inner.x), Mathf.Clamp(raw.y, -inner.y, inner.y), Mathf.Clamp(raw.z, -inner.z, inner.z));
                    Vector3 norm = (raw - core).normalized;
                    vertices.Add(core + norm * radius); normals.Add(norm);
                    uvs.Add(new Vector2((u + 1f) * .5f, (v + 1f) * .5f));
                }
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    int a = start + y * (n + 1) + x, b = a + 1, c = a + n + 1, d = c + 1;
                    triangles.AddRange(new[] { a, b, d, a, d, c });
                }
            }
            return Save(key, vertices, triangles, uvs, normals);
        }

        private static float Sample(int i, int n)
        {
            float[] values = { -1, -.94f, -.78f, 0f, .78f, .94f, 1f };
            return values[i];
        }

        internal static Mesh Lathe(string key, Vector2[] profile, int segments = 32)
        {
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;
            var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
            for (int y = 0; y < profile.Length; y++) for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(Mathf.Sin(a) * profile[y].x, profile[y].y, Mathf.Cos(a) * profile[y].x));
                uvs.Add(new Vector2((float)i / segments, (float)y / (profile.Length - 1)));
            }
            for (int y = 0; y < profile.Length - 1; y++) for (int i = 0; i < segments; i++)
            {
                int a = y * (segments + 1) + i, b = a + 1, c = a + segments + 1, d = c + 1;
                triangles.AddRange(new[] { a, b, d, a, d, c });
            }
            return Save(key, vertices, triangles, uvs);
        }

        internal static Mesh Leaf()
        {
            const string key = "CurvedLeaf";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            const int rows = 8;
            for (int i = 0; i <= rows; i++)
            {
                float f = (float)i / rows, width = Mathf.Sin(f * Mathf.PI) * .24f;
                v.Add(new Vector3(-width, f, .16f * f * f));
                v.Add(new Vector3(0, f, .16f * f * f - width * .25f));
                v.Add(new Vector3(width, f, .16f * f * f));
                uv.Add(new Vector2(0, f)); uv.Add(new Vector2(.5f, f)); uv.Add(new Vector2(1, f));
            }
            for (int i = 0; i < rows; i++) for (int j = 0; j < 2; j++)
            {
                int a = i * 3 + j; t.AddRange(new[] { a, a + 3, a + 4, a, a + 4, a + 1 });
            }
            return Save(key, v, t, uv);
        }

        internal static Mesh Cloth(string key, float width, float height, bool draped)
        {
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            const int nx = 24, ny = 14;
            for (int y = 0; y <= ny; y++) for (int x = 0; x <= nx; x++)
            {
                float u = (float)x / nx, f = (float)y / ny;
                float wave = Mathf.Cos(u * Mathf.PI * 12) * .027f;
                Vector3 p = draped
                    ? new Vector3((u - .5f) * width, -.74f * f + .07f * Mathf.Sin(f * Mathf.PI), height * Mathf.Min(f * 2.8f, .5f) + wave)
                    : new Vector3((u - .5f) * width, -f * height, wave);
                v.Add(p); uv.Add(new Vector2(u * 3, f * 6));
            }
            for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
            {
                int a = y * (nx + 1) + x; t.AddRange(new[] { a, a + nx + 1, a + nx + 2, a, a + nx + 2, a + 1 });
            }
            return Save(key, v, t, uv);
        }

        private static Mesh Save(string key, List<Vector3> v, List<int> t, List<Vector2> uv, List<Vector3> normals = null)
        {
            var mesh = new Mesh { name = key };
            mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.SetUVs(0, uv);
            if (normals != null) mesh.SetNormals(normals); else mesh.RecalculateNormals();
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            string path = Folder + "/" + key + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
            else AssetDatabase.CreateAsset(mesh, path);
            Cache[key] = mesh; return mesh;
        }
    }
}
