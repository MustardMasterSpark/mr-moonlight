using MrMoonlight.Input;
using PolymindGames;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Next-weapon cycling on Q / right bumper (MRM-9, MRM-25).
    ///
    /// <para>HQ FPS ships direct slot selection only — number keys 1-5 bound to its <c>Select</c>
    /// action — so there is no "next weapon" behaviour to copy. Carlos asked for a single cycle
    /// button that walks the list and wraps back to the first weapon at the end, which is this.</para>
    ///
    /// <para>It drives PolymindGames' own <see cref="IWieldableInventoryCC.SelectAtIndex"/> rather
    /// than equipping wieldables itself, so equip/unequip animations, the holster state machine and
    /// the arms handler all still run exactly as the asset intends.</para>
    ///
    /// <para><b>Known limit.</b> This walks every slot up to <see cref="slotCount"/>, including
    /// empty ones — a press on an empty slot equips nothing rather than skipping ahead. That is
    /// invisible for the demo loadout, where all four slots are always filled (Carlos's testing
    /// choice), but it needs revisiting when weapons become droppable/pickup-gated under MRM-26.
    /// <see cref="IWieldableInventoryCC"/> exposes no slot-occupancy query, so skipping properly
    /// means reaching past it into the holster container.</para>
    ///
    /// Owner: MRM-9.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Player/Moonlight Weapon Cycler")]
    [RequireComponent(typeof(MoonlightPlayerRig))]
    public sealed class MoonlightWeaponCycler : MonoBehaviour
    {
        [Tooltip("How many holster slots to consider. Must match the holster container size on the character's Inventory.")]
        [SerializeField, Range(1, 10)] private int slotCount = 4;

        private MoonlightPlayerRig _rig;
        private IWieldableInventoryCC _selection;
        private InputAction _switchWeapon;

        private void Awake()
        {
            _rig = GetComponent<MoonlightPlayerRig>();
        }

        private void Start()
        {
            var character = GetComponentInParent<ICharacter>() ?? GetComponent<ICharacter>();
            if (character != null)
            {
                _selection = character.GetCC<IWieldableInventoryCC>();
            }

            if (_selection == null)
            {
                Debug.LogError("[MRM-9] MoonlightWeaponCycler found no IWieldableInventoryCC on the character — weapon switching is dead.", this);
                enabled = false;
                return;
            }

            InputMapController input = _rig != null ? _rig.Input : null;
            if (input == null)
            {
                Debug.LogError("[MRM-9] MoonlightWeaponCycler has no InputMapController — weapon switching is dead.", this);
                enabled = false;
                return;
            }

            // Already bound to Q and the gamepad right shoulder by MRM-8; nothing to rebind here.
            _switchWeapon = input.Actions.Gameplay.SwitchWeapon;
        }

        private void Update()
        {
            if (_switchWeapon == null || !_switchWeapon.WasPerformedThisFrame())
            {
                return;
            }

            SelectNext();
        }

        /// <summary>
        /// Advances one slot and wraps. <see cref="IWieldableInventoryCC.SelectedIndex"/> is -1 when
        /// nothing is equipped, so the +1 lands on slot 0 first, which is what a player expects from
        /// a cold start.
        /// </summary>
        private void SelectNext()
        {
            int next = _selection.SelectedIndex + 1;
            if (next >= slotCount || next < 0)
            {
                next = 0;
            }

            _selection.SelectAtIndex(next, false);
        }
    }
}
