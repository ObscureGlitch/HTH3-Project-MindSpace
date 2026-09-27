using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using uLipSync;

namespace TheLastWatch.Integrations
{
    // Reads only the AI AudioSource's played samples, never the microphone.
    // Five MFCC vowel classes; one bounded worker job, up to 30 Hz. No neural model/GPU.
    [DisallowMultipleComponent]
    public sealed class WellnessSpeechAnalysis : MonoBehaviour
    {
        public WellnessVoiceChat chat;
        public AudioSource source;
        public Profile julienProfile,camilleProfile;
        public float sensitivity=1;
        public float Volume {get;private set;}
        public readonly float[] Vowels=new float[5];
        public static readonly string[] Names={"A","I","U","E","O"};
        private Profile activeProfile;
        private float[] samples;
        private NativeArray<float> input,means,deviations,phonemes,mfcc,scores;
        private NativeArray<LipSyncJob.Info> info;
        private JobHandle job;
        private bool allocated,pending;
        private float nextSample;
        private int rate;
        private void Update()
        {
            if(!Application.isPlaying||chat==null||!chat.IsConnected||source==null||!source.isPlaying)
            {ResetOutput();if(allocated)Release();return;}
            Profile wanted=chat.SelectedIndex==0?julienProfile:camilleProfile;
            if(wanted==null){ResetOutput();return;}
            if(!allocated||activeProfile!=wanted||rate!=AudioSettings.outputSampleRate){Release();Allocate(wanted);}
            if(pending)
            {
                if(!job.IsCompleted)return;
                job.Complete();pending=false;
                float raw=info[0].volume*sensitivity;
                Volume=float.IsNaN(raw)||float.IsInfinity(raw)||raw<.0012f?0:Mathf.Clamp01((Mathf.Log10(Mathf.Max(raw,.00001f))+2.9f)/1.6f);
                for(int i=0;i<Vowels.Length;i++)Vowels[i]=0;
                float sum=0;
                if(Volume>0)for(int i=0;i<scores.Length;i++)
                {
                    float score=scores[i];if(float.IsNaN(score)||float.IsInfinity(score)||score<=0)continue;
                    for(int v=0;v<Names.Length;v++)if(activeProfile.GetPhoneme(i)==Names[v]){Vowels[v]+=score;sum+=score;}
                }
                if(sum>0)for(int v=0;v<5;v++)Vowels[v]/=sum;
                else if(Volume>0)Vowels[0]=1; // Unclassified voiced audio still gets an audio-driven jaw opening.
            }
            if(Time.unscaledTime<nextSample)return;nextSample=Time.unscaledTime+1f/30;
            source.GetOutputData(samples,0);
            // Unity requires power-of-two buffers; MFCC requires exactly 64 ms of input.
            int offset=samples.Length-input.Length;for(int i=0;i<input.Length;i++)input[i]=samples[offset+i];
            var work=new LipSyncJob{input=input,startIndex=0,outputSampleRate=rate,targetSampleRate=activeProfile.targetSampleRate,
                melFilterBankChannels=activeProfile.melFilterBankChannels,compareMethod=activeProfile.compareMethod,
                means=means,standardDeviations=deviations,phonemes=phonemes,mfcc=mfcc,scores=scores,info=info};
            job=work.Schedule();pending=true;
        }
        private void Allocate(Profile p)
        {
            activeProfile=p;rate=AudioSettings.outputSampleRate;
            int n=Mathf.CeilToInt(p.sampleCount*(float)rate/p.targetSampleRate);
            samples=new float[Mathf.NextPowerOfTwo(n)];input=new NativeArray<float>(n,Allocator.Persistent);
            means=new NativeArray<float>(p.means,Allocator.Persistent);deviations=new NativeArray<float>(p.standardDeviation,Allocator.Persistent);
            phonemes=new NativeArray<float>(12*p.mfccs.Count,Allocator.Persistent);mfcc=new NativeArray<float>(12,Allocator.Persistent);
            scores=new NativeArray<float>(p.mfccs.Count,Allocator.Persistent);info=new NativeArray<LipSyncJob.Info>(1,Allocator.Persistent);
            for(int i=0;i<p.mfccs.Count;i++)for(int c=0;c<12;c++)phonemes[i*12+c]=p.mfccs[i].mfccNativeArray[c];
            allocated=true;nextSample=0;
        }
        public void ResetOutput(){Volume=0;for(int i=0;i<Vowels.Length;i++)Vowels[i]=0;}
        private void Release()
        {
            if(!allocated)return;job.Complete();pending=false;
            input.Dispose();means.Dispose();deviations.Dispose();phonemes.Dispose();mfcc.Dispose();scores.Dispose();info.Dispose();
            allocated=false;samples=null;activeProfile=null;
        }
        private void OnDisable(){ResetOutput();Release();}
    }
}
