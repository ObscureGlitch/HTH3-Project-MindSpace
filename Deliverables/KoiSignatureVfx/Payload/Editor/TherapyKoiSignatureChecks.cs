using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TheLastWatch.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using static TherapyGame.Editor.TherapyPantheonSetup;
namespace TherapyGame.Editor
{
    public static class TherapyKoiSignatureChecks
    {
        public const string Output="D:/Hack the Hill/Deliverables/KoiSignatureVfx/Checks/";
        public static void VerifyBatch()
        {
            try
            {
                Require(!EditorApplication.isPlaying,"Do not enter Play mode.");EditorSceneManager.OpenScene("Assets/TherapyGame/Scenes/TherapyRoom.unity");var fishing=Object.FindAnyObjectByType<WellnessFishing>();var library=fishing.Library;
                var root=new GameObject("Signature checks"){hideFlags=HideFlags.HideAndDontSave};var report=new List<string>();var themes=new HashSet<string>();int legendaryMax=0,godlyMin=int.MaxValue,presentations=0,lower=0;
                try
                {
                    foreach(var variety in library.varieties)
                    {
                        var fish=KoiCatch.Create(variety.name,1);int rank=KoiFishingLoot.Rank(fish.RarityName);
                        if(rank<4)
                        {
                            foreach(KoiRarityVfx.Presentation p in Enum.GetValues(typeof(KoiRarityVfx.Presentation)))Require(KoiRarityVfx.Create(root.transform,fish,1,p)==null,"Lower tier received an aura: "+variety.name);
                            Require(KoiRarityVfx.Signature(fish)=="","Lower tier advertises an active aura.");lower++;continue;
                        }
                        Require(themes.Add(KoiRarityVfx.ThemeFor(variety.name)),"Duplicated species signature.");
                        foreach(KoiRarityVfx.Presentation p in Enum.GetValues(typeof(KoiRarityVfx.Presentation)))
                        {
                            using(var fx=KoiRarityVfx.Create(root.transform,fish,1,p))
                            {
                                foreach(float t in new[]{0f,.2f,.4f,1.8f,2.3f,3.1f,10f,60f,3600f,float.NaN,float.PositiveInfinity,-1f})
                                {
                                    fx.Pose(Vector3.zero,Quaternion.identity,t);Require(fx.VertexCount>0&&fx.VertexCount<=KoiRarityVfx.MaxQuads*4,"Effect geometry budget exceeded.");
                                    var mesh=fx.Root.GetComponent<MeshFilter>().sharedMesh;
                                    foreach(var v in mesh.vertices)Require(float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&mesh.bounds.Contains(v),"Nonfinite or clipped effect bounds: "+variety.name);
                                    foreach(var c in mesh.colors)Require(float.IsFinite(c.r)&&float.IsFinite(c.g)&&float.IsFinite(c.b)&&float.IsFinite(c.a)&&c.a>=0&&c.a<=1,"Invalid glow color.");
                                }
                                fx.Pose(Vector3.zero,Quaternion.identity,60);int builds=fx.BuildCount;fx.Pose(Vector3.one,Quaternion.identity,60.0001f);Require(fx.BuildCount==builds&&fx.Root.transform.position==Vector3.one,"Effect rebuilds within same 30 Hz tick or leaves stale pose.");
                                fx.SetVisible(false);fx.Pose(Vector3.zero,Quaternion.identity,61);Require(fx.BuildCount==builds,"Hidden effect still updates.");fx.SetVisible(true);fx.Pose(Vector3.zero,Quaternion.identity,62);
                                Require(fx.Root.GetComponentsInChildren<Renderer>().Length==1&&fx.Root.GetComponentsInChildren<Light>().Length==0&&fx.Root.GetComponentsInChildren<Collider>().Length==0&&fx.Root.GetComponentsInChildren<ParticleSystem>().Length==0,"Effect creates extra draws/lights/colliders/particles.");
                                if(p==KoiRarityVfx.Presentation.Held)Require(Mathf.Abs(fx.Root.transform.localScale.x-.65f)<.001f,"Held aura is not compact.");
                                if(p==KoiRarityVfx.Presentation.Collection){if(rank==4)legendaryMax=Mathf.Max(legendaryMax,fx.VertexCount);else godlyMin=Mathf.Min(godlyMin,fx.VertexCount);report.Add(variety.name+" / "+fx.Theme+" / "+fish.RarityName+": "+fx.VertexCount/4+" quads, primary "+fx.Primary+", secondary "+fx.Secondary);}
                                presentations++;
                            }
                        }
                    }
                    Require(themes.Count==12&&lower==12&&presentations==36,"Wrong tier/species coverage.");Require(godlyMin>legendaryMax,"Godly signatures are not more elaborate than Legendary.");
                    var legacy=KoiCatch.Create("kigoi",1);legacy.rarity="Godly";using(var fx=KoiRarityVfx.Create(root.transform,legacy,1,KoiRarityVfx.Presentation.Catch)){Require(fx!=null&&fx.Rank==5,"Legacy stored rarity ignored.");fx.Pose(Vector3.zero,Quaternion.identity,2.3f);}
                    Require(root.transform.childCount==0,"Effects not fully disposed.");
                }
                finally{Object.DestroyImmediate(root);}
                var shader=Resources.Load<Shader>("KoiSignatureAura");Require(shader!=null&&!ShaderUtil.ShaderHasError(shader),"Signature shader failed.");
                File.WriteAllText(Output+"SignatureCheck.txt","PASS: twelve unique species signatures; all twelve lower-tier models have no aura in Catch, Held or Collection.\nPASS: 36 effect presentations, <=1024 quads, valid bounds/colors at reveal/long-running/invalid times, one renderer, 30 Hz cap, hidden pause, compact held scale and complete disposal.\nPASS: every Godly signature has more persistent detail than the most detailed Legendary. Legacy stored Godly rarity preserved.\n"+string.Join("\n",report)+"\n"+TherapyPantheonChecks.CheckOdds(library)+TherapyPantheonChecks.CheckRounds()+TherapyPantheonChecks.CheckSaves()+TherapyPantheonChecks.CheckViews(library));
                Debug.Log("KOI_SIGNATURE_CHECKS_PASSED");EditorApplication.Exit(0);
            }
            catch(Exception e){File.WriteAllText(Output+"SignatureCheck.txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
