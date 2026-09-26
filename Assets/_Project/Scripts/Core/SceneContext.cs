using UnityEngine;

namespace TheLastWatch.Core
{
    public sealed class SceneContext : MonoBehaviour
    {
        [SerializeField] private SceneRole role;

        public SceneRole Role => role;

        private void Start()
        {
            if (!GameServices.IsInitialized)
            {
                return;
            }

            switch (role)
            {
                case SceneRole.MainMenu:
                    GameServices.State.SetState(GameState.MainMenu);
                    break;
                case SceneRole.Gameplay:
                case SceneRole.Development:
                    GameServices.State.SetState(GameState.Playing);
                    break;
            }
        }
    }
}
