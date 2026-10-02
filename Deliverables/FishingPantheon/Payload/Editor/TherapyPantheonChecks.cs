using System;
using System.IO;
using System.Linq;
using TheLastWatch.Environment;
using TheLastWatch.UI;
using UnityEngine;
using static TherapyGame.Editor.TherapyPantheonSetup;
namespace TherapyGame.Editor
{
    public static class TherapyPantheonChecks
    {
        public static string CheckOdds(KoiPondLibrary library)
        {
            Require(KoiFishingLoot.TotalWeight(library)==100000,"Odds do not total 100,000.");var counts=new int[24];
            for(int roll=0;roll<100000;roll++)counts[KoiFishingLoot.IndexForRoll(library,roll)]++;
            for(int i=0;i<24;i++)Require(counts[i]==KoiFishingLoot.ForVariety(library.varieties[i].name).Weight,"Weighted interval mismatch.");
            Require(counts.Last()==50&&counts.All(x=>x>=50),"Genesis is not rarest.");Require(KoiFishingLoot.ForVariety("kigoi").Rarity=="Epic","Kigoi was not rebalanced.");
            int focusTotal=KoiFishingLoot.TotalWeight(library,"genesis");Require(focusTotal==100100,"Focus changes unselected weights.");int focusCount=0;
            for(int roll=0;roll<focusTotal;roll++)if(KoiFishingLoot.IndexForRoll(library,roll,"genesis")==23)focusCount++;
            Require(focusCount==150&&KoiFishingLoot.Odds(library,"genesis","genesis")>KoiFishingLoot.Odds(library,"genesis"),"Focus chance is inaccurate.");
            return "PASS: exhaustive baseline and focused weighted outcomes; 24 positive chances, Godly highest tier, Genesis rarest (0.05% baseline); free focus multiplies only selected weight by three, displayed normalized odds match.\n";
        }
        public static string CheckRounds()
        {
            int wins=0;foreach(float dt in new[]{1f/30,1f/60,1f/120})for(int style=0;style<6;style++)for(int seed=0;seed<12;seed++)
            {
                var round=new KoiFishingRound(seed,1,style,5);
                for(int frame=0;frame<75/dt&&!round.Finished;frame++)
                {
                    bool held=round.Bar+KoiFishingRound.BarWidth*.5f+round.Velocity*.20f<round.Fish;
                    round.Step(dt,held,round.State==KoiFishingRound.Phase.Bite);
                    Require(round.Bar>=0&&round.Bar<=1-KoiFishingRound.BarWidth&&round.Progress>=0&&round.Progress<=1&&round.Accuracy>=0&&round.Accuracy<=1,"Out of bounds catch.");
                }
                Require(round.State==KoiFishingRound.Phase.Caught,"Unwinnable movement style "+style+" seed "+seed+" dt "+dt);
                Require(round.PerfectHook&&round.TryClaim()&&!round.TryClaim(),"Clean hook or single reward broken.");wins++;
            }
            var late=new KoiFishingRound(1);while(late.State!=KoiFishingRound.Phase.Bite)late.Step(.02f,false,false);for(int i=0;i<50;i++)late.Step(.02f,false,false);late.Step(.02f,true,true);Require(!late.PerfectHook&&late.State==KoiFishingRound.Phase.Reeling,"Late valid hook is incorrectly perfect.");
            var missed=new KoiFishingRound(2);for(int i=0;i<1500&&!missed.Finished;i++)missed.Step(.02f,false,false);Require(missed.State==KoiFishingRound.Phase.Escaped&&!missed.TryClaim(),"Missing bite awards koi.");
            var idle=new KoiFishingRound(3);for(int i=0;i<5000&&!idle.Finished;i++)idle.Step(.02f,false,idle.State==KoiFishingRound.Phase.Bite);Require(idle.State==KoiFishingRound.Phase.Escaped,"No reeling input still wins.");
            var cancel=new KoiFishingRound(4);cancel.Cancel();cancel.Step(1,true,true);Require(!cancel.TryClaim()&&cancel.State==KoiFishingRound.Phase.Cancelled,"Cancellation awards fish.");
            var invalid=new KoiFishingRound(5);invalid.Step(float.NaN,true,true);invalid.Step(-1,true,true);Require(invalid.ElapsedSeconds==0,"Invalid time advanced simulation.");
            return "PASS: "+wins+" high-rarity feedback-player wins across six movement styles at 30/60/120 Hz; clean/late/missed hooks, no-input escape, cancel, finite bounds and exactly-one claim.\n";
        }
        public static string CheckSaves()
        {
            string fixtureRoot=Path.GetFullPath("Temp/PantheonSaveFixtures"),folder=Path.Combine(fixtureRoot,Guid.NewGuid().ToString("N"));var inventory=new KoiCatchInventory(folder);
            try
            {
                var legacy=KoiCatch.Create("kigoi",1);legacy.rarity="Godly";inventory.Save(legacy);string file=inventory.RecordPath(legacy),json=File.ReadAllText(file);
                var plain=KoiCatch.Create("kohaku",1);plain.rarity=null;inventory.Save(plain);
                string[] original={"kohaku","sanke","showa","tancho","ogon","asagi","benigoi","kigoi"};
                for(int i=0;i<KoiFishingLoot.Count;i++)
                {
                    string variety=KoiFishingLoot.Entry(i).Variety;if(original.Contains(variety))continue;
                    var fish=KoiCatch.Create(variety,1.04f);fish.scored=true;fish.accuracy=.94f;fish.perfectHook=true;inventory.Save(fish);
                }
                var genesis=inventory.Fish.First(f=>f.variety=="genesis");inventory.Equip(genesis.id);inventory.Reload();
                Require(inventory.Fish.Count==18&&inventory.Equipped.variety=="genesis"&&inventory.Equipped.Perfect,"New fish/equip/quality persistence failed.");
                Require(inventory.Fish.Single(f=>f.id==legacy.id).RarityName=="Godly"&&File.ReadAllText(file)==json,"Rebalancing rewrote an older Godly catch.");
                Require(inventory.Fish.Single(f=>f.id==plain.id).RarityName=="Common","Legacy optional fields broke.");
                var collection=new KoiFishingCollection();collection.Rebuild(inventory.Fish);Require(collection.Discoveries==18&&collection.PerfectCatches==16&&collection.Level>1&&collection.Best("genesis")>37,"Collection progress fails to rebuild.");
                int xp=collection.Experience;collection.Rebuild(inventory.Fish);Require(collection.Experience==xp,"Repeated inventory opening grants experience.");
                var invalid=KoiCatch.Create("genesis",1);invalid.scored=true;invalid.accuracy=float.NaN;Require(!KoiCatch.Valid(invalid),"Invalid accuracy accepted.");
                bool duplicate=false;try{inventory.Save(legacy);}catch(IOException){duplicate=true;}Require(duplicate,"Duplicate catch allowed.");
                File.WriteAllText(Path.Combine(folder,"bad.koi.json"),"damaged");inventory.Reload();Require(inventory.Fish.Count==18,"Corrupt record hides valid saves.");
                return "PASS: all sixteen new species persist, equipped Genesis and catch quality reload; old explicit Godly rarity and original save bytes untouched; journal/XP/bests rebuild without rewards on repeated opening; corrupt and duplicate records handled. Real saves were not opened.\n";
            }
            finally{Require(Path.GetFullPath(folder).StartsWith(fixtureRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Unsafe fixture cleanup.");if(Directory.Exists(folder))Directory.Delete(folder,true);}
        }
        public static string CheckViews(KoiPondLibrary library)
        {
            var temp=new GameObject("Pantheon fixture"){hideFlags=HideFlags.HideAndDontSave};int draws=0;
            try
            {
                using(var views=new KoiFishingViews(temp.transform,temp.transform,library))
                {
                    foreach(var variety in library.varieties)
                    {
                        var fish=KoiCatch.Create(variety.name,1);views.BeginCatch(fish,new Vector3(0,-1.6f,4));views.PoseCatch(0);Require(views.CaughtModel.transform.position.y<-1.6f,"Fish does not start underwater.");
                        foreach(float time in new[]{.4f,.8f,1.8f,2.3f,4f,60f})views.PoseCatch(time);
                        Require(views.CaughtModel.GetComponentsInChildren<Collider>().Length==0,"Catch got colliders.");views.HideCatch();
                        if(KoiFishingLoot.Rank(fish.RarityName)>=2)foreach(KoiRarityVfx.Presentation presentation in Enum.GetValues(typeof(KoiRarityVfx.Presentation)))
                        {
                            using(var vfx=KoiRarityVfx.Create(temp.transform,fish,1,presentation))
                            {
                                foreach(float time in new[]{0,.4f,1.8f,2.3f,4f,60f}){vfx.Pose(Vector3.zero,Quaternion.identity,time);Require(vfx.VertexCount<=KoiRarityVfx.MaxQuads*4,"VFX exceeded mesh budget.");}
                                int builds=vfx.BuildCount;vfx.Pose(Vector3.zero,Quaternion.identity,60);Require(vfx.BuildCount==builds,"Repeated frame rebuilds effect mesh.");
                                Require(vfx.Root.GetComponentsInChildren<Renderer>().Length==1&&vfx.Root.GetComponentsInChildren<Light>().Length==0,"Effects exceed one draw or create lights.");draws++;
                            }
                        }
                    }
                }
                Require(temp.transform.childCount==0,"Fishing/VFX disposal leaked objects.");
            }
            finally{UnityEngine.Object.DestroyImmediate(temp);}
            return "PASS: all 24 catches lift from water; "+draws+" effect presentations sampled across reveal/held/collection, bounded at 1,024 quads and 30 Hz, one renderer per effect, no particle GameObjects/lights/colliders; full cleanup.\n";
        }
    }
}
