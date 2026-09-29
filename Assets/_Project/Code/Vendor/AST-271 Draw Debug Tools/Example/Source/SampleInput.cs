using UnityEngine;
#if ENABLE_INPUT_SYSTEM && DDT_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DDT.Samples
{
    /// <summary>
    /// Reads the mouse and keyboard for the samples through whichever input backend the project has enabled.
    /// </summary>
    public static class SampleInput
    {
        /// <summary>Mouse position in screen pixels.</summary>
        public static Vector3 MousePosition
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePosition;
#elif ENABLE_INPUT_SYSTEM && DDT_INPUT_SYSTEM
                Mouse Pointer = Mouse.current;
                return Pointer != null ? (Vector3)Pointer.position.ReadValue() : Vector3.zero;
#else
                return Vector3.zero;
#endif
            }
        }

        /// <summary>True on the frame the left mouse button is pressed.</summary>
        public static bool LeftMouseButtonDown()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#elif ENABLE_INPUT_SYSTEM && DDT_INPUT_SYSTEM
            Mouse Pointer = Mouse.current;
            return Pointer != null && Pointer.leftButton.wasPressedThisFrame;
#else
            return false;
#endif
        }

        /// <summary>True on the frame the K key is pressed.</summary>
        public static bool KKeyDown()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.K);
#elif ENABLE_INPUT_SYSTEM && DDT_INPUT_SYSTEM
            Keyboard Board = Keyboard.current;
            return Board != null && Board.kKey.wasPressedThisFrame;
#else
            return false;
#endif
        }
    }
}
