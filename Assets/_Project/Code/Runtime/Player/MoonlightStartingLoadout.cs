using System.Collections.Generic;
using PolymindGames;
using PolymindGames.InventorySystem;
using UnityEngine;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Puts Tracey's test weapons in the holster at spawn (MRM-9). Replaces
    /// <c>BurntwaxStartingLoadout</c>.
    ///
    /// <para>Carlos, on the HQ FPS swap: <i>"For purposes of testing right now we would always have
    /// at our disposal four weapons: the double barrel shotgun, the pistol, the crossbow, the
    /// baseball bat."</i> Extended 2026-09-04 (MRM-25) to the full thirteen-weapon testing arsenal:
    /// <i>"For now we will be able to have all the weapons at once."</i></para>
    ///
    /// <para><b>Order matters, and it is grouped by category.</b>
    /// <see cref="MoonlightWeaponCategorySwitcher"/> resolves its number keys by looking item names
    /// up in the live holster rather than by index, so this list can be re-ordered safely — but
    /// keeping it grouped means the gamepad's next-weapon cycle
    /// (<see cref="MoonlightWeaponCycler"/>) also walks category by category rather than jumping
    /// between a knife and a rifle. Slot 0 is the Combat Knife because Carlos asked that the player
    /// always spawn with it.</para>
    ///
    /// <para><b>This is a testing scaffold, not the shipping loadout.</b> Once MRM-26's weapon
    /// pickups are live, weapons should arrive by being picked up and this component comes off the
    /// player - which is why the list is serialized rather than hardcoded, and why it fails loudly
    /// instead of silently equipping nothing. See Docs/mrm25-weapon-test-arsenal.md for how to
    /// restore this mode after it is switched off.</para>
    ///
    /// Owner: MRM-9, extended by MRM-25.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Player/Moonlight Starting Loadout")]
    [DefaultExecutionOrder(200)]
    public sealed class MoonlightStartingLoadout : MonoBehaviour
    {
        [Tooltip("Item definition names, grouped by the number-key category that selects them.")]
        [SerializeField]
        private List<string> weapons = new List<string>
        {
            // Key 1 - melee. The Club is the BaseballBat asset (Docs/glossary.md, ruled 2026-08-28).
            "Combat Knife",
            "Fire Axe",
            "Baseball Bat",
            // Key 2 - pistols.
            "M1911",
            "Revolver",
            // Key 3 - shotguns.
            "R870",
            "Double Barrel Shotgun",
            // Key 4 - rifles.
            "M1A",
            "AKM",
            // Key 5 - precision.
            "Crossbow",
            "Hunting Rifle",
            // Key 7 / G - throwables.
            "Frag Grenade",
            "Molotov Cocktail",
            // Not on a number key. Reached with the Heal key (H), which drives
            // WieldableHealingHandler.TryHeal() - see Docs/mrm25-weapon-test-arsenal.md.
            "Syringe",
        };

        [Tooltip("Item definition names that go in with a stack larger than 1, and how large. "
                 + "Throwables are consumed per throw, so they need a stack to draw from.")]
        [SerializeField]
        private List<StackedItem> stackedItems = new List<StackedItem>
        {
            new StackedItem { Name = "Frag Grenade", Count = 99 },
            new StackedItem { Name = "Molotov Cocktail", Count = 99 },
            new StackedItem { Name = "Syringe", Count = 5 },
        };

        [Tooltip("Name of the container these go into. PolymindGames' player prefab calls it Holster.")]
        [SerializeField] private string containerName = "Holster";

        [Tooltip("Which slot to equip once the loadout is in. -1 leaves Tracey empty-handed. "
                 + "0 is the Combat Knife, which Carlos asked to always spawn with.")]
        [SerializeField, Range(-1, 15)] private int startingSlot = 0;

        /// <summary>An item that goes into the holster with a stack bigger than one.</summary>
        [System.Serializable]
        public sealed class StackedItem
        {
            public string Name;
            [Range(1, 999)] public int Count = 1;
        }

        private void Start()
        {
            var character = GetComponentInParent<ICharacter>() ?? GetComponent<ICharacter>();
            if (character == null || character.Inventory == null)
            {
                Debug.LogError("[MRM-9] MoonlightStartingLoadout found no character inventory — Tracey will spawn unarmed.", this);
                return;
            }

            IItemContainer holster = null;
            foreach (IItemContainer c in character.Inventory.Containers)
            {
                if (c.Name == containerName)
                {
                    holster = c;
                    break;
                }
            }

            if (holster == null)
            {
                Debug.LogError("[MRM-9] No inventory container named '" + containerName + "' — Tracey will spawn unarmed.", this);
                return;
            }

            int added = 0;
            foreach (string weaponName in weapons)
            {
                ItemDefinition def;
                if (!ItemDefinition.TryGetWithName(weaponName, out def))
                {
                    Debug.LogError("[MRM-9] No item definition named '" + weaponName + "'. Check the names in "
                                   + "Assets/_Project/Data/PolymindGames/Resources/Definitions/Item.", this);
                    continue;
                }

                var result = holster.AddItem(new ItemStack(new Item(def), StackCountFor(weaponName)));
                if (result.addedCount > 0)
                {
                    added++;
                }
                else
                {
                    Debug.LogWarning("[MRM-9] Holster refused '" + weaponName + "': " + result.rejectReason, this);
                }
            }

            if (added == 0)
            {
                return;
            }

            if (added < weapons.Count)
            {
                Debug.LogWarning("[MRM-9] Holster took " + added + " of " + weapons.Count
                                 + " items. Raise the Holster container's Max Slot Count on the "
                                 + "player's Inventory component if this is a capacity problem.", this);
            }

            // Equip the first weapon so the player starts with something in hand rather than having
            // to press the cycle button once before anything appears.
            var selection = character.GetCC<IWieldableInventoryCC>();
            if (selection != null && startingSlot >= 0)
            {
                selection.SelectAtIndex(startingSlot);
            }
        }

        /// <summary>
        /// How many of <paramref name="weaponName"/> to put in the holster. One unless
        /// <see cref="stackedItems"/> says otherwise — throwables are consumed per throw, so they
        /// need a stack to draw from (see <see cref="MoonlightInfiniteThrowables"/>).
        /// </summary>
        private int StackCountFor(string weaponName)
        {
            foreach (StackedItem stacked in stackedItems)
            {
                if (stacked != null && stacked.Name == weaponName)
                {
                    return Mathf.Max(1, stacked.Count);
                }
            }

            return 1;
        }
    }
}
