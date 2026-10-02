using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace TherapyGame.Editor
{
    internal static class WinterTreePreview
    {
        // Offscreen geometry QA, not a Play session. Uses actual authored bark/trunks
        // and the same per-tree layout as the saved combined winter mesh.
        internal static void Capture(WinterTreeGeometry.Tree[] trees,Transform garden)
        {
            var temp=new GameObject("Temporary winter preview"){hideFlags=HideFlags.HideAndDontSave};
            var owned=new List<UnityEngine.Object>();var previous=RenderTexture.active;
            var oldMode=RenderSettings.ambientMode;Color oldAmbient=RenderSettings.ambientLight;
            var source=garden.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name.StartsWith("Tree trunks",StringComparison.Ordinal));
            var lights=UnityEngine.Object.FindObjectsByType<Light>().Where(l=>l.type==LightType.Directional).ToArray();
            var enabled=lights.Select(l=>l.enabled).ToArray();
            try
            {
                for(int i=0;i<lights.Length;i++)lights[i].enabled=false;
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.63f,.68f,.72f);
                var selected=trees.OrderBy(t=>t.trunk.center.x*t.trunk.center.x+t.trunk.center.z*t.trunk.center.z).Take(3).ToArray();
                for(int tree=0;tree<selected.Length;tree++)
                {
                    var input=selected[tree];var root=Child(temp,"Preview tree "+tree);
                    var branches=new Mesh();owned.Add(branches);
                    WinterTreeGeometry.Build(WinterTreeGeometry.Generate(new[]{input}),root.transform,branches);
                    root.AddComponent<MeshFilter>().sharedMesh=branches;root.AddComponent<MeshRenderer>().sharedMaterial=source.sharedMaterial;
                    // Complete winter geometry includes the shorter exposed bole;
                    // don't overlay the old tall Spring/Summer trunk in this preview.
                    root.transform.position=new Vector3((tree-1)*5-input.trunk.center.x,-input.trunk.min.y,-input.trunk.center.z);
                }
                var floor=Child(temp,"Snow floor");var floorMesh=new Mesh();owned.Add(floorMesh);
                floorMesh.vertices=new[]{new Vector3(-14,-.02f,-8),new Vector3(-14,-.02f,8),new Vector3(14,-.02f,-8),new Vector3(14,-.02f,8)};
                floorMesh.triangles=new[]{0,1,2,2,1,3};floorMesh.RecalculateNormals();
                var snow=new Material(Shader.Find("Universal Render Pipeline/Unlit")){hideFlags=HideFlags.HideAndDontSave};owned.Add(snow);snow.SetColor("_BaseColor",new Color(.88f,.91f,.92f));snow.SetColor("_Color",new Color(.88f,.91f,.92f));
                floor.AddComponent<MeshFilter>().sharedMesh=floorMesh;floor.AddComponent<MeshRenderer>().sharedMaterial=snow;
                var light=Child(temp,"Preview daylight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.color=new Color(1,.97f,.91f);light.shadows=LightShadows.Soft;
                light.transform.rotation=Quaternion.Euler(38,-32,0);
                var camera=Child(temp,"Preview camera").AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.67f,.77f,.82f);camera.orthographic=true;camera.orthographicSize=4.65f;
                camera.nearClipPlane=.1f;camera.farClipPlane=60;camera.transform.position=new Vector3(0,4.6f,-19);camera.transform.LookAt(new Vector3(0,3.6f,0));
                var target=new RenderTexture(1200,700,24,RenderTextureFormat.ARGB32);owned.Add(target);target.Create();camera.targetTexture=target;camera.aspect=1200f/700;
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target;var image=new Texture2D(1200,700,TextureFormat.RGB24,false);owned.Add(image);image.ReadPixels(new Rect(0,0,1200,700),0,0);image.Apply();
                string output="D:/Hack the Hill/Deliverables/Seasons/Checks/WinterTreeProportions.png";File.WriteAllBytes(output,image.EncodeToPNG());
                Debug.Log("NATURAL_WINTER_PREVIEW: "+output);
            }
            finally
            {
                RenderTexture.active=previous;RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;
                for(int i=0;i<lights.Length;i++)if(lights[i]!=null)lights[i].enabled=enabled[i];
                UnityEngine.Object.DestroyImmediate(temp);foreach(var value in owned)if(value!=null)UnityEngine.Object.DestroyImmediate(value);
            }
        }
        static GameObject Child(GameObject parent,string name)
        {var value=new GameObject(name){hideFlags=HideFlags.HideAndDontSave,layer=31};value.transform.SetParent(parent.transform,false);return value;}
    }
}
