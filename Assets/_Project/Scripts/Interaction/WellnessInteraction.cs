using TheLastWatch.Environment;
using UnityEngine;
using UnityEngine.Events;

namespace TheLastWatch.Interaction
{
    public sealed class WellnessInteraction : MonoBehaviour
    {
        public enum Area { Grounding, Breathing, Reflection }
        public Area area;
        public string displayName = "Notice this object";
        [TextArea] public string reflection = "Take a moment to notice the room around you.";
        public WellnessBreathingOrb breathingOrb;
        public UnityEvent onInteract = new UnityEvent();
        public string Interact()
        {
            if (breathingOrb != null) breathingOrb.Toggle();
            onInteract.Invoke();
            return reflection;
        }
    }
}
