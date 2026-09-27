using UnityEngine;

namespace TheLastWatch.UI
{
    public static class PresagePositionOverlay
    {
        private static Texture2D silhouette;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTexture()
        {
            if (silhouette != null) Object.Destroy(silhouette);
            silhouette = null;
        }

        // Match ScaleToFit's letterboxing so the guide always sits inside the camera image.
        public static Rect ImageRect(Rect viewport, float aspect)
        {
            if (aspect <= 0 || float.IsNaN(aspect) || float.IsInfinity(aspect)) aspect = 16f / 9;
            float w = Mathf.Min(viewport.width, viewport.height * aspect);
            float h = w / aspect;
            return new Rect(viewport.center.x - w / 2, viewport.center.y - h / 2, w, h);
        }
        public static void Draw(Rect viewport, Texture image, bool positioned, GUIStyle label)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (silhouette == null)
            {
                silhouette = new Texture2D(PresageUiRaster.GuideSize, PresageUiRaster.GuideSize, TextureFormat.RGBA32, false)
                { name = "Camera positioning silhouette", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                silhouette.LoadRawTextureData(PresageUiRaster.Guide());
                silhouette.Apply(false, true);
            }
            Rect imageRect = ImageRect(viewport, image != null ? (float)image.width / image.height : 16f / 9);
            float size = Mathf.Min(imageRect.width, imageRect.height) * .96f;
            Rect guide = new Rect(imageRect.center.x - size / 2, imageRect.center.y - size / 2, size, size);
            Color old = GUI.color;
            try
            {
                // One fixed texture works inside nested GUILayout areas and scaled HUD canvases.
                GUI.color = new Color(0, .035f, .05f, .8f);
                GUI.DrawTexture(new Rect(guide.x+1, guide.y+2, size, size), silhouette);
                GUI.color = positioned ? new Color(.43f, 1, .73f, .98f) : new Color(.91f, .98f, 1, .96f);
                GUI.DrawTexture(guide, silhouette);
            }
            finally { GUI.color = old; }
        }
    }
}
