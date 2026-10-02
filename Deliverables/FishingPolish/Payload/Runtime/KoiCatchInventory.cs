using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TheLastWatch.UI
{
    [Serializable] public sealed class KoiCatch
    {
        public string id,variety,caughtUtc,rarity;
        // Legacy records have no rarity field. Resolve it without rewriting saves.
        public string RarityName=>string.IsNullOrEmpty(rarity)?KoiFishingLoot.ForVariety(variety).Rarity:rarity;
        public float lengthCm,weightKg,scale;
        public static KoiCatch Create(string variety,float scale)=>new KoiCatch{id=Guid.NewGuid().ToString("N"),variety=variety,
            caughtUtc=DateTime.UtcNow.ToString("o"),rarity=KoiFishingLoot.ForVariety(variety).Rarity,scale=scale,lengthCm=36*scale,weightKg=.8f*scale*scale*scale};
        public static bool Valid(KoiCatch fish)=>fish!=null&&Guid.TryParseExact(fish.id,"N",out _)&&
            !string.IsNullOrWhiteSpace(fish.variety)&&fish.variety.Length<=64&&DateTime.TryParse(fish.caughtUtc,out _)&&KoiFishingLoot.ValidRarity(fish.rarity)&&
            float.IsFinite(fish.scale)&&fish.scale>=.8f&&fish.scale<=1.2f&&float.IsFinite(fish.lengthCm)&&fish.lengthCm>0&&fish.lengthCm<100&&
            float.IsFinite(fish.weightKg)&&fish.weightKg>0&&fish.weightKg<10;
    }
    public sealed class KoiCatchInventory
    {
        public const int Capacity=500;
        public readonly List<KoiCatch> Fish=new List<KoiCatch>();
        public string DirectoryPath {get;}
        public string EquippedId {get;private set;}
        public KoiCatch Equipped=>Fish.Find(f=>f.id==EquippedId);
        public KoiCatchInventory(string directory){DirectoryPath=Path.GetFullPath(directory);}
        public string RecordPath(KoiCatch fish)
        {if(!KoiCatch.Valid(fish))throw new ArgumentException("Invalid koi record.");return Path.Combine(DirectoryPath,fish.id+".koi.json");}
        public void Reload()
        {
            Fish.Clear();EquippedId=null;if(!Directory.Exists(DirectoryPath))return;
            var ids=new HashSet<string>();
            foreach(string file in Directory.EnumerateFiles(DirectoryPath,"*.koi.json"))
            {
                try
                {
                    if(new FileInfo(file).Length>4096)continue;
                    var fish=JsonUtility.FromJson<KoiCatch>(File.ReadAllText(file));
                    if(!KoiCatch.Valid(fish)||Path.GetFileName(file)!=fish.id+".koi.json"||!ids.Add(fish.id))continue;
                    Fish.Add(fish);
                }
                catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}
            }
            Fish.Sort((a,b)=>string.CompareOrdinal(b.caughtUtc,a.caughtUtc));
            string equip=Path.Combine(DirectoryPath,"equipped.txt");
            if(File.Exists(equip)&&new FileInfo(equip).Length<=64){string id=File.ReadAllText(equip).Trim();if(Fish.Exists(f=>f.id==id))EquippedId=id;}
        }
        public void Save(KoiCatch fish)
        {
            if(Fish.Count>=Capacity)throw new IOException("Your koi inventory is full. This catch cannot be stored.");
            string path=RecordPath(fish);Directory.CreateDirectory(DirectoryPath);
            if(File.Exists(path)||File.Exists(path+".tmp"))throw new IOException("Catch ID already exists.");
            try{File.WriteAllText(path+".tmp",JsonUtility.ToJson(fish));File.Move(path+".tmp",path);Fish.Insert(0,fish);}
            finally{if(File.Exists(path+".tmp"))File.Delete(path+".tmp");}
        }
        public void Equip(string id)
        {
            if(id!=null&&!Fish.Exists(f=>f.id==id))throw new ArgumentException("That fish is not in your inventory.");
            Directory.CreateDirectory(DirectoryPath);string path=Path.Combine(DirectoryPath,"equipped.txt"),temp=path+".tmp";
            try
            {
                File.WriteAllText(temp,id??"");
                if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
                EquippedId=id;
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
