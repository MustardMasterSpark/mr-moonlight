using UnityEngine;

namespace MrMoonlight.Combat
{
    /// <summary>
    /// One hit, described once. Passed by <c>in</c> so adding a field later costs nothing at the
    /// call sites.
    ///
    /// This exists as a struct rather than five <c>TakeDamage</c> overloads because MRM-32 (enemy
    /// hitboxes, damage multipliers, damage reactions) is going to want the hit point, the
    /// direction and the hit bone for gore and reaction picking, and it should be able to add
    /// them here instead of rewriting every caller. Owner: MRM-34, extended by MRM-32.
    /// </summary>
    public readonly struct DamageInfo
    {
        /// <summary>Damage after any hitbox multiplier has already been applied.</summary>
        public readonly float Amount;

        /// <summary>World-space point the hit landed on. Used for impact VFX and gore placement.</summary>
        public readonly Vector3 Point;

        /// <summary>Normalised travel direction of whatever caused the hit. Drives knockback and reaction direction.</summary>
        public readonly Vector3 Direction;

        /// <summary>Who dealt it. May be null for environmental damage. Enemies use this to decide who to turn on.</summary>
        public readonly GameObject Source;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject source)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Source = source;
        }
    }
}
