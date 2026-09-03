using System;
using MrMoonlight.Input;
using MrMoonlight.Items;
using MrMoonlight.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.UI
{
    /// <summary>
    /// MRM-42's open/close/navigate/use state machine, deliberately built without any layout.
    /// The issue's own text blocks visual work until Carlos attaches a mockup ("Do not guess the
    /// layout") - see Docs/mrm41-items-interaction-kickoff.md - but the mechanics underneath don't
    /// depend on that layout, so they're built and testable now:
    ///
    /// <para>Opens on <c>InventoryScroll</c>'s first read away from zero while closed (D-pad
    /// left/right, <c>[</c>/<c>]</c>, or the mouse wheel, per MRM-8), and edge-detects that read
    /// itself (comparing this frame's value against last frame's) rather than trusting
    /// <c>WasPerformedThisFrame</c> - a continuous Value action like a scroll wheel can fire
    /// "performed" on every sub-step of a spin, and the open/step distinction is explicitly this
    /// issue's job to define, not MRM-8's (see the MRM-8 comment on MRM-42). The same edge, once
    /// open, steps the selection by one.</para>
    ///
    /// <para>Does <b>not</b> pause and does <b>not</b> switch input maps - confirmed while wiring
    /// MRM-8 that Open/Navigate/Use/Close (<c>InventoryScroll</c>, <c>Jump</c>, <c>EquipMelee</c>)
    /// all live in the <c>Gameplay</c> map already, so switching to <c>UI</c> would strip exactly
    /// the controls the open panel needs. "Locked in place" instead comes from
    /// <see cref="MoonlightPlayerRig.SetMovementLocked"/>, reversible unlike death's
    /// <see cref="MoonlightPlayerRig.DisableControl"/> - nothing here touches the damage path, so
    /// Tracey stays fully attackable while open, which is the point per the issue.</para>
    ///
    /// <para>Blocked while any non-Gameplay map (cutscene, turret, stretcher) is active - reads
    /// <see cref="InputMapController.CurrentMode"/> directly rather than a separately-set flag, so
    /// there's nothing else to remember to wire when a cutscene starts. Force-closes on
    /// <see cref="HudCloseRequest.OnForceCloseAll"/>, which MRM-17's death sequence already raises -
    /// "dying with it open closes it cleanly" falls out of that existing hook for free.</para>
    ///
    /// <para>Raises <see cref="OnOpened"/>/<see cref="OnClosed"/>/<see cref="OnSelectionChanged"/>
    /// for whatever view eventually renders the spinning items and their animations - that view
    /// doesn't exist yet. This is the model half of MRM-42; the view half is blocked on Carlos.</para>
    /// Owner: MRM-42
    /// </summary>
    // MRM-9: no [RequireComponent(typeof(MoonlightPlayerRig))]. The rig lives on the player
    // ROOT, next to PolymindGames' character, while this component sits further down the
    // hierarchy - RequireComponent would force a second, non-functional rig onto whatever
    // GameObject this is on, which is exactly the duplication the swap was meant to remove.
    [RequireComponent(typeof(Inventory))]
    public sealed class InventoryUIController : MonoBehaviour
    {
        private MoonlightPlayerRig _playerController;
        private Inventory _inventory;
        private float _previousScrollValue;

        public bool IsOpen { get; private set; }
        public int SelectedIndex { get; private set; }

        public event Action OnOpened;
        public event Action OnClosed;
        public event Action OnSelectionChanged;

        private void Awake()
        {
            _playerController = GetComponentInParent<MoonlightPlayerRig>();
            _inventory = GetComponent<Inventory>();
        }

        private void OnEnable() => HudCloseRequest.OnForceCloseAll += HandleForceClose;

        private void OnDisable() => HudCloseRequest.OnForceCloseAll -= HandleForceClose;

        private void Update()
        {
            InputAction scroll = _playerController.Input.Actions.Gameplay.InventoryScroll;
            float scrollValue = scroll.ReadValue<float>();
            bool crossedFromZero = Mathf.Approximately(_previousScrollValue, 0f) && !Mathf.Approximately(scrollValue, 0f);
            _previousScrollValue = scrollValue;

            if (!IsOpen)
            {
                bool gameplayActive = _playerController.Input.CurrentMode == InputMode.Gameplay;
                if (gameplayActive && crossedFromZero)
                {
                    Open();
                }
                return;
            }

            if (crossedFromZero)
            {
                Navigate(scrollValue > 0f ? 1 : -1);
            }

            if (_playerController.Input.Actions.Gameplay.Jump.WasPerformedThisFrame())
            {
                UseSelected();
            }

            if (_playerController.Input.Actions.Gameplay.EquipMelee.WasPerformedThisFrame())
            {
                Close();
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            SelectedIndex = 0;
            _playerController.SetMovementLocked(true);
            OnOpened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            _playerController.SetMovementLocked(false);
            OnClosed?.Invoke();
        }

        private void Navigate(int direction)
        {
            int count = _inventory.Entries.Count;
            if (count == 0)
            {
                return;
            }

            SelectedIndex = (SelectedIndex + direction + count) % count;
            OnSelectionChanged?.Invoke();
        }

        private void UseSelected()
        {
            if (_inventory.Entries.Count == 0 || SelectedIndex >= _inventory.Entries.Count)
            {
                return;
            }

            _inventory.UseItem(_inventory.Entries[SelectedIndex].Definition);
        }

        private void HandleForceClose()
        {
            if (IsOpen)
            {
                Close();
            }
        }
    }
}
