using System;
using TheLastWatch.Environment;
using UnityEngine;

namespace TheLastWatch.UI
{
    public readonly struct KoiFishingLootEntry
    {
        public readonly string Variety,Rarity;
        public readonly int Weight,Rank;
        public KoiFishingLootEntry(string variety,string rarity,int weight,int rank)
        {Variety=variety;Rarity=rarity;Weight=weight;Rank=rank;}
    }
    public static class KoiFishingLoot
    {
        // Gameplay collection tiers, not claims about biological koi rarity.
        static readonly KoiFishingLootEntry[] entries={
            new KoiFishingLootEntry("kohaku","Common",3500,0),new KoiFishingLootEntry("benigoi","Common",2500,0),
            new KoiFishingLootEntry("sanke","Uncommon",1800,1),new KoiFishingLootEntry("showa","Uncommon",1200,1),
            new KoiFishingLootEntry("asagi","Rare",600,2),new KoiFishingLootEntry("ogon","Epic",250,3),
            new KoiFishingLootEntry("tancho","Legendary",125,4),new KoiFishingLootEntry("kigoi","Godly",25,5)};
        static readonly string[] names={"Common","Uncommon","Rare","Epic","Legendary","Godly"};
        static readonly Color[] colors={new Color(.94f,.94f,.88f),new Color(.56f,.92f,.62f),new Color(.43f,.76f,1),new Color(.81f,.61f,1),new Color(1,.79f,.35f),new Color(1,.47f,.75f)};
        public static KoiFishingLootEntry ForVariety(string variety)
        {foreach(var entry in entries)if(entry.Variety==variety)return entry;return new KoiFishingLootEntry(variety,"Common",1000,0);}
        public static bool ValidRarity(string rarity)=>string.IsNullOrEmpty(rarity)||Array.IndexOf(names,rarity)>=0;
        public static int Rank(string rarity)=>Mathf.Max(0,Array.IndexOf(names,rarity));
        public static Color ColorFor(string rarity)=>colors[Rank(rarity)];
        public static int TotalWeight(KoiPondLibrary library)
        {if(library==null||library.varieties==null||library.varieties.Length==0)throw new ArgumentException("Koi library missing.");int total=0;foreach(var variety in library.varieties)total+=ForVariety(variety.name).Weight;return total;}
        public static int IndexForRoll(KoiPondLibrary library,int roll)
        {
            int total=TotalWeight(library);if(roll<0||roll>=total)throw new ArgumentOutOfRangeException(nameof(roll));
            for(int i=0;i<library.varieties.Length;i++){roll-=ForVariety(library.varieties[i].name).Weight;if(roll<0)return i;}
            throw new InvalidOperationException("Koi loot table was inconsistent.");
        }
        public static int PickIndex(KoiPondLibrary library,System.Random random)=>IndexForRoll(library,random.Next(TotalWeight(library)));
    }
}
