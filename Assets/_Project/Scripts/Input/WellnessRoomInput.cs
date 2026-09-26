using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastWatch.Input
{
    // Self-contained input boundary so the room can be imported into another URP project.
    public sealed class WellnessRoomInput : IDisposable
    {
        private readonly InputActionMap _map = new InputActionMap("TherapyRoom");
        private readonly InputAction _move, _look;
        public event Action InteractPerformed, PausePerformed, SubmitPerformed;
        public WellnessRoomInput()
        {
            _move = _map.AddAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _look = _map.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            _map.AddAction("Notice", InputActionType.Button, "<Keyboard>/e").performed += _ => InteractPerformed?.Invoke();
            _map.AddAction("ReleaseCursor", InputActionType.Button, "<Keyboard>/escape").performed += _ => PausePerformed?.Invoke();
            _map.AddAction("Resume", InputActionType.Button, "<Keyboard>/enter").performed += _ => SubmitPerformed?.Invoke();
        }
        public Vector2 Move => _move.ReadValue<Vector2>();
        public Vector2 Look => _look.ReadValue<Vector2>();
        public void Enable() => _map.Enable();
        public void Disable() => _map.Disable();
        public void Dispose() => _map.Dispose();
    }
}
