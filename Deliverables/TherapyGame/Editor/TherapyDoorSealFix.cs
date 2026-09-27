using System;
using System.IO;
using System.Linq;
using TheLastWatch.Integrations;
using TheLastWatch.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyDoorSealFix
    {
        private const string Root="Assets/TherapyGame",Folder=Root+"/Exterior/DoorSeal";
        private const string Request=Root+"/DoorSealRequest.txt",Report=Folder+"/DoorSealCheck.txt";
        static TherapyDoorSealFix()=>EditorApplication.delayCall+=Once;
        private static void Once()
        {
            if(!File.Exists(Request)||File.ReadAllText(Request).Trim()!="seal-entrance-door-once")return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Stop Play/baking, then choose Therapy Game > Seal Entrance Door Frame.");return;}
            File.WriteAllText(Request,"installing-once");EnsureFolder(Folder);
            try{Install();}catch(Exception e){File.WriteAllText(Request,"failed");File.WriteAllText(Report,e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Therapy Game/Seal Entrance Door Frame")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new Exception("Stop Play/baking first.");
            Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");if(!scene.isLoaded)throw new Exception("Keep TherapyRoom open.");
            Transform room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
            Transform frame=room.Find("Architecture/Door"),hinge=frame?.Find("Open door hinge");
            var door=hinge!=null?hinge.GetComponent<WellnessDoor>():null;var chat=room.GetComponentInChildren<WellnessVoiceChat>();
            if(door==null||door.leaf==null||chat==null||chat.IsBusy||chat.IsConnected||chat.entranceDoor!=door)throw new Exception("Expected idle door/voice references required.");
            if(frame.Find("Continuous jamb and door stops")!=null)throw new Exception("Door seal already installed; not duplicating.");
            if(Vector3.Distance(hinge.localPosition,DoorSwingGeometry.Pivot)>.0002f||Vector3.Distance(door.leaf.transform.localPosition,DoorSwingGeometry.Center)>.0002f||
                Vector3.Distance(Vector3.Scale(door.leaf.size,door.leaf.transform.localScale),DoorSwingGeometry.Size)>.0002f||Quaternion.Angle(frame.rotation,Quaternion.identity)>.001f||frame.position.sqrMagnitude>.0001f)
                throw new Exception("Door fit changed; do not apply fixed-size trim to another layout.");
            string checks=DoorSealGeometry.Check();var geometry=DoorSealGeometry.Build();
            var handles=hinge.Cast<Transform>().Where(t=>t.name=="Door handle").ToArray();
            if(handles.Length!=2)throw new Exception("Keep both existing handles.");
            foreach(var handle in handles)CheckHandleClearance(handle,geometry,door.openAngle);
            if(door.player==null||door.player.stepOffset<.09f||door.player.radius*2>=1.18f)throw new Exception("Player cannot clear the low threshold or opening.");
            EnsureFolder(Folder+"/Backups");string backup=Folder+"/Backups/TherapyRoom_BeforeDoorSeal.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
            var wood=frame.Find("Oak door frame")?.GetComponent<Renderer>()?.sharedMaterial;
            if(wood==null)throw new Exception("Matching existing oak material missing.");
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Seal doorway without changing swing");
            try
            {
                var root=new GameObject("Continuous jamb and door stops");root.transform.SetParent(frame,false);Undo.RegisterCreatedObjectUndo(root,"Add sealed frame lining");
                var gasket=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Recessed oak door gasket"};
                gasket.SetColor("_BaseColor",new Color(.12f,.105f,.075f));gasket.SetFloat("_Smoothness",.05f);AssetDatabase.CreateAsset(gasket,Folder+"/RecessedGasket.mat");
                foreach(var part in geometry)
                {
                    var go=new GameObject(part.Key);go.transform.SetParent(root.transform,false);
                    var mesh=part.Value.Mesh(part.Key);AssetDatabase.CreateAsset(mesh,Folder+"/"+part.Key+".asset");
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=part.Key.StartsWith("Oak")?wood:gasket;
                    renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                }
                // Only the shallow sill needs new walkable collision surfaces; the
                // side/top lining remains backed by existing architectural colliders.
                var threshold=new GameObject("Low walkable threshold");threshold.transform.SetParent(root.transform,false);
                var collider=threshold.AddComponent<BoxCollider>();collider.center=new Vector3(-2.43f,.039f,-3.1525f);collider.size=new Vector3(1.25f,.042f,.435f);
                var lip=threshold.AddComponent<BoxCollider>();lip.center=new Vector3(-2.43f,.067f,-3.285f);lip.size=new Vector3(1.30f,.036f,.17f);
                Physics.SyncTransforms();
                CheckSavedParts(root.transform,door);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Door seal save failed.");
                File.WriteAllText(Report,"Closed doorway sealed "+DateTime.Now.ToString("s")+"\n"+checks+
                    "PASS: existing wood material reused; continuous jamb/header lining; four overlapping stops and recessed gasket; low 85mm threshold. Closed leaf, both handles and hinge placement unchanged.\n"+
                    "PASS: both handles clear new trim throughout 0–105 degrees; 1.18m clear width; open voice portal unobstructed; threshold within player step height.\n"+
                    "PASS: 144 triangles, two renderers, two small threshold colliders matching the sill/stop heights; no extra lights, textures, cameras, runtime scripts, bake, Play mode or microphone. Scene backed up and saved.\n"+
                    "CPU/scene checks passed; final visual appearance still needs user-controlled Play verification.\n");
                File.WriteAllText(Request,"installed-live-check-pending");Undo.CollapseUndoOperations(undo);
                Debug.Log("THERAPY_DOOR_SEAL_READY: closure coverage and swing clearance passed; continuous jamb and door stops saved.");
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }
        private static void CheckHandleClearance(Transform handle,System.Collections.Generic.Dictionary<string,MindSpaceExteriorGeometry.Batch> geometry,float open)
        {
            var bounds=handle.GetComponent<MeshFilter>().sharedMesh.bounds;var matrix=Matrix4x4.TRS(handle.localPosition,handle.localRotation,handle.localScale);
            var box=new Bounds(matrix.MultiplyPoint3x4(bounds.center),Vector3.zero);
            for(int i=0;i<8;i++)box.Encapsulate(matrix.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            foreach(var batch in geometry.Values)for(int t=0;t<batch.vertices.Count;t+=3)for(float yaw=0;yaw<=open+.001f;yaw+=.25f)
                if(DoorSwingGeometry.Hits(batch.vertices[t],batch.vertices[t+1],batch.vertices[t+2],DoorSwingGeometry.Pivot,box.center,box.extents+Vector3.one*.004f,yaw))throw new Exception("A handle crosses the door stops at "+yaw);
        }
        private static void CheckSavedParts(Transform root,WellnessDoor door)
        {
            if(root.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.triangles.Length/3)!=144||root.GetComponentsInChildren<Renderer>().Length!=2)throw new Exception("Door seal geometry budget changed.");
            var thresholds=root.GetComponentsInChildren<BoxCollider>();if(thresholds.Length!=2||thresholds.Any(t=>t.center.y+t.size.y*.5f>.086f))throw new Exception("Low threshold collision missing.");
            // Test only the new colliders: no frame-wide opaque collision plane across entry.
            foreach(var threshold in thresholds)if(threshold.Raycast(new Ray(new Vector3(-2.43f,1.45f,-3.25f),Vector3.back),out _,3))throw new Exception("Threshold blocks voice portal.");
            if(door.leaf==null||!door.leaf.enabled||door.leaf.isTrigger)throw new Exception("Door leaf collision changed.");
        }
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            if(Directory.Exists(path)){AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);return;}
            int slash=path.LastIndexOf('/');EnsureFolder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
        }
    }
}
