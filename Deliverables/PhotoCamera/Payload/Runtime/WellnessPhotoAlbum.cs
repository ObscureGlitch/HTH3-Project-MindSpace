using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TheLastWatch.UI
{
    [Serializable]
    public sealed class WellnessPhotoRecord
    {
        public string id,capturedUtc,weather;
        public int width,height;
        public float zoom,hour;
    }

    // Only game-world screenshots. No webcam, microphone, network or provider API.
    public sealed class WellnessPhotoAlbum
    {
        public const int Capacity=200;
        public string DirectoryPath {get;}
        public readonly List<WellnessPhotoRecord> Photos=new List<WellnessPhotoRecord>();
        public WellnessPhotoAlbum(string directory){DirectoryPath=Path.GetFullPath(directory);}
        public static bool ValidId(string id)=>Guid.TryParseExact(id,"N",out _);
        public string ImagePath(WellnessPhotoRecord photo,bool thumbnail=false)
        {
            if(photo==null||!ValidId(photo.id))throw new ArgumentException("Invalid photo ID.");
            return Path.Combine(DirectoryPath,photo.id+(thumbnail?".thumb.jpg":".png"));
        }
        public void Reload()
        {
            Photos.Clear();if(!Directory.Exists(DirectoryPath))return;
            foreach(string path in Directory.EnumerateFiles(DirectoryPath,"*.json"))
            {
                try
                {
                    if(new FileInfo(path).Length>4096)continue;
                    var photo=JsonUtility.FromJson<WellnessPhotoRecord>(File.ReadAllText(path));
                    if(photo==null||!ValidId(photo.id)||Path.GetFileNameWithoutExtension(path)!=photo.id||
                        photo.width<1||photo.height<1||photo.width>1920||photo.height>1080||
                        !DateTime.TryParse(photo.capturedUtc,out _)||!File.Exists(ImagePath(photo)))continue;
                    Photos.Add(photo);
                }
                catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}
            }
            Photos.Sort((a,b)=>string.CompareOrdinal(b.capturedUtc,a.capturedUtc));
        }
        public void Save(WellnessPhotoRecord photo,byte[] png,byte[] thumbnail)
        {
            if(Photos.Count>=Capacity)throw new IOException("Your album is full. Move some saved photos out of the photo folder to make room.");
            if(photo==null||!ValidId(photo.id)||png==null||png.Length==0||thumbnail==null||thumbnail.Length==0)
                throw new ArgumentException("The photo was not captured correctly.");
            Directory.CreateDirectory(DirectoryPath);
            string image=ImagePath(photo),thumb=ImagePath(photo,true),metadata=Path.Combine(DirectoryPath,photo.id+".json");
            string[] targets={image,thumb,metadata};
            foreach(string path in targets)if(File.Exists(path)||File.Exists(path+".tmp"))throw new IOException("Photo ID already exists.");
            try
            {
                File.WriteAllBytes(image+".tmp",png);File.Move(image+".tmp",image);
                File.WriteAllBytes(thumb+".tmp",thumbnail);File.Move(thumb+".tmp",thumb);
                File.WriteAllText(metadata+".tmp",JsonUtility.ToJson(photo));File.Move(metadata+".tmp",metadata);
                Photos.Insert(0,photo); // Publish only after all three files are committed.
            }
            catch
            {
                foreach(string path in targets){if(File.Exists(path+".tmp"))File.Delete(path+".tmp");if(File.Exists(path))File.Delete(path);}
                throw;
            }
        }
        public byte[] ReadImage(WellnessPhotoRecord photo,bool thumbnail)
        {
            string path=ImagePath(photo,thumbnail);
            long bytes=new FileInfo(path).Length;
            if(bytes<=0||bytes>(thumbnail?512*1024:12*1024*1024))throw new IOException("Photo file is unreadable or too large.");
            return File.ReadAllBytes(path);
        }
    }
}
