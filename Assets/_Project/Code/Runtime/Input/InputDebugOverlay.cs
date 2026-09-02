using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace MrMoonlight.Input
{
    /// <summary>
    /// Temporary on-screen readout of the last key, button or mouse scroll on any connected
    /// device, and which named action(s) it's bound to — so Carlos can visually confirm the
    /// Input System is capturing input and wired to the right action before a player prefab
    /// exists to react to it (see MRM-9). Drop this on any empty GameObject in a test scene; not
    /// part of the shipped game. Toggle with F1 (keyboard) or Select/View (gamepad), or via the
    /// inspector checkbox. Owner: MRM-8
    /// </summary>
    public sealed class InputDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool visible = true;

        [Tooltip("Debug overlay font, so it doesn't read as generic default-Unity text. Carlos, 2026-09-02 (MRM-76).")]
        [SerializeField] private Font font;

        private IDisposable _buttonPressListener;
        private InputSystem_Actions _lookupActions;
        private GUIStyle _style;
        private string _lastPress = "(none yet — press a key or button)";

        private void OnEnable()
        {
            // A separate, never-enabled instance purely to read binding metadata (action
            // names, owning map) for whatever control was pressed — doesn't touch or compete
            // with whichever InputSystem_Actions instance the game itself is using.
            _lookupActions = new InputSystem_Actions();
            _buttonPressListener = InputSystem.onAnyButtonPress.Call(OnButtonPressed);
            Debug.Log("[InputDebugOverlay] Started — watching for key/button presses. Press F1 or a gamepad's Select/View button to hide.");
        }

        private void OnDisable()
        {
            _buttonPressListener?.Dispose();
            _buttonPressListener = null;
            _lookupActions?.Dispose();
            _lookupActions = null;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                visible = !visible;

            if (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame)
                visible = !visible;

            CheckMouseScroll();
        }

        // Scroll doesn't fire InputSystem.onAnyButtonPress — Mouse.scroll is a Vector2Control,
        // not a ButtonControl, so onAnyButtonPress never sees it. It reports the per-frame delta
        // rather than a held state, so it naturally reads 0 when idle — polling it here for a
        // non-zero Y is the whole check, no wasPressedThisFrame equivalent needed.
        private void CheckMouseScroll()
        {
            if (Mouse.current == null)
                return;

            var scrollY = Mouse.current.scroll.y.ReadValue();
            if (Mathf.Approximately(scrollY, 0f))
                return;

            var direction = scrollY > 0f ? "Scroll Wheel up" : "Scroll Wheel down";
            _lastPress = $"{Mouse.current.displayName}: {direction} -> {FindBoundActions(Mouse.current.scroll.y)}";
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            // Legacy IMGUI, not a Canvas element — no scene wiring needed, but the default
            // skin is small black text with no backing, which reads as "nothing" against a
            // dark scene. Force a background box and bold, high-contrast text instead.
            _style ??= new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.yellow }
            };

            var rect = new Rect(10, 10, 900, 50);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, $"[MRM-8 input debug]\nLast press: {_lastPress}   (F1 / Select to hide)", _style);
        }

        private void OnButtonPressed(InputControl control)
        {
            _lastPress = $"{control.device.displayName}: {control.displayName} -> {FindBoundActions(control)}";
        }

        private string FindBoundActions(InputControl control)
        {
            var matches = new List<string>();

            foreach (var binding in _lookupActions.bindings)
            {
                if (binding.isComposite || string.IsNullOrEmpty(binding.effectivePath))
                    continue;

                if (!InputControlPath.Matches(binding.effectivePath, control))
                    continue;

                var action = _lookupActions.FindAction(binding.action);
                if (action == null)
                    continue;

                var mapName = action.actionMap != null ? action.actionMap.name : "?";
                var qualified = $"{mapName}/{action.name}";
                if (!matches.Contains(qualified))
                    matches.Add(qualified);
            }

            return matches.Count > 0 ? string.Join(", ", matches) : "(unbound)";
        }
    }
}
