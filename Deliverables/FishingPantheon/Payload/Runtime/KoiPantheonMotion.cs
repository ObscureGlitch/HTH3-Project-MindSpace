using UnityEngine;
namespace TheLastWatch.UI
{
    // Explicit ticks from catch/held/preview owners; no hidden per-model Update.
    public sealed class KoiPantheonMotion : MonoBehaviour
    {
        Transform[] parts;Vector3[] positions;
        public void Initialize()
        {int n=transform.childCount;parts=new Transform[n];positions=new Vector3[n];for(int i=0;i<n;i++){parts[i]=transform.GetChild(i);positions[i]=parts[i].localPosition;}}
        public void Pose(float seconds)
        {
            if(parts==null)return;
            for(int i=0;i<parts.Length;i++)
            {
                var part=parts[i];part.localPosition=positions[i];
                if(part.name=="tail")part.localRotation=Quaternion.Euler(0,Mathf.Sin(seconds*5.5f)*8,0);
                else if(part.name=="leftFin"||part.name=="rightFin")part.localRotation=Quaternion.Euler(0,0,Mathf.Sin(seconds*3.5f)*5);
                else if(part.name=="orbs")part.localRotation=Quaternion.Euler(0,seconds*22,0);
                else if(part.name=="halo"||part.name=="corona"||part.name=="crescent")part.localPosition+=Vector3.up*Mathf.Sin(seconds*1.4f)*.009f;
            }
        }
    }
}
