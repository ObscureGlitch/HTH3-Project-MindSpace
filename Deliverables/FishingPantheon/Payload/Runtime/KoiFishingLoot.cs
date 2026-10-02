using System;
using TheLastWatch.Environment;
using UnityEngine;

namespace TheLastWatch.UI
{
    public readonly struct KoiFishingLootEntry
    {
        public readonly string Variety,Rarity,Signature,Behavior;
        public readonly int Weight,Rank,Style;
        public KoiFishingLootEntry(string variety,int rank,int weight,string signature="",int style=0)
        {Variety=variety;Rank=rank;Rarity=KoiFishingLoot.Tier(rank);Weight=weight;Signature=signature;Style=style;Behavior=KoiFishingLoot.Movement(style);}
    }
    public static class KoiFishingLoot
    {
        // Collection tiers, not biological claims. Baseline 100,000 outcomes.
        static readonly string[] names={"Common","Uncommon","Rare","Epic","Legendary","Godly"};
        static readonly string[] movement={"Gentle cruise","Quick surges","Swaying current","Playful feints","Wide sweeps","Celestial dance"};
        static readonly KoiFishingLootEntry[] entries={
            new KoiFishingLootEntry("kohaku",0,26000),new KoiFishingLootEntry("benigoi",0,18000),
            new KoiFishingLootEntry("sanke",1,14500,"",1),new KoiFishingLootEntry("showa",1,9500,"",3),
            new KoiFishingLootEntry("asagi",2,7000,"Azure Wake",2),new KoiFishingLootEntry("kujaku",2,5000,"Platinum shimmer",0),new KoiFishingLootEntry("kumonryu",2,4000,"Ink and snow",3),
            new KoiFishingLootEntry("ogon",3,2800,"Golden current",4),new KoiFishingLootEntry("kigoi",3,2400,"Amber glow",0),
            new KoiFishingLootEntry("yurei",3,1800,"Ghostlight",2),new KoiFishingLootEntry("sakura",3,1600,"Petal drift",2),new KoiFishingLootEntry("jade",3,1400,"Jade radiance",4),
            new KoiFishingLootEntry("tancho",4,1200,"Solar Crown",0),new KoiFishingLootEntry("ryujin",4,800,"Dragonfire",1),new KoiFishingLootEntry("raijin",4,650,"Thunder orbit",1),
            new KoiFishingLootEntry("hoo",4,650,"Phoenix embers",4),new KoiFishingLootEntry("yuki",4,650,"Frostfall",2),new KoiFishingLootEntry("abyss",4,550,"Abyssal beacon",3),
            new KoiFishingLootEntry("nebula",4,500,"Starlit orbit",4),new KoiFishingLootEntry("kitsune",4,500,"Nine spirit flames",3),
            new KoiFishingLootEntry("amaterasu",5,200,"Solar divinity",5),new KoiFishingLootEntry("tsukuyomi",5,150,"Moonlit grace",5),
            new KoiFishingLootEntry("void",5,100,"Rift sovereign",5),new KoiFishingLootEntry("genesis",5,50,"Prismatic genesis",5)};
        static readonly Color[] colors={new Color(.94f,.94f,.88f),new Color(.56f,.92f,.62f),new Color(.43f,.76f,1),new Color(.81f,.61f,1),new Color(1,.79f,.35f),new Color(1,.47f,.75f)};
        public static KoiFishingLootEntry ForVariety(string variety)
        {foreach(var entry in entries)if(entry.Variety==variety)return entry;return new KoiFishingLootEntry(variety,0,1000);}
        public static int Count=>entries.Length;
        public static KoiFishingLootEntry Entry(int index)=>entries[index];
        public static string Tier(int rank)=>names[Mathf.Clamp(rank,0,5)];
        public static string Movement(int style)=>movement[Mathf.Clamp(style,0,5)];
        public static string DisplayName(string variety)
        {if(variety=="hoo")return "Hōō koi";return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(variety??"Unknown")+" koi";}
        public static bool ValidRarity(string rarity)=>string.IsNullOrEmpty(rarity)||Array.IndexOf(names,rarity)>=0;
        public static int Rank(string rarity)=>Mathf.Max(0,Array.IndexOf(names,rarity));
        public static Color ColorFor(string rarity)=>colors[Rank(rarity)];
        static int Weight(string variety,string focus)=>ForVariety(variety).Weight*(variety==focus?3:1);
        public static int TotalWeight(KoiPondLibrary library,string focus=null)
        {if(library==null||library.varieties==null||library.varieties.Length==0)throw new ArgumentException("Koi library missing.");int total=0;foreach(var variety in library.varieties)total+=Weight(variety.name,focus);return total;}
        public static float Odds(KoiPondLibrary library,string variety,string focus=null)=>Weight(variety,focus)/(float)TotalWeight(library,focus);
        public static int IndexForRoll(KoiPondLibrary library,int roll,string focus=null)
        {
            int total=TotalWeight(library,focus);if(roll<0||roll>=total)throw new ArgumentOutOfRangeException(nameof(roll));
            for(int i=0;i<library.varieties.Length;i++){roll-=Weight(library.varieties[i].name,focus);if(roll<0)return i;}
            throw new InvalidOperationException("Koi loot table was inconsistent.");
        }
        public static int PickIndex(KoiPondLibrary library,System.Random random,string focus=null)=>IndexForRoll(library,random.Next(TotalWeight(library,focus)),focus);
    }
}
