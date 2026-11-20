using UnityEngine;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace NumberRush3D.Controls
{
    /// <summary>
    /// Reads the primary pointer (touch on device, mouse in the editor) and the keyboard
    /// through whichever input backend the project is configured for.
    ///
    /// Unity 6 projects can be set to "Input System Package (New)", "Input Manager (Old)" or "Both",
    /// so every access is guarded by the ENABLE_* defines Unity generates. This file is the only
    /// place in the project that talks to an input API.
    /// </summary>
    internal static class PointerInputBridge
    {
        public const int NoLaneAxis = 0;

        public readonly struct PointerFrame
        {
            public readonly Vector2 Position;
            public readonly bool PressedThisFrame;
            public readonly bool ReleasedThisFrame;
            public readonly bool Held;

            /// <summary>True when the frame comes from a real touch (used to query the UI with a finger id).</summary>
            public readonly bool IsTouch;

            public PointerFrame(Vector2 position, bool pressed, bool released, bool held, bool isTouch)
            {
                Position = position;
                PressedThisFrame = pressed;
                ReleasedThisFrame = released;
                Held = held;
                IsTouch = isTouch;
            }
        }

        /// <summary>Snapshot of the primary pointer for the current frame.</summary>
        public static PointerFrame ReadPointer()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                bool began = touch.phase == TouchPhase.Began;
                bool ended = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                bool held = touch.phase == TouchPhase.Began ||
                            touch.phase == TouchPhase.Moved ||
                            touch.phase == TouchPhase.Stationary;
                return new PointerFrame(touch.position, began, ended, held, true);
            }

            Vector2 mousePosition = Input.mousePosition;
            return new PointerFrame(
                mousePosition,
                Input.GetMouseButtonDown(0),
                Input.GetMouseButtonUp(0),
                Input.GetMouseButton(0),
                false);
#elif ENABLE_INPUT_SYSTEM
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                var primaryTouch = touchscreen.primaryTouch;
                return new PointerFrame(
                    primaryTouch.position.ReadValue(),
                    primaryTouch.press.wasPressedThisFrame,
                    primaryTouch.press.wasReleasedThisFrame,
                    primaryTouch.press.isPressed,
                    true);
            }

            Touchscreen releasedTouchscreen = Touchscreen.current;
            if (releasedTouchscreen != null && releasedTouchscreen.primaryTouch.press.wasReleasedThisFrame)
            {
                return new PointerFrame(
                    releasedTouchscreen.primaryTouch.position.ReadValue(),
                    false,
                    true,
                    false,
                    true);
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                return new PointerFrame(
                    mouse.position.ReadValue(),
                    mouse.leftButton.wasPressedThisFrame,
                    mouse.leftButton.wasReleasedThisFrame,
                    mouse.leftButton.isPressed,
                    false);
            }

            return default;
#else
            return default;
#endif
        }

        /// <summary>Keyboard lane change used for editor testing: A / Left arrow = -1, D / Right arrow = +1.</summary>
        public static int ReadKeyboardLaneAxis()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                return -1;
            }

            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                return 1;
            }

            return NoLaneAxis;
#elif ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return NoLaneAxis;
            }

            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
            {
                return -1;
            }

            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
            {
                return 1;
            }

            return NoLaneAxis;
#else
            return NoLaneAxis;
#endif
        }

        /// <summary>Restart shortcut (R / Space) used while a results panel is visible.</summary>
        public static bool ReadRestartShortcut()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#elif ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return keyboard.rKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame;
#else
            return false;
#endif
        }

        /// <summary>Pause shortcut (Escape / P), handy while testing in the editor.</summary>
        public static bool ReadPauseShortcut()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
#elif ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame;
#else
            return false;
#endif
        }
    }
}
