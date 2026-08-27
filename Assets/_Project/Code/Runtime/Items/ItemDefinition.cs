using UnityEngine;

namespace MrMoonlight.Items
{
    /// <summary>Broad grouping used for stacking/use rules, not display. See <see cref="ItemEffectApplier"/> for the actual per-item effect logic. Owner: MRM-41</summary>
    public enum ItemCategory
    {
        Food,
        Heal,
        Drug,
        Ammo,
        Equipment
    }

    /// <summary>
    /// One asset per item type, per Docs/unity-conventions.md's ScriptableObjects section - identity
    /// and display data only. Effect *values* live in <see cref="Data.MoonlightTunables"/>, one
    /// named field per item (the CLAUDE.md no-hardcoded-values rule), keyed off <see cref="Id"/> by
    /// <see cref="ItemEffectApplier"/>, rather than duplicated as fields here - one flat tunables
    /// asset stays the single place Carlos already knows to open for numbers. Owner: MRM-41
    /// </summary>
    [CreateAssetMenu(menuName = "MrMoonlight/Item Definition", fileName = "Item_")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Header("Identity — MRM-41")]

        /// <summary>Which catalogue entry this is. Drives both stacking identity and which tunable fields <see cref="ItemEffectApplier"/> reads for it.</summary>
        public ItemId Id;

        public ItemCategory Category = ItemCategory.Equipment;

        public string DisplayName = string.Empty;

        [TextArea]
        public string Description = string.Empty;

        [Header("Presentation")]

        /// <summary>Prefab spun and displayed by MRM-42's inventory UI once it's unblocked. Separate from the world pickup prefab (<see cref="Item"/>) since in-hand/in-UI presentation may need its own scale or pivot. Left unassigned until Carlos supplies prop models - see Docs/mrm41-items-interaction-kickoff.md.</summary>
        public GameObject DisplayPrefab;
    }
}
