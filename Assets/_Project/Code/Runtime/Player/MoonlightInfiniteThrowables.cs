using System.Collections.Generic;
using PolymindGames;
using PolymindGames.InventorySystem;
using UnityEngine;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Keeps the grenade and molotov stacks topped up while the testing arsenal is active (MRM-25).
    ///
    /// <para>Carlos, 2026-09-04: <i>"In the case of grenades once you throw one grenade, you will
    /// spawn another one of the same type."</i></para>
    ///
    /// <para><b>Why this exists at all.</b> Firearms get infinite ammo for free — the reserve is a
    /// pluggable provider, and <c>PolymindPlayerBuild</c> swaps every firearm's
    /// <c>FirearmInventoryAmmoProvider</c> for <c>FirearmInfiniteAmmoProvider</c>. Throwables have
    /// no such seam: <c>MeleeThrowAttack.ConsumeItemFromInventory</c> calls
    /// <c>inventorySlot.AdjustStack(-1)</c> directly against the holster slot the throwable is
    /// sitting in. So the only non-invasive way to make them infinite is to put the count back,
    /// which is what this does — no vendor code is modified.</para>
    ///
    /// <para><b>Why a full stack and not a refill-of-one.</b> <c>MeleeThrowAttack</c> takes a
    /// different branch on the *last* item in a stack (it can link the real item to the thrown
    /// projectile so it can be picked back up, and the wieldable gets holstered as the slot
    /// empties). Keeping the slot full keeps throwing on the ordinary path, so the weapon stays in
    /// hand and the player can keep throwing without re-selecting it.</para>
    ///
    /// <para><b>The ceiling is the item definition's, not ours.</b> HQ FPS gives both throwables
    /// <c>StackSize = 3</c>, and a container will not hold more than that however large a number
    /// this component asks for — measured live on 2026-09-04, when a request for 99 produced a
    /// stack of 3. So the target is clamped to the definition's own stack size and the refill is
    /// immediate, which is what actually delivers "throw one, get another". If a bigger visible
    /// count is ever wanted, that is one field on
    /// <c>Resources/Definitions/Item/HQFPS_Frag Grenade.asset</c>, not a change here.</para>
    ///
    /// <para><b>Event-driven, not polled.</b> It listens to the container's own
    /// <c>SlotChanged</c> and tops the stack back up in the same frame the throw consumed it, so
    /// there is no window in which the player is holding a throwable that is about to vanish. The
    /// refill re-raises <c>SlotChanged</c>, which re-enters this once and finds the stack already
    /// full — one level deep, then it stops.</para>
    ///
    /// <para><b>Testing scaffold.</b> Comes off with the rest of the arsenal once MRM-26's pickups
    /// are live. See Docs/mrm25-weapon-test-arsenal.md.</para>
    ///
    /// Owner: MRM-25.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Player/Moonlight Infinite Throwables")]
    [DefaultExecutionOrder(310)]
    public sealed class MoonlightInfiniteThrowables : MonoBehaviour
    {
        [Tooltip("Item definition names to keep stocked.")]
        [SerializeField]
        private List<string> throwables = new List<string>
        {
            "Frag Grenade",
            "Molotov Cocktail",
        };

        [Tooltip("Stack size to hold each throwable at. Clamped down to the item definition's own "
                 + "StackSize, which is 3 for both HQ FPS throwables — a container will not hold "
                 + "more than that however large a number this asks for.")]
        [SerializeField, Range(2, 999)] private int stockCount = 99;

        [Tooltip("Name of the wieldable container. PolymindGames' player prefab calls it Holster.")]
        [SerializeField] private string containerName = "Holster";

        private readonly List<int> _slots = new List<int>();
        private IItemContainer _holster;
        private bool _refilling;

        private void Start()
        {
            ICharacter character = GetComponentInParent<ICharacter>() ?? GetComponent<ICharacter>();
            if (character == null || character.Inventory == null)
            {
                Debug.LogError("[MRM-25] No character inventory — throwables will run out.", this);
                enabled = false;
                return;
            }

            foreach (IItemContainer c in character.Inventory.Containers)
            {
                if (c.Name == containerName)
                {
                    _holster = c;
                    break;
                }
            }

            if (_holster == null)
            {
                Debug.LogError("[MRM-25] No inventory container named '" + containerName
                               + "' — throwables will run out.", this);
                enabled = false;
                return;
            }

            ResolveSlots();

            if (_slots.Count == 0)
            {
                // Not an error: a loadout with no throwables in it is a legitimate configuration.
                enabled = false;
                return;
            }

            _holster.SlotChanged += OnSlotChanged;
            TopUpAll();
        }

        private void OnDestroy()
        {
            if (_holster != null)
            {
                _holster.SlotChanged -= OnSlotChanged;
            }
        }

        private void OnSlotChanged(in SlotReference slot, SlotChangeType changeType)
        {
            if (_refilling || !_slots.Contains(slot.Index))
            {
                return;
            }

            TopUpAll();
        }

        /// <summary>
        /// Restores every throwable slot to its target count.
        ///
        /// <para>The <see cref="_refilling"/> guard is what stops this recursing: the
        /// <c>AdjustStack</c> below raises <c>SlotChanged</c> again, and without the guard that
        /// would re-enter here while the loop is still running.</para>
        /// </summary>
        private void TopUpAll()
        {
            _refilling = true;
            try
            {
                for (int i = 0; i < _slots.Count; i++)
                {
                    int index = _slots[i];
                    ItemStack stack = _holster.GetItemAtIndex(index);
                    if (!stack.HasItem())
                    {
                        // The slot emptied completely — the item object itself is gone, so there is
                        // no stack left to adjust. Keeping the slot topped up on every change is
                        // what prevents ever reaching this.
                        continue;
                    }

                    int target = Mathf.Min(stockCount, Mathf.Max(1, stack.Item.Definition.StackSize));
                    if (stack.Count < target)
                    {
                        _holster.GetSlot(index).AdjustStack(target - stack.Count);
                    }
                }
            }
            finally
            {
                _refilling = false;
            }
        }

        private void ResolveSlots()
        {
            _slots.Clear();
            for (int i = 0; i < _holster.SlotsCount; i++)
            {
                ItemStack stack = _holster.GetItemAtIndex(i);
                if (!stack.HasItem())
                {
                    continue;
                }

                if (throwables.Contains(stack.Item.Name))
                {
                    _slots.Add(i);
                }
            }
        }

    }
}
