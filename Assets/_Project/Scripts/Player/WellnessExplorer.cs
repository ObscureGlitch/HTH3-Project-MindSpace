using TheLastWatch.Input;
using TheLastWatch.Interaction;
using UnityEngine;

namespace TheLastWatch.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class WellnessExplorer : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float walkSpeed = 1.8f;
        [SerializeField] private float mouseSensitivity = 0.07f;
        private CharacterController _controller;
        private WellnessRoomInput _input;
        private float _pitch, _verticalSpeed;
        private bool _released;
        private WellnessInteraction _focused;
        private string _message;
        private float _messageUntil;
        private GUIStyle _titleStyle, _bodyStyle, _smallStyle;
        private Texture2D _panel;

        public void Configure(Camera camera) { viewCamera = camera; }
        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = new WellnessRoomInput();
            _input.InteractPerformed += Interact;
            _input.PausePerformed += ToggleCursor;
            _input.SubmitPerformed += Resume;
        }
        private void OnEnable() { _input?.Enable(); SetCursor(false); }
        private void OnDisable() { _input?.Disable(); SetCursor(true); }
        private void OnDestroy()
        {
            _input?.Dispose();
            if (_panel != null) Destroy(_panel);
        }
        private void SetCursor(bool released)
        {
            _released = released;
            Cursor.lockState = released ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = released;
        }
        private void ToggleCursor() => SetCursor(!_released);
        private void Resume() { if (_released) SetCursor(false); }
        private void Interact()
        {
            if (_released || _focused == null) return;
            _message = _focused.Interact();
            _messageUntil = Time.unscaledTime + 7f;
        }
        private void Update()
        {
            if (_released) return;
            Vector2 look = _input.Look;
            transform.Rotate(0f, look.x * mouseSensitivity, 0f);
            _pitch = Mathf.Clamp(_pitch - look.y * mouseSensitivity, -75f, 75f);
            viewCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            Vector2 move = Vector2.ClampMagnitude(_input.Move, 1f);
            _verticalSpeed = _controller.isGrounded ? -2f : _verticalSpeed - 9.81f * Time.deltaTime;
            _controller.Move((transform.right * move.x * walkSpeed + transform.forward * move.y * walkSpeed + Vector3.up * _verticalSpeed) * Time.deltaTime);
            _focused = null;
            if (Physics.Raycast(viewCamera.ViewportPointToRay(new Vector3(.5f, .5f)), out RaycastHit hit, 2.6f))
                _focused = hit.collider.GetComponentInParent<WellnessInteraction>();
        }
        private void OnGUI()
        {
            if (_panel == null)
            {
                _panel = new Texture2D(1, 1); _panel.SetPixel(0, 0, new Color(.10f, .14f, .12f, .88f)); _panel.Apply();
                _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = new Color(.94f, .91f, .82f) } };
                _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.94f, .91f, .82f) } };
                _smallStyle = new GUIStyle(_bodyStyle) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.DrawTexture(new Rect(24, 24, 260, 86), _panel);
            GUI.Label(new Rect(44, 33, 230, 36), "A quiet place", _titleStyle);
            GUI.Label(new Rect(44, 70, 220, 28), "Explore at your own pace", _smallStyle);
            GUI.DrawTexture(new Rect(24, height - 53, 470, 29), _panel);
            GUI.Label(new Rect(38, height - 51, 450, 26), "WASD  Walk     Mouse  Look     E  Notice     Esc  Release cursor", _smallStyle);
            if (!_released) GUI.Label(new Rect(width / 2f - 6, height / 2f - 12, 12, 24), "·", _bodyStyle);
            string prompt = _released ? "Cursor released · Press Enter to explore" : _focused != null ? "E  ·  " + _focused.displayName : null;
            if (prompt != null)
            {
                GUI.DrawTexture(new Rect(width / 2f - 230, height - 135, 460, 44), _panel);
                GUI.Label(new Rect(width / 2f - 220, height - 133, 440, 40), prompt, _bodyStyle);
            }
            if (Time.unscaledTime < _messageUntil)
            {
                GUI.DrawTexture(new Rect(width / 2f - 290, height - 224, 580, 72), _panel);
                GUI.Label(new Rect(width / 2f - 272, height - 218, 544, 60), _message, _bodyStyle);
            }
            GUI.matrix = old;
        }
    }
}
