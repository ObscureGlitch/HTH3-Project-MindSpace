using UnityEngine;

namespace TheLastWatch.Integrations
{
    // Authored body clips follow conversation state. Live vowel analysis remains a separate
    // facial layer so the supplied generic mouth motion never replaces real speech timing.
    [DisallowMultipleComponent]
    public sealed class WellnessTherapist : MonoBehaviour
    {
        public string characterName;
        public int voiceIndex;
        public WellnessVoiceChat chat;
        public Transform voiceAnchor, head, spine, lowerLip, lowerTeeth, tongue;
        public string Prompt => "Talk to " + characterName + " / follow options";
        public WellnessCompanionMovement movement;
        public Transform leftLid,rightLid,leftShoulder,rightShoulder;
        public SkinnedMeshRenderer faceRenderer;
        public WellnessSpeechAnalysis speechAnalysis;
        [Header("Conversation-driven supplied animation pack")]
        public Animation bodyAnimation;
        public string ActiveBodyClip => currentBodyClip;

        private Quaternion headRest,spineRest,leftLidRest,rightLidRest,leftShoulderRest,rightShoulderRest;
        private Vector3 spinePosition;
        private readonly float[] weights=new float[5];
        private bool initialized,agentWasSpeaking;
        private int observedUserTurn;
        private string currentBodyClip,oneShotClip;
        private float oneShotUntil;

        private void Awake()=>CaptureRestPose();
        private void OnEnable()
        {
            if(!initialized)CaptureRestPose();
            observedUserTurn=chat!=null?chat.UserTurnSerial:0;
            agentWasSpeaking=chat!=null&&chat.IsSpeakingWith(voiceIndex);
            currentBodyClip=oneShotClip=null;oneShotUntil=0;
            if(Application.isPlaying&&HasClip("Wave"))PlayOneShot("Wave");
        }

        public void CaptureRestPose()
        {
            if(head!=null)headRest=head.localRotation;
            if(spine!=null){spineRest=spine.localRotation;spinePosition=spine.localPosition;}
            if(leftLid!=null)leftLidRest=leftLid.localRotation;if(rightLid!=null)rightLidRest=rightLid.localRotation;
            if(leftShoulder!=null)leftShoulderRest=leftShoulder.localRotation;if(rightShoulder!=null)rightShoulderRest=rightShoulder.localRotation;
            initialized=true;
        }

        public string Interact()
        {
            if(chat==null)return "Voice controls are not configured.";
            if(movement!=null)movement.RequestAttention();
            chat.OpenFor(voiceIndex);
            return chat.HasVoiceConsent ? "Choose your companion in V controls. Voice permission lasts for this play session; M mutes and X pauses." :
                "Choose Julien or Camille and review voice permission. No microphone starts until you click Enable voice.";
        }

        private bool HasClip(string name)=>bodyAnimation!=null&&bodyAnimation[name]!=null;

        private void PlayLoop(string name)
        {
            if(!HasClip(name))return;
            if(currentBodyClip==name&&bodyAnimation.IsPlaying(name))return;
            bodyAnimation.CrossFade(name,.2f);
            currentBodyClip=name;
        }

        private void PlayOneShot(string name)
        {
            if(!HasClip(name))return;
            oneShotClip=name;
            oneShotUntil=Time.time+bodyAnimation[name].length;
            bodyAnimation.CrossFade(name,.16f);
            currentBodyClip=name;
        }

        private void CancelOneShot(){oneShotClip=null;oneShotUntil=0;}

        private void Update()
        {
            if(!Application.isPlaying||!HasClip("Idle"))return;
            bool selected=chat!=null&&chat.SelectedIndex==voiceIndex;
            bool connected=selected&&chat.IsConnected;
            bool agentSpeaking=chat!=null&&chat.IsSpeakingWith(voiceIndex);
            bool userSpeaking=chat!=null&&chat.IsUserSpeakingWith(voiceIndex);
            int userTurn=selected?chat.UserTurnSerial:observedUserTurn;
            if(!connected)observedUserTurn=userTurn;

            // Reset-safe serial handling prevents a new session or re-enable from inventing a nod.
            if(userTurn<observedUserTurn)observedUserTurn=userTurn;

            if(movement!=null&&movement.IsMoving)
            {
                CancelOneShot();observedUserTurn=userTurn;
                if(HasClip("Walk"))bodyAnimation["Walk"].speed=Mathf.Clamp(movement.Speed/1.25f,.45f,1.7f);
                PlayLoop("Walk");
            }
            else if(agentSpeaking)
            {
                CancelOneShot();observedUserTurn=userTurn;PlayLoop("Talk");
            }
            else if(userSpeaking)
            {
                // A real user voice turn always wins over note-taking or acknowledgement.
                CancelOneShot();PlayLoop("Listen");
            }
            else
            {
                if(!connected&&oneShotClip!="Wave")CancelOneShot();
                if(connected&&userTurn>observedUserTurn)
                {
                    observedUserTurn=userTurn;
                    PlayOneShot("Nod");
                }
                else if(agentWasSpeaking&&connected)PlayOneShot("Write");

                if(string.IsNullOrEmpty(oneShotClip)||Time.time>=oneShotUntil)
                {
                    CancelOneShot();
                    if(chat!=null&&chat.IsAwaitingAgentResponseWith(voiceIndex))PlayLoop("Think");
                    else if(connected)PlayLoop("Listen");
                    else PlayLoop("Idle");
                }
            }
            agentWasSpeaking=agentSpeaking;
        }

        private void LateUpdate()
        {
            if(!initialized)return;
            float dt=Mathf.Min(Time.deltaTime,.1f);
            if(!HasClip("Idle"))ApplyDeterministicFallback(dt);

            bool speaking=speechAnalysis!=null&&chat!=null&&chat.IsSpeakingWith(voiceIndex);
            for(int i=0;i<5;i++)
            {
                float target=speaking?speechAnalysis.Volume*speechAnalysis.Vowels[i]*100:0;
                weights[i]=Mathf.Lerp(weights[i],target,1-Mathf.Exp(-dt*(target>weights[i]?28:38)));
                if(faceRenderer!=null&&faceRenderer.sharedMesh!=null&&faceRenderer.sharedMesh.blendShapeCount>=5)
                    faceRenderer.SetBlendShapeWeight(i,weights[i]);
            }
        }

        // Safe fallback for a scene that has not run the supplied-clip installer yet. Its timing is
        // periodic and deterministic; the installed characters use authored eyelid/body motion.
        private void ApplyDeterministicFallback(float dt)
        {
            if(head==null||spine==null)return;
            float t=Time.time+voiceIndex*1.73f;
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
            float blink=EvaluateBlink(Mathf.Repeat(t,4.6f));
            if(leftLid!=null)leftLid.localRotation=leftLidRest*Quaternion.Euler(blink*66,0,0);
            if(rightLid!=null)rightLid.localRotation=rightLidRest*Quaternion.Euler(blink*66,0,0);
        }

        public static float EvaluateBlink(float elapsed)
        {
            if(elapsed<0||elapsed>.19f)return 0;
            float phase=elapsed/.19f;return phase<.4f?Mathf.SmoothStep(0,1,phase/.4f):Mathf.SmoothStep(1,0,(phase-.4f)/.6f);
        }

        private void OnDisable()
        {
            if(!initialized)return;
            if(bodyAnimation!=null&&bodyAnimation["Idle"]!=null)
            {
                bodyAnimation.Stop();bodyAnimation.Play("Idle");bodyAnimation.Sample();bodyAnimation.Stop();
            }
            else
            {
                if(head!=null)head.localRotation=headRest;if(spine!=null){spine.localRotation=spineRest;spine.localPosition=spinePosition;}
                if(leftLid!=null)leftLid.localRotation=leftLidRest;if(rightLid!=null)rightLid.localRotation=rightLidRest;
                if(leftShoulder!=null)leftShoulder.localRotation=leftShoulderRest;if(rightShoulder!=null)rightShoulder.localRotation=rightShoulderRest;
            }
            for(int i=0;i<5;i++)
            {
                weights[i]=0;
                if(faceRenderer!=null&&faceRenderer.sharedMesh!=null&&faceRenderer.sharedMesh.blendShapeCount>=5)
                    faceRenderer.SetBlendShapeWeight(i,0);
            }
        }
    }
}
