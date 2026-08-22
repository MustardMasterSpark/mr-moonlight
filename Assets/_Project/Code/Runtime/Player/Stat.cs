using System.Collections.Generic;
using UnityEngine;

namespace MrMoonlight.Player
{
    /// <summary>
    /// One readable, modifiable stat (health, stamina, speed, melee damage, defense, audio
    /// pitch - see MRM-12). Generic across all six so nothing grows its own ad-hoc modification
    /// path, per MRM-12's Model note.
    ///
    /// <para><b>Stacking rule</b> - every modifier is additive or multiplicative
    /// (<see cref="StatModifier"/>). All additive modifiers sum onto <see cref="BaseValue"/>
    /// first; every multiplicative modifier's factor then multiplies that sum:</para>
    /// <para><c>Value = (BaseValue + sum of additive) * (product of multiplicative)</c></para>
    /// <para>Example: base defense 1.0, a difficulty modifier of +0.2 additive and a boots
    /// modifier of x1.5 multiplicative together produce (1.0 + 0.2) * 1.5 = 1.8. Two
    /// multiplicative modifiers combine by multiplying their factors together, not by adding
    /// percentages - x1.5 and x1.2 together are x1.8, not x1.7. See
    /// StatModifierStackingTests for a runnable proof. Owner: MRM-12</para>
    ///
    /// <para><b>Locking</b> (MRM-12 section 11.6) - <see cref="Lock"/> pins <see cref="Value"/>
    /// at an exact number, bypassing the modifier stack entirely, until <see cref="Unlock"/>.
    /// Callers that drain or regenerate a stat (see <see cref="PlayerStats"/>'s stamina
    /// handling) must check <see cref="IsLocked"/> themselves before mutating
    /// <see cref="BaseValue"/> - locking only changes what <see cref="Value"/> reports, it does
    /// not freeze callers.</para>
    /// </summary>
    public sealed class Stat
    {
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();

        public float BaseValue;
        public float MinValue;
        public float MaxValue;

        public bool IsLocked { get; private set; }
        private float _lockedValue;

        /// <summary>The modifier-stacked, clamped value - or the locked value while <see cref="IsLocked"/>.</summary>
        public float Value
        {
            get
            {
                if (IsLocked)
                {
                    return Mathf.Clamp(_lockedValue, MinValue, MaxValue);
                }

                float additiveSum = 0f;
                float multiplicativeProduct = 1f;

                foreach (var modifier in _modifiers)
                {
                    if (modifier.Type == StatModifierType.Additive)
                    {
                        additiveSum += modifier.Value;
                    }
                    else
                    {
                        multiplicativeProduct *= modifier.Value;
                    }
                }

                return Mathf.Clamp((BaseValue + additiveSum) * multiplicativeProduct, MinValue, MaxValue);
            }
        }

        public Stat(float baseValue, float minValue = 0f, float maxValue = float.MaxValue)
        {
            BaseValue = baseValue;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        /// <summary>Adds a modifier. Does not deduplicate - adding the same source twice stacks it twice.</summary>
        public void AddModifier(StatModifier modifier) => _modifiers.Add(modifier);

        /// <summary>Removes every modifier whose Source matches, by reference. Returns true if any were removed.</summary>
        public bool RemoveModifiersFromSource(object source) => _modifiers.RemoveAll(m => ReferenceEquals(m.Source, source)) > 0;

        public void ClearModifiers() => _modifiers.Clear();

        /// <summary>Subtracts directly from <see cref="BaseValue"/>, clamped - for depleting pools (health damage, stamina drain).</summary>
        public void Deplete(float amount) => BaseValue = Mathf.Clamp(BaseValue - amount, MinValue, MaxValue);

        /// <summary>Adds directly to <see cref="BaseValue"/>, clamped - for recovering pools (health items, stamina regen).</summary>
        public void Restore(float amount) => BaseValue = Mathf.Clamp(BaseValue + amount, MinValue, MaxValue);

        /// <summary>Pins <see cref="Value"/> at <paramref name="value"/>, ignoring the modifier stack, until <see cref="Unlock"/>.</summary>
        public void Lock(float value)
        {
            IsLocked = true;
            _lockedValue = value;
        }

        public void Unlock() => IsLocked = false;
    }
}
