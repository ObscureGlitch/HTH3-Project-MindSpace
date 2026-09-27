using System;
using UnityEngine;

namespace TheLastWatch.Environment
{
    /// <summary>Photographic clouds projected onto the distant sky. Virtual positions
    /// and wind are in real metres/seconds, independent of the accelerated game clock.</summary>
    [DisallowMultipleComponent]
    public sealed class WellnessCloudDeck : MonoBehaviour
    {
        [Serializable] public sealed class Card
        {
            public Transform transform;
            public Renderer renderer;
            public Vector3 virtualPosition;
            public Vector2 sizeMetres;
            public float speedFactor=1, rollDegrees, opacity=1;
            public int atlasTile;
        }
        public Camera viewer;
        public Material cloudMaterial;
        public Texture2D cumulusAtlas, altocumulusAtlas;
        public Texture2D cumulusBankTexture;
        public WellnessSkyCycle skyCycle;
        public WellnessCloudType initialCloudType=WellnessCloudType.PuffyCumulus;
        [SerializeField] private WellnessCloudMode cloudMode=WellnessCloudMode.Automatic;
        public Card[] cards=Array.Empty<Card>();
        [Range(0,4)] public float windMetresPerSecond=2.4f;
        private const float HalfSpan=6500, SkyRadius=150;
        private Material liveMaterial;
        private Material reflectionTarget;
        private readonly Vector4[] reflectionFacing=new Vector4[16],reflectionRight=new Vector4[16],reflectionUp=new Vector4[16];
        private Material[] previousMaterials;
        private MaterialPropertyBlock[] blocks;
        private Vector3[] positions, scales, virtualPositions;
        private Quaternion[] rotations;
        private bool[] previousEnabled;
        private MeshFilter bankFilter;
        private Mesh previousBankMesh,bankMesh;
        private WellnessCumulusLife bankLife;
        private float bankAzimuth;
        private bool SingleBank => currentType==WellnessCloudType.PuffyCumulus&&cumulusBankTexture!=null&&bankMesh!=null;
        private float coverage=.4f;
        private Color tint=Color.white, horizon=new Color(.7f,.8f,.88f);
        private bool ready;
        private WellnessCloudSequence sequence;
        private WellnessCloudType currentType;
        private Quaternion layoutRotation=Quaternion.identity;
        public bool CloudTypesAvailable => cumulusAtlas!=null&&altocumulusAtlas!=null;
        public WellnessCloudMode CloudMode => sequence!=null?sequence.Mode:cloudMode;
        public WellnessCloudType ActiveCloudType => ready?currentType:initialCloudType;
        public float CloudTypeOpacity => sequence!=null?sequence.Opacity:cloudMode==WellnessCloudMode.Off?0:1;
        private static readonly int Opacity=Shader.PropertyToID("_Opacity");
        private static readonly int CloudFacing=Shader.PropertyToID("_CloudFacing"),CloudRight=Shader.PropertyToID("_CloudRight"),CloudUp=Shader.PropertyToID("_CloudUp"),CloudCount=Shader.PropertyToID("_PondCloudCount"),CloudTint=Shader.PropertyToID("_CloudTint");

        public void SetReflectionTarget(Material target)
        {
            if(reflectionTarget!=null)reflectionTarget.SetInt(CloudCount,0);
            reflectionTarget=target;
            Material source=liveMaterial!=null?liveMaterial:cloudMaterial;
            if(target!=null&&source!=null)target.SetTexture("_CloudAtlas",source.GetTexture("_BaseMap"));
            if(target!=null)target.SetFloat("_PondCloudSingle",SingleBank?1:0);
        }

        public static float DriftDegreesPerSecond(float wind,float altitude) => Mathf.Rad2Deg*Mathf.Max(0,wind)/Mathf.Max(1,altitude);
        public static float AdvanceWind(float x,float metresPerSecond,float seconds) => Mathf.Repeat(x+HalfSpan+metresPerSecond*seconds,HalfSpan*2)-HalfSpan;
        public static Vector3 Project(Vector3 position,Vector3 eye) => eye+position.normalized*SkyRadius;
        public static Vector3 ProjectedScale(Vector3 position,Vector2 size)
        {float scale=SkyRadius/Mathf.Max(1,position.magnitude);return new Vector3(size.x*scale,size.y*scale,1);}
        public void SetConditions(float cover,Color cloudTint,Color skyHorizon)
        {
            coverage=Mathf.Clamp01(cover);tint=cloudTint;horizon=skyHorizon;
            if(liveMaterial!=null){liveMaterial.SetColor("_Tint",tint);liveMaterial.SetColor("_HorizonColor",horizon);}
            if(reflectionTarget!=null)reflectionTarget.SetColor(CloudTint,tint);
        }
        public void SetCloudMode(WellnessCloudMode mode)
        {
            if((int)mode<0||(int)mode>4)throw new ArgumentOutOfRangeException(nameof(mode));
            if(!CloudTypesAvailable&&mode!=WellnessCloudMode.Off)mode=WellnessCloudMode.SoftBanks;
            cloudMode=mode;
            if(!ready||sequence==null)return;
            sequence.SetMode(mode);
            if(sequence.Current!=currentType)ApplyCloudType(sequence.Current);
            // Refresh both sky and pond immediately, even while timeScale is zero.
            if(viewer!=null)PlaceCards(0);
        }
        private void OnEnable()
        {
            if(!Application.isPlaying||viewer==null||cloudMaterial==null)return;
            liveMaterial=new Material(cloudMaterial){name="Single active cloud family (runtime)",hideFlags=HideFlags.DontSave};
            int count=cards.Length;positions=new Vector3[count];scales=new Vector3[count];rotations=new Quaternion[count];
            virtualPositions=new Vector3[count];previousMaterials=new Material[count];blocks=new MaterialPropertyBlock[count];
            previousEnabled=new bool[count];
            for(int i=0;i<count;i++)
            {
                Card card=cards[i];if(card?.transform==null||card.renderer==null)continue;
                positions[i]=card.transform.position;scales[i]=card.transform.localScale;rotations[i]=card.transform.rotation;
                virtualPositions[i]=card.virtualPosition;previousMaterials[i]=card.renderer.sharedMaterial;
                previousEnabled[i]=card.renderer.enabled;
                card.renderer.sharedMaterial=liveMaterial;blocks[i]=new MaterialPropertyBlock();
            }
            if(count>0&&cards[0]?.transform!=null&&cumulusBankTexture!=null)
            {
                bankFilter=cards[0].transform.GetComponent<MeshFilter>();
                if(bankFilter!=null&&bankFilter.sharedMesh!=null)
                {
                    previousBankMesh=bankFilter.sharedMesh;
                    bankMesh=Instantiate(previousBankMesh);bankMesh.name="Single cumulus bank quad (runtime)";bankMesh.hideFlags=HideFlags.DontSave;
                    bankMesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
                }
            }
            // Graceful compatibility until the two new atlases are installed.
            sequence=new WellnessCloudSequence(global::System.Environment.TickCount^0x43594B,
                CloudTypesAvailable?initialCloudType:WellnessCloudType.SoftBanks);
            sequence.SetMode(CloudTypesAvailable?cloudMode:cloudMode==WellnessCloudMode.Off?cloudMode:WellnessCloudMode.SoftBanks);
            ApplyCloudType(sequence.Current);
            ready=true;SetConditions(coverage,tint,horizon);PlaceCards(0);
        }
        private void LateUpdate()
        {
            if(!ready||viewer==null)return;
            float dt=Application.isFocused?Mathf.Min(Time.deltaTime,.25f):0;
            if(sequence!=null)
            {
                sequence.Tick(dt);
                if(sequence.Current!=currentType)ApplyCloudType(sequence.Current);
            }
            PlaceCards(dt);
        }
        private void ApplyCloudType(WellnessCloudType type)
        {
            currentType=type;
            // Exactly one texture is bound to the one shared material. A switch
            // only happens with zero opacity; no mixed cloud-family crossfade.
            Texture atlas=SingleBank?cumulusBankTexture:type==WellnessCloudType.PuffyCumulus?cumulusAtlas:
                type==WellnessCloudType.Altocumulus?altocumulusAtlas:cloudMaterial.GetTexture("_BaseMap");
            liveMaterial.SetTexture("_BaseMap",atlas);
            liveMaterial.SetFloat("_SingleBank",SingleBank?1:0);
            if(reflectionTarget!=null){reflectionTarget.SetTexture("_CloudAtlas",atlas);reflectionTarget.SetFloat("_PondCloudSingle",SingleBank?1:0);}
            if(bankFilter!=null)bankFilter.sharedMesh=SingleBank?bankMesh:previousBankMesh;
            if(SingleBank)
            {
                bankLife=new WellnessCumulusLife(skyCycle!=null?skyCycle.Hour:14);
                bankAzimuth=sequence!=null&&sequence.SwitchCount>0?sequence.LayoutYawDegrees:viewer.transform.eulerAngles.y+20;
            }
            layoutRotation=Quaternion.Euler(0,sequence!=null?sequence.LayoutYawDegrees:0,0);
            for(int i=0;i<cards.Length;i++)if(cards[i]!=null)
                virtualPositions[i]=layoutRotation*PositionForType(cards[i].virtualPosition,type);
        }
        public static Vector3 PositionForType(Vector3 position,WellnessCloudType type)
        {
            if(type==WellnessCloudType.PuffyCumulus)return Vector3.Scale(position,new Vector3(.96f,.96f,.96f));
            if(type==WellnessCloudType.Altocumulus)return Vector3.Scale(position,new Vector3(1.1f,2.05f,1.1f));
            return position;
        }
        public static Vector2 SizeForType(Vector2 size,WellnessCloudType type)
        {
            if(type==WellnessCloudType.PuffyCumulus)return Vector2.Scale(size,new Vector2(.94f,1.18f));
            if(type==WellnessCloudType.Altocumulus)return Vector2.Scale(size,new Vector2(2.25f,2.10f));
            return size;
        }
        private void PlaceCards(float dt)
        {
            Vector3 eye=viewer.transform.position;
            if(SingleBank){PlaceBank(dt,eye);return;}
            for(int i=0;i<cards.Length;i++)
            {
                Card card=cards[i];if(card?.transform==null||card.renderer==null||blocks[i]==null)continue;
                card.renderer.enabled=WellnessCumulusLife.CardVisible(false,i,previousEnabled[i],CloudTypeOpacity);
                Vector3 p=virtualPositions[i];p.x=AdvanceWind(p.x,Mathf.Clamp(windMetresPerSecond,0,4)*card.speedFactor,dt);virtualPositions[i]=p;
                Vector3 direction=p.normalized;
                Vector2 size=SizeForType(card.sizeMetres,currentType);
                card.transform.SetPositionAndRotation(Project(p,eye),Quaternion.LookRotation(-direction,Vector3.up)*Quaternion.Euler(0,0,card.rollDegrees));
                card.transform.localScale=ProjectedScale(p,size);
                float edge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(HalfSpan,HalfSpan-900,Mathf.Abs(p.x)));
                float horizonFade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.07f,.22f,direction.y));
                // Fade additional cloud banks in as the weather closes, without scaling shapes.
                float density=i<6?Mathf.Lerp(.72f,1,coverage):Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,.85f,coverage));
                float alpha=card.opacity*edge*horizonFade*density*CloudTypeOpacity;
                blocks[i].SetFloat(Opacity,alpha);card.renderer.SetPropertyBlock(blocks[i]);
                if(reflectionTarget!=null&&i<16)
                {
                    // SortingOrder 0 (second bank) is behind SortingOrder 1 (first bank).
                    int slot=cards.Length==16?(i+8)%16:i;
                    reflectionFacing[slot]=new Vector4(direction.x,direction.y,direction.z,alpha);
                    Vector3 right=card.transform.right*(p.magnitude/Mathf.Max(1,size.x))*(i>=8?-1:1);
                    Vector3 up=card.transform.up*(p.magnitude/Mathf.Max(1,size.y));
                    reflectionRight[slot]=new Vector4(right.x,right.y,right.z,card.atlasTile%4);
                    reflectionUp[slot]=new Vector4(up.x,up.y,up.z,1-card.atlasTile/4);
                }
            }
            if(reflectionTarget!=null)
            {
                reflectionTarget.SetInt(CloudCount,Mathf.Min(16,cards.Length));
                reflectionTarget.SetVectorArray(CloudFacing,reflectionFacing);reflectionTarget.SetVectorArray(CloudRight,reflectionRight);reflectionTarget.SetVectorArray(CloudUp,reflectionUp);
            }
        }
        public static Vector3 BankPosition(float azimuth,float height)
        {
            // Hold the bottom edge at a constant angular cloud-base height while
            // the top grows. This avoids scaling around the centre like a balloon.
            float elevation=Mathf.Atan2(WellnessCumulusLife.BaseHeight,WellnessCumulusLife.Distance)+Mathf.Atan2(height*.5f,WellnessCumulusLife.Distance);
            float yaw=azimuth*Mathf.Deg2Rad,flat=Mathf.Cos(elevation)*WellnessCumulusLife.Distance;
            return new Vector3(Mathf.Sin(yaw)*flat,Mathf.Sin(elevation)*WellnessCumulusLife.Distance,Mathf.Cos(yaw)*flat);
        }
        private void PlaceBank(float dt,Vector3 eye)
        {
            bankLife.Tick(dt,skyCycle!=null?skyCycle.Hour:14);
            bankAzimuth=WellnessCumulusLife.AdvanceAzimuth(bankAzimuth,windMetresPerSecond,dt);
            Vector2 size=new Vector2(WellnessCumulusLife.Width*bankLife.WidthFactor,WellnessCumulusLife.Height*bankLife.HeightFactor);
            Vector3 p=BankPosition(bankAzimuth,size.y),direction=p.normalized;
            float alpha=CloudTypeOpacity*(CloudMode==WellnessCloudMode.Automatic?bankLife.AutomaticOpacity:1);
            for(int i=0;i<cards.Length;i++)
            {
                Card card=cards[i];if(card?.renderer==null)continue;
                card.renderer.enabled=WellnessCumulusLife.CardVisible(true,i,previousEnabled[i],alpha);
                if(blocks[i]!=null){blocks[i].SetFloat(Opacity,i==0?alpha:0);card.renderer.SetPropertyBlock(blocks[i]);}
            }
            Card bank=cards[0];bank.transform.SetPositionAndRotation(Project(p,eye),Quaternion.LookRotation(-direction,Vector3.up));
            bank.transform.localScale=ProjectedScale(p,size);
            if(reflectionTarget==null)return;
            Vector3 right=bank.transform.right*(p.magnitude/size.x),up=bank.transform.up*(p.magnitude/size.y);
            reflectionFacing[0]=new Vector4(direction.x,direction.y,direction.z,previousEnabled[0]?alpha:0);
            reflectionRight[0]=new Vector4(right.x,right.y,right.z,0);reflectionUp[0]=new Vector4(up.x,up.y,up.z,0);
            reflectionTarget.SetInt(CloudCount,alpha>0?1:0);
            reflectionTarget.SetVectorArray(CloudFacing,reflectionFacing);reflectionTarget.SetVectorArray(CloudRight,reflectionRight);reflectionTarget.SetVectorArray(CloudUp,reflectionUp);
        }
        private void OnDisable()
        {
            if(!ready)return;ready=false;
            if(reflectionTarget!=null){reflectionTarget.SetInt(CloudCount,0);reflectionTarget.SetFloat("_PondCloudSingle",0);}
            for(int i=0;i<cards.Length;i++)
            {
                Card card=cards[i];if(card?.transform==null||card.renderer==null||blocks[i]==null)continue;
                card.transform.SetPositionAndRotation(positions[i],rotations[i]);card.transform.localScale=scales[i];
                card.renderer.SetPropertyBlock(null);
                card.renderer.enabled=previousEnabled[i];
                if(card.renderer.sharedMaterial==liveMaterial)card.renderer.sharedMaterial=previousMaterials[i];
            }
            if(bankFilter!=null&&bankFilter.sharedMesh==bankMesh)bankFilter.sharedMesh=previousBankMesh;
            if(bankMesh!=null)Destroy(bankMesh);
            bankMesh=null;bankFilter=null;bankLife=null;
            if(liveMaterial!=null)Destroy(liveMaterial);
            liveMaterial=null;sequence=null;currentType=WellnessCloudType.SoftBanks;
        }
    }
}
