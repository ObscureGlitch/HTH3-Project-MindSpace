using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastWatch.Input
{
    public sealed class UnityGameInput : IGameInput
    {
        private readonly InputActionAsset _asset;
        private readonly InputAction _move;
        private readonly InputAction _look;
        private readonly InputAction _interact;
        private readonly InputAction _sprint;
        private readonly InputAction _flashlight;
        private readonly InputAction _pause;
        private readonly InputAction _pushToTalk;
        private readonly InputAction _submit;
        private readonly InputAction _cancel;

        public UnityGameInput(InputActionAsset asset)
        {
            _asset = asset != null ? asset : throw new ArgumentNullException(nameof(asset));
            _move = Find(InputActionNames.Move);
            _look = Find(InputActionNames.Look);
            _interact = Find(InputActionNames.Interact);
            _sprint = Find(InputActionNames.Sprint);
            _flashlight = Find(InputActionNames.Flashlight);
            _pause = Find(InputActionNames.Pause);
            _pushToTalk = Find(InputActionNames.PushToTalk);
            _submit = Find(InputActionNames.Submit);
            _cancel = Find(InputActionNames.Cancel);

            _interact.performed += OnInteract;
            _flashlight.performed += OnFlashlight;
            _pause.performed += OnPause;
            _submit.performed += OnSubmit;
            _cancel.performed += OnCancel;
        }

        public event Action InteractPerformed;
        public event Action FlashlightPerformed;
        public event Action PausePerformed;
        public event Action SubmitPerformed;
        public event Action CancelPerformed;

        public Vector2 Move => _move.ReadValue<Vector2>();
        public Vector2 Look => _look.ReadValue<Vector2>();
        public bool SprintPressed => _sprint.IsPressed();
        public bool PushToTalkPressed => _pushToTalk.IsPressed();

        public void Enable()
        {
            _asset.Enable();
        }

        public void Disable()
        {
            _asset.Disable();
        }

        public void Dispose()
        {
            Disable();
            _interact.performed -= OnInteract;
            _flashlight.performed -= OnFlashlight;
            _pause.performed -= OnPause;
            _submit.performed -= OnSubmit;
            _cancel.performed -= OnCancel;
        }

        private InputAction Find(string path)
        {
            return _asset.FindAction(path, true);
        }

        private void OnInteract(InputAction.CallbackContext context) => InteractPerformed?.Invoke();
        private void OnFlashlight(InputAction.CallbackContext context) => FlashlightPerformed?.Invoke();
        private void OnPause(InputAction.CallbackContext context) => PausePerformed?.Invoke();
        private void OnSubmit(InputAction.CallbackContext context) => SubmitPerformed?.Invoke();
        private void OnCancel(InputAction.CallbackContext context) => CancelPerformed?.Invoke();
    }
}
