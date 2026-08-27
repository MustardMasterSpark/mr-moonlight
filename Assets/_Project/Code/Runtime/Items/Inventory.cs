using System;
using System.Collections.Generic;
using MrMoonlight.Data;
using MrMoonlight.Player;
using UnityEngine;

namespace MrMoonlight.Items
{
    /// <summary>One stored item type and how many of it Tracey is carrying.</summary>
    public readonly struct InventoryEntry
    {
        public readonly ItemDefinition Definition;
        public readonly int Quantity;

        public InventoryEntry(ItemDefinition definition, int quantity)
        {
            Definition = definition;
            Quantity = quantity;
        }
    }

    /// <summary>
    /// Tracey's item storage (MRM-41). Counts distinct item types against a capacity - pocket (4)
    /// before the cabin event, backpack (10) after - never total item count, so ammo of any
    /// quantity is one type. Sits on the player; <see cref="Item"/> reaches this via the interactor
    /// GameObject MRM-16's <see cref="Interaction.Interactable.OnInteracted"/> passes through, not a
    /// scene Find. Owner: MRM-41
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public sealed class Inventory : MonoBehaviour
    {
        [Header("Inventory — MRM-41")]

        /// <summary>Raised true by the cabin event (Event Director, not yet built) once Tracey has the backpack, which raises the type cap from 4 to 10 and switches MRM-42's open animation. Owner: MRM-41</summary>
        [SerializeField] private bool hasBackpack;

        private PlayerStats _stats;
        private readonly List<InventoryEntry> _entries = new List<InventoryEntry>();

        public bool HasBackpack => hasBackpack;
        public int Capacity => hasBackpack ? Tunables.I.BackpackStorageCap : Tunables.I.PocketStorageCap;
        public IReadOnlyList<InventoryEntry> Entries => _entries;

        /// <summary>Raised whenever an item is added, its quantity changes, or it's fully consumed. Owner: MRM-41</summary>
        public event Action OnInventoryChanged;

        /// <summary>Raised when a pickup is refused because every storage slot is full - the "5th type refused with clear feedback" AC. Owner: MRM-41</summary>
        public event Action<ItemDefinition> OnPickupRefused;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
        }

        /// <summary>Called by the cabin event to raise the cap from 4 to 10. Owner: MRM-41</summary>
        public void UnlockBackpack()
        {
            hasBackpack = true;
        }

        public bool TryGetQuantity(ItemDefinition definition, out int quantity)
        {
            int index = _entries.FindIndex(e => e.Definition == definition);
            quantity = index >= 0 ? _entries[index].Quantity : 0;
            return index >= 0;
        }

        /// <summary>
        /// Adds one of <paramref name="definition"/>. An existing stack (same type, any quantity -
        /// ammo included) just increments and always succeeds. A brand-new type is refused once
        /// <see cref="Capacity"/> is already full - <see cref="OnPickupRefused"/> fires so the
        /// refusal has somewhere to surface feedback, and the prop stays in the world (the caller,
        /// <see cref="Item"/>, only destroys it on a true return).
        /// </summary>
        public bool TryAddItem(ItemDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            int index = _entries.FindIndex(e => e.Definition == definition);
            if (index >= 0)
            {
                InventoryEntry existing = _entries[index];
                _entries[index] = new InventoryEntry(existing.Definition, existing.Quantity + 1);
                OnInventoryChanged?.Invoke();
                return true;
            }

            if (_entries.Count >= Capacity)
            {
                OnPickupRefused?.Invoke(definition);
                return false;
            }

            _entries.Add(new InventoryEntry(definition, 1));
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Applies the item's effect (<see cref="ItemEffectApplier"/>) and, only if it actually did
        /// something, decrements its quantity - removing the entry once it hits zero. Items whose
        /// effect is a no-op (equipment, ammo) are left untouched, so MRM-42's A-to-use can call
        /// this unconditionally without needing to know which items are consumable. Owner: MRM-41
        /// </summary>
        public void UseItem(ItemDefinition definition)
        {
            int index = _entries.FindIndex(e => e.Definition == definition);
            if (index < 0)
            {
                return;
            }

            bool consumed = ItemEffectApplier.Apply(definition.Id, _stats);
            if (!consumed)
            {
                return;
            }

            InventoryEntry entry = _entries[index];
            int remaining = entry.Quantity - 1;
            if (remaining <= 0)
            {
                _entries.RemoveAt(index);
            }
            else
            {
                _entries[index] = new InventoryEntry(entry.Definition, remaining);
            }

            OnInventoryChanged?.Invoke();
        }
    }
}
