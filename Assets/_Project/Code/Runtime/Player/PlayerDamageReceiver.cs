using System;
using MrMoonlight.Combat;
using UnityEngine;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Lets anything that shoots hurt Tracey. This is the player side of
    /// <see cref="IDamageable"/> — the same interface enemies implement, so a weapon never has to
    /// ask what it just hit.
    ///
    /// <para><b>It does not own any health.</b> The project already decided where health lives:
    /// <see cref="PlayerStats.Health"/> is the single source of truth (MRM-12), Burntwax's
    /// <c>PlayerHealth</c> is display-and-marker only, and <see cref="BurntwaxHealthBridge"/>
    /// connects them. Adding a third float here would desync exactly the way that bridge exists to
    /// prevent. So this routes damage into the same stat, through the same
    /// <see cref="Stat.Deplete"/> call, and defense modifiers, the death event and MRM-17's death
    /// sequence all keep working untouched.</para>
    ///
    /// <para>Goes on the <b>Player root</b>, not on <c>MrMoonlight Systems</c> where
    /// <see cref="PlayerStats"/> lives. A shot resolves its target with
    /// <c>GetComponentInParent&lt;IDamageable&gt;()</c> from the collider it hit, and the player's
    /// only collider is <c>Body</c>, whose parent chain reaches the Player root — not the Systems
    /// child. Put this on Systems and every shot would pass straight through.</para>
    ///
    /// Owner: MRM-34 (enemies needed something to shoot at). MRM-32 will extend the enemy side of
    /// the same interface; nothing here has to change for that.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Mr. Moonlight/Player/Player Damage Receiver")]
    public sealed class PlayerDamageReceiver : MonoBehaviour, IDamageable
    {
        [Tooltip("Where health actually lives. Found anywhere under this root if left empty.")]
        [SerializeField] private PlayerStats stats;

        [Tooltip("Debug only. While set, damage is reported but never applied. Driven by InvulnerableDebugToggle (F4).")]
        [SerializeField] private bool invulnerable;

        /// <summary>Raised with the amount for every hit that actually removed health.</summary>
        public event Action<float> Damaged;

        /// <summary>
        /// Raised with the amount for every hit that <em>would</em> have removed health but was
        /// absorbed by <see cref="Invulnerable"/>. This is what lets the debug overlay show that
        /// the player is being shot while they are not dying — invulnerability that silently eats
        /// hits looks identical to an enemy that is missing entirely.
        /// </summary>
        public event Action<float> DamageBlocked;

        /// <summary>Debug invulnerability. Hits still register and still raise <see cref="DamageBlocked"/>.</summary>
        public bool Invulnerable
        {
            get => invulnerable;
            set => invulnerable = value;
        }

        public bool IsDead => stats != null && stats.Health.Value <= 0f;

        public Transform DamageTransform => transform;

        private void Awake()
        {
            if (stats == null) stats = GetComponentInChildren<PlayerStats>(true);

            if (stats == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerDamageReceiver)} on '{name}' found no {nameof(PlayerStats)} — " +
                    "enemy shots will hit and do nothing.", this);
            }
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (info.Amount <= 0f || stats == null) return;
            if (IsDead) return;

            if (invulnerable)
            {
                DamageBlocked?.Invoke(info.Amount);
                return;
            }

            stats.Health.Deplete(info.Amount);
            Damaged?.Invoke(info.Amount);
        }
    }
}
