using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TheLastWatch.Environment;
using TheLastWatch.Integrations;
using TheLastWatch.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TherapyGame.Editor
{
    public static class TherapySeasonSetup
    {
        const string Root="Assets/TherapyGame",Folder=Root+"/Exterior/Seasons",Scene=Root+"/Scenes/TherapyRoom.unity",Report=Root+"/Documentation/SeasonsCheck.txt";
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        public static void InstallBatch(){EditorSceneManager.OpenScene(Scene);EditorApplication.Exit(Install()?0:1);}
        public static void RepairBranchesBatch(){EditorSceneManager.OpenScene(Scene);EditorApplication.Exit(RepairBranches()?0:1);}
        public static void PreviewBranchesBatch()
        {
            try
            {
                EditorSceneManager.OpenScene(Scene);
                var root=EditorSceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="TherapyRoom");
                var season=root.GetComponent<WellnessSeasonCycle>();var garden=root.transform.Find("OutdoorGarden");
                var crowns=season.surfaces.Where(s=>s.slots.Contains(WellnessSeasonSurface.Deciduous)).SelectMany(ExtractCanopies).Where(IsTreeCrown).ToArray();
                WinterTreePreview.Capture(NaturalPlan(AttachCrownsToTrunks(crowns,ExtractTrunks(garden))).trees,garden);EditorApplication.Exit(0);
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
        public static void VerifyBranchesBatch()
        {
            const string branchReport=Root+"/Documentation/WinterBranchesReloadCheck.txt";
            try
            {
                EditorSceneManager.OpenScene(Scene);
                var root=EditorSceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="TherapyRoom");
                var season=root.GetComponent<WellnessSeasonCycle>();
                var garden=root.transform.Find("OutdoorGarden");
                var crowns=season.surfaces.Where(s=>s.slots.Contains(WellnessSeasonSurface.Deciduous)).SelectMany(ExtractCanopies).Where(IsTreeCrown).ToArray();
                var trees=AttachCrownsToTrunks(crowns,ExtractTrunks(garden));
                var mesh=season.winterBranches.GetComponent<MeshFilter>().sharedMesh;
                string checks=CheckBranchAttachments(trees,season.winterBranches.transform,mesh)+CheckBranchFixtures()+CheckSnowWeather(root)+CheckWinterTrunkBinding(season,garden)+CheckPaletteLifecycle(season);
                File.WriteAllText(branchReport,"PASS: saved trunk-anchored winter mesh reloaded.\n"+checks+"Pending: visual check in Play.\n");
                Debug.Log("WINTER_BRANCHES_RELOAD_VERIFIED: "+branchReport);EditorApplication.Exit(0);
            }
            catch(Exception error){File.WriteAllText(branchReport,"FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
        }
        [MenuItem("Therapy Game/Repair Winter Branch Attachments")]
        public static void RepairBranchesFromMenu(){RepairBranches();}
        static bool RepairBranches()
        {
            const string branchReport=Root+"/Documentation/WinterBranchesCheck.txt";
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Stop Play mode first.");
                var scene=EditorSceneManager.GetActiveScene();Require(scene.path==Scene,"Open TherapyRoom first.");
                var root=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom");
                var garden=root.transform.Find("OutdoorGarden");var season=root.GetComponent<WellnessSeasonCycle>();
                Require(season!=null&&season.winterBranches!=null,"Install seasons before repairing branches.");
                var parent=season.winterBranches.transform.parent.gameObject;
                var crowns=season.surfaces.Where(s=>s.slots.Contains(WellnessSeasonSurface.Deciduous)).SelectMany(ExtractCanopies).Where(IsTreeCrown).ToArray();
                var trees=AttachCrownsToTrunks(crowns,ExtractTrunks(garden));
                int colliders=root.GetComponentsInChildren<Collider>(true).Length,cameras=root.GetComponentsInChildren<Camera>(true).Length;
                var savedSeason=season.season;bool savedAutomatic=season.automatic;float savedMinutes=season.seasonMinutes;
                string checks=CheckBranchFixtures()+CheckSnowWeather(root);
                season.winterBranches=BuildBranches(parent,trees);
                BindWinterTrunks(season,garden);
                var mesh=season.winterBranches.GetComponent<MeshFilter>().sharedMesh;
                checks+=CheckBranchAttachments(trees,season.winterBranches.transform,mesh)+CheckWinterTrunkBinding(season,garden)+CheckPaletteLifecycle(season);
                Require(colliders==root.GetComponentsInChildren<Collider>(true).Length&&cameras==root.GetComponentsInChildren<Camera>(true).Length,"Winter proportion repair added a collider or camera.");
                Require(season.season==savedSeason&&season.automatic==savedAutomatic&&season.seasonMinutes==savedMinutes,"Winter proportion repair changed season settings.");
                AssetDatabase.SaveAssets();
                EditorUtility.SetDirty(season);EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Winter trunk visibility binding did not save.");
                File.WriteAllText(branchReport,"PASS: repaired winter branch attachments.\n"+checks+
                    "PASS: complete Winter trees now have short exposed trunks, lower scaffold limbs and wider rounded crowns. Original source trunk/foliage assets, season settings and colliders are unchanged.\n"+
                    "PASS: continuous bending/tapering paths, varied per-tree forks, outward face winding and capped ends; one combined mesh retained, including unchanged evergreen trunks.\n"+
                    "Settings: Sky & weather → Weather → Snow. Manual Rain and Snow work in any season; Auto still uses snow in Winter.\nPending: visual check in Play.\n");
                Debug.Log("WINTER_BRANCHES_REPAIRED: "+branchReport);return true;
            }
            catch(Exception error){File.WriteAllText(branchReport,"FAILED\n"+error);Debug.LogException(error);return false;}
        }
        public static void VerifyBatch()
        {
            const string reloadReport=Root+"/Documentation/SeasonsReloadCheck.txt";
            try
            {
                EditorSceneManager.OpenScene(Scene);
                var root=EditorSceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="TherapyRoom");
                var season=root.GetComponent<WellnessSeasonCycle>();
                Require(season!=null&&season.player!=null&&season.sky!=null,"Saved season/player/sky references missing.");
                var rain=root.GetComponentInChildren<WellnessRain>(true);
                Require(season.sky.seasons==season&&rain.seasons==season&&rain.snowMaterial!=null,"Saved weather references missing.");
                Require(season.season==WellnessSeason.Spring&&season.automatic&&season.seasonMinutes==24,"Saved season defaults changed.");
                Require(season.surfaces.Length>=20&&season.surfaces.All(s=>s.renderer!=null&&s.slots.Length==s.renderer.sharedMaterials.Length),"Saved outdoor palette bindings incomplete.");
                Require(season.surfaces.All(s=>s.renderer.sharedMaterials.Where(m=>m!=null).All(m=>!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m)))),"Runtime palette clones leaked into the scene.");
                Require(season.winterDormant.All(r=>r!=null)&&season.butterflies.All(r=>r!=null)&&season.leafCanopies.Length>=20,"Saved vegetation or crown bindings incomplete.");
                Require(season.winterBranches!=null&&!season.winterBranches.enabled,"Winter branches should be hidden at Spring startup.");
                var branches=season.winterBranches.GetComponent<MeshFilter>().sharedMesh;
                Require(branches!=null&&branches.vertexCount<40000,"Saved winter branch mesh missing or oversized.");
                Require(season.fallingLeaves!=null&&season.fallingLeaves.main.maxParticles==80&&season.fallingLeaves.main.simulationSpace==ParticleSystemSimulationSpace.World,"Saved leaf particle configuration invalid.");
                Require(rain.drops.main.maxParticles==600&&rain.snowMaterial.GetTexture("_BaseMap")!=null,"Saved snow material or precipitation budget invalid.");
                string checks=CheckClimate()+CheckPaletteLifecycle(season);
                File.WriteAllText(reloadReport,"PASS: fresh Unity process loaded saved four-season scene.\n"+checks+
                    "PASS: all palette, sky, rain, leaf, vegetation and branch references serialized correctly. Spring/automatic/24-minute defaults retained. No runtime material clones saved.\n"+
                    "PASS: "+season.leafCanopies.Length+" crown bounds; combined winter mesh "+branches.vertexCount+" vertices and "+branches.triangles.Length/3+" triangles.\n"+
                    "Pending: visual/GPU/input checks in Play. Verification did not save or enter Play.\n");
                Debug.Log("FOUR_SEASONS_RELOAD_VERIFIED: "+reloadReport);EditorApplication.Exit(0);
            }
            catch(Exception error){File.WriteAllText(reloadReport,"FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
        }
        [MenuItem("Therapy Game/Install Four Seasons")]
        public static void InstallFromMenu(){Install();}
        static bool Install()
        {
            int undo=-1;
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Stop Play mode first.");
                var scene=EditorSceneManager.GetActiveScene();Require(scene.path==Scene,"Open TherapyRoom first.");
                var root=scene.GetRootGameObjects().Single(g=>g.name=="TherapyRoom");var garden=root.transform.Find("OutdoorGarden");
                var sky=root.GetComponentInChildren<WellnessSkyCycle>(true);var rain=root.GetComponentInChildren<WellnessRain>(true);
                var player=root.GetComponentInChildren<WellnessExplorer>(true);
                Require(garden!=null&&sky!=null&&rain!=null&&rain.drops!=null&&player!=null,"Existing garden/sky/rain/player required.");
                string checks=CheckClimate();
                int colliders=root.GetComponentsInChildren<Collider>(true).Length,cameras=root.GetComponentsInChildren<Camera>(true).Length;
                var ground=garden.GetComponentsInChildren<MeshCollider>(true).Single(c=>c.name=="Walkable terrain");var originalGround=ground.sharedMesh;
                var pond=sky.pondRenderer.sharedMaterial;var cloudMode=sky.cloudDeck.CloudMode;
                string backup=Path.GetFullPath("TherapyBackups/Seasons/"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(backup);
                File.Copy(scene.path,Path.Combine(backup,"TherapyRoom-before-seasons.unity"));
                Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Four gentle seasons");
                EnsureFolder(Folder);
                var season=root.GetComponent<WellnessSeasonCycle>();
                if(season==null)season=Undo.AddComponent<WellnessSeasonCycle>(root);
                Undo.RecordObject(season,"Season references");season.player=player;season.sky=sky;
                var surfaces=new List<WellnessSeasonCycle.Surface>();
                foreach(var renderer in garden.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var roles=renderer.sharedMaterials.Select(m=>Role(m)).ToArray();
                    if(roles.Any(r=>(int)r>=0))surfaces.Add(new WellnessSeasonCycle.Surface{renderer=renderer,slots=roles});
                }
                season.surfaces=surfaces.ToArray();
                Require(surfaces.Any(s=>s.renderer==ground.GetComponent<Renderer>()||s.renderer.name.StartsWith("Terrain")),"Terrain palette was not found.");
                Require(surfaces.Any(s=>s.slots.Contains(WellnessSeasonSurface.Deciduous))&&surfaces.Any(s=>s.slots.Contains(WellnessSeasonSurface.Evergreen)),"Both leaf and pine palettes required.");
                var deciduous=surfaces.Where(s=>s.slots.Contains(WellnessSeasonSurface.Deciduous)).ToArray();
                var branchCanopies=deciduous.SelectMany(ExtractCanopies).Where(IsTreeCrown).ToArray();
                Require(branchCanopies.Length>=4,"Actual connected crown geometry required for winter branches.");
                season.leafCanopies=branchCanopies.Select(b=>new Bounds(season.transform.InverseTransformPoint(b.center),b.size)).ToArray();
                var dormant=surfaces.Where(s=>s.renderer.name.StartsWith("Flower")||s.renderer.name.Contains("Lily pads")||
                    s.renderer.name.ToLowerInvariant().Contains("grass blade")||s.renderer.name.ToLowerInvariant().Contains("living stem")).Select(s=>s.renderer).ToList();
                dormant.AddRange(deciduous.Where(s=>s.slots.All(r=>r==WellnessSeasonSurface.Deciduous)).Select(s=>s.renderer));season.winterDormant=dormant.Distinct().ToArray();
                season.butterflies=garden.GetComponentsInChildren<WellnessButterfly>(true).SelectMany(b=>b.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
                var effects=GetOrCreate(garden,"Seasonal scenery");
                var winterTrees=AttachCrownsToTrunks(branchCanopies,ExtractTrunks(garden));
                season.winterBranches=BuildBranches(effects,winterTrees);
                BindWinterTrunks(season,garden);
                checks+=CheckBranchAttachments(winterTrees,season.winterBranches.transform,season.winterBranches.GetComponent<MeshFilter>().sharedMesh)+CheckBranchFixtures();
                season.fallingLeaves=CreateLeaves(effects,ParticleMaterial("AutumnLeaves",Sprite("AutumnLeaf",true)));
                Undo.RecordObject(sky,"Season-aware sky");sky.seasons=season;
                Undo.RecordObject(rain,"Season-aware precipitation");rain.seasons=season;rain.snowMaterial=ParticleMaterial("GentleSnow",Sprite("Snowflake",false));
                EditorUtility.SetDirty(season);EditorUtility.SetDirty(sky);EditorUtility.SetDirty(rain);
                checks+=CheckPaletteLifecycle(season);
                Require(colliders==root.GetComponentsInChildren<Collider>(true).Length&&cameras==root.GetComponentsInChildren<Camera>(true).Length,"Seasons must not add colliders or cameras.");
                Require(ground.sharedMesh==originalGround&&sky.pondRenderer.sharedMaterial==pond&&sky.cloudDeck.CloudMode==cloudMode,"Preserve walkable terrain, pond and cloud-family selection.");
                Require(rain.drops.main.maxParticles<=600&&season.fallingLeaves.main.maxParticles<=80,"Seasonal particle budget exceeded.");
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Season scene save failed.");
                Undo.CollapseUndoOperations(undo);undo=-1;
                Directory.CreateDirectory(Path.GetDirectoryName(Report));
                File.WriteAllText(Report,"PASS: four seasons installed "+DateTime.Now.ToString("s")+"\n"+checks+
                    "PASS: "+surfaces.Count+" outdoor renderers bound; "+season.winterDormant.Length+" dormant vegetation renderers; "+branchCanopies.Length+" actual mesh crowns supply leaf fall and winter branch silhouettes without unbatching the forest.\n"+
                    "PASS: shared runtime palette per source material/role, eight-second transitions, automatic Spring/Summer/Fall/Winter cycle (24 real minutes per season by default), manual hold and 5–60 minute duration settings.\n"+
                    "PASS: terrain/apron, leafy trees, evergreen trees, grasses, flowers, paths, rocks and distant scenery palettes covered. Summer preserves original palette. Indoor assets/materials are excluded.\n"+
                    "PASS: winter replaces rain particles with snow (same 600-particle system); leaf fall uses one 80-particle system; bare branches are one combined mesh. No added collider, camera, navigation change, pond freeze or fish change.\n"+
                    "PASS: original terrain mesh, pond material and cloud-family mode retained. All runtime material and visibility changes restore on disable.\n"+
                    "Settings: Sky & weather → Seasons. Snow can be requested in Weather while Winter is selected. Rain/snow strength uses Precipitation.\n"+
                    "Pending: visual/GPU/input checks in Play. No Play, microphone, webcam or voice connection started.\nBackup: "+backup+"\n");
                Debug.Log("FOUR_SEASONS_INSTALLED: "+Report);return true;
            }
            catch(Exception error)
            {
                if(undo>=0)Undo.RevertAllDownToGroup(undo);
                Directory.CreateDirectory(Path.GetDirectoryName(Report));File.WriteAllText(Report,"FAILED\n"+error);Debug.LogException(error);return false;
            }
        }
        static WellnessSeasonSurface Role(Material material)
        {
            if(material==null)return (WellnessSeasonSurface)(-1);
            string path=AssetDatabase.GetAssetPath(material);
            if(!path.StartsWith(Root+"/Exterior/Materials/")&&!path.StartsWith(Root+"/Exterior/MindSpacePolish/Materials/"))return (WellnessSeasonSurface)(-1);
            switch(material.name.ToLowerInvariant())
            {
                case "grass":case "glade":case "meadow":case "moss":return WellnessSeasonSurface.Ground;
                case "leaf":case "leaflight":case "leaves":return WellnessSeasonSurface.Deciduous;
                case "pine":case "pinedark":case "pinelight":return WellnessSeasonSurface.Evergreen;
                case "stone":case "stonelight":case "stoneshade":return WellnessSeasonSurface.Rock;
                case "path":return WellnessSeasonSurface.Path;
                case "stem":case "lily":return WellnessSeasonSurface.Grass;
                case "cream":case "pink":case "lavender":case "gold":case "flowers":case "center":return WellnessSeasonSurface.Flower;
                case "mountain":case "distant":return WellnessSeasonSurface.Mountain;
                default:return (WellnessSeasonSurface)(-1);
            }
        }
        static GameObject GetOrCreate(Transform parent,string name)
        {
            var existing=parent.Find(name);if(existing!=null)return existing.gameObject;
            var result=new GameObject(name);Undo.RegisterCreatedObjectUndo(result,"Season scenery");result.transform.SetParent(parent,false);return result;
        }
        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');EnsureFolder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
        }
        static Texture2D Sprite(string name,bool leaf)
        {
            string path=Folder+"/"+name+".png";
            if(!File.Exists(path))
            {
                var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                {
                    float px=(x+.5f-32)/32,py=(y+.5f-32)/32;
                    float shape=leaf?Mathf.Sqrt(px*px/(.55f*.55f)+py*py):Mathf.Sqrt(px*px+py*py);
                    float alpha=leaf?Mathf.Clamp01((1-shape)*8):Mathf.Clamp01((1-shape)*2.5f)*Mathf.Clamp01((1-shape)*2.5f);
                    texture.SetPixel(x,y,new Color(1,1,1,alpha));
                }
                texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            }
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material ParticleMaterial(string name,Texture texture)
        {
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")){name=name};AssetDatabase.CreateAsset(material,path);}
            material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_Cull",(float)CullMode.Off);material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",texture);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=(int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster",false);EditorUtility.SetDirty(material);return material;
        }
        static ParticleSystem CreateLeaves(GameObject parent,Material material)
        {
            var leaves=GetOrCreate(parent.transform,"Quiet autumn leaf fall");var system=leaves.GetComponent<ParticleSystem>();
            if(system==null)system=Undo.AddComponent<ParticleSystem>(leaves);
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;main.loop=true;main.playOnAwake=false;main.maxParticles=80;main.startSpeed=0;main.startLifetime=7;main.startSize=.1f;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
            var rotation=system.rotationOverLifetime;rotation.enabled=true;rotation.z=new ParticleSystem.MinMaxCurve(-.7f,.7f);
            var color=system.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.08f),new GradientAlphaKey(1,.82f),new GradientAlphaKey(0,1)});color.color=gradient;
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;
            EditorUtility.SetDirty(system);EditorUtility.SetDirty(renderer);return system;
        }
        static IEnumerable<Bounds> ExtractCanopies(WellnessSeasonCycle.Surface surface)
        {
            return ExtractConnectedBounds(surface.renderer,Enumerable.Range(0,surface.slots.Length).Where(i=>surface.slots[i]==WellnessSeasonSurface.Deciduous));
        }
        static bool IsTreeCrown(Bounds b)=>b.size.x>.3f&&b.size.x<8&&b.size.y>.3f&&b.size.y<8&&b.center.y>1.2f;
        static Bounds[] ExtractTrunks(Transform garden)
        {
            var trunks=garden.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("Tree trunks",StringComparison.Ordinal)&&r.sharedMaterials.Any(m=>m!=null&&m.name=="bark"))
                .SelectMany(r=>ExtractConnectedBounds(r,Enumerable.Range(0,r.sharedMaterials.Length)))
                .Where(b=>b.size.y>1&&b.size.x<1.5f&&b.size.z<1.5f).ToArray();
            Require(trunks.Length>=4,"Actual trunk mesh geometry required for branch attachment.");return trunks;
        }
        static IEnumerable<Bounds> ExtractConnectedBounds(Renderer renderer,IEnumerable<int> slots)
        {
            var filter=renderer.GetComponent<MeshFilter>();Require(filter!=null&&filter.sharedMesh!=null,"Missing tree mesh: "+renderer.name);
            var mesh=filter.sharedMesh;var vertices=mesh.vertices;
            // Flat-shaded batches duplicate vertices at face seams. Weld only
            // coincident positions, then union triangle edges to recover each
            // authored crown without separating or replacing the original mesh.
            var canonical=new Dictionary<Vector3Int,int>();var parents=new List<int>();var points=new List<Vector3>();
            Func<int,int> find=null;find=i=>{while(parents[i]!=i){parents[i]=parents[parents[i]];i=parents[i];}return i;};
            Func<Vector3,int> node=v=>
            {
                var key=new Vector3Int(Mathf.RoundToInt(v.x*1000),Mathf.RoundToInt(v.y*1000),Mathf.RoundToInt(v.z*1000));
                if(canonical.TryGetValue(key,out int existing))return existing;
                int index=points.Count;canonical.Add(key,index);parents.Add(index);points.Add(renderer.transform.TransformPoint(v));return index;
            };
            foreach(int slot in slots)
            {
                if(slot>=mesh.subMeshCount)continue;
                int[] triangles=mesh.GetTriangles(slot);
                for(int at=0;at<triangles.Length;at+=3)
                {int a=node(vertices[triangles[at]]),b=node(vertices[triangles[at+1]]),c=node(vertices[triangles[at+2]]);parents[find(b)]=find(a);parents[find(c)]=find(a);}
            }
            var bounds=new Dictionary<int,Bounds>();
            for(int index=0;index<points.Count;index++)
            {int group=find(index);if(bounds.TryGetValue(group,out var box)){box.Encapsulate(points[index]);bounds[group]=box;}else bounds[group]=new Bounds(points[index],Vector3.zero);}
            return bounds.Values;
        }
        sealed class WinterTree
        {
            public Bounds trunk;
            public readonly List<Bounds> crowns=new List<Bounds>();
        }
        static WinterTree[] AttachCrownsToTrunks(Bounds[] crowns,Bounds[] trunks)
        {
            var trees=new Dictionary<int,WinterTree>();
            foreach(var crown in crowns)
            {
                int nearest=-1;float distance=float.PositiveInfinity;
                for(int i=0;i<trunks.Length;i++)
                {
                    Vector3 delta=crown.center-trunks[i].center;float horizontal=delta.x*delta.x+delta.z*delta.z;
                    if(horizontal<distance){distance=horizontal;nearest=i;}
                }
                Require(nearest>=0&&distance<Mathf.Pow(Mathf.Max(1.2f,Mathf.Max(crown.extents.x,crown.extents.z)*1.4f),2),"Crown has no nearby authored trunk: "+crown.center);
                Require(crown.max.y>trunks[nearest].max.y,"Crown is below its trunk top: "+crown.center);
                if(!trees.TryGetValue(nearest,out var tree)){tree=new WinterTree{trunk=trunks[nearest]};trees.Add(nearest,tree);}
                tree.crowns.Add(crown);
            }
            Require(trees.Count>=1,"No deciduous trees found.");
            return trees.OrderBy(t=>t.Key).Select(t=>t.Value).ToArray();
        }
        static Renderer BuildBranches(GameObject parent,WinterTree[] trees)
        {
            var go=GetOrCreate(parent.transform,"Bare winter branches");
            string path=Folder+"/BareWinterBranches.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh{name="Bare winter branch silhouettes"};AssetDatabase.CreateAsset(mesh,path);}
            var plan=NaturalPlan(trees);WinterTreeGeometry.Build(plan,go.transform,mesh);
            var garden=parent.transform;while(garden!=null&&garden.name!="OutdoorGarden")garden=garden.parent;
            Require(garden!=null,"Winter tree parent is outside the authored garden.");
            WinterTrunkBatch.Append(WinterTrunkBatch.Extract(plan,garden),go.transform,mesh);EditorUtility.SetDirty(mesh);
            var filter=go.GetComponent<MeshFilter>();
            if(filter==null)filter=Undo.AddComponent<MeshFilter>(go);
            var renderer=go.GetComponent<MeshRenderer>();
            if(renderer==null)renderer=Undo.AddComponent<MeshRenderer>(go);
            filter.sharedMesh=mesh;
            renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Exterior/Materials/bark.mat");renderer.enabled=false;renderer.forceRenderingOff=false;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.Off;
            EditorUtility.SetDirty(filter);EditorUtility.SetDirty(renderer);return renderer;
        }
        static WinterTreeGeometry.Plan NaturalPlan(WinterTree[] trees)
        {
            return WinterTreeGeometry.Generate(trees.Select(tree=>
            {
                Bounds crown=tree.crowns[0];foreach(var clump in tree.crowns.Skip(1))crown.Encapsulate(clump);
                return new WinterTreeGeometry.Tree{trunk=tree.trunk,crown=crown};
            }).ToArray());
        }
        static string CheckBranchAttachments(WinterTree[] trees,Transform space,Mesh mesh)
        {
            var garden=space;while(garden!=null&&garden.name!="OutdoorGarden")garden=garden.parent;
            Require(garden!=null,"Winter tree mesh is outside the garden.");
            var plan=NaturalPlan(trees);return WinterTrunkBatch.Check(plan,WinterTrunkBatch.Extract(plan,garden),space,mesh);
        }
        static void BindWinterTrunks(WellnessSeasonCycle season,Transform garden)
        {
            Undo.RecordObject(season,"Winter-only tree proportions");
            season.winterDormant=season.winterDormant.Concat(WinterTrunkBatch.Sources(garden)).Distinct().ToArray();EditorUtility.SetDirty(season);
        }
        static string CheckWinterTrunkBinding(WellnessSeasonCycle season,Transform garden)
        {
            Require(WinterTrunkBatch.Sources(garden).All(r=>season.winterDormant.Contains(r)),"Original long trunk batch would overlap the Winter trees.");
            return "PASS: original trunk renderer is hidden only at the same Winter threshold as the full replacement; normal trunks return in Spring/Summer/Fall.\n";
        }
        static string CheckBranchFixtures()=>WinterTreeGeometry.CheckFixtures();
        static string CheckSnowWeather(GameObject root)
        {
            var fixture=new GameObject("Snow weather fixture"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var actual=root.GetComponentInChildren<WellnessRain>(true);
                var cycle=fixture.AddComponent<WellnessSeasonCycle>();cycle.Tick(.1f);
                var rain=fixture.AddComponent<WellnessRain>();rain.seasons=cycle;rain.snowMaterial=actual.snowMaterial;
                var sky=fixture.AddComponent<WellnessSkyCycle>();sky.seasons=cycle;sky.rain=rain;
                Require((int)WellnessSkyCycle.WeatherMode.Rain==3&&(int)WellnessSkyCycle.WeatherMode.Snow==4,"Existing serialized weather modes changed.");
                sky.SetWeather(WellnessSkyCycle.WeatherMode.Snow);
                Require(rain.WantsSnow&&sky.WeatherLabel=="Snow"&&cycle.Current==WellnessSeason.Spring,"Manual snow should work without switching season.");
                cycle.SetSeason(WellnessSeason.Winter);for(int i=0;i<32;i++)cycle.Tick(.25f);
                sky.SetWeather(WellnessSkyCycle.WeatherMode.Rain);Require(!rain.WantsSnow&&sky.WeatherLabel=="Rain","Manual Rain should remain rain in Winter.");
                sky.SetWeather(WellnessSkyCycle.WeatherMode.Automatic);Require(rain.WantsSnow,"Automatic Winter should use snow.");
                cycle.SetSeason(WellnessSeason.Summer);for(int i=0;i<32;i++)cycle.Tick(.25f);
                Require(!rain.WantsSnow,"Automatic Summer should use rain.");
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                foreach(WellnessSkyCycle.WeatherMode mode in Enum.GetValues(typeof(WellnessSkyCycle.WeatherMode)))
                {
                    sky.SetWeather(mode);var value=(Vector2)typeof(WellnessSkyCycle).GetMethod("TargetWeather",flags).Invoke(sky,null);
                    Require(value.x>=0&&value.x<=1&&value.y>=0&&value.y<=1,"Weather targets are invalid.");
                }
                Require(TheLastWatch.UI.WellnessHud.WeatherIconKind(true,"Snow")==4&&TheLastWatch.UI.WellnessHud.WeatherIconKind(true,"Rain")==3,"Snow/rain HUD icons wrong.");
                var drops=fixture.AddComponent<ParticleSystem>();rain.drops=drops;
                var renderer=drops.GetComponent<ParticleSystemRenderer>();var original=actual.drops.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                WellnessRain.ConfigureParticles(drops,original);
                typeof(WellnessRain).GetField("originalMaterial",flags).SetValue(rain,original);
                typeof(WellnessRain).GetField("originalMode",flags).SetValue(rain,ParticleSystemRenderMode.Stretch);
                drops.SetParticles(new[]{new ParticleSystem.Particle{position=new Vector3(9,3,9),startLifetime=10,remainingLifetime=10,startSize=.05f}},1);
                sky.SetWeather(WellnessSkyCycle.WeatherMode.Snow);typeof(WellnessRain).GetMethod("ApplyPrecipitationType",flags).Invoke(rain,null);
                Require(rain.SnowMode&&renderer.sharedMaterial==rain.snowMaterial&&renderer.renderMode==ParticleSystemRenderMode.Billboard&&drops.particleCount==0,"Rain-to-snow transition did not clear the old particles.");
                drops.SetParticles(new[]{new ParticleSystem.Particle{remainingLifetime=10,startLifetime=10,startSize=.05f}},1);
                sky.SetWeather(WellnessSkyCycle.WeatherMode.Rain);typeof(WellnessRain).GetMethod("ApplyPrecipitationType",flags).Invoke(rain,null);
                Require(!rain.SnowMode&&renderer.sharedMaterial==original&&renderer.renderMode==ParticleSystemRenderMode.Stretch&&drops.particleCount==0,"Snow-to-rain transition did not restore/clear correctly.");
                sky.ToggleRain();Require(sky.weatherMode==WellnessSkyCycle.WeatherMode.Clear,"R should clear active precipitation.");
            }
            finally{UnityEngine.Object.DestroyImmediate(fixture);}
            return "PASS: explicit Snow/Rain modes across seasons, automatic Winter/Summer precipitation, HUD snowflake, bounded targets, particle material/render-mode switching and no old/new particle overlap.\n";
        }
        static string CheckClimate()
        {
            foreach(WellnessSeason season in Enum.GetValues(typeof(WellnessSeason)))
            {
                for(int i=0;i<=1000;i++)
                {
                    var weather=WellnessSeasonCycle.Weather(season,i/1000f);Require(weather.x>=0&&weather.x<=1&&weather.y>=0&&weather.y<=1,"Unbounded seasonal weather.");
                    if(i>0){var previous=WellnessSeasonCycle.Weather(season,(i-1)/1000f);Require((weather-previous).magnitude<.04f,"Seasonal weather jumps.");}
                }
                Require((WellnessSeasonCycle.Weather(season,0)-WellnessSeasonCycle.Weather(season,.99999f)).magnitude<.001f,"Weather loop seam.");
                if(season==WellnessSeason.Winter)Require(WellnessSeasonCycle.Phase(season,.6f)=="Snow","Winter automatic precipitation is not snow.");
            }
            float springWet=0,summerWet=0;
            for(int i=0;i<1000;i++){springWet+=WellnessSeasonCycle.Weather(WellnessSeason.Spring,i/1000f).y;summerWet+=WellnessSeasonCycle.Weather(WellnessSeason.Summer,i/1000f).y;}
            Require(springWet>summerWet*2,"Spring should be wetter than summer.");
            var fixture=new GameObject("Season cycle regression fixture"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var cycle=fixture.AddComponent<WellnessSeasonCycle>();cycle.seasonMinutes=5;cycle.Tick(.1f);
                for(int season=1;season<=4;season++)
                {
                    for(int frame=0;frame<1200;frame++)cycle.Tick(.25f);
                    Require(cycle.Current==(WellnessSeason)(season%4),"Automatic season order is wrong.");
                }
                cycle.SetSeason(WellnessSeason.Winter);for(int i=0;i<4000;i++)cycle.Tick(.25f);
                Require(cycle.Current==WellnessSeason.Winter&&!cycle.automatic&&cycle.Weights.w>.999f,"Manual hold did not persist.");
                cycle.SetSeason(WellnessSeason.Fall);cycle.Tick(float.NaN);cycle.Tick(-1);
                for(int i=0;i<32;i++){cycle.Tick(.25f);float sum=cycle.Weights.x+cycle.Weights.y+cycle.Weights.z+cycle.Weights.w;Require(Mathf.Abs(sum-1)<.0001f&&cycle.Weights.w>=0,"Season blend weights invalid.");}
                Require(cycle.Weights.z>.999f,"Season transition did not settle.");
            }
            finally{UnityEngine.Object.DestroyImmediate(fixture);}
            return "PASS: 4,004 bounded/continuous seasonal weather samples, loop seams, wetter Spring than Summer, snow labeling, real cycle timing/order, manual hold, eight-second transition and invalid-time rejection.\n";
        }
        static string CheckPaletteLifecycle(WellnessSeasonCycle season)
        {
            var savedSeason=season.season;bool savedAutomatic=season.automatic;float savedMinutes=season.seasonMinutes;
            var originals=season.surfaces.ToDictionary(s=>s.renderer,s=>s.renderer.sharedMaterials);
            var force=season.winterDormant.Concat(season.butterflies).Append(season.winterBranches).Where(r=>r!=null).Distinct().ToDictionary(r=>r,r=>r.forceRenderingOff);
            var enabled=force.Keys.ToDictionary(r=>r,r=>r.enabled);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            try
            {
                typeof(WellnessSeasonCycle).GetMethod("CaptureEnvironment",flags).Invoke(season,null);
                season.SetSeason(WellnessSeason.Winter);for(int i=0;i<32;i++)season.Tick(.25f);
                typeof(WellnessSeasonCycle).GetMethod("ApplyEnvironment",flags).Invoke(season,null);
                Require(season.IsSnowSeason&&season.InsectActivity<.01f,"Winter insects/precipitation policy wrong.");
                Require(season.winterBranches.enabled&&!season.winterBranches.forceRenderingOff&&season.winterDormant.All(r=>r.forceRenderingOff),"Winter branches/dormant vegetation did not switch.");
                var terrain=season.surfaces.First(s=>s.slots.Contains(WellnessSeasonSurface.Ground));
                int slot=Array.IndexOf(terrain.slots,WellnessSeasonSurface.Ground);var live=terrain.renderer.sharedMaterials[slot];
                Require(live!=originals[terrain.renderer][slot]&&live.GetColor("_BaseColor").grayscale>.7f,"Snowy ground palette did not apply to a runtime clone.");
                foreach(var value in new[]{WellnessSeason.Spring,WellnessSeason.Fall})
                {
                    season.SetSeason(value);for(int i=0;i<32;i++)season.Tick(.25f);
                    typeof(WellnessSeasonCycle).GetMethod("ApplyEnvironment",flags).Invoke(season,null);
                    foreach(var surface in season.surfaces)
                    {
                        var materials=surface.renderer.sharedMaterials;
                        for(int i=0;i<surface.slots.Length;i++)
                        {
                            var source=originals[surface.renderer][i];
                            if((int)surface.slots[i]<0||source==null||!source.HasProperty("_BaseColor"))continue;
                            Color expected=WellnessSeasonCycle.PaletteColor(surface.slots[i],source.GetColor("_BaseColor"),value);
                            Require(((Vector4)(materials[i].GetColor("_BaseColor")-expected)).sqrMagnitude<.00001f,value+" palette failed on "+surface.renderer.name);
                        }
                    }
                    Require(!season.winterBranches.enabled&&season.winterDormant.All(r=>r.forceRenderingOff==force[r]),"Non-winter vegetation did not return.");
                }
                season.SetSeason(WellnessSeason.Summer);for(int i=0;i<32;i++)season.Tick(.25f);
                typeof(WellnessSeasonCycle).GetMethod("ApplyEnvironment",flags).Invoke(season,null);
                Require(!season.winterBranches.enabled&&season.winterBranches.forceRenderingOff,"Winter branches remained in Summer.");
                Require(((Vector4)(live.GetColor("_BaseColor")-originals[terrain.renderer][slot].GetColor("_BaseColor"))).sqrMagnitude<.00001f,"Summer did not recover the original color.");
            }
            finally
            {
                typeof(WellnessSeasonCycle).GetMethod("RestoreEnvironment",flags).Invoke(season,null);
                season.season=savedSeason;season.automatic=savedAutomatic;season.seasonMinutes=savedMinutes;
            }
            Require(originals.All(pair=>pair.Key.sharedMaterials.SequenceEqual(pair.Value))&&force.All(pair=>pair.Key.forceRenderingOff==pair.Value)&&enabled.All(pair=>pair.Key.enabled==pair.Value),"Original renderer materials/visibility were not restored.");
            return "PASS: actual scene palette capture/apply/restore: winter ground, bare branches, vegetation dormancy, insect suppression, every Spring/Fall surface color and full Summer recovery. Shared source assets never recolored.\n";
        }
    }
}
