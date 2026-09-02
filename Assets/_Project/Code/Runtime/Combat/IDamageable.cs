using UnityEngine;

namespace MrMoonlight.Combat
{
    /// <summary>
    /// Anything that can be shot. Implemented by <see cref="EnemyHealth"/> today; the player's own
    /// health and destructible props are expected to implement it too, so a weapon never has to
    /// ask what it just hit.
    ///
    /// Deliberately built ahead of the player being able to deal damage at all (MRM-32 is still
    /// Backlog) — Carlos's explicit call, so the player-weapon work has something to plug into
    /// without a rewrite. Owner: MRM-34.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>True once this thing is dead and should stop absorbing hits.</summary>
        bool IsDead { get; }

        /// <summary>The transform to aim at / report hits against. Usually the root, not the collider.</summary>
        Transform DamageTransform { get; }

        void TakeDamage(in DamageInfo info);
    }
}
