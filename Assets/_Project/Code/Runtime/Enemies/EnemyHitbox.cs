using MrMoonlight.Combat;
using UnityEngine;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// A collider on an enemy that forwards hits to the root <see cref="EnemyHealth"/>, scaled by
    /// a multiplier. Put one on a head collider with a multiplier of 2 and headshots hurt more.
    ///
    /// This is the deliberately minimal stub of MRM-32, not MRM-32 itself — MRM-32 owns the real
    /// hitbox layout, the damage-reaction picking and the gore hooks. What is here is only enough
    /// surface area for the player-weapon work to aim at something meaningful. Owner: MRM-34.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Enemies/Enemy Hitbox")]
    public sealed class EnemyHitbox : MonoBehaviour, IDamageable
    {
        [Tooltip("Damage taken through this collider is multiplied by this before it reaches the enemy's health.")]
        [SerializeField] private float damageMultiplier = 1f;

        [Tooltip("Leave empty to find the health on a parent automatically.")]
        [SerializeField] private EnemyHealth health;

        public bool IsDead => health == null || health.IsDead;

        public Transform DamageTransform => health != null ? health.transform : transform;

        private void Awake()
        {
            if (health == null) health = GetComponentInParent<EnemyHealth>();
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (health == null) return;

            var scaled = new DamageInfo(
                info.Amount * damageMultiplier,
                info.Point,
                info.Direction,
                info.Source);

            health.TakeDamage(scaled);
        }
    }
}
