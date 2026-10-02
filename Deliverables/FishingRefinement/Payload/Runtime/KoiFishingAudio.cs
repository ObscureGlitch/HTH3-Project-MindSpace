using System;
using UnityEngine;
namespace TheLastWatch.UI
{
    public sealed class KoiFishingAudio : IDisposable
    {
        readonly GameObject root;readonly AudioSource source;readonly AudioClip[] clips=new AudioClip[6];
        public KoiFishingAudio(Transform parent)
        {
            root=new GameObject("Fishing plucks (runtime)"){hideFlags=HideFlags.DontSave};root.transform.SetParent(parent,false);
            source=root.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.volume=.12f;
            for(int i=0;i<clips.Length;i++)
            {
                int count=i>=4?13230:6615;var samples=new float[count];float frequency=new[]{440f,660f,880f,330f,270f,660f}[i];
                for(int s=0;s<count;s++)
                {
                    float t=s/22050f;float envelope=Mathf.Sin(Mathf.Min(1,t/.015f)*Mathf.PI*.5f)*Mathf.Exp(-t*(i>=4?9:18));
                    float tone=Mathf.Sin(t*frequency*Mathf.PI*2)+.18f*Mathf.Sin(t*frequency*Mathf.PI*4);
                    if(i==4)tone=.4f*Mathf.Sin(t*(frequency+180*t)*Mathf.PI*2)+.2f*Mathf.Sin(t*1937*Mathf.PI*2)*Mathf.Sin(t*2791*Mathf.PI*2);
                    if(i==5)tone=.42f*(Mathf.Sin(t*660*Mathf.PI*2)+Mathf.Sin(t*825*Mathf.PI*2)+Mathf.Sin(t*990*Mathf.PI*2));
                    samples[s]=tone*envelope*.55f;
                }
                var clip=AudioClip.Create("Soft fishing pluck "+i,count,1,22050,false);clip.hideFlags=HideFlags.DontSave;clip.SetData(samples,0);clips[i]=clip;
            }
        }
        public void Play(int sound){if(Application.isPlaying&&source!=null)source.PlayOneShot(clips[Mathf.Clamp(sound,0,clips.Length-1)]);}
        static void Destroy(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        public void Dispose(){if(root!=null)root.SetActive(false);Destroy(root);foreach(var clip in clips)Destroy(clip);}
    }
}
