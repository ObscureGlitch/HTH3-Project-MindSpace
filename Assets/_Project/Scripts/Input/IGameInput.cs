using System;
using UnityEngine;

namespace TheLastWatch.Input
{
    public interface IGameInput : IDisposable
    {
        event Action InteractPerformed;
        event Action FlashlightPerformed;
        event Action PausePerformed;
        event Action SubmitPerformed;
        event Action CancelPerformed;

        Vector2 Move { get; }
        Vector2 Look { get; }
        bool SprintPressed { get; }
        bool PushToTalkPressed { get; }

        void Enable();
        void Disable();
    }
}
