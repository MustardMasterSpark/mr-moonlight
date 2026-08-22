namespace MrMoonlight.Player
{
    /// <summary>
    /// How a <see cref="StatModifier"/> combines with a <see cref="Stat"/>'s base value. See
    /// <see cref="Stat"/> for the combined stacking formula. Owner: MRM-12
    /// </summary>
    public enum StatModifierType
    {
        /// <summary>Sums with every other additive modifier before the multiplicative pass.</summary>
        Additive,

        /// <summary>Multiplies the post-additive result. Store the factor itself (1.5 = +50%, 0.8 = -20%), not a delta.</summary>
        Multiplicative
    }

    /// <summary>
    /// One entry in a <see cref="Stat"/>'s modifier list. <paramref name="Source"/> is whatever
    /// object applied it (an item, a status, a difficulty setting) so it can be removed later via
    /// <see cref="Stat.RemoveModifiersFromSource"/> without the stat needing to know who added
    /// what. Owner: MRM-12
    /// </summary>
    public readonly struct StatModifier
    {
        public readonly object Source;
        public readonly StatModifierType Type;
        public readonly float Value;

        public StatModifier(object source, StatModifierType type, float value)
        {
            Source = source;
            Type = type;
            Value = value;
        }
    }
}
