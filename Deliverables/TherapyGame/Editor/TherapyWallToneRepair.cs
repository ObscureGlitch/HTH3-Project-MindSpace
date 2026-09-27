using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using TheLastWatch.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyWallToneRepair
    {
        private const string Root="Assets/TherapyGame";
        private const string Request=Root+"/WallToneRequest.txt",Report=Root+"/Exterior/MindSpacePolish/WallToneCheck.txt";
        private static readonly Vector3[] Faces={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
        static TherapyWallToneRepair()
        {
            EditorApplication.delayCall+=Once;
            EditorSceneManager.sceneOpened+=(s,m)=>EditorApplication.delayCall+=Once;
        }
        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="repair-wall-tone-once")return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Once;return;}
            if(!SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity").isLoaded)return;
            File.WriteAllText(Request,"installing-once");Install();
        }
        [MenuItem("Therapy Game/Match Off-White Wall Panels")]
        public static void Install()
        {
            int undo=-1;
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!Lightmapping.isRunning,"Keep Play and baking stopped.");
                RequireMemory();
                var scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");Require(scene.isLoaded,"Open TherapyRoom.");
                var room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
                var original=room.Find("Architecture/Walls/Entry front wall");
                var window=room.Find("Architecture/Front garden window");
                var reference=room.Find("Architecture/Walls/Left plaster wall");
                Require(original!=null&&window!=null&&reference!=null,"Preserved wall and window references required.");
                var source=original.GetComponent<MeshFilter>().sharedMesh;
                var paint=reference.GetComponent<MeshRenderer>().sharedMaterial;
                Require(source!=null&&paint!=null&&original.GetComponent<MeshRenderer>().sharedMaterial==paint,"Existing matching off-white paint required.");
                Require(!original.gameObject.activeSelf,"Original solid wall must stay hidden behind the window.");
                var panels=window.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Plaster ",StringComparison.Ordinal)).ToArray();
                Require(panels.Length==4,"Exactly four replacement wall panels required.");
                var binding=window.GetComponent<WellnessPlasterLighting>();
                Require(binding!=null&&binding.bakedAtlas!=null,"Existing saved room lightmap binding required.");
                int atlas=Array.FindIndex(LightmapSettings.lightmaps,m=>m.lightmapColor==binding.bakedAtlas);
                Require(atlas>=0,"Original room lighting atlas must be loaded.");
                var maps=BuildMaps(source);var checks=new StringBuilder(PlasterFaceProjection.Checks());
                var replacements=new Mesh[panels.Length];
                int changed=0;
                for(int i=0;i<panels.Length;i++)
                {
                    var panel=panels[i];var old=panel.GetComponent<MeshFilter>().sharedMesh;
                    Require(old!=null&&old.vertexCount==24&&old.uv2.Length==24,"Expected preserved panel cube mesh.");
                    var mesh=UnityEngine.Object.Instantiate(old);mesh.name=panel.name+" correct face lighting";
                    var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];var uv2=new Vector2[vertices.Length];
                    for(int v=0;v<vertices.Length;v++)
                    {
                        Vector3 position=original.InverseTransformPoint(panel.transform.TransformPoint(vertices[v]));
                        Vector3 normal=original.InverseTransformDirection(panel.transform.TransformDirection(normals[v])).normalized;
                        int face=Array.FindIndex(Faces,n=>Vector3.Dot(n,normal)>.9999f);
                        Require(face>=0,"Axis-aligned panel face required.");
                        var xy=Coordinates(position,face);uv[v]=Sample(maps[face,0],xy);uv2[v]=Sample(maps[face,1],xy);
                        Require(uv2[v].x>=0&&uv2[v].x<=1&&uv2[v].y>=0&&uv2[v].y<=1,"Corrected UV must stay inside the lightmap atlas.");
                        if(Vector2.Distance(old.uv2[v],uv2[v])>.005f)changed++;
                    }
                    mesh.uv=uv;mesh.uv2=uv2;mesh.RecalculateTangents();
                    Require(mesh.vertices.SequenceEqual(old.vertices)&&mesh.triangles.SequenceEqual(old.triangles)&&mesh.normals.SequenceEqual(old.normals),"Wall geometry must not change.");
                    replacements[i]=mesh;
                }
                CheckSeams(panels,replacements,original);
                int renderers=room.GetComponentsInChildren<Renderer>(true).Length,colliders=room.GetComponentsInChildren<Collider>(true).Length;
                string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"),backup=Path.GetFullPath("TherapyBackups/WallTone/"+stamp);
                Directory.CreateDirectory(backup);File.Copy(scene.path,Path.Combine(backup,"TherapyRoom-on-disk.unity"));
                Require(EditorSceneManager.SaveScene(scene),"Save current scene before repair.");
                File.Copy(scene.path,Path.Combine(backup,"TherapyRoom-before-wall-tone.unity"));
                string folder=Root+"/Exterior/MindSpacePolish/WallTone-"+stamp;
                Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder(Root+"/Exterior/MindSpacePolish","WallTone-"+stamp)),"Create recoverable mesh folder.");
                Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Match off-white wall panels");
                for(int i=0;i<panels.Length;i++)
                {
                    var panel=panels[i];var filter=panel.GetComponent<MeshFilter>();
                    AssetDatabase.CreateAsset(replacements[i],folder+"/"+panel.name.Replace(' ','_')+".asset");
                    Undo.RecordObject(filter,"Correct wall face UVs");Undo.RecordObject(panel,"Match original wall paint");
                    filter.sharedMesh=replacements[i];panel.sharedMaterial=paint;EditorUtility.SetDirty(filter);EditorUtility.SetDirty(panel);
                }
                Undo.RecordObject(binding,"Preserve corrected wall lighting");binding.panels=panels;EditorUtility.SetDirty(binding);
                Require(binding.Apply(),"Restore saved room lighting.");
                Require(panels.All(p=>p.sharedMaterial==paint&&p.lightmapIndex==atlas&&p.lightmapScaleOffset==binding.atlasScaleOffset),"All panels must share paint and correct room lighting.");
                Require(renderers==room.GetComponentsInChildren<Renderer>(true).Length&&colliders==room.GetComponentsInChildren<Collider>(true).Length,"No scene objects or collisions added.");
                // Exercise the persistence path without starting Play or reloading the scene.
                foreach(var panel in panels)panel.lightmapIndex=-1;
                Require(binding.Apply()&&panels.All(p=>p.lightmapIndex==atlas),"Wall lighting must survive index restoration.");
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Save repaired panels.");
                var saved=File.ReadAllText(scene.path);
                foreach(var mesh in replacements)Require(saved.Contains(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mesh))),"New wall mesh reference not saved.");
                Undo.CollapseUndoOperations(undo);undo=-1;
                checks.AppendLine("PASS: six separate planar face maps; no sampling of bevel/edge lightmap islands. "+changed+" incorrect vertex lightmap coordinates replaced.");
                checks.AppendLine("PASS: continuous matching front-face UVs, original warm off-white paint, correct tangents, shared existing baked atlas, and simulated lighting-index restoration.");
                checks.AppendLine("PASS: four saved mesh references; original positions, vertices, normals, triangles, door/window openings, collider count and renderer count unchanged.");
                checks.AppendLine("Original mesh assets retained. Backup: "+backup);
                checks.AppendLine("No lighting bake, new shader, light, Play mode, camera/microphone or reflection capture. Visual check still needed in Unity.");
                File.WriteAllText(Report,"Wall tone repair installed "+DateTime.Now.ToString("s")+"\n"+checks);
                File.WriteAllText(Request,"installed-live-check-pending");Debug.Log("THERAPY_WALL_TONE_READY: correct off-white wall-face mapping saved.");
            }
            catch(Exception e)
            {
                if(undo>=0)Undo.RevertAllDownToGroup(undo);
                File.WriteAllText(Report,"Wall tone repair stopped "+DateTime.Now.ToString("s")+"\n"+e);
                File.WriteAllText(Request,"needs-attention-no-auto-retry");Debug.LogException(e);
            }
        }
        private static PlasterFaceProjection.Map[,] BuildMaps(Mesh source)
        {
            var v=source.vertices;var n=source.normals;var uv=source.uv;var uv2=source.uv2;
            Require(v.Length==n.Length&&uv.Length==v.Length&&uv2.Length==v.Length,"Source wall needs both UV channels.");
            var maps=new PlasterFaceProjection.Map[6,2];
            for(int face=0;face<6;face++)
            {
                int f=face;var ids=Enumerable.Range(0,v.Length).Where(i=>Vector3.Dot(n[i],Faces[f])>.9999f).ToArray();
                for(int channel=0;channel<2;channel++)
                {
                    var rows=new double[ids.Length,4];
                    for(int j=0;j<ids.Length;j++)
                    {
                        int i=ids[j];var xy=Coordinates(v[i],face);var value=channel==0?uv[i]:uv2[i];
                        rows[j,0]=xy.x;rows[j,1]=xy.y;rows[j,2]=value.x;rows[j,3]=value.y;
                    }
                    maps[face,channel]=PlasterFaceProjection.Fit(rows);
                }
            }
            return maps;
        }
        private static Vector2 Coordinates(Vector3 p,int face)=>face<2?new Vector2(p.z,p.y):face<4?new Vector2(p.x,p.z):new Vector2(p.x,p.y);
        private static Vector2 Sample(PlasterFaceProjection.Map map,Vector2 xy){var uv=map.At(xy.x,xy.y);return new Vector2((float)uv[0],(float)uv[1]);}
        private static void CheckSeams(MeshRenderer[] panels,Mesh[] meshes,Transform original)
        {
            // At any shared front-facing edge, a single source-face map must give
            // the same UV regardless of which replacement panel is evaluated.
            for(int a=0;a<meshes.Length;a++)for(int b=a+1;b<meshes.Length;b++)
            for(int i=0;i<meshes[a].vertexCount;i++)for(int j=0;j<meshes[b].vertexCount;j++)
            {
                if(meshes[a].normals[i].z<.999f||meshes[b].normals[j].z<.999f)continue;
                var pa=original.InverseTransformPoint(panels[a].transform.TransformPoint(meshes[a].vertices[i]));
                var pb=original.InverseTransformPoint(panels[b].transform.TransformPoint(meshes[b].vertices[j]));
                if(Vector3.Distance(pa,pb)<.0001f)Require(Vector2.Distance(meshes[a].uv2[i],meshes[b].uv2[j])<.0001f,"Visible wall seam has discontinuous lighting.");
            }
        }
        private static void Require(bool ok,string message){if(!ok)throw new Exception("Wall tone: "+message);}
        [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
        {
            public uint length,load;
            public ulong totalPhysical,availablePhysical,totalPageFile,availablePageFile,totalVirtual,availableVirtual,availableExtendedVirtual;
        }
        [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        private static void RequireMemory()
        {
            if(Application.platform!=RuntimePlatform.WindowsEditor)return;
            var m=new MemoryStatus{length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};
            if(!GlobalMemoryStatusEx(ref m)||m.availablePageFile<1536UL*1024*1024||m.availablePhysical<1024UL*1024*1024)
                throw new Exception("Low memory: close unused apps before using Therapy Game > Match Off-White Wall Panels. No automatic retry.");
        }
    }
}
