using System.Collections.Generic;
using MrMoonlight.Input;
using PolymindGames;
using PolymindGames.InventorySystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Number-key weapon selection for the testing arsenal (MRM-25).
    ///
    /// <para>Carlos, 2026-09-04: <i>"I want you to find some weapons for certain keys. These keys,
    /// whenever you press this, will alternate in the list of the weapons inside of this list. They
    /// are meant to be categories of weapons."</i> One key per category; pressing the same key again
    /// walks to the next weapon inside that category and wraps.</para>
    ///
    /// <para><b>Keyboard only, deliberately.</b> Carlos, same session: <i>"all of these new keys
    /// obviously don't have an equivalent on the controller so don't try to integrate those."</i>
    /// None of the six category actions carries a gamepad binding.
    /// <see cref="MoonlightWeaponCycler"/> still runs alongside this on the gamepad right shoulder,
    /// so a controller is not left with no way to change weapon at all.</para>
    ///
    /// <para><b>This is a testing scaffold, not the shipping control scheme.</b> The real game gates
    /// weapons behind pickups (MRM-26) and will not hand the player thirteen at once. The whole
    /// arrangement — the category map, infinite reserve ammo, and
    /// <see cref="MoonlightInfiniteThrowables"/> — is documented as a restorable "weapon test range"
    /// mode in Docs/mrm25-weapon-test-arsenal.md so it can be switched back on later.</para>
    ///
    /// <para>Slots are resolved by <b>item definition name</b> against the live holster rather than
    /// by hardcoded index, so re-ordering <see cref="MoonlightStartingLoadout"/> cannot silently
    /// desync the two.</para>
    ///
    /// Owner: MRM-25.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Player/Moonlight Weapon Category Switcher")]
    [RequireComponent(typeof(MoonlightPlayerRig))]
    [DefaultExecutionOrder(300)]
    public sealed class MoonlightWeaponCategorySwitcher : MonoBehaviour
    {
        /// <summary>Which of the six number keys a category answers to. An enum rather than a raw
        /// <see cref="InputActionReference"/> so the inspector can't be pointed at an action that
        /// isn't one of the category keys.</summary>
        public enum CategoryKey
        {
            Melee,
            Pistol,
            Shotgun,
            Rifle,
            Precision,
            Throwable,
        }

        [System.Serializable]
        public sealed class WeaponCategory
        {
            [Tooltip("Which number key selects this category.")]
            public CategoryKey Key = CategoryKey.Melee;

            [Tooltip("Item definition names, in the order repeated presses walk through them.")]
            public List<string> Weapons = new List<string>();

            /// <summary>Holster slot indices, resolved from <see cref="Weapons"/> at startup.
            /// Empty if none of the named items made it into the holster.</summary>
            [System.NonSerialized] public readonly List<int> Slots = new List<int>();

            /// <summary>Where in <see cref="Slots"/> the next press lands.</summary>
            [System.NonSerialized] public int Cursor;
        }

        [Tooltip("One entry per number key. Names must match item definitions in "
                 + "Assets/_Project/Data/PolymindGames/Resources/Definitions/Item.")]
        [SerializeField]
        private List<WeaponCategory> categories = new List<WeaponCategory>
        {
            // The Club is the BaseballBat asset - see Docs/glossary.md, ruled 2026-08-28.
            new WeaponCategory { Key = CategoryKey.Melee,     Weapons = { "Combat Knife", "Fire Axe", "Baseball Bat" } },
            new WeaponCategory { Key = CategoryKey.Pistol,    Weapons = { "M1911", "Revolver" } },
            new WeaponCategory { Key = CategoryKey.Shotgun,   Weapons = { "R870", "Double Barrel Shotgun" } },
            new WeaponCategory { Key = CategoryKey.Rifle,     Weapons = { "M1A", "AKM" } },
            new WeaponCategory { Key = CategoryKey.Precision, Weapons = { "Crossbow", "Hunting Rifle" } },
            new WeaponCategory { Key = CategoryKey.Throwable, Weapons = { "Frag Grenade", "Molotov Cocktail" } },
        };

        [Tooltip("On: pressing a category key returns to the weapon last held in that category. "
                 + "Off: it always starts at the first weapon in the list. Off is more predictable "
                 + "for A/B testing, which is what this mode is for.")]
        [SerializeField] private bool rememberLastInCategory;

        [Tooltip("Name of the wieldable container. PolymindGames' player prefab calls it Holster.")]
        [SerializeField] private string containerName = "Holster";

        private readonly Dictionary<CategoryKey, InputAction> _actions = new Dictionary<CategoryKey, InputAction>();
        private MoonlightPlayerRig _rig;
        private IWieldableInventoryCC _selection;
        private IItemContainer _holster;

        private void Awake()
        {
            _rig = GetComponent<MoonlightPlayerRig>();
        }

        private void Start()
        {
            ICharacter character = GetComponentInParent<ICharacter>() ?? GetComponent<ICharacter>();
            if (character == null || character.Inventory == null)
            {
                Debug.LogError("[MRM-25] No character inventory found — number-key weapon selection is dead.", this);
                enabled = false;
                return;
            }

            _selection = character.GetCC<IWieldableInventoryCC>();
            if (_selection == null)
            {
                Debug.LogError("[MRM-25] No IWieldableInventoryCC on the character — number-key weapon selection is dead.", this);
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
                               + "' — number-key weapon selection is dead.", this);
                enabled = false;
                return;
            }

            InputMapController input = _rig != null ? _rig.Input : null;
            if (input == null)
            {
                Debug.LogError("[MRM-25] No InputMapController — number-key weapon selection is dead.", this);
                enabled = false;
                return;
            }

            BindActions(input);
            ResolveSlots();
        }

        private void BindActions(InputMapController input)
        {
            InputSystem_Actions.GameplayActions g = input.Actions.Gameplay;
            _actions[CategoryKey.Melee] = g.WeaponCategoryMelee;
            _actions[CategoryKey.Pistol] = g.WeaponCategoryPistol;
            _actions[CategoryKey.Shotgun] = g.WeaponCategoryShotgun;
            _actions[CategoryKey.Rifle] = g.WeaponCategoryRifle;
            _actions[CategoryKey.Precision] = g.WeaponCategoryPrecision;
            _actions[CategoryKey.Throwable] = g.WeaponCategoryThrowable;
        }

        /// <summary>
        /// Maps each category's item names onto live holster slot indices.
        ///
        /// <para>Reading the holster back rather than trusting a parallel index list is what makes
        /// this safe to edit: a weapon the loadout failed to add (bad name, holster full) simply
        /// drops out of its category instead of shifting every later index by one and silently
        /// equipping the wrong gun.</para>
        /// </summary>
        private void ResolveSlots()
        {
            var slotForName = new Dictionary<string, int>(_holster.SlotsCount);
            for (int i = 0; i < _holster.SlotsCount; i++)
            {
                ItemStack stack = _holster.GetItemAtIndex(i);
                if (stack.HasItem() && !slotForName.ContainsKey(stack.Item.Name))
                {
                    slotForName.Add(stack.Item.Name, i);
                }
            }

            foreach (WeaponCategory category in categories)
            {
                category.Slots.Clear();
                category.Cursor = 0;

                foreach (string weaponName in category.Weapons)
                {
                    int slot;
                    if (slotForName.TryGetValue(weaponName, out slot))
                    {
                        category.Slots.Add(slot);
                    }
                    else
                    {
                        Debug.LogWarning("[MRM-25] '" + weaponName + "' is not in the " + containerName
                                         + " — key " + category.Key + " will skip it. Check "
                                         + nameof(MoonlightStartingLoadout) + "'s weapon list.", this);
                    }
                }
            }
        }

        private void Update()
        {
            foreach (WeaponCategory category in categories)
            {
                InputAction action;
                if (!_actions.TryGetValue(category.Key, out action) || action == null)
                {
                    continue;
                }

                if (action.WasPerformedThisFrame())
                {
                    Select(category);
                    // One key per frame. Two categories cannot both be entered in the same frame,
                    // and stopping here means the second SelectAtIndex can't cancel the first
                    // weapon's equip animation halfway through.
                    return;
                }
            }
        }

        /// <summary>
        /// Equips the next weapon in <paramref name="category"/>.
        ///
        /// <para>If the player is already holding something from this category, this advances one
        /// step and wraps. If they are not, it enters the category — at the first weapon by default,
        /// or the one last held there when <see cref="rememberLastInCategory"/> is on.</para>
        /// </summary>
        private void Select(WeaponCategory category)
        {
            if (category.Slots.Count == 0)
            {
                return;
            }

            int currentSlot = _selection.SelectedIndex;
            int positionInCategory = category.Slots.IndexOf(currentSlot);

            int next;
            if (positionInCategory >= 0)
            {
                next = (positionInCategory + 1) % category.Slots.Count;
            }
            else if (rememberLastInCategory)
            {
                next = Mathf.Clamp(category.Cursor, 0, category.Slots.Count - 1);
            }
            else
            {
                next = 0;
            }

            category.Cursor = next;
            _selection.SelectAtIndex(category.Slots[next], false);
        }
    }
}
