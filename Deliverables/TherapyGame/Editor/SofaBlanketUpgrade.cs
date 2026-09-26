using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    // Author-time cloth: stable, fitted folds without a runtime cloth simulation.
    [InitializeOnLoad]
    public static class SofaBlanketUpgrade
    {
        private const string Root = "Assets/TherapyGame";
        private const string Request = Root + "/BlanketUpgradeRequest.txt";
        private const int Columns = 48, Rows = 140;
        private const float Width = .74f, Thickness = .006f;
        private static readonly Vector2[][] Curves =
        {
            C(-.505f,.94f, -.505f,1.08f, -.465f,1.15f, -.36f,1.15f),
            C(-.36f,1.15f, -.24f,1.15f, -.095f,1.12f, -.095f,1.00f),
            C(-.095f,1f, -.095f,.88f, -.10f,.714f, -.07f,.665f),
            C(-.07f,.665f, -.04f,.616f, .02f,.616f, .11f,.616f),
            C(.11f,.616f, .20f,.616f, .26f,.624f, .345f,.612f),
            C(.345f,.612f, .43f,.60f, .49f,.54f, .49f,.435f),
            C(.49f,.435f, .49f,.33f, .51f,.24f, .54f,.145f)
        };

        static SofaBlanketUpgrade() => EditorApplication.delayCall += TryRequest;

        private static void TryRequest()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "pending") return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryRequest;
                return;
            }
            // Never switch away from a user's current scene for this local edit.
            Scene scene = SceneManager.GetSceneByPath(Root + "/Scenes/TherapyRoom.unity");
            if (!scene.isLoaded)
            {
                File.WriteAllText(Root + "/Documentation/BlanketUpgrade.txt", "Waiting: open TherapyRoom, then use Therapy Game > Upgrade Sofa Blanket. No other scene was changed.\n");
                return;
            }
            File.WriteAllText(Request, "running");
            try { Upgrade(); File.WriteAllText(Request, "complete"); }
            catch (Exception error)
            {
                File.WriteAllText(Request, "failed");
                File.WriteAllText(Root + "/Documentation/BlanketUpgrade.txt", error.ToString());
                Debug.LogException(error);
            }
        }

        [MenuItem("Therapy Game/Upgrade Sofa Blanket")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before editing the blanket.");
            Scene scene = SceneManager.GetSceneByPath(Root + "/Scenes/TherapyRoom.unity");
            if (!scene.isLoaded) throw new InvalidOperationException("Open TherapyRoom first.");
            Transform room = scene.GetRootGameObjects().Single(g => g.name == "TherapyRoom").transform;
            Transform sofa = room.Find("Furniture/Sofa");
            Transform blanket = room.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Knitted blanket");
            MeshFilter filter = blanket.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "Draped knit");
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (sofa == null || renderer == null) throw new InvalidOperationException("Expected sofa or blanket renderer is missing.");
            bool alreadyDirty = scene.isDirty;
            string backup = Root + "/Documentation/TherapyRoom.before-blanket.unity.backup";
            if (!File.Exists(backup)) File.Copy(scene.path, backup);
            int originalTriangles = filter.sharedMesh.triangles.Length / 3;
            Mesh mesh = BuildMesh();
            ValidateMesh(mesh);
            string meshPath = Root + "/Models/SofaDrapedBlanket.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
            else
            {
                // Refresh the native vertex buffers as well as the serialized asset.
                existing.Clear(); existing.vertices = mesh.vertices; existing.normals = mesh.normals;
                existing.uv = mesh.uv; existing.triangles = mesh.triangles;
                existing.RecalculateTangents(); existing.RecalculateBounds(); existing.UploadMeshData(false);
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(mesh); mesh = existing;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Drape knitted blanket over sofa");
            Undo.SetTransformParent(blanket, sofa, "Place blanket on sofa");
            Undo.RecordObject(blanket, "Fit blanket to sofa");
            blanket.localPosition = Vector3.zero; blanket.localRotation = Quaternion.identity; blanket.localScale = Vector3.one;
            Undo.RecordObject(filter.transform, "Reset blanket mesh transform");
            filter.transform.localPosition = Vector3.zero; filter.transform.localRotation = Quaternion.identity; filter.transform.localScale = Vector3.one;
            Undo.RecordObject(filter, "Subdivide and shape blanket"); filter.sharedMesh = null; filter.sharedMesh = mesh;
            Undo.RecordObject(renderer, "Use light probes for revised cloth");
            renderer.sharedMaterial = KnitMaterial();
            // The rest of the room keeps its bake. New cloth must not reuse its old UV lightmap.
            renderer.lightmapIndex = -1;
            renderer.receiveGI = ReceiveGI.LightProbes;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            renderer.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(filter.gameObject, StaticEditorFlags.BatchingStatic);
            foreach (Transform child in blanket)
                if (child.name == "Blanket fringe") { Undo.RecordObject(child.gameObject, "Replace straight fringe with curved fringe"); child.gameObject.SetActive(false); }
            PrefabUtility.RecordPrefabInstancePropertyModifications(blanket);
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssetIfDirty(mesh);
            if (!alreadyDirty) EditorSceneManager.SaveScene(scene);
            RenderPreview(sofa);
            string report = $"Sofa blanket upgrade: PASS\nOriginal surface triangles: {originalTriangles}\nNew mesh vertices: {mesh.vertexCount}\nNew mesh triangles including thickness and curved fringe: {mesh.triangles.Length / 3}\nGrid: {Columns} x {Rows}\nWidth: {Width:F2} m; thickness: {Thickness:F3} m\nBack, seat and front-edge fitted drape; uneven hanging folds; curved fringe.\nMinimum furniture surface clearance: {MinimumClearance(mesh):F4} m\nScene: {(alreadyDirty ? "Left unsaved to preserve the user's existing unsaved edits. Save when ready." : "Saved.")}\nOriginal scene backup: {backup}\nNo runtime cloth simulation, no other furniture or project settings changed.\n";
            File.WriteAllText(Root + "/Documentation/BlanketUpgrade.txt", report);
            Selection.activeGameObject = blanket.gameObject;
            SceneView.RepaintAll(); Debug.Log(report);
        }

        private static Material KnitMaterial()
        {
            string path = Root + "/Materials/SofaBlanketKnit.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            // A small tiling normal texture carries the yarn detail; geometry carries
            // the silhouette and folds. The room's shared Throw material is untouched.
            const int size = 256;
            var texture = new Texture2D(size,size,TextureFormat.RGBA32,true,true) { name="SoftKnitNormal", wrapMode=TextureWrapMode.Repeat };
            var pixels = new Color[size*size];
            float step = 1f/size;
            for (int y=0;y<size;y++) for (int x=0;x<size;x++)
            {
                float u=(x+.5f)/size,v=(y+.5f)/size;
                float dx=KnitHeight(u+step,v)-KnitHeight(u-step,v);
                float dy=KnitHeight(u,v+step)-KnitHeight(u,v-step);
                Vector3 n=new Vector3(-dx*2f,-dy*2f,1).normalized;
                pixels[y*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);
            }
            texture.SetPixels(pixels); texture.Apply(true,false);
            AssetDatabase.CreateAsset(texture,Root + "/Materials/SoftKnitNormal.asset");
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Throw.mat")) { name="SofaBlanketKnit" };
            material.SetFloat("_Cull",2); material.SetFloat("_Smoothness",.04f);
            material.SetTexture("_BumpMap",texture); material.SetFloat("_BumpScale",.6f); material.EnableKeyword("_NORMALMAP");
            AssetDatabase.CreateAsset(material,path);
            return material;
        }
        private static float KnitHeight(float u,float v)
        {
            Vector2 p=new Vector2(Mathf.Repeat(u*8f,1)-.5f,Mathf.Repeat(v*8f,1)-.5f);
            float distance=Mathf.Min(SegmentDistance(p,new Vector2(-.34f,-.44f),new Vector2(0,.37f)),SegmentDistance(p,new Vector2(.34f,-.44f),new Vector2(0,.37f)));
            return Mathf.Exp(-distance*distance/ .007f);
        }
        private static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {
            Vector2 ab=b-a; return (p-a-ab*Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude)).magnitude;
        }

        private static Vector2[] C(params float[] p) => new[] { new Vector2(p[0],p[1]), new Vector2(p[2],p[3]), new Vector2(p[4],p[5]), new Vector2(p[6],p[7]) };
        private static void Profile(float v, out Vector2 point, out Vector2 tangent)
        {
            float f = Mathf.Clamp01(v) * Curves.Length;
            int segment = Mathf.Min((int)f, Curves.Length - 1);
            float t = f - segment, s = 1f - t;
            Vector2[] p = Curves[segment];
            point = s*s*s*p[0] + 3*s*s*t*p[1] + 3*s*t*t*p[2] + t*t*t*p[3];
            tangent = (3*s*s*(p[1]-p[0]) + 6*s*t*(p[2]-p[1]) + 3*t*t*(p[3]-p[2])).normalized;
        }
        private static Vector3 Surface(float u, float v)
        {
            Profile(v, out Vector2 p, out Vector2 tangent);
            Vector3 normal = new Vector3(0, tangent.x, -tangent.y);
            float hanging = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.78f, 1f, v));
            float phase = u * Mathf.PI * 9.4f + .65f * Mathf.Sin(v * 7f) + .24f * Mathf.Sin(u * 17f + v * 11f);
            float folds = (.006f + .010f * hanging) * (1f + Mathf.Sin(phase));
            folds += .003f * (1f + Mathf.Sin(u * 39f - v * 5f));
            float edge = Mathf.Pow(Mathf.Abs(u * 2 - 1), 8) * .005f * (1 + Mathf.Sin(v * 19f));
            float x = (u-.5f) * Width * (1f + .018f * Mathf.Sin(v * 13f)) + .10f * v*v - .025f;
            Vector3 result = new Vector3(x, p.y, p.x) + normal * (.004f + folds + edge);
            result.y += hanging * (.010f * Mathf.Sin(u * 18f + .7f) + .007f * Mathf.Sin(u * 31f));
            return result;
        }

        private static Mesh BuildMesh()
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uv = new List<Vector2>(); var triangles = new List<int>();
            int layerSize = (Columns+1)*(Rows+1);
            for (int side = 0; side < 2; side++)
                for (int y = 0; y <= Rows; y++) for (int x = 0; x <= Columns; x++)
                {
                    float u = (float)x/Columns, v = (float)y/Rows;
                    Vector3 du = Surface(Mathf.Min(1,u+.001f),v) - Surface(Mathf.Max(0,u-.001f),v);
                    Vector3 dv = Surface(u,Mathf.Min(1,v+.001f)) - Surface(u,Mathf.Max(0,v-.001f));
                    // Normalize derivatives first: the cross product of two .001-step
                    // derivatives is below Unity's Vector3 normalization epsilon.
                    Vector3 n = Vector3.Cross(dv.normalized,du.normalized).normalized;
                    float sign = side == 0 ? 1 : -1;
                    vertices.Add(Surface(u,v) + sign * Thickness*.5f*n); normals.Add(sign*n);
                    uv.Add(new Vector2(u*3, v*8));
                }
            for (int y = 0; y < Rows; y++) for (int x = 0; x < Columns; x++)
            {
                int a = y*(Columns+1)+x, b=a+1, c=a+Columns+1, d=c+1;
                triangles.AddRange(new[] { a,c,b, b,c,d, a+layerSize,b+layerSize,c+layerSize, b+layerSize,d+layerSize,c+layerSize });
            }
            var perimeter = new List<int>();
            for (int x = 0; x <= Columns; x++) perimeter.Add(x);
            for (int y = 1; y <= Rows; y++) perimeter.Add(y*(Columns+1)+Columns);
            for (int x = Columns-1; x >= 0; x--) perimeter.Add(Rows*(Columns+1)+x);
            for (int y = Rows-1; y > 0; y--) perimeter.Add(y*(Columns+1));
            for (int i = 0; i < perimeter.Count; i++)
            {
                int a=perimeter[i], b=perimeter[(i+1)%perimeter.Count];
                triangles.AddRange(new[] { a,b,b+layerSize, a,b+layerSize,a+layerSize });
            }
            // Soft pairs of yarn at the hem, with slightly different lengths and curl.
            for (int i = 0; i < 19; i++) for (int strand = 0; strand < 2; strand++)
            {
                float u = .025f + i*.95f/18 + (strand-.5f)*.009f;
                Vector3 start = Surface(u,1);
                int offset = vertices.Count;
                const int segments = 9, sides = 6;
                float length = .055f + .012f*Mathf.Sin(i*1.7f + strand);
                for (int j = 0; j <= segments; j++) for (int k = 0; k <= sides; k++)
                {
                    float t=(float)j/segments, a=k*Mathf.PI*2/sides;
                    Vector3 center = start + new Vector3(.007f*Mathf.Sin(t*4+i)*t, -length*t, .006f*Mathf.Sin(t*3+strand)*t);
                    Vector3 n=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    vertices.Add(center + n * Mathf.Lerp(.0028f,.0013f,t)); normals.Add(n); uv.Add(new Vector2((float)k/sides,t));
                }
                for (int j=0;j<segments;j++) for (int k=0;k<sides;k++)
                {
                    int a=offset+j*(sides+1)+k,b=a+1,c=a+sides+1,d=c+1;
                    triangles.AddRange(new[] { a,c,b, b,c,d });
                }
            }
            var mesh = new Mesh { name = "SofaDrapedBlanket_48x140" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        private static float BoxDistance(Vector3 p, Vector3 center, Vector3 size, float radius)
        {
            Vector3 q = p-center;
            q = new Vector3(Mathf.Abs(q.x),Mathf.Abs(q.y),Mathf.Abs(q.z)) - (size*.5f-Vector3.one*radius);
            return new Vector3(Mathf.Max(q.x,0),Mathf.Max(q.y,0),Mathf.Max(q.z,0)).magnitude + Mathf.Min(Mathf.Max(q.x,Mathf.Max(q.y,q.z)),0) - radius;
        }
        private static float MinimumClearance(Mesh mesh)
        {
            float min = float.MaxValue;
            foreach (Vector3 p in mesh.vertices)
            {
                min=Mathf.Min(min,BoxDistance(p,new Vector3(0,.79f,-.36f),new Vector3(2.30f,.68f,.24f),.10f));
                min=Mathf.Min(min,BoxDistance(p,new Vector3(0,.33f,0),new Vector3(2.35f,.29f,.91f),.10f));
                foreach (float x in new[] { -.51f,.51f })
                {
                    min=Mathf.Min(min,BoxDistance(p,new Vector3(x,.51f,.10f),new Vector3(.98f,.19f,.67f),.07f));
                    min=Mathf.Min(min,BoxDistance(p,new Vector3(x,.80f,-.20f),new Vector3(.99f,.51f,.17f),.075f));
                }
            }
            return min;
        }
        private static void ValidateMesh(Mesh mesh)
        {
            if (mesh.vertexCount > 65535 || mesh.triangles.Length/3 < 20000) throw new InvalidOperationException("Unexpected blanket topology.");
            if (mesh.vertices.Any(p => !float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z))) throw new InvalidOperationException("Non-finite blanket vertex.");
            if (mesh.normals.Any(n => !float.IsFinite(n.sqrMagnitude) || n.sqrMagnitude < .95f)) throw new InvalidOperationException("Invalid cloth normal.");
            if (MinimumClearance(mesh) < -.003f) throw new InvalidOperationException("Blanket penetrates sofa upholstery: " + MinimumClearance(mesh));
            if (mesh.bounds.min.y < .04f) throw new InvalidOperationException("Fringe reaches the floor.");
        }

        private static void RenderPreview(Transform sofa)
        {
            var go = new GameObject("Blanket preview camera") { hideFlags = HideFlags.HideAndDontSave };
            Camera camera = go.AddComponent<Camera>();
            camera.transform.position = sofa.TransformPoint(new Vector3(1.48f,1.75f,2.40f));
            camera.transform.LookAt(sofa.TransformPoint(new Vector3(0,.66f,.05f)));
            camera.fieldOfView = 43; camera.nearClipPlane = .03f; camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.7f,.72f,.67f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            RenderTexture previous = RenderTexture.active;
            var rt = new RenderTexture(1400,1100,24,RenderTextureFormat.ARGB32);
            Texture2D texture = null;
            try
            {
                camera.targetTexture=rt; camera.aspect=(float)rt.width/rt.height;
                camera.Render(); camera.Render(); camera.Render(); RenderTexture.active=rt;
                texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); texture.Apply();
                File.WriteAllBytes(Root + "/Documentation/SofaBlanket.png",texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous; camera.targetTexture=null;
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
