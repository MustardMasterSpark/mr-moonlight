using System;
using MrMoonlight.Combat;
using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.Events;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// Health, damage entry point and death for every enemy. Shared — the Spotter has no health
    /// script of its own, it just carries this one (MRM-34).
    ///
    /// This is built ahead of anything that can actually damage it. The player cannot deal damage
    /// yet and MRM-32 (hitboxes, multipliers, damage reactions) is still Backlog — Carlos's
    /// explicit call was to stand up the hooks now so the player-weapon work plugs in rather than
    /// forcing a rewrite. Kept deliberately thin for that reason: a value, an entry point, and
    /// three events. Hitbox multipliers live in <see cref="EnemyHitbox"/>; gore belongs to MRM-32.
    ///
    /// Blaze is driven, not depended on. If a <c>BlazeAI</c> is present this forwards hits and
    /// death into it (so Blaze plays its hit reaction, ragdolls, and calls nearby allies); if one
    /// is missing the health still works, which is what makes this testable on a bare prefab.
    /// Owner: MRM-34.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Mr. Moonlight/Enemies/Enemy Health")]
    public sealed class EnemyHealth : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [Tooltip("Tick to ignore the tunables default and use the override below instead. Per-instance override pattern — see MoonlightTunables' class doc.")]
        [SerializeField] private bool overrideMaxHealth;

        [SerializeField] private float maxHealthOverride = 100f;

        [Tooltip("Tick to ignore the shared low-health fraction and use the override below.")]
        [SerializeField] private bool overrideLowHealthThreshold;

        [Range(0f, 1f)]
        [SerializeField] private float lowHealthThresholdOverride = 0.35f;

        [Header("Death")]
        [Tooltip("Radius within which allies are told about this death, so they turn on the killer. Passed straight to Blaze.")]
        [SerializeField] private bool callAlliesOnDeath = true;

        [Header("Events")]
        [Tooltip("Fires on every hit that actually removes health.")]
        public UnityEvent<float> Damaged;

        [Tooltip("Fires exactly once, the first time health crosses below the low-health threshold. The Spotter's panic reinforcement call listens here.")]
        public UnityEvent LowHealthReached;

        [Tooltip("Fires once when health hits zero, before Blaze's own death handling runs.")]
        public UnityEvent Died;

        /// <summary>
        /// Every enemy death in the scene, whoever it was. Owner: MRM-11.
        ///
        /// <para>Static because the objective tracker has to count enemies that <i>did not exist</i>
        /// when it woke up — a reinforcement wave has no inspector to wire <see cref="Died"/> in,
        /// and asking Carlos to remember to hook up each newly placed Spotter is a bug waiting to
        /// happen. Narrow and typed, per <c>Docs/csharp-conventions.md</c>: one signal, one payload,
        /// every subscriber findable by right-click.</para>
        ///
        /// <para>Subscribers must unsubscribe in <c>OnDisable</c>. The reset below covers the
        /// remaining case — Enter Play Mode with domain reload switched off, where a stale
        /// subscriber would otherwise survive into the next session.</para>
        /// </summary>
        public static event Action<EnemyHealth> AnyDied;

        private BlazeAI _blaze;
        private bool _lowHealthRaised;

        public float MaxHealth => overrideMaxHealth ? maxHealthOverride : Tunables.I.SpotterMaxHealth;

        public float CurrentHealth { get; private set; }

        public float HealthFraction => MaxHealth <= 0f ? 0f : CurrentHealth / MaxHealth;

        public bool IsDead { get; private set; }

        public Transform DamageTransform => transform;

        /// <summary>Whoever landed the last hit. Blaze needs it to know who to retaliate against.</summary>
        public GameObject LastAttacker { get; private set; }

        private float LowHealthThreshold =>
            overrideLowHealthThreshold ? lowHealthThresholdOverride : Tunables.I.EnemyLowHealthThreshold;

        private void Awake()
        {
            _blaze = GetComponent<BlazeAI>();
            CurrentHealth = MaxHealth;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (IsDead || info.Amount <= 0f) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - info.Amount);
            LastAttacker = ResolveAttacker(info.Source);

            Damaged?.Invoke(info.Amount);

            if (CurrentHealth <= 0f)
            {
                Kill(info.Source);
                return;
            }

            // Blaze owns the flinch/knockdown reaction itself — knockdown is ragdoll physics driven
            // by HitStateBehaviour, not a keyframed state, so there is nothing to trigger here
            // beyond handing it the hit.
            if (_blaze != null) _blaze.Hit(ResolveAttacker(info.Source), callAlliesOnDeath);

            // One-shot, and deliberately *after* the hit reaction: the Spotter's panic call should
            // read as a response to being hurt, not as something that pre-empts the flinch.
            if (!_lowHealthRaised && HealthFraction <= LowHealthThreshold)
            {
                _lowHealthRaised = true;
                LowHealthReached?.Invoke();
            }
        }

        /// <summary>Kill outright, skipping the damage maths. Used by scripted deaths and debug tools.</summary>
        public void Kill(GameObject killer = null)
        {
            if (IsDead) return;

            IsDead = true;
            CurrentHealth = 0f;
            if (killer != null) LastAttacker = killer;

            // Our own listeners run first so the lamp and shotgun detach while the body is still
            // upright — Blaze's death handling may hand the body to a ragdoll on the same frame.
            Died?.Invoke();
            AnyDied?.Invoke(this);

            if (_blaze != null) _blaze.Death(callAlliesOnDeath, LastAttacker);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStaticSubscribers() => AnyDied = null;

        /// <summary>
        /// Walk a hit's source up to the object Blaze will actually accept as an enemy.
        ///
        /// <para>Blaze identifies enemies by tag, and drops any <c>enemyToAttack</c> whose tag is not
        /// in <c>vision.hostileTags</c> (<c>BlazeAI.cs</c> ~line 1293). But a hit's source is
        /// whatever component fired it, which for the player's gun is the <c>GunStateMachine</c> on
        /// <c>Arms</c> — an untagged child. Handing that straight to <c>Hit()</c> means
        /// <c>HitStateBehaviour.FinishHitState()</c> ends the flinch by calling
        /// <c>SetEnemy(Arms)</c>, Blaze rejects it on the next vision pass, and the agent churns
        /// between having and not having a target — which on screen is an enemy stuck in his hit
        /// animation. Resolving to the root fixes it: the tag that identifies a combatant lives
        /// there, on the player and on every enemy alike.</para>
        ///
        /// Found the hard way 2026-09-02, from a "hit him once and he looped in his stunned
        /// animation" report. Owner: MRM-34.
        /// </summary>
        private static GameObject ResolveAttacker(GameObject source)
        {
            return source == null ? null : source.transform.root.gameObject;
        }

        /// <summary>Restore to full and clear the one-shot low-health latch. For pooling and debug respawns.</summary>
        public void ResetHealth()
        {
            IsDead = false;
            _lowHealthRaised = false;
            LastAttacker = null;
            CurrentHealth = MaxHealth;
        }
    }
}
