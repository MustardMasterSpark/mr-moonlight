// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


namespace PampelGames.Shared.Utility
{
    /// <summary>
    ///     Helper script to dynamically access both the old and new Input System.
    /// </summary>
    public static class PGHybridInput
    {
        // ------------------------------
        // Keyboard
        // ------------------------------

        public static bool GetKey(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            // Handle mouse buttons
            if (key == KeyCode.Mouse0) return LeftClick;
            if (key == KeyCode.Mouse1) return RightClick;
            if (key == KeyCode.Mouse2) return MiddleClick;

            var kb = Keyboard.current;
            
            if (kb == null) return false;

            if (!TryConvertKeyCode(key, out var newKey))
                return false;

            return kb[newKey].isPressed;
#else
            return Input.GetKey(key);
#endif
        }

        public static bool GetKeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            // Handle mouse buttons
            if (key == KeyCode.Mouse0) return LeftClickDown;
            if (key == KeyCode.Mouse1) return RightClickDown;
            if (key == KeyCode.Mouse2) return MiddleClickDown;

            var kb = Keyboard.current;
            if (kb == null) return false;

            if (!TryConvertKeyCode(key, out var newKey))
                return false;

            return kb[newKey].wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }

        public static bool GetKeyUp(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            // Handle mouse buttons
            if (key == KeyCode.Mouse0) return LeftClickUp;
            if (key == KeyCode.Mouse1) return RightClickUp;
            if (key == KeyCode.Mouse2) return MiddleClickUp;

            var kb = Keyboard.current;
            if (kb == null) return false;

            if (!TryConvertKeyCode(key, out var newKey))
                return false;

            return kb[newKey].wasReleasedThisFrame;
#else
            return Input.GetKeyUp(key);
#endif
        }

        // Modifier keys (Shift, Ctrl, Alt)
        public static bool Shift
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
#else
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
            }
        }

        public static bool Ctrl
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                return kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
#else
                return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#endif
            }
        }

        public static bool Alt
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                return kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
#else
                return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
#endif
            }
        }

        // ------------------------------
        // Mouse
        // ------------------------------

        public static Vector3 MousePosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                if (mouse == null) return Vector3.zero;
                return mouse.position.ReadValue();
#else
                return Input.mousePosition;
#endif
            }
        }

        public static Vector2 MouseDelta
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                if (mouse == null) return Vector2.zero;
                return mouse.delta.ReadValue();
#else
                return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
            }
        }

        public static Vector2 MouseScroll
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                if (mouse == null) return Vector2.zero;
                return mouse.scroll.ReadValue();
#else
                return new Vector2(0f, Input.mouseScrollDelta.y);
#endif
            }
        }

        // ------------------------------
        // Mouse buttons
        // ------------------------------

        public static bool LeftClickDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        public static bool LeftClick
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.isPressed;
#else
                return Input.GetMouseButton(0);
#endif
            }
        }

        public static bool LeftClickUp
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.wasReleasedThisFrame;
#else
                return Input.GetMouseButtonUp(0);
#endif
            }
        }

        public static bool RightClickDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null && mouse.rightButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(1);
#endif
            }
        }

        public static bool RightClick
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null && mouse.rightButton.isPressed;
#else
                return Input.GetMouseButton(1);
#endif
            }
        }

        public static bool RightClickUp
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null && mouse.rightButton.wasReleasedThisFrame;
#else
                return Input.GetMouseButtonUp(1);
#endif
            }
        }

        public static bool MiddleClickDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null && mouse.middleButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(2);
#endif
            }
        }

        public static bool MiddleClick
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null && mouse.middleButton.isPressed;
#else
                return Input.GetMouseButton(2);
#endif
            }
        }

        public static bool MiddleClickUp
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null && mouse.middleButton.wasReleasedThisFrame;
#else
                return Input.GetMouseButtonUp(2);
#endif
            }
        }

        // ------------------------------
        // Helper methods
        // ------------------------------

        public static Ray MouseRay(Camera cam = null)
        {
            if (cam == null)
                cam = Camera.main;

            return cam.ScreenPointToRay(MousePosition);
        }

#if ENABLE_INPUT_SYSTEM
        // ------------------------------
        // Internal KeyCode -> Key mapping
        // ------------------------------

        private static bool TryConvertKeyCode(KeyCode keyCode, out Key key)
        {
            switch (keyCode)
            {
                // Letters
                case KeyCode.A:
                    key = Key.A;
                    return true;
                case KeyCode.B:
                    key = Key.B;
                    return true;
                case KeyCode.C:
                    key = Key.C;
                    return true;
                case KeyCode.D:
                    key = Key.D;
                    return true;
                case KeyCode.E:
                    key = Key.E;
                    return true;
                case KeyCode.F:
                    key = Key.F;
                    return true;
                case KeyCode.G:
                    key = Key.G;
                    return true;
                case KeyCode.H:
                    key = Key.H;
                    return true;
                case KeyCode.I:
                    key = Key.I;
                    return true;
                case KeyCode.J:
                    key = Key.J;
                    return true;
                case KeyCode.K:
                    key = Key.K;
                    return true;
                case KeyCode.L:
                    key = Key.L;
                    return true;
                case KeyCode.M:
                    key = Key.M;
                    return true;
                case KeyCode.N:
                    key = Key.N;
                    return true;
                case KeyCode.O:
                    key = Key.O;
                    return true;
                case KeyCode.P:
                    key = Key.P;
                    return true;
                case KeyCode.Q:
                    key = Key.Q;
                    return true;
                case KeyCode.R:
                    key = Key.R;
                    return true;
                case KeyCode.S:
                    key = Key.S;
                    return true;
                case KeyCode.T:
                    key = Key.T;
                    return true;
                case KeyCode.U:
                    key = Key.U;
                    return true;
                case KeyCode.V:
                    key = Key.V;
                    return true;
                case KeyCode.W:
                    key = Key.W;
                    return true;
                case KeyCode.X:
                    key = Key.X;
                    return true;
                case KeyCode.Y:
                    key = Key.Y;
                    return true;
                case KeyCode.Z:
                    key = Key.Z;
                    return true;

                // Numbers
                case KeyCode.Alpha0:
                    key = Key.Digit0;
                    return true;
                case KeyCode.Alpha1:
                    key = Key.Digit1;
                    return true;
                case KeyCode.Alpha2:
                    key = Key.Digit2;
                    return true;
                case KeyCode.Alpha3:
                    key = Key.Digit3;
                    return true;
                case KeyCode.Alpha4:
                    key = Key.Digit4;
                    return true;
                case KeyCode.Alpha5:
                    key = Key.Digit5;
                    return true;
                case KeyCode.Alpha6:
                    key = Key.Digit6;
                    return true;
                case KeyCode.Alpha7:
                    key = Key.Digit7;
                    return true;
                case KeyCode.Alpha8:
                    key = Key.Digit8;
                    return true;
                case KeyCode.Alpha9:
                    key = Key.Digit9;
                    return true;

                // Arrows
                case KeyCode.UpArrow:
                    key = Key.UpArrow;
                    return true;
                case KeyCode.DownArrow:
                    key = Key.DownArrow;
                    return true;
                case KeyCode.LeftArrow:
                    key = Key.LeftArrow;
                    return true;
                case KeyCode.RightArrow:
                    key = Key.RightArrow;
                    return true;

                // Space / Enter / Backspace / Tab / Escape
                case KeyCode.Space:
                    key = Key.Space;
                    return true;
                case KeyCode.Return:
                    key = Key.Enter;
                    return true;
                case KeyCode.Backspace:
                    key = Key.Backspace;
                    return true;
                case KeyCode.Tab:
                    key = Key.Tab;
                    return true;
                case KeyCode.Escape:
                    key = Key.Escape;
                    return true;

                // Modifiers
                case KeyCode.LeftShift:
                    key = Key.LeftShift;
                    return true;
                case KeyCode.RightShift:
                    key = Key.RightShift;
                    return true;
                case KeyCode.LeftControl:
                    key = Key.LeftCtrl;
                    return true;
                case KeyCode.RightControl:
                    key = Key.RightCtrl;
                    return true;
                case KeyCode.LeftAlt:
                    key = Key.LeftAlt;
                    return true;
                case KeyCode.RightAlt:
                    key = Key.RightAlt;
                    return true;

                default:
                    key = Key.None;
                    return false;
            }
        }
#endif
    }
}