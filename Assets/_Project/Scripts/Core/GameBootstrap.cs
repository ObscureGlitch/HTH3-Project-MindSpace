using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TheLastWatch.Core
{
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameConfiguration configuration;
        [SerializeField] private InputActionAsset inputActions;

        private static GameBootstrap _instance;
        private CancellationTokenSource _lifetime;

        public GameConfiguration Configuration => configuration;
        public InputActionAsset InputActions => inputActions;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            _lifetime = new CancellationTokenSource();

            try
            {
                GameServices.Initialize(configuration, inputActions);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private async void Start()
        {
            if (!enabled || !GameServices.IsInitialized || !configuration.AutoLoadInitialScene)
            {
                return;
            }

            if (SceneManager.GetActiveScene().name != "Bootstrap")
            {
                return;
            }

            try
            {
                await GameServices.Scenes.LoadAsync(configuration.InitialSceneName, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected during teardown.
            }
            catch (Exception exception)
            {
                GameServices.Logger.Error("Initial scene load failed.", exception);
            }
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            _lifetime?.Cancel();
            _lifetime?.Dispose();
            GameServices.Shutdown();
            _instance = null;
        }
    }
}
