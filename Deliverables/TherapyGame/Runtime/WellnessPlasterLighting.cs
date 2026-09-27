using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastWatch.Environment
{
    // Editor scenes do not serialize lightmapIndex. Restore these four reprojected panels
    // using the existing atlas, without baking or allocating another lightmap.
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class WellnessPlasterLighting : MonoBehaviour
    {
        public MeshRenderer[] panels=System.Array.Empty<MeshRenderer>();
        public Texture2D bakedAtlas;
        public Vector4 atlasScaleOffset;
        private int remaining;
        private void OnEnable(){remaining=30;SceneManager.sceneLoaded+=Loaded;Apply();}
        private void OnDisable()=>SceneManager.sceneLoaded-=Loaded;
        private void Loaded(Scene scene,LoadSceneMode mode){if(scene==gameObject.scene){remaining=30;Apply();}}
        private void LateUpdate(){if(remaining>0){remaining--;if(Apply())remaining=0;}}
        public bool Apply()
        {
            if(bakedAtlas==null||panels==null||panels.Length==0)return false;
            var maps=LightmapSettings.lightmaps;int index=-1;
            for(int i=0;i<maps.Length;i++)if(maps[i].lightmapColor==bakedAtlas){index=i;break;}
            if(index<0)return false;
            foreach(var panel in panels){if(panel==null)continue;panel.lightmapIndex=index;panel.lightmapScaleOffset=atlasScaleOffset;}
            return true;
        }
    }
}
