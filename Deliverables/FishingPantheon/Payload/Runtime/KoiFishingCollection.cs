using System.Collections.Generic;
using UnityEngine;
namespace TheLastWatch.UI
{
    // Rebuilt on a catch or inventory opening, never by per-frame save scans.
    public sealed class KoiFishingCollection
    {
        readonly Dictionary<string,int> counts=new Dictionary<string,int>();
        readonly Dictionary<string,float> best=new Dictionary<string,float>();
        public int Discoveries=>counts.Count;
        public int Experience {get;private set;}
        public int PerfectCatches {get;private set;}
        public int Level=>1+Mathf.FloorToInt(Mathf.Sqrt(Experience/90f));
        public string Title=>Level<3?"Pond wanderer":Level<6?"Koi keeper":Level<10?"Current reader":Level<16?"Pond naturalist":"Pantheon angler";
        public int Count(string variety)=>counts.TryGetValue(variety,out int count)?count:0;
        public float Best(string variety)=>best.TryGetValue(variety,out float value)?value:0;
        public void Rebuild(IReadOnlyList<KoiCatch> fish)
        {
            counts.Clear();best.Clear();Experience=PerfectCatches=0;
            foreach(var koi in fish)
            {
                int n=Count(koi.variety);counts[koi.variety]=n+1;best[koi.variety]=Mathf.Max(Best(koi.variety),koi.lengthCm);
                Experience+=8+KoiFishingLoot.Rank(koi.RarityName)*6+(n==0?30:0)+(koi.Perfect?12:0)+(koi.perfectHook?4:0);
                if(koi.Perfect)PerfectCatches++;
            }
        }
    }
}
