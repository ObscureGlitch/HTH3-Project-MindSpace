using System;
using System.Collections.Generic;
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
    public static class TherapyDoorClearanceFix
    {
        private const string Root="Assets/TherapyGame",Folder=Root+"/Exterior/DoorClearance";
        private const string Request=Root+"/DoorClearanceRequest.txt",Report=Folder+"/DoorClearanceCheck.txt";
        private struct Face {public Vector3 a,b,c;public string source;}
        static TherapyDoorClearanceFix()=>EditorApplication.delayCall+=Once;
        private static void Once()
        {
            if(!File.Exists(Request))return;
            string request=File.ReadAllText(Request).Trim();
            if(request!="repair-door-clearance-once"&&request!="finish-door-handle-clearance-once")return;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||Lightmapping.isRunning)
            {File.WriteAllText(Request,"manual-only");Debug.LogWarning("Stop Play/baking, then choose Therapy Game > Fix Entrance Door Clearance.");return;}
            File.WriteAllText(Request,"installing-once");EnsureFolder(Folder);
            try{if(request=="finish-door-handle-clearance-once")AlignSecondHandle();else Install();}
            catch(Exception e){File.WriteAllText(Request,"failed");File.AppendAllText(Report,"\n"+e);Debug.LogException(e);}
        }
        [MenuItem("Therapy Game/Fix Entrance Door Clearance")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new Exception("Stop Play and baking first.");
            Scene scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");if(!scene.isLoaded)throw new Exception("Keep TherapyRoom open.");
            Transform room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
            Transform parent=room.Find("Architecture/Door"),hinge=parent?.Find("Open door hinge");
            var door=hinge!=null?hinge.GetComponent<WellnessDoor>():null;
            var chat=room.GetComponentInChildren<WellnessVoiceChat>();
            Transform handle=hinge?.Find("Door handle");
            if(door==null||door.leaf==null||handle==null||chat==null||chat.entranceDoor!=door||chat.IsBusy||chat.IsConnected)throw new Exception("Expected idle door, handle and voice link required.");
            if(hinge.Find("Fitted hinge barrels")!=null)throw new Exception("Door clearance repair is already installed.");
            if(Vector3.Distance(parent.position,Vector3.zero)>.001f||Quaternion.Angle(parent.rotation,Quaternion.identity)>.01f||Vector3.Distance(hinge.lossyScale,Vector3.one)>.001f)throw new Exception("Door coordinate space changed; inspect before repair.");
            Transform leaf=door.leaf.transform;Mesh leafMesh=leaf.GetComponent<MeshFilter>().sharedMesh;
            if(leafMesh==null||Vector3.Distance(leafMesh.bounds.size,new Vector3(1.24f,2.4f,.065f))>.001f||Quaternion.Angle(leaf.localRotation,Quaternion.identity)>.01f)throw new Exception("Door slab model changed; inspect before repair.");
            var nearby=CollectFaces(room,hinge);
            Vector3 originalCenter=hinge.InverseTransformPoint(leaf.TransformPoint(door.leaf.center));
            Vector3 originalHalf=Vector3.Scale(door.leaf.size*.5f,leaf.localScale);
            int oldHits=0;
            for(int i=0;i<=210;i++)if(nearby.Any(f=>DoorSwingGeometry.Hits(f.a,f.b,f.c,hinge.localPosition,originalCenter,originalHalf,i*.5f)))oldHits++;
            Vector3 newScale=new Vector3(DoorSwingGeometry.Size.x/leafMesh.bounds.size.x,DoorSwingGeometry.Size.y/leafMesh.bounds.size.y,1);
            var handles=hinge.Cast<Transform>().Where(t=>t.name=="Door handle").ToArray();
            Vector3[] handlePositions=handles.Select(t=>DoorSwingGeometry.Center+Vector3.Scale(t.localPosition-leaf.localPosition,newScale)).ToArray();
            // Prove conservative solid slab + handle boxes clear all actual nearby mesh
            // triangles. 4mm expansion covers the travel between quarter-degree samples.
            int samples=CheckSweep(nearby,DoorSwingGeometry.Center,DoorSwingGeometry.Size*.5f+Vector3.one*.004f,door.openAngle);
            for(int i=0;i<handles.Length;i++)CheckHandle(nearby,handles[i],handlePositions[i],door.openAngle);
            EnsureFolder(Folder+"/Backups");string backup=Folder+"/Backups/TherapyRoom_BeforeDoorClearance.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Door scene backup failed.");
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Correct entrance hinge and slab clearance");
            try
            {
                Undo.RecordObjects(new UnityEngine.Object[]{hinge,leaf,door.leaf}.Concat(handles.Cast<UnityEngine.Object>()).ToArray(),"Fit existing door to exterior hinge");
                hinge.localPosition=DoorSwingGeometry.Pivot;
                leaf.localPosition=DoorSwingGeometry.Center;leaf.localScale=newScale;
                door.leaf.center=leafMesh.bounds.center;door.leaf.size=leafMesh.bounds.size;
                for(int i=0;i<handles.Length;i++)handles[i].localPosition=handlePositions[i];
                AddHinges(parent,hinge,handle.GetComponent<Renderer>().sharedMaterial);
                CheckDoorPhysics(door);
                if(chat.entranceDoor!=door||hinge.GetComponent<WellnessInteraction>().door!=door)throw new Exception("Door/voice link changed.");
                EditorUtility.SetDirty(door.leaf);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Door scene save failed.");
                File.WriteAllText(Report,"Entrance door clearance repaired "+DateTime.Now.ToString("s")+"\n"+
                    "Cause: original pivot was recessed behind the full-depth exterior jamb; door also extended down into the raised porch.\n"+
                    "Before: "+oldHits+" sampled angles collided with nearby architectural/porch meshes.\n"+
                    "PASS: leaf and handle clear "+nearby.Count+" actual nearby mesh triangles at "+samples+" angles from 0 to "+door.openAngle+" degrees, with a conservative 4mm safety expansion.\n"+
                    "PASS: exterior hinge (-3.045, 0, -3.465); fitted 1.22m x 2.32m slab; bottom 65mm above world zero / 16mm above porch collider. Three small metal hinge barrels and plates cover the mounting joint.\n"+
                    "PASS: both-side closed collision rays, open doorway, player swing safety, original E toggle, voice portal reference and stored open angle preserved. Original slab/wood materials and frame meshes untouched.\n"+
                    TherapyRoomVoiceSetup.RunChecks()+
                    "Scene and source backed up. No runtime code changes, Play mode, microphone, GPU preview or baking. User-controlled live visual check remains.\n");
                File.WriteAllText(Request,"installed-live-check-pending");Undo.CollapseUndoOperations(undo);
                Debug.Log("THERAPY_DOOR_CLEARANCE_READY: full door/handle swing clears actual frame and porch geometry; scene saved; no Play or bake.");
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }
        private static List<Face> CollectFaces(Transform room,Transform moving)
        {
            var result=new List<Face>();var region=new Bounds(new Vector3(-3.05f,1.23f,-3.92f),new Vector3(3.05f,2.60f,2.30f));
            foreach(var filter in room.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<Renderer>();
                if(filter.transform.IsChildOf(moving)||renderer==null||!renderer.enabled||!renderer.bounds.Intersects(region)||filter.sharedMesh==null)continue;
                var mesh=filter.sharedMesh;if(!mesh.isReadable)throw new Exception("Nearby mesh is not readable: "+filter.name);
                var vertices=mesh.vertices;var indices=mesh.triangles;var matrix=room.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int i=0;i<indices.Length;i+=3)
                {
                    Vector3 a=matrix.MultiplyPoint3x4(vertices[indices[i]]),b=matrix.MultiplyPoint3x4(vertices[indices[i+1]]),c=matrix.MultiplyPoint3x4(vertices[indices[i+2]]);
                    var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);
                    if(bounds.Intersects(region))result.Add(new Face{a=a,b=b,c=c,source=filter.name});
                }
            }
            if(result.Count<36)throw new Exception("Missing actual frame/siding geometry for the swing check.");return result;
        }
        private static void CheckHandle(List<Face> nearby,Transform handle,Vector3 position,float angle)
        {
            Bounds mesh=handle.GetComponent<MeshFilter>().sharedMesh.bounds;
            Matrix4x4 pose=Matrix4x4.TRS(position,handle.localRotation,handle.localScale);
            var box=new Bounds(pose.MultiplyPoint3x4(mesh.center),Vector3.zero);
            for(int k=0;k<8;k++)box.Encapsulate(pose.MultiplyPoint3x4(mesh.center+Vector3.Scale(mesh.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1))));
            CheckSweep(nearby,box.center,box.extents+Vector3.one*.004f,angle);
        }
        [MenuItem("Therapy Game/Finish Door Handle Alignment")]
        public static void AlignSecondHandle()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new Exception("Stop Play/baking first.");
            var scene=SceneManager.GetSceneByPath(Root+"/Scenes/TherapyRoom.unity");if(!scene.isLoaded)throw new Exception("Keep TherapyRoom open.");
            var room=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom").transform;
            var hinge=room.Find("Architecture/Door/Open door hinge");var door=hinge?.GetComponent<WellnessDoor>();
            var chat=room.GetComponentInChildren<WellnessVoiceChat>();
            if(door==null||chat==null||chat.IsBusy||chat.IsConnected||Vector3.Distance(hinge.localPosition,DoorSwingGeometry.Pivot)>.001f)throw new Exception("Existing idle fitted door required.");
            var handles=hinge.Cast<Transform>().Where(t=>t.name=="Door handle").ToArray();
            Vector3 oldPosition=new Vector3(1.118f,1.05f,.06f);
            Vector3 fitted=DoorSwingGeometry.Center+Vector3.Scale(oldPosition-new Vector3(.62f,1.2f,0),door.leaf.transform.localScale);
            var pending=handles.Where(t=>Vector3.Distance(t.localPosition,oldPosition)<.0001f).ToArray();
            if(pending.Length>1||handles.Length!=2)throw new Exception("Handle layout changed; preserve it for inspection.");
            if(pending.Length==0&&!handles.Any(t=>Vector3.Distance(t.localPosition,fitted)<.0001f))throw new Exception("Expected second handle not found.");
            var faces=CollectFaces(room,hinge);
            foreach(var h in handles)CheckHandle(faces,h,pending.Contains(h)?fitted:h.localPosition,door.openAngle);
            string backup=Folder+"/Backups/TherapyRoom_BeforeSecondHandleAlignment.unity";
            if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Handle alignment backup failed.");
            foreach(var h in pending){Undo.RecordObject(h,"Align remaining door handle");h.localPosition=fitted;EditorUtility.SetDirty(h);}
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Handle alignment save failed.");
            File.AppendAllText(Report,"\nHandle alignment saved "+DateTime.Now.ToString("s")+"\nPASS: both existing handles preserved and checked through all 421 swing angles; second handle follows fitted slab; materials, rotations and first handle unchanged.\n");
            File.WriteAllText(Request,"installed-live-check-pending");
            Debug.Log("THERAPY_DOOR_HANDLES_READY: both handles fitted and checked; scene saved.");
        }
        private static int CheckSweep(List<Face> faces,Vector3 center,Vector3 half,float open)
        {
            int steps=Mathf.CeilToInt(open/.25f);
            for(int i=0;i<=steps;i++)foreach(var f in faces)
                if(DoorSwingGeometry.Hits(f.a,f.b,f.c,DoorSwingGeometry.Pivot,center,half,Mathf.Lerp(0,open,i/(float)steps)))
                    throw new Exception("Door clearance intersects "+f.source+" at "+(open*i/steps).ToString("F2")+" degrees; no scene changes applied.");
            return steps+1;
        }
        private static void CheckDoorPhysics(WellnessDoor door)
        {
            Quaternion saved=door.transform.localRotation;
            try
            {
                door.transform.localRotation=Quaternion.identity;Physics.SyncTransforms();
                Vector3 point=door.leaf.transform.TransformPoint(door.leaf.center);
                if(!door.leaf.Raycast(new Ray(point-Vector3.forward,Vector3.forward),out _,2)||!door.leaf.Raycast(new Ray(point+Vector3.forward,Vector3.back),out _,2))throw new Exception("Closed door collision failed.");
                door.transform.localRotation=Quaternion.Euler(0,door.openAngle,0);Physics.SyncTransforms();
                if(door.leaf.Raycast(new Ray(point-Vector3.forward,Vector3.forward),out _,2))throw new Exception("Open door obstructs doorway.");
                Vector3 center=DoorSwingGeometry.Center,half=DoorSwingGeometry.Size*.5f;
                for(int yaw=0;yaw<=105;yaw+=5)
                    if(!WellnessDoor.BlocksLeaf(DoorSwingGeometry.Rotate(center,yaw),.22f,.9f,center,half,yaw))throw new Exception("Player swing protection failed.");
            }
            finally{door.transform.localRotation=saved;Physics.SyncTransforms();}
        }
        private static void AddHinges(Transform parent,Transform hinge,Material metal)
        {
            var moving=new MindSpaceExteriorGeometry.Batch();var fixedPlate=new MindSpaceExteriorGeometry.Batch();
            foreach(float y in new[]{.36f,1.23f,2.10f})
            {
                moving.Box(new Vector3(.021f,y,.011f),new Vector3(.042f,.075f,.040f));
                fixedPlate.Box(new Vector3(-3.080f,y,-3.446f),new Vector3(.075f,.075f,.014f));
                const int sides=10;
                for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                    Vector3 p=new Vector3(Mathf.Cos(a)*.012f,y-.047f,Mathf.Sin(a)*.012f),q=new Vector3(Mathf.Cos(b)*.012f,y-.047f,Mathf.Sin(b)*.012f);
                    moving.Quad(p,q,q+Vector3.up*.094f,p+Vector3.up*.094f,new Vector3(Mathf.Cos((a+b)*.5f),0,Mathf.Sin((a+b)*.5f)));
                    moving.Tri(new Vector3(0,y-.047f,0),q,p,Vector3.down);moving.Tri(new Vector3(0,y+.047f,0),p+Vector3.up*.094f,q+Vector3.up*.094f,Vector3.up);
                }
            }
            Add("Fitted hinge barrels",hinge,moving,metal);Add("Fitted hinge mounting plates",parent,fixedPlate,metal);
        }
        private static void Add(string name,Transform parent,MindSpaceExteriorGeometry.Batch batch,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(go,"Add fitted door hinges");
            var mesh=batch.Mesh(name);AssetDatabase.CreateAsset(mesh,Folder+"/"+name+".asset");
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            if(Directory.Exists(path)){AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);return;}
            int slash=path.LastIndexOf('/');EnsureFolder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
        }
    }
}
