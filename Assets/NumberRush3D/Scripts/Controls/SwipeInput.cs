using System.Collections.Generic;
using NumberRush3D.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NumberRush3D.Controls
{
    /// <summary>
    /// Turns a horizontal swipe into a single lane step request, and keeps the queue short so a
    /// fast flurry of swipes cannot send the army flying across the track.
    ///
    /// Works identically on device (touch) and in the editor (left mouse drag). Nothing is
    /// sampled while the pointer is over a UI element, so tapping the HUD / pause button never
    /// moves the army. Project input settings are untouched: every API access goes through
    /// <see cref="PointerInputBridge"/>.
    ///
    /// Runner upgrade: the forward motion needs no input at all, so this component only ever
    /// answers "left or right".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwipeInput : MonoBehaviour, ILaneInputSource
    {
        [Tooltip("Gameplay tuning asset. Used to clamp the pending swipe queue.")]
        [SerializeField] private GameSettings settings;

        [Tooltip("Horizontal travel in pixels before a drag counts as a swipe.")]
        [SerializeField] private float minSwipeDistance = 40f;

        [Tooltip("Maximum time in seconds between press and release for a drag to count as a swipe.")]
        [SerializeField] private float maxSwipeDuration = 0.6f;

        [Tooltip("Seconds after which an unconsumed swipe request expires.")]
        [SerializeField] private float requestTimeout = 0.5f;

        [Tooltip("Editor convenience: A/D or arrow keys also change lane.")]
        [SerializeField] private bool enableKeyboardTesting = true;

        [Tooltip("Swipes that start on a UI element are ignored. Keep this on for touch devices.")]
        [SerializeField] private bool ignorePointerOverUi = true;

        [Tooltip("Camera used for UI raycasts. Leave empty: the EventSystem camera or Camera.main is used.")]
        [SerializeField] private Camera uiRaycastCamera;

        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(8);
        private PointerEventData _eventData;
        private Vector2 _pressPosition;
        private float _pressTime;
        private bool _tracking;
        private int _pendingAxis;
        private float _pendingAxisTime;
        private bool _interactionEnabled = true;

        private void Awake()
        {
            if (settings == null)
            {
                settings = Resources.Load<GameSettings>("GameSettings");
            }
        }

        /// <summary>
        /// Gate for the gameplay input while a menu / pause panel is open.
        /// Disabling this never touches global input settings, only this component.
        /// </summary>
        public void SetInteractionEnabled(bool enabled)
        {
            _interactionEnabled = enabled;

            if (!enabled)
            {
                _tracking = false;
                _pendingAxis = 0;
            }
        }

        /// <summary>Clears any queued request, e.g. on restart.</summary>
        public void ResetRequests()
        {
            _tracking = false;
            _pendingAxis = 0;
        }

        private void Update()
        {
            if (settings == null)
            {
                return;
            }

            if (!_interactionEnabled)
            {
                ResetRequests();
                return;
            }

            PointerInputBridge.PointerFrame frame = PointerInputBridge.ReadPointer();

            if (frame.PressedThisFrame)
            {
                if (!IsPointerOverUi(frame))
                {
                    _tracking = true;
                    _pressPosition = frame.Position;
                    _pressTime = Time.unscaledTime;
                }
            }

            if (frame.ReleasedThisFrame && _tracking)
            {
                _tracking = false;

                float duration = Time.unscaledTime - _pressTime;
                float deltaX = frame.Position.x - _pressPosition.x;
                float deltaY = frame.Position.y - _pressPosition.y;

                if (duration <= maxSwipeDuration &&
                    Mathf.Abs(deltaX) >= minSwipeDistance &&
                    Mathf.Abs(deltaX) > Mathf.Abs(deltaY) * 0.8f)
                {
                    RequestLane(deltaX > 0f ? 1 : -1);
                }
            }

            if (_tracking && Time.unscaledTime - _pressTime > maxSwipeDuration)
            {
                // Held fingers are not swipes; stop tracking so a long press never commits late.
                _tracking = false;
            }

            if (enableKeyboardTesting)
            {
                int keyboardAxis = PointerInputBridge.ReadKeyboardLaneAxis();
                if (keyboardAxis != 0)
                {
                    RequestLane(keyboardAxis);
                }
            }

            if (_pendingAxis != 0 && Time.unscaledTime - _pendingAxisTime > requestTimeout)
            {
                _pendingAxis = 0;
            }
        }

        /// <summary>Queues one lane step. Extra requests within <see cref="requestTimeout"/> are ignored.</summary>
        public void RequestLane(int direction)
        {
            direction = direction < 0 ? -1 : 1;

            if (!_interactionEnabled)
            {
                return;
            }

            if (_pendingAxis != 0)
            {
                // Keep the queue at one step: a long drag should not skip lanes.
                return;
            }

            _pendingAxis = direction;
            _pendingAxisTime = Time.unscaledTime;
        }

        public bool TryConsumeLaneStep(out int direction)
        {
            if (_pendingAxis == 0)
            {
                direction = 0;
                return false;
            }

            direction = _pendingAxis;
            _pendingAxis = 0;
            return true;
        }

        private bool IsPointerOverUi(PointerInputBridge.PointerFrame frame)
        {
            if (!ignorePointerOverUi)
            {
                return false;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            if (frame.IsTouch)
            {
                // Multi-touch device: fingers resting on the HUD must not swipe the army.
                return eventSystem.IsPointerOverGameObject(0);
            }

            if (_eventData == null)
            {
                _eventData = new PointerEventData(eventSystem);
            }

            _eventData.position = frame.Position;
            _eventData.pressEventCamera = uiRaycastCamera != null ? uiRaycastCamera : Camera.main;
            _raycastResults.Clear();

            eventSystem.RaycastAll(_eventData, _raycastResults);
            return _raycastResults.Count > 0;
        }
    }
}
