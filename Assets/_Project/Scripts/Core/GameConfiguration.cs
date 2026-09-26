using UnityEngine;

namespace TheLastWatch.Core
{
    [CreateAssetMenu(fileName = "GameConfiguration", menuName = "The Last Watch/Game Configuration")]
    public sealed class GameConfiguration : ScriptableObject, IGameConfiguration
    {
        [SerializeField] private BuildConfiguration buildConfiguration = BuildConfiguration.Development;
        [SerializeField] private string initialSceneName = "MainMenu";
        [SerializeField] private bool autoLoadInitialScene = true;
        [SerializeField] private bool verboseLogging = true;

        public BuildConfiguration BuildConfiguration => buildConfiguration;
        public string InitialSceneName => initialSceneName;
        public bool AutoLoadInitialScene => autoLoadInitialScene;
        public bool VerboseLogging => verboseLogging;
    }
}
