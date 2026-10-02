using System;
using System.Collections;
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
        private bool _uiInputBlocked, _cursorBeforeUi, _theatreMode;
        private WellnessInteraction _focused;
        private WellnessSeat _focusedSeat, _occupiedSeat;
        private int _focusedSpot = -1, _occupiedSpot = -1;
        private bool _seated, _transitioning, _hasStandingState, _standingControllerEnabled;
        private float _seatYaw, _seatLookYaw, _fade;
        private Vector3 _standingPosition, _standingCameraPosition;
        private Quaternion _standingRotation, _standingCameraRotation;
        private readonly RaycastHit[] _aimHits = new RaycastHit[32];
        private string _message;
        private float _messageUntil;
        private GUIStyle _titleStyle, _bodyStyle, _smallStyle;
        private Texture2D _panel;

        public bool IsSeated => _seated;
        public bool IsTransitioning => _transitioning;
        public WellnessSeat CurrentSeat => _occupiedSeat;
        public int CurrentSpot => _occupiedSpot;
        public WellnessSeat FocusedSeat => _focusedSeat;
        public int FocusedSpot => _focusedSpot;
        public Camera ViewCamera => viewCamera;
        public bool IsUiInputBlocked => _uiInputBlocked;
        public bool useReferenceHud;
        public bool CursorReleased => _released;
        public bool TheatreMode => _theatreMode;
        public float TransitionFade => _fade;
        public string NoticeMessage => Time.unscaledTime<_messageUntil?_message:null;
        public string HudPrompt
        {
            get
            {
                if(_released)return "Press Enter to explore";
                if(_transitioning)return null;
                if(_seated)return _focused!=null&&_focusedSeat==null?_focused.Prompt:"Stand up";
                if(_focusedSeat!=null)return "Sit · "+_focusedSeat.Label(_focusedSpot);
                return _focused!=null?_focused.Prompt:null;
            }
        }
        public string HudPromptKey => _released?"Enter":_seated&&(_focused==null||_focusedSeat!=null)?"Space":"E";

        public void SetUiInputBlocked(bool blocked)
        {
            if (_uiInputBlocked == blocked) return;
            if (blocked) { _cursorBeforeUi = _released; _uiInputBlocked = true; SetCursor(true); }
            else { _uiInputBlocked = false; SetCursor(_cursorBeforeUi); }
        }

        public void SetTheatreMode(bool enabled)
        {
            _theatreMode = enabled;
            if (enabled) SetCursor(false);
        }

        public void Configure(Camera camera) { viewCamera = camera; }
        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = new WellnessRoomInput();
            _input.InteractPerformed += Interact;
            _input.PausePerformed += ToggleCursor;
            _input.SubmitPerformed += Resume;
            _input.StandPerformed += StandFromInput;
        }
        private void OnEnable() { _input?.Enable(); SetCursor(false); }
        private void OnDisable()
        {
            _input?.Disable(); StopAllCoroutines();
            // Also handles Unity's no-domain/no-scene-reload Play mode and disabling the player mid-transition.
            if (_hasStandingState && _controller != null && viewCamera != null)
            {
                _controller.enabled = false;
                transform.SetPositionAndRotation(_standingPosition, _standingRotation);
                viewCamera.transform.localPosition = _standingCameraPosition;
                viewCamera.transform.localRotation = _standingCameraRotation;
                _pitch = Mathf.DeltaAngle(0, _standingCameraRotation.eulerAngles.x);
                _controller.enabled = _standingControllerEnabled;
            }
            _seated = _transitioning = _hasStandingState = false; _fade = 0;
            _uiInputBlocked = _theatreMode = false;
            _occupiedSeat = _focusedSeat = null; _occupiedSpot = _focusedSpot = -1; _focused = null;
            SetCursor(true);
        }
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
        private void ToggleCursor() { if (!_uiInputBlocked && !_theatreMode) SetCursor(!_released); }
        private void Resume() { if (_released && !_uiInputBlocked && !_theatreMode) SetCursor(false); }
        private void StandFromInput() { if (!_released && !_uiInputBlocked && !TheLastWatch.UI.WellnessPhotoCamera.CameraActive) TryStand(); }
        private void Interact()
        {
            if (_released || _transitioning || _uiInputBlocked || TheLastWatch.UI.WellnessPhotoCamera.CameraActive) return;
            UpdateFocus(); // Do not act on a stale prompt after the camera moves.
            if (_seated)
            {
                if (_focused != null && _focusedSeat == null) ShowMessage(_focused.Interact());
                else TryStand();
                return;
            }
            if (_focusedSeat != null)
            {
                if (!TrySit(_focusedSeat, _focusedSpot)) ShowMessage("Move a little closer to the seat, with a clear path in front of you.");
                return;
            }
            if (_focused != null) ShowMessage(_focused.Interact());
        }
        private void ShowMessage(string text) { _message = text; _messageUntil = Time.unscaledTime + 7f; }

        public bool TrySit(WellnessSeat seat, int spot)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || _seated || _transitioning || seat == null || !seat.Valid(spot) || viewCamera == null) return false;
            Vector3 target = seat.AimPosition(spot);
            if (Vector3.Distance(viewCamera.transform.position, target) > 2.9f) return false;
            Vector3 toSeat = target - viewCamera.transform.position;
            if (RaycastWorld(new Ray(viewCamera.transform.position, toSeat.normalized), toSeat.magnitude, out RaycastHit blocker) &&
                blocker.collider.GetComponentInParent<WellnessSeat>() != seat) return false;
            _standingPosition = transform.position; _standingRotation = transform.rotation;
            _standingCameraPosition = viewCamera.transform.localPosition; _standingCameraRotation = viewCamera.transform.localRotation;
            _standingControllerEnabled = _controller.enabled; _hasStandingState = true;
            StartCoroutine(ChangePosture(() =>
            {
                if (seat == null || !seat.Valid(spot)) { _hasStandingState = false; return; }
                _controller.enabled = false; _verticalSpeed = 0;
                transform.SetPositionAndRotation(seat.BodyPosition(spot), seat.Facing(spot));
                viewCamera.transform.localPosition = new Vector3(0, seat.spots[spot].eyeHeight, 0);
                viewCamera.transform.localRotation = Quaternion.identity; _pitch = _seatLookYaw = 0;
                _seatYaw = transform.eulerAngles.y; _occupiedSeat = seat; _occupiedSpot = spot; _seated = true;
                _messageUntil = 0;
                // Keep the reflection chair's existing thoughtful prompt and UnityEvent intact.
                WellnessInteraction reflection = seat.GetComponent<WellnessInteraction>();
                if (reflection != null) ShowMessage(reflection.Interact());
            }));
            return true;
        }

        public bool TryStand()
        {
            if (!_seated || _transitioning) return false;
            if (!TryFindStandingPosition(out _)) { ShowMessage("There isn't enough room to stand here yet."); return false; }
            StartCoroutine(ChangePosture(() =>
            {
                // Recheck after the short fade in case a moving object entered the exit space.
                if (!TryFindStandingPosition(out Vector3 exit)) { ShowMessage("The standing space is blocked."); return; }
                transform.position = exit; viewCamera.transform.localPosition = _standingCameraPosition;
                _controller.enabled = _standingControllerEnabled; _verticalSpeed = -2;
                _seated = _hasStandingState = false; _occupiedSeat = null; _occupiedSpot = -1;
                _messageUntil = 0;
            }));
            return true;
        }

        private IEnumerator ChangePosture(Action change)
        {
            _transitioning = true; _focused = null; _focusedSeat = null; _focusedSpot = -1;
            const float duration = .12f;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            { _fade = Mathf.SmoothStep(0, 1, elapsed / duration); yield return null; }
            _fade = 1; change(); yield return null;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            { _fade = 1 - Mathf.SmoothStep(0, 1, elapsed / duration); yield return null; }
            _fade = 0; _transitioning = false;
        }

        private bool TryFindStandingPosition(out Vector3 result)
        {
            Physics.SyncTransforms();
            if (StandingSpace(_standingPosition, out result)) return true;
            if (_occupiedSeat != null && _occupiedSeat.Valid(_occupiedSpot))
                for (int i = 0; i < 4; i++)
                    if (StandingSpace(_occupiedSeat.ExitCandidate(_occupiedSpot, i), out result)) return true;
            result = default; return false;
        }

        private bool StandingSpace(Vector3 candidate, out Vector3 grounded)
        {
            grounded = candidate;
            if (!RaycastWorld(new Ray(candidate + Vector3.up * .35f, Vector3.down), .8f, out RaycastHit floor) || floor.normal.y < .7f) return false;
            grounded.y = floor.point.y - _controller.center.y + _controller.height * .5f + _controller.skinWidth + .01f;
            float radius = _controller.radius * .96f;
            Vector3 center = grounded + Vector3.up * _controller.center.y;
            float half = _controller.height * .5f - radius;
            foreach (Collider obstacle in Physics.OverlapCapsule(center - Vector3.up * half, center + Vector3.up * half, radius, ~0, QueryTriggerInteraction.Ignore))
                if (!obstacle.transform.IsChildOf(transform)) return false;
            return true;
        }

        private void Update()
        {
            if (_released || _transitioning || _uiInputBlocked) return;
            Vector2 look = _input.Look;
            if (_seated)
            {
                _seatLookYaw = Mathf.Repeat(_seatLookYaw + look.x * mouseSensitivity, 360);
                transform.rotation = Quaternion.Euler(0, _seatYaw + _seatLookYaw, 0);
            }
            else transform.Rotate(0f, look.x * mouseSensitivity, 0f);
            _pitch = Mathf.Clamp(_pitch - look.y * mouseSensitivity, -75f, 75f);
            viewCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            if (!_seated)
            {
                Vector2 move = Vector2.ClampMagnitude(_input.Move, 1f);
                _verticalSpeed = _controller.isGrounded ? -2f : _verticalSpeed - 9.81f * Time.deltaTime;
                _controller.Move((transform.right * move.x * walkSpeed + transform.forward * move.y * walkSpeed + Vector3.up * _verticalSpeed) * Time.deltaTime);
            }
            else if (_occupiedSeat == null || !_occupiedSeat.isActiveAndEnabled) TryStand();
            UpdateFocus();
        }
        private void UpdateFocus()
        {
            _focused = null;
            _focusedSeat = null; _focusedSpot = -1;
            if (RaycastWorld(viewCamera.ViewportPointToRay(new Vector3(.5f, .5f)), 2.6f, out RaycastHit hit))
            {
                WellnessSeat seat = hit.collider.GetComponentInParent<WellnessSeat>();
                if (seat != null && seat.isActiveAndEnabled)
                {
                    int spot = seat.NearestSpot(hit.point);
                    if (spot >= 0) { _focusedSeat = seat; _focusedSpot = spot; }
                }
                _focused = hit.collider.GetComponentInParent<WellnessInteraction>();
            }
        }
        private bool RaycastWorld(Ray ray, float distance, out RaycastHit hit)
        {
            int count = Physics.RaycastNonAlloc(ray, _aimHits, distance, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = _aimHits;
            // If a future dense scene fills the buffer, do not accidentally select through its nearest wall.
            if (count == _aimHits.Length) { hits = Physics.RaycastAll(ray, distance, ~0, QueryTriggerInteraction.Ignore); count = hits.Length; }
            hit = default; float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform) || hits[i].distance >= nearest) continue;
                hit = hits[i]; nearest = hit.distance;
            }
            return nearest < float.PositiveInfinity;
        }
        private void OnGUI()
        {
            if (_uiInputBlocked || _theatreMode || useReferenceHud || TheLastWatch.UI.WellnessPhotoCamera.HideGameplayHud) return;
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
            GUI.DrawTexture(new Rect(24, 24, 320, 86), _panel);
            GUI.Label(new Rect(44, 33, 290, 36), "A quiet place", _titleStyle);
            string seatLabel = _occupiedSeat != null ? _occupiedSeat.Label(_occupiedSpot) : "Seat";
            GUI.Label(new Rect(44, 70, 290, 28), _seated ? "Sitting · " + seatLabel : "Look at a seat to choose your spot", _smallStyle);
            GUI.DrawTexture(new Rect(24, height - 53, 570, 29), _panel);
            GUI.Label(new Rect(38, height - 51, 550, 26), _seated ? "Mouse  Look     Space  Stand up     E  Notice / stand     Alt  Cursor" : "WASD  Walk     Mouse  Look     E  Sit / door / notice     Alt  Cursor", _smallStyle);
            if (!_released && !_transitioning) GUI.Label(new Rect(width / 2f - 8, height / 2f - 12, 16, 24), _focusedSeat != null && !_seated ? "○" : "·", _bodyStyle);
            string prompt = null;
            if (_released) prompt = "Cursor released · Press Enter to explore";
            else if (!_transitioning)
            {
                if (_seated) prompt = _focused != null && _focusedSeat == null ? "E  ·  " + _focused.Prompt : "Space or E  ·  Stand up";
                else if (_focusedSeat != null) prompt = "E  ·  Sit on " + _focusedSeat.Label(_focusedSpot);
                else if (_focused != null) prompt = "E  ·  " + _focused.Prompt;
            }
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
            if (_fade > 0)
            {
                Color oldColor = GUI.color; GUI.color = new Color(.04f, .05f, .04f, _fade);
                GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture); GUI.color = oldColor;
            }
            GUI.matrix = old;
        }
    }
}
