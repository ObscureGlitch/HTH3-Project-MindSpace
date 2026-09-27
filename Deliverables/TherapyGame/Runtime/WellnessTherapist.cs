using UnityEngine;

namespace TheLastWatch.Integrations
{
    // The supplied GLBs have rigid articulated parts, not humanoid animation clips.
    // These small joint offsets layer on top of the saved standing pose; no IK or cloth simulation.
    [DisallowMultipleComponent]
    public sealed class WellnessTherapist : MonoBehaviour
    {
        public string characterName;
        public int voiceIndex;
        public WellnessVoiceChat chat;
        public Transform voiceAnchor, head, spine, lowerLip, lowerTeeth, tongue;
        public string Prompt => "Talk to " + characterName + " / choose companion";
        public Transform leftLid,rightLid,leftShoulder,rightShoulder;
        public SkinnedMeshRenderer faceRenderer;
        public WellnessSpeechAnalysis speechAnalysis;
        private Quaternion headRest,spineRest,leftLidRest,rightLidRest,leftShoulderRest,rightShoulderRest;
        private Vector3 spinePosition;
        private readonly float[] weights=new float[5];
        private bool initialized;
        private float blinkAt,blinkStart=-10;
        private uint randomState;
        private float Random01(){randomState=1664525*randomState+1013904223;return (randomState&0xffffff)/16777216f;}
        private void Awake()
        {CaptureRestPose();}
        private void OnEnable(){if(!initialized)CaptureRestPose();blinkAt=Time.time+2+voiceIndex;}
        public void CaptureRestPose()
        {
            if(head!=null)headRest=head.localRotation;
            if(spine!=null){spineRest=spine.localRotation;spinePosition=spine.localPosition;}
            if(leftLid!=null)leftLidRest=leftLid.localRotation;if(rightLid!=null)rightLidRest=rightLid.localRotation;
            if(leftShoulder!=null)leftShoulderRest=leftShoulder.localRotation;if(rightShoulder!=null)rightShoulderRest=rightShoulder.localRotation;
            randomState=(uint)(90261+voiceIndex*197);initialized=true;
        }
        public string Interact()
        {
            if(chat==null)return "Voice controls are not configured.";
            chat.OpenFor(voiceIndex);
            return chat.HasVoiceConsent ? "Choose your companion in V controls. Voice permission lasts for this play session; M mutes and X pauses." :
                "Choose Julien or Camille and review voice permission. No microphone starts until you click Enable voice.";
        }
        private void LateUpdate()
        {
            if(!initialized||head==null||spine==null)return;
            float t=Time.time+voiceIndex*1.73f,dt=Mathf.Min(Time.deltaTime,.1f);
            bool inView=chat!=null&&chat.player!=null&&chat.player.ViewCamera!=null;
            Vector3 toward=inView?chat.player.ViewCamera.transform.position-head.position:transform.forward;
            bool nearby=inView&&toward.sqrMagnitude<36;
            float breathe=Mathf.Sin(t*1.24f);
            spine.localRotation=spineRest*Quaternion.Euler(nearby?breathe*.55f:0,0,nearby?Mathf.Sin(t*.37f)*.25f:0);
            spine.localPosition=spinePosition+Vector3.up*(nearby?breathe*.0015f:0);
            if(leftShoulder!=null)leftShoulder.localRotation=leftShoulderRest*Quaternion.Euler(nearby?breathe*.24f:0,0,0);
            if(rightShoulder!=null)rightShoulder.localRotation=rightShoulderRest*Quaternion.Euler(nearby?breathe*.24f:0,0,0);
            Quaternion look=headRest*Quaternion.Euler(Mathf.Sin(t*.72f)*.65f,Mathf.Sin(t*.31f)*1.2f,0);
            if(nearby&&Vector3.Dot(transform.forward,toward.normalized)>.15f)
            {
                Vector3 direction=head.parent.InverseTransformDirection(toward).normalized;
                float yaw=Mathf.Clamp(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,-18,18);
                float pitch=Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(direction.y,-1,1))*Mathf.Rad2Deg,-8,10);
                look=headRest*Quaternion.Euler(pitch+Mathf.Sin(t*.9f)*.45f,yaw,0);
            }
            head.localRotation=Quaternion.Slerp(head.localRotation,look,1-Mathf.Exp(-dt*2.6f));
            if(Time.time>=blinkAt){blinkStart=Time.time;blinkAt=Time.time+3.1f+Random01()*3.6f;}
            float blink=EvaluateBlink(Time.time-blinkStart);
            if(leftLid!=null)leftLid.localRotation=leftLidRest*Quaternion.Euler(blink*66,0,0);
            if(rightLid!=null)rightLid.localRotation=rightLidRest*Quaternion.Euler(blink*66,0,0);
            bool speaking=speechAnalysis!=null&&chat!=null&&chat.IsConnected&&chat.SelectedIndex==voiceIndex;
            for(int i=0;i<5;i++)
            {
                float target=speaking?speechAnalysis.Volume*speechAnalysis.Vowels[i]*100:0;
                weights[i]=Mathf.Lerp(weights[i],target,1-Mathf.Exp(-dt*(target>weights[i]?28:38)));
                if(faceRenderer!=null&&faceRenderer.sharedMesh!=null&&faceRenderer.sharedMesh.blendShapeCount>=5)faceRenderer.SetBlendShapeWeight(i,weights[i]);
            }
        }
        public static float EvaluateBlink(float elapsed)
        {
            if(elapsed<0||elapsed>.19f)return 0;
            float phase=elapsed/.19f;return phase<.4f?Mathf.SmoothStep(0,1,phase/.4f):Mathf.SmoothStep(1,0,(phase-.4f)/.6f);
        }
        private void OnDisable()
        {
            if(!initialized)return;
            if(head!=null)head.localRotation=headRest;if(spine!=null){spine.localRotation=spineRest;spine.localPosition=spinePosition;}
            if(leftLid!=null)leftLid.localRotation=leftLidRest;if(rightLid!=null)rightLid.localRotation=rightLidRest;
            if(leftShoulder!=null)leftShoulder.localRotation=leftShoulderRest;if(rightShoulder!=null)rightShoulder.localRotation=rightShoulderRest;
            for(int i=0;i<5;i++){weights[i]=0;if(faceRenderer!=null&&faceRenderer.sharedMesh!=null&&faceRenderer.sharedMesh.blendShapeCount>=5)faceRenderer.SetBlendShapeWeight(i,0);}
        }
    }
}
