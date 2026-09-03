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
    /// baseball bat."</i> Slot order here is the cycle order Q / right bumper walks, and it wraps
    /// back to the first weapon at the end.</para>
    ///
    /// <para><b>This is a testing scaffold, not the shipping loadout.</b> Once MRM-26's weapon
    /// pickups are live, weapons should arrive by being picked up and this component comes off the
    /// player - which is why the list is serialized rather than hardcoded, and why it fails loudly
    /// instead of silently equipping nothing.</para>
    ///
    /// Owner: MRM-9.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Player/Moonlight Starting Loadout")]
    [DefaultExecutionOrder(200)]
    public sealed class MoonlightStartingLoadout : MonoBehaviour
    {
        [Tooltip("Item definition names, in the order Q / right bumper cycles through them.")]
        [SerializeField]
        private List<string> weapons = new List<string>
        {
            "Double Barrel Shotgun",
            "M1911",
            "Crossbow",
            "Baseball Bat",
        };

        [Tooltip("Name of the container these go into. PolymindGames' player prefab calls it Holster.")]
        [SerializeField] private string containerName = "Holster";

        [Tooltip("Which slot to equip once the loadout is in. -1 leaves Tracey empty-handed.")]
        [SerializeField, Range(-1, 5)] private int startingSlot = 0;

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

                var result = holster.AddItem(new ItemStack(new Item(def), 1));
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

            // Equip the first weapon so the player starts with something in hand rather than having
            // to press the cycle button once before anything appears.
            var selection = character.GetCC<IWieldableInventoryCC>();
            if (selection != null && startingSlot >= 0)
            {
                selection.SelectAtIndex(startingSlot);
            }
        }
    }
}
