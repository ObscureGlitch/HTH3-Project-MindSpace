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
        public Card[] cards=Array.Empty<Card>();
        [Range(0,4)] public float windMetresPerSecond=2.4f;
        private const float HalfSpan=6500, SkyRadius=150;
        private Material liveMaterial;
        private Material[] previousMaterials;
        private MaterialPropertyBlock[] blocks;
        private Vector3[] positions, scales, virtualPositions;
        private Quaternion[] rotations;
        private float coverage=.4f;
        private Color tint=Color.white, horizon=new Color(.7f,.8f,.88f);
        private bool ready;
        private static readonly int Opacity=Shader.PropertyToID("_Opacity");

        public static float DriftDegreesPerSecond(float wind,float altitude) => Mathf.Rad2Deg*Mathf.Max(0,wind)/Mathf.Max(1,altitude);
        public static float AdvanceWind(float x,float metresPerSecond,float seconds) => Mathf.Repeat(x+HalfSpan+metresPerSecond*seconds,HalfSpan*2)-HalfSpan;
        public static Vector3 Project(Vector3 position,Vector3 eye) => eye+position.normalized*SkyRadius;
        public static Vector3 ProjectedScale(Vector3 position,Vector2 size)
        {float scale=SkyRadius/Mathf.Max(1,position.magnitude);return new Vector3(size.x*scale,size.y*scale,1);}
        public void SetConditions(float cover,Color cloudTint,Color skyHorizon)
        {
            coverage=Mathf.Clamp01(cover);tint=cloudTint;horizon=skyHorizon;
            if(liveMaterial!=null){liveMaterial.SetColor("_Tint",tint);liveMaterial.SetColor("_HorizonColor",horizon);}
        }
        private void OnEnable()
        {
            if(!Application.isPlaying||viewer==null||cloudMaterial==null)return;
            liveMaterial=new Material(cloudMaterial){name="Photographic clouds (runtime)",hideFlags=HideFlags.DontSave};
            int count=cards.Length;positions=new Vector3[count];scales=new Vector3[count];rotations=new Quaternion[count];
            virtualPositions=new Vector3[count];previousMaterials=new Material[count];blocks=new MaterialPropertyBlock[count];
            for(int i=0;i<count;i++)
            {
                Card card=cards[i];if(card?.transform==null||card.renderer==null)continue;
                positions[i]=card.transform.position;scales[i]=card.transform.localScale;rotations[i]=card.transform.rotation;
                virtualPositions[i]=card.virtualPosition;previousMaterials[i]=card.renderer.sharedMaterial;
                card.renderer.sharedMaterial=liveMaterial;blocks[i]=new MaterialPropertyBlock();
            }
            ready=true;SetConditions(coverage,tint,horizon);PlaceCards(0);
        }
        private void LateUpdate()
        {
            if(!ready||viewer==null)return;
            float dt=Application.isFocused?Mathf.Min(Time.deltaTime,.25f):0;
            PlaceCards(dt);
        }
        private void PlaceCards(float dt)
        {
            Vector3 eye=viewer.transform.position;
            for(int i=0;i<cards.Length;i++)
            {
                Card card=cards[i];if(card?.transform==null||card.renderer==null||blocks[i]==null)continue;
                Vector3 p=virtualPositions[i];p.x=AdvanceWind(p.x,Mathf.Clamp(windMetresPerSecond,0,4)*card.speedFactor,dt);virtualPositions[i]=p;
                Vector3 direction=p.normalized;
                card.transform.SetPositionAndRotation(Project(p,eye),Quaternion.LookRotation(-direction,Vector3.up)*Quaternion.Euler(0,0,card.rollDegrees));
                card.transform.localScale=ProjectedScale(p,card.sizeMetres);
                float edge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(HalfSpan,HalfSpan-900,Mathf.Abs(p.x)));
                float horizonFade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.07f,.22f,direction.y));
                // Fade additional cloud banks in as the weather closes, without scaling shapes.
                float density=i<6?Mathf.Lerp(.72f,1,coverage):Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,.85f,coverage));
                blocks[i].SetFloat(Opacity,card.opacity*edge*horizonFade*density);card.renderer.SetPropertyBlock(blocks[i]);
            }
        }
        private void OnDisable()
        {
            if(!ready)return;ready=false;
            for(int i=0;i<cards.Length;i++)
            {
                Card card=cards[i];if(card?.transform==null||card.renderer==null||blocks[i]==null)continue;
                card.transform.SetPositionAndRotation(positions[i],rotations[i]);card.transform.localScale=scales[i];
                card.renderer.SetPropertyBlock(null);
                if(card.renderer.sharedMaterial==liveMaterial)card.renderer.sharedMaterial=previousMaterials[i];
            }
            if(liveMaterial!=null)Destroy(liveMaterial);
        }
    }
}
