using System;
using UnityEngine;

namespace TheLastWatch.Integrations
{
    [CreateAssetMenu(menuName = "Therapy Game/Voice Companions")]
    public sealed class WellnessVoiceSettings : ScriptableObject
    {
        [Serializable]
        public sealed class Agent
        {
            public string displayName;
            [Tooltip("Public agent identifier only. Never put an API key here.")]
            public string agentId;
            public string colorVariable;
        }
        public Agent[] agents = Array.Empty<Agent>();
        [Range(10, 30)] public int connectionTimeoutSeconds = 15;
        [Range(60, 900)] public int maximumSessionSeconds = 600;
    }
}
