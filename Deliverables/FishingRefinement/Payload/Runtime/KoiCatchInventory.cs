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
        // Optional additions; older records do not need a file rewrite.
        public float accuracy;
        public bool perfectHook,scored;
        public bool Perfect=>scored&&accuracy>=.90f;
        public static KoiCatch Create(string variety,float scale)=>new KoiCatch{id=Guid.NewGuid().ToString("N"),variety=variety,
            caughtUtc=DateTime.UtcNow.ToString("o"),rarity=KoiFishingLoot.ForVariety(variety).Rarity,scale=scale,lengthCm=36*scale,weightKg=.8f*scale*scale*scale};
        public static bool Valid(KoiCatch fish)=>fish!=null&&Guid.TryParseExact(fish.id,"N",out _)&&
            !string.IsNullOrWhiteSpace(fish.variety)&&fish.variety.Length<=64&&DateTime.TryParse(fish.caughtUtc,out _)&&KoiFishingLoot.ValidRarity(fish.rarity)&&
            float.IsFinite(fish.scale)&&fish.scale>=.8f&&fish.scale<=1.2f&&float.IsFinite(fish.lengthCm)&&fish.lengthCm>0&&fish.lengthCm<100&&
            float.IsFinite(fish.weightKg)&&fish.weightKg>0&&fish.weightKg<10&&
            (!fish.scored||(float.IsFinite(fish.accuracy)&&fish.accuracy>=0&&fish.accuracy<=1));
    }
    public sealed class KoiCatchInventory
    {
        public const int Capacity=5000;
        public readonly List<KoiCatch> Fish=new List<KoiCatch>();
        public string DirectoryPath {get;}
        public string EquippedId {get;private set;}
        public int ReadIssueCount {get;private set;}
        readonly HashSet<string> pendingIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public int PendingCount=>pendingIds.Count;
        public KoiCatch Equipped=>Fish.Find(f=>f.id==EquippedId);
        public KoiCatchInventory(string directory){DirectoryPath=Path.GetFullPath(directory);}
        public string RecordPath(KoiCatch fish)
        {if(!KoiCatch.Valid(fish))throw new ArgumentException("Invalid koi record.");return Path.Combine(DirectoryPath,fish.id+".koi.json");}
        public void Reload()
        {
            // There is no delete action in this inventory. A transient read failure
            // must not erase previously loaded catches from the live collection.
            var known=new Dictionary<string,KoiCatch>(StringComparer.OrdinalIgnoreCase);
            foreach(var fish in Fish)if(KoiCatch.Valid(fish))known[fish.id]=fish;
            ReadIssueCount=0;if(!Directory.Exists(DirectoryPath))return;
            foreach(string pattern in new[]{"*.koi.json","*.koi.json.pending","*.koi.json.tmp"})
            {
                string[] files;
                try{files=Directory.GetFiles(DirectoryPath,pattern);}catch(IOException){ReadIssueCount++;continue;}catch(UnauthorizedAccessException){ReadIssueCount++;continue;}
                foreach(string file in files)
                {
                    if(!Read(file,out var fish)){ReadIssueCount++;continue;}
                    // The committed copy wins over an interrupted journal copy.
                    if(pattern=="*.koi.json"||!known.ContainsKey(fish.id))known[fish.id]=fish;
                }
            }
            Fish.Clear();Fish.AddRange(known.Values);EquippedId=null;pendingIds.Clear();
            foreach(var fish in Fish)if(!IsCommitted(fish))pendingIds.Add(fish.id);
            Fish.Sort((a,b)=>string.CompareOrdinal(b.caughtUtc,a.caughtUtc));
            string equip=Path.Combine(DirectoryPath,"equipped.txt");
            try{if(File.Exists(equip)&&new FileInfo(equip).Length<=64){string id=File.ReadAllText(equip).Trim();if(Fish.Exists(f=>f.id==id))EquippedId=id;}}
            catch(IOException){ReadIssueCount++;}catch(UnauthorizedAccessException){ReadIssueCount++;}
        }
        bool Read(string file,out KoiCatch fish)
        {
            fish=null;
            try
            {
                if(new FileInfo(file).Length>4096)return false;
                fish=JsonUtility.FromJson<KoiCatch>(File.ReadAllText(file));if(!KoiCatch.Valid(fish))return false;
                string name=Path.GetFileName(file),expected=fish.id+".koi.json";
                return string.Equals(name,expected,StringComparison.OrdinalIgnoreCase)||string.Equals(name,expected+".pending",StringComparison.OrdinalIgnoreCase)||string.Equals(name,expected+".tmp",StringComparison.OrdinalIgnoreCase);
            }
            catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}catch(ArgumentException){return false;}
        }
        static void DurableWrite(string path,string json)
        {
            using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read,4096,FileOptions.WriteThrough))
            {
                byte[] bytes=System.Text.Encoding.UTF8.GetBytes(json);stream.Write(bytes,0,bytes.Length);stream.Flush(true);
            }
        }
        bool Same(KoiCatch a,KoiCatch b)=>a!=null&&b!=null&&JsonUtility.ToJson(a)==JsonUtility.ToJson(b);
        public bool IsCommitted(KoiCatch fish)=>Read(RecordPath(fish),out var saved)&&Same(saved,fish);
        public void RememberPending(KoiCatch fish)
        {
            RecordPath(fish);
            if(!Fish.Exists(f=>f.id==fish.id))Fish.Insert(0,fish);
            pendingIds.Add(fish.id);
        }
        public void PreservePending(KoiCatch fish)
        {
            string path=RecordPath(fish);Directory.CreateDirectory(DirectoryPath);string pending=path+".pending";
            if(File.Exists(path)&&Read(path,out var saved)&&Same(saved,fish))return;
            if(File.Exists(pending))
            {
                if(Read(pending,out var existing)){if(Same(existing,fish))return;throw new IOException("Conflicting pending catch retained for recovery.");}
                // Retry can repair a partial journal while retaining the damaged bytes.
                File.Move(pending,pending+".damaged-"+DateTime.UtcNow.Ticks+".bak");
            }
            DurableWrite(pending,JsonUtility.ToJson(fish));
        }
        void Commit(KoiCatch fish)
        {
            string path=RecordPath(fish),temp=path+".tmp",pending=path+".pending";
            if(File.Exists(path))
            {
                if(Read(path,out var saved)&&Same(saved,fish))return;
                throw new IOException("Catch file already exists. Its original contents have been retained.");
            }
            if(File.Exists(temp)&&(!Read(temp,out var prior)||!Same(prior,fish)))
                File.Move(temp,temp+".damaged-"+DateTime.UtcNow.Ticks+".bak");
            if(!File.Exists(temp))DurableWrite(temp,JsonUtility.ToJson(fish));
            File.Move(temp,path);
            // Remove only our matching journal after the final file is committed.
            try{if(File.Exists(pending)&&Read(pending,out var journal)&&Same(journal,fish))File.Delete(pending);}
            catch(IOException){}catch(UnauthorizedAccessException){} // Committed catch is already safe.
        }
        public int RecoverPending()
        {
            int recovered=0;
            foreach(var fish in Fish)
            {
                if(IsCommitted(fish)){pendingIds.Remove(fish.id);continue;}
                try{PreservePending(fish);Commit(fish);pendingIds.Remove(fish.id);recovered++;}catch(IOException){ReadIssueCount++;}catch(UnauthorizedAccessException){ReadIssueCount++;}
            }
            return recovered;
        }
        public void Save(KoiCatch fish)
        {
            if(Fish.Count>=Capacity)throw new IOException("Your koi inventory is full. This catch cannot be stored.");
            string path=RecordPath(fish);Directory.CreateDirectory(DirectoryPath);
            if(File.Exists(path)||Fish.Exists(f=>f.id==fish.id))throw new IOException("Catch ID already exists.");
            PreservePending(fish);Commit(fish);Fish.Insert(0,fish);
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
