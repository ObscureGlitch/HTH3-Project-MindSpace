using System;

namespace TheLastWatch.Core
{
    public sealed class GameStateController
    {
        public event Action<GameState, GameState> Changed;

        public GameState Current { get; private set; } = GameState.Uninitialized;

        public void SetState(GameState next)
        {
            if (Current == next)
            {
                return;
            }

            GameState previous = Current;
            Current = next;
            Changed?.Invoke(previous, next);
        }
    }
}
