using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MrMoonlight.UI
{
    /// <summary>Which device the player is currently driving menu UI with.</summary>
    public enum MenuInputScheme
    {
        KeyboardAndMouse,
        Gamepad
    }

    /// <summary>
    /// Switches a menu between two input schemes based on whether a gamepad is connected (MRM-18,
    /// 2026-09-08 — Carlos: buttons should not auto-highlight for mouse users, but a gamepad should
    /// get D-pad navigation with the first item pre-selected). General-purpose, not main-menu-only
    /// - drop it on any screen with <see cref="Selectable"/>s and point <see cref="firstSelected"/>
    /// at whichever one should get focus first.
    ///
    /// <para><b>Keyboard/mouse scheme never carries an EventSystem selection.</b> This is the fix
    /// for two symptoms that are actually the same root cause: (1) a button staying highlighted
    /// yellow forever even while the mouse hovers a different button - that persistent tint is
    /// Selectable's "Selected" colour state, which sticks to whatever the EventSystem last
    /// selected until something deselects it, entirely independent of mouse hover (hover only
    /// drives the separate "Highlighted" state); (2) arrow keys/WASD silently navigating the menu
    /// - Unity's UI Move action can only move focus *away from* a currently-selected object, so
    /// with nothing ever selected, arrow/WASD navigation has nothing to act on and does nothing,
    /// with no need to disable the Move action itself. <see cref="LateUpdate"/> enforces this every
    /// frame, so it self-corrects the same frame regardless of what else selected something (a
    /// menu's own reveal-selection call, a controller unplugged mid-navigation, etc.) - no other
    /// script needs to know which scheme is active.</para>
    ///
    /// <para><b>Gamepad scheme</b> selects <see cref="firstSelected"/> the instant it's entered
    /// (already connected on enable, or connected later) so the D-pad/stick has something to move
    /// from immediately - EventSystem starts every scene with no selection at all.</para>
    /// </summary>
    public sealed class MenuInputSchemeController : MonoBehaviour
    {
        [Tooltip("Selected automatically the instant gamepad scheme is entered.")]
        [SerializeField] private Selectable firstSelected;

        private MenuInputScheme _scheme;

        /// <summary>Raised whenever the active scheme changes.</summary>
        public event Action<MenuInputScheme> OnSchemeChanged;

        public MenuInputScheme Scheme => _scheme;

        private void OnEnable()
        {
            InputSystem.onDeviceChange += HandleDeviceChange;
            _scheme = Gamepad.current != null ? MenuInputScheme.Gamepad : MenuInputScheme.KeyboardAndMouse;
            ApplySchemeState();
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= HandleDeviceChange;
        }

        private void LateUpdate()
        {
            if (_scheme == MenuInputScheme.KeyboardAndMouse
                && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        /// <summary>
        /// Selects <paramref name="target"/> only in Gamepad scheme; a no-op in Keyboard &amp;
        /// Mouse scheme. Lets a menu's own reveal/panel-transition code call this unconditionally
        /// (e.g. "select the first button once the reveal finishes") without needing to check the
        /// scheme itself - and, unlike relying on <see cref="LateUpdate"/> to clean up an
        /// unconditional <c>SetSelectedGameObject</c> call after the fact, this never lets the
        /// selection happen in the first place, so <see cref="ISelectHandler"/> listeners (like
        /// <see cref="ButtonDescriptionDisplay"/>) never fire from it in Keyboard &amp; Mouse
        /// scheme either.
        /// </summary>
        public void SelectIfGamepad(Selectable target)
        {
            if (_scheme != MenuInputScheme.Gamepad || target == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad)) return;

            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                    SetScheme(MenuInputScheme.Gamepad);
                    break;
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                    if (Gamepad.current == null) SetScheme(MenuInputScheme.KeyboardAndMouse);
                    break;
            }
        }

        private void SetScheme(MenuInputScheme scheme)
        {
            if (_scheme == scheme) return;
            _scheme = scheme;
            ApplySchemeState();
            OnSchemeChanged?.Invoke(_scheme);
        }

        private void ApplySchemeState()
        {
            if (EventSystem.current == null) return;

            if (_scheme == MenuInputScheme.Gamepad && firstSelected != null)
            {
                EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}
