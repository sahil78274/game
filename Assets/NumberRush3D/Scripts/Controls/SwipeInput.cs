using UnityEngine;

namespace NumberRush3D.Controls
{
    /// <summary>
    /// Turns horizontal swipes (or A/D and the arrow keys in the editor) into discrete lane steps.
    ///
    /// Design notes:
    /// * The component only produces "lane step" intents, it never moves anything itself,
    ///   so the same input can drive a player, a bot or a replay.
    /// * Requests are queued in a signed counter clamped to +/-2, which makes fast double swipes
    ///   feel responsive without letting the army teleport across the whole track.
    /// * Uses unscaled time so it keeps working during the game over slow motion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwipeInput : MonoBehaviour, ILaneInputSource
    {
        [Header("Swipe detection")]
        [Tooltip("Minimum horizontal travel, as a fraction of screen width, to count as a swipe.")]
        [Range(0.01f, 0.3f)] [SerializeField] private float minSwipeScreenFraction = 0.05f;

        [Tooltip("A swipe must be finished within this many seconds.")]
        [Range(0.05f, 2f)] [SerializeField] private float maxSwipeSeconds = 0.45f;

        [Tooltip("Minimum time between two accepted lane changes.")]
        [Range(0.02f, 0.5f)] [SerializeField] private float stepCooldown = 0.09f;

        [Tooltip("When enabled, holding and dragging across the screen emits a step every time the finger crosses the threshold. When disabled only a completed swipe counts (recommended).")]
        [SerializeField] private bool continuousDrag = false;

        [Header("Editor convenience")]
        [Tooltip("A / Left arrow and D / Right arrow also change lane. Harmless on device.")]
        [SerializeField] private bool keyboardFallback = true;

        private Vector2 _pressPosition;
        private Vector2 _dragAnchor;
        private float _pressTime;
        private float _lastStepTime = -10f;
        private bool _tracking;
        private int _queuedSteps;
        private float _thresholdPixels;

        /// <summary>Number of lane changes waiting to be consumed (signed).</summary>
        public int QueuedSteps => _queuedSteps;

        /// <summary>Raised every time a lane change is accepted. Optional, the army polls instead.</summary>
        public event System.Action<int> LaneStepAccepted;

        private void Awake()
        {
            RecalculateThreshold();
        }

        private void Update()
        {
            if (Screen.width != _lastScreenWidth)
            {
                RecalculateThreshold();
            }

            PointerInputBridge.PointerFrame pointer = PointerInputBridge.ReadPointer();
            float now = Time.unscaledTime;

            if (pointer.PressedThisFrame)
            {
                _tracking = true;
                _pressPosition = pointer.Position;
                _dragAnchor = pointer.Position;
                _pressTime = now;
            }

            if (_tracking && continuousDrag && pointer.Held)
            {
                float dragDelta = pointer.Position.x - _dragAnchor.x;
                if (Mathf.Abs(dragDelta) >= _thresholdPixels && now - _lastStepTime >= stepCooldown)
                {
                    EnqueueStep(dragDelta > 0f ? 1 : -1, now);
                    _dragAnchor = pointer.Position;
                }
            }

            if (_tracking && pointer.ReleasedThisFrame)
            {
                _tracking = false;

                if (!continuousDrag)
                {
                    Vector2 delta = pointer.Position - _pressPosition;
                    bool isHorizontal = Mathf.Abs(delta.x) >= _thresholdPixels &&
                                        Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.2f;
                    bool isQuick = now - _pressTime <= maxSwipeSeconds;

                    if (isHorizontal && isQuick && now - _lastStepTime >= stepCooldown)
                    {
                        EnqueueStep(delta.x > 0f ? 1 : -1, now);
                    }
                }
            }

            if (!pointer.Held && !pointer.ReleasedThisFrame)
            {
                _tracking = false;
            }

            if (keyboardFallback)
            {
                int axis = PointerInputBridge.ReadKeyboardLaneAxis();
                if (axis != 0 && now - _lastStepTime >= stepCooldown)
                {
                    EnqueueStep(axis, now);
                }
            }
        }

        public bool TryConsumeLaneStep(out int direction)
        {
            if (_queuedSteps == 0)
            {
                direction = 0;
                return false;
            }

            direction = _queuedSteps > 0 ? 1 : -1;
            _queuedSteps -= direction;
            return true;
        }

        private void EnqueueStep(int direction, float now)
        {
            if (direction == 0)
            {
                return;
            }

            _queuedSteps = Mathf.Clamp(_queuedSteps + direction, -2, 2);
            _lastStepTime = now;
            LaneStepAccepted?.Invoke(direction);
        }

        private int _lastScreenWidth;

        private void RecalculateThreshold()
        {
            _lastScreenWidth = Mathf.Max(1, Screen.width);
            _thresholdPixels = Mathf.Max(24f, _lastScreenWidth * minSwipeScreenFraction);
        }
    }
}
