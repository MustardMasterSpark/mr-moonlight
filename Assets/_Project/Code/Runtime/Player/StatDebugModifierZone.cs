using UnityEngine;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Sandbox-only debug tool: applies a modifier to a chosen stat on whatever
    /// <see cref="PlayerStats"/> enters this trigger, removes it on exit. Same
    /// enter/exit-collider shape as <see cref="StatDebugPoolZone"/>, so all of MRM-12's debug
    /// test objects behave the same way - walk in to see the effect, walk out to revert. Applies
    /// once on entry rather than every frame while inside, so standing in the zone doesn't stack
    /// the same modifier repeatedly. Not part of the shipped game - same "debug tool, not
    /// shipped" category as MRM-8's InputDebugOverlay. Set the collider to Is Trigger.
    /// Owner: MRM-12
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class StatDebugModifierZone : MonoBehaviour
    {
        private enum TargetStat
        {
            MeleeDamage,
            Defense,
            Speed,
            AudioPitch
        }

        [SerializeField] private TargetStat targetStat = TargetStat.Defense;
        [SerializeField] private StatModifierType modifierType = StatModifierType.Additive;
        [SerializeField] private float modifierValue = 0.5f;

        private void OnTriggerEnter(Collider other)
        {
            var stats = other.GetComponentInParent<PlayerStats>();
            if (stats == null)
            {
                return;
            }

            ResolveStat(stats).AddModifier(new StatModifier(this, modifierType, modifierValue));
        }

        private void OnTriggerExit(Collider other)
        {
            var stats = other.GetComponentInParent<PlayerStats>();
            if (stats == null)
            {
                return;
            }

            ResolveStat(stats).RemoveModifiersFromSource(this);
        }

        private Stat ResolveStat(PlayerStats stats)
        {
            switch (targetStat)
            {
                case TargetStat.MeleeDamage:
                    return stats.MeleeDamage;
                case TargetStat.Defense:
                    return stats.Defense;
                case TargetStat.Speed:
                    return stats.Speed;
                default:
                    return stats.AudioPitch;
            }
        }
    }
}
