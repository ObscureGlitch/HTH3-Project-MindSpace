using UnityEngine;

namespace TheLastWatch.UI
{
    public static class WellnessUiText
    {
        // GUIStyle(GUI.skin.label) inherits white hover/active colors unless all states are explicit.
        public static GUIStyle Static(GUIStyle style,Color color)
        {
            style.normal.textColor=style.hover.textColor=style.active.textColor=style.focused.textColor=color;
            style.onNormal.textColor=style.onHover.textColor=style.onActive.textColor=style.onFocused.textColor=color;
            style.hover.background=style.normal.background;
            style.hover.scaledBackgrounds=style.normal.scaledBackgrounds;
            style.onHover.background=style.onNormal.background;
            style.onHover.scaledBackgrounds=style.onNormal.scaledBackgrounds;
            return style;
        }
        public static Color NoteColor(int index,bool darkBackdrop=false)
        {
            Color c=index%3==0?new Color(.36f,.52f,.39f):index%3==1?new Color(.66f,.49f,.24f):new Color(.55f,.42f,.60f);
            return darkBackdrop?Color.Lerp(c,new Color(.96f,.95f,.90f),.43f):c;
        }
    }
}
