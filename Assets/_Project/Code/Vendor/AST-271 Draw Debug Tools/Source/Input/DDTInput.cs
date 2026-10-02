using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && DDT_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace DDT
{
    /// <summary>
    /// Reads debug camera input through whichever input backend the project has enabled.
    /// Keys are configured as <see cref="KeyCode"/> in <see cref="DDTSettings"/> and mapped to
    /// the Input System's <c>Key</c> when that is the active backend.
    /// </summary>
    internal static class DDTInput
    {
        // Matches the sensitivity the legacy Mouse X and Mouse Y axes apply
        private const float LegacyMouseSensitivity = 0.1f;

        // The Input System reports this many scroll units per wheel notch
        private const float ScrollUnitsPerNotch = 120.0f;

        public static bool GetKeyDown(KeyCode Key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(Key);
#elif ENABLE_INPUT_SYSTEM && DDT_INPUT_SYSTEM
            ButtonControl Control = ResolveKey(Key);
            return Control != null && Control.wasPressedThisFrame;
#else
            return false;
#endif
        }

        public static bool GetKey(KeyCode Key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(Key);
#elif ENABLE_INPUT_SYSTEM && DDT_INPUT_SYSTEM
            ButtonControl Control = ResolveKey(Key);
            return Control != null && Control.isPressed;
#else
            return false;
#endif
        }

        /// <summary>Movement input where x is right and y is forward, each in the range -1 to 1.</summary>
        public static Vector2 GetMoveAxes()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#elif ENABLE_INPUT_SYSTEM && DDT_INPUT_SYSTEM
            Keyboard Board = Keyboard.current;
            if (Board == null)
                return Vector2.zero;

            float Right = Held(Board.dKey) + Held(Board.rightArrowKey) - Held(Board.aKey) - Held(Board.leftArrowKey);
            float Forward = Held(Board.wKey) + Held(Board.upArrowKey) - Held(Board.sKey) - Held(Board.downArrowKey);
            return new Vector2(Mathf.Clamp(Right, -1.0f, 1.0f), Mathf.Clamp(Forward, -1.0f, 1.0f));
#else
            return Vector2.zero;
#endif
        }

        /// <summary>Look input in the same units as the legacy "Mouse X" and "Mouse Y" axes.</summary>
        public static Vector2 GetLookDelta()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#elif ENABLE_INPUT_SYSTEM && DDT_INPUT_SYSTEM
            Mouse Pointer = Mouse.current;
            if (Pointer == null)
                return Vector2.zero;

            return Pointer.delta.ReadValue() * LegacyMouseSensitivity;
#else
            return Vector2.zero;
#endif
        }

        public static float GetScrollDelta()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#elif ENABLE_INPUT_SYSTEM && DDT_INPUT_SYSTEM
            Mouse Pointer = Mouse.current;
            if (Pointer == null)
                return 0.0f;

            return Pointer.scroll.ReadValue().y / ScrollUnitsPerNotch;
#else
            return 0.0f;
#endif
        }

        /// <summary>True while the button that enables look rotation is held.</summary>
        public static bool GetLookButton()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(0);
#elif ENABLE_INPUT_SYSTEM && DDT_INPUT_SYSTEM
            Mouse Pointer = Mouse.current;
            return Pointer != null && Pointer.leftButton.isPressed;
#else
            return false;
#endif
        }

#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM && DDT_INPUT_SYSTEM
        static float Held(ButtonControl Control)
        {
            return Control != null && Control.isPressed ? 1.0f : 0.0f;
        }

        /// <summary>
        /// KeyCode names that differ from their Input System counterparts. Names that match,
        /// such as the function keys and letters, resolve without an entry here.
        /// </summary>
        static readonly Dictionary<KeyCode, Key> KeyNameOverrides = new Dictionary<KeyCode, Key>
        {
            { KeyCode.Return, Key.Enter },
            { KeyCode.Alpha0, Key.Digit0 }, { KeyCode.Alpha1, Key.Digit1 }, { KeyCode.Alpha2, Key.Digit2 },
            { KeyCode.Alpha3, Key.Digit3 }, { KeyCode.Alpha4, Key.Digit4 }, { KeyCode.Alpha5, Key.Digit5 },
            { KeyCode.Alpha6, Key.Digit6 }, { KeyCode.Alpha7, Key.Digit7 }, { KeyCode.Alpha8, Key.Digit8 },
            { KeyCode.Alpha9, Key.Digit9 },
            { KeyCode.KeypadEnter, Key.NumpadEnter }, { KeyCode.KeypadDivide, Key.NumpadDivide },
            { KeyCode.KeypadMultiply, Key.NumpadMultiply }, { KeyCode.KeypadMinus, Key.NumpadMinus },
            { KeyCode.KeypadPlus, Key.NumpadPlus }, { KeyCode.KeypadPeriod, Key.NumpadPeriod },
            { KeyCode.UpArrow, Key.UpArrow }, { KeyCode.DownArrow, Key.DownArrow },
            { KeyCode.LeftArrow, Key.LeftArrow }, { KeyCode.RightArrow, Key.RightArrow },
            { KeyCode.BackQuote, Key.Backquote }, { KeyCode.Print, Key.PrintScreen },
        };

        static readonly Dictionary<KeyCode, ButtonControl> ResolvedKeys = new Dictionary<KeyCode, ButtonControl>();
        static Keyboard CachedKeyboard;

        static ButtonControl ResolveKey(KeyCode Code)
        {
            Keyboard Board = Keyboard.current;
            if (Board == null)
                return null;

            if (!ReferenceEquals(Board, CachedKeyboard))
            {
                ResolvedKeys.Clear();
                CachedKeyboard = Board;
            }

            ButtonControl Cached;
            if (ResolvedKeys.TryGetValue(Code, out Cached))
                return Cached;

            Key Mapped;
            if (!KeyNameOverrides.TryGetValue(Code, out Mapped)
                && !System.Enum.TryParse(Code.ToString(), false, out Mapped))
            {
                Debug.LogWarning($"[DDT] {Code} has no Input System equivalent. Pick a different key in DDTSettings.");
                ResolvedKeys[Code] = null;
                return null;
            }

            Cached = Board[Mapped];
            ResolvedKeys[Code] = Cached;
            return Cached;
        }
#endif
    }
}
