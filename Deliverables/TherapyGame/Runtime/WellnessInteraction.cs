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
        public WellnessDoor door;
        public TheLastWatch.Integrations.WellnessTherapist therapist;
        public string Prompt => therapist != null && therapist.isActiveAndEnabled ? therapist.Prompt : door != null && door.isActiveAndEnabled ? door.Prompt : displayName;
        public UnityEvent onInteract = new UnityEvent();
        public event System.Action<WellnessInteraction> Noticed;
        public string Interact()
        {
            if (therapist != null && therapist.isActiveAndEnabled) return therapist.Interact();
            if (door != null && door.isActiveAndEnabled) return door.Toggle();
            if (breathingOrb != null) breathingOrb.Toggle();
            Noticed?.Invoke(this);
            onInteract.Invoke();
            return reflection;
        }
    }
}
