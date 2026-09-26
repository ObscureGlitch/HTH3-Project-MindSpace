using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastWatch.Core
{
    public sealed class SceneTransitionService : ISceneTransitionService
    {
        private readonly GameStateController _gameState;
        private readonly IProjectLogger _logger;

        public SceneTransitionService(GameStateController gameState, IProjectLogger logger)
        {
            _gameState = gameState;
            _logger = logger;
        }

        public bool IsLoading { get; private set; }

        public async Task LoadAsync(string sceneName, CancellationToken cancellationToken)
        {
            if (IsLoading)
            {
                throw new InvalidOperationException("A scene transition is already in progress.");
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("A scene name is required.", nameof(sceneName));
            }

            IsLoading = true;
            _gameState.SetState(GameState.Loading);
            _logger.Info($"Loading scene '{sceneName}'.");

            try
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (operation == null)
                {
                    throw new InvalidOperationException($"Unity could not start loading scene '{sceneName}'.");
                }

                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
