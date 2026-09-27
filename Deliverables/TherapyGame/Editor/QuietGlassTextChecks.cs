using System;
using TheLastWatch.UI;
using UnityEngine;

namespace TherapyGame.Editor
{
    public static class QuietGlassTextChecks
    {
        // Runs in the existing editor, without OnGUI, rendering or microphone access.
        public static int Run()
        {
            int count=0;
            foreach(Color color in new[]{WellnessQuietGlassTheme.Ink,WellnessQuietGlassTheme.Muted,new Color(.96f,.97f,.93f,.94f),new Color(.025f,.045f,.045f,.72f)})
            {
                var style=new GUIStyle();style.normal.textColor=color;
                style.hover.textColor=style.active.textColor=style.focused.textColor=Color.white;
                style.onNormal.textColor=style.onHover.textColor=style.onActive.textColor=style.onFocused.textColor=Color.white;
                WellnessUiText.Static(style,color);
                foreach(var state in new[]{style.normal,style.hover,style.active,style.focused,style.onNormal,style.onHover,style.onActive,style.onFocused})
                {count++;if(state.textColor!=color)throw new Exception("Text must keep the same color in every state.");}
                count++;if(style.hover.background!=style.normal.background||style.onHover.background!=style.onNormal.background)
                    throw new Exception("Hover must not change a text control's background.");
            }
            count++;if(WellnessUiText.NoteColor(0)==WellnessUiText.NoteColor(1)||WellnessUiText.NoteColor(1)==WellnessUiText.NoteColor(2))
                throw new Exception("Music notes must use different muted colors.");
            return count;
        }
    }
}
