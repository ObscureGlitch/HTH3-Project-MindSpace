using System;
using UnityEngine;

namespace TheLastWatch.Core
{
    public sealed class PauseStateManager : IDisposable
    {
        private readonly GameStateController _gameState;
        private float _timeScaleBeforePause = 1f;

        public PauseStateManager(GameStateController gameState)
        {
            _gameState = gameState;
        }

        public bool IsPaused => _gameState.Current == GameState.Paused;

        public void Toggle()
        {
            if (_gameState.Current == GameState.Playing || IsPaused)
            {
                SetPaused(!IsPaused);
            }
        }

        public void SetPaused(bool paused)
        {
            if (paused == IsPaused)
            {
                return;
            }

            if (paused)
            {
                _timeScaleBeforePause = Time.timeScale;
                Time.timeScale = 0f;
                _gameState.SetState(GameState.Paused);
                return;
            }

            Time.timeScale = _timeScaleBeforePause;
            _gameState.SetState(GameState.Playing);
        }

        public void Dispose()
        {
            if (IsPaused)
            {
                Time.timeScale = _timeScaleBeforePause;
            }
        }
    }
}
