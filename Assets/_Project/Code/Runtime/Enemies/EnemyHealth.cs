using System;
using DamageNumbersPro;
using MrMoonlight.Combat;
using MrMoonlight.Data;
using PampelGames.BloodFactory;
using PampelGames.GoreSimulator;
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
    /// three events. Hitbox multipliers live in <see cref="EnemyHitbox"/>.
    ///
    /// Blaze is driven, not depended on. If a <c>BlazeAI</c> is present this forwards hits and
    /// death into it (so Blaze plays its hit reaction, ragdolls, and calls nearby allies); if one
    /// is missing the health still works, which is what makes this testable on a bare prefab.
    ///
    /// <see cref="Kill"/> has one more step on top of that: if this enemy carries a Pampel Games
    /// <c>GoreSimulator</c> component (only the Spotter's dedicated gore prefab variant does, not
    /// the base enemy prefabs), a lethal hit has a
    /// <see cref="Data.MoonlightTunables.EnemyDismembermentChance"/> chance of detaching the limb
    /// nearest the killing blow — mesh-cut only, no ragdoll. Blaze's own keyframed <c>deathAnim</c>
    /// still plays as normal alongside it (Carlos's call, 2026-09-03: a full-body ragdoll read as
    /// too aggressive — squashed, physically unstable — next to the tuned death animation, so gore
    /// is now a visual detach layered on top of the existing death, not a replacement for it).
    ///
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

        [Header("Blood Effects — Blood Factory (Pampel Games)")]
        [Tooltip("Small spatter spawned at the hit point on every hit that doesn't kill, player and enemy weapons alike. Left empty (None) means no effect — only wired on the Spotter's gore prefab variant for now. Carlos's pick: BloodSplash06.")]
        [SerializeField] private GameObject hitBloodEffect;

        [Tooltip("Big blood explosion spawned from the torso on every kill, dismembered or not. Carlos's pick: BloodSplash01_Radial.")]
        [SerializeField] private GameObject killBloodEffect;

        [Tooltip("Blood spill spawned at the cut point on the body's side of a dismemberment (not on the severed piece itself). Carlos's pick: BloodSplatter06.")]
        [SerializeField] private GameObject dismembermentBloodEffect;

        [Header("Debug — Damage Numbers")]
        [Tooltip("Debug-only visualization, NOT part of the shipped game — a quick way to see hit damage while tuning weapons/hitboxes. Pops the Damage Numbers Pro 'Bleed' style at the hit point on every hit; rapid hits on the same enemy combine into a running total in the center while each individual chip number still shows. Off by default — flip per-prefab to enable. Deliberately not routed through MoonlightTunables since it isn't a shipping feature.")]
        [SerializeField] private bool showDebugDamageNumbers;

        [Tooltip("The Damage Numbers Pro prefab to spawn (Bleed, 3D/world-space style). Only used when showDebugDamageNumbers is on.")]
        [SerializeField] private DamageNumber debugDamageNumberPrefab;

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
        private GoreSimulator _goreSimulator;
        private Animator _animator;
        private bool _lowHealthRaised;

        // Defaults cover a direct Kill() call (debug tools, scripted deaths) that never went
        // through TakeDamage and so never recorded a real hit — a sane torso-height position and
        // an upward force still read fine rather than pinning the cut to the origin.
        private Vector3 _lastHitPoint;
        private Vector3 _lastHitDirection = Vector3.up;

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
            _goreSimulator = GetComponent<GoreSimulator>();
            _animator = GetComponent<Animator>();
            _lastHitPoint = transform.position + Vector3.up;
            CurrentHealth = MaxHealth;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (IsDead || info.Amount <= 0f) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - info.Amount);
            LastAttacker = ResolveAttacker(info.Source);
            _lastHitPoint = info.Point;
            if (info.Direction != Vector3.zero) _lastHitDirection = info.Direction;

            Damaged?.Invoke(info.Amount);
            SpawnDebugDamageNumber(info.Point, info.Amount);

            if (CurrentHealth <= 0f)
            {
                Kill(info.Source);
                return;
            }

            SpawnBloodEffect(hitBloodEffect, info.Point, HitEffectRotation(info.Direction), NearestBone(info.Point));

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

            // Always, dismembered or not — Carlos's ask: every kill gets the big splash, on top of
            // whatever the dismemberment spill below adds.
            Transform torso = TorsoBone();
            SpawnBloodEffect(killBloodEffect, torso.position, Quaternion.identity, torso);

            if (_goreSimulator != null && UnityEngine.Random.value < Tunables.I.EnemyDismembermentChance)
                Dismember();

            if (_blaze != null) _blaze.Death(callAlliesOnDeath, LastAttacker);
        }

        /// <summary>Torso/chest bone for the kill blood effect, doubling as the parent it rides
        /// along on. Falls back to this enemy's own root if this isn't a Humanoid rig or the bone
        /// isn't mapped — never null, so callers don't have to guard it.</summary>
        private Transform TorsoBone()
        {
            if (_animator != null && _animator.isHuman)
            {
                Transform chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
                if (chest != null) return chest;
            }

            return transform;
        }

        /// <summary>
        /// Detaches the limb nearest <see cref="_lastHitPoint"/> — mesh cut only, no ragdoll.
        /// <see cref="Kill"/> still calls <c>BlazeAI.Death</c> right after this, so the body keeps
        /// playing its normal keyframed death animation; the cut piece falls away on its own
        /// physics (<c>SubModulePhysics</c>, configured on the gore prefab variant) while the rest
        /// of the mesh is unaffected. Owner: MRM-34.
        /// </summary>
        private void Dismember()
        {
            Vector3 force = _lastHitDirection.normalized * Tunables.I.EnemyDismembermentForce;
            _goreSimulator.ExecuteCut(_lastHitPoint, force);

            // On the body's side of the cut, not the severed piece — Carlos's call 2026-09-03: one
            // spatter for now, revisit spawning a second one on the detached limb later.
            SpawnBloodEffect(dismembermentBloodEffect, _lastHitPoint, HitEffectRotation(_lastHitDirection), NearestBone(_lastHitPoint));
        }

        /// <summary>
        /// Instantiates a Blood Factory effect prefab and runs it via <c>BloodFactory.Execute()</c>
        /// (Pampel Games' own trigger API — the prefab's particle systems don't play on their own).
        ///
        /// Parented to <paramref name="parent"/> with <c>worldPositionStays: true</c>, so it keeps
        /// its spawn position/rotation but then rides along with the body from then on — found live
        /// 2026-09-03: an unparented spatter stayed fixed in world space while Blaze's death
        /// animation moved the body out from under it (confirmed on a beheaded Spotter — the spill
        /// was left floating in mid-air once the body fell), which happens whether or not the clip
        /// uses root motion, since it's the bones underneath the spatter that are moving.
        ///
        /// Null-safe: <paramref name="prefab"/> is None on any enemy that hasn't had an effect
        /// assigned, which is every enemy except the Spotter's gore prefab variant for now. Owner:
        /// MRM-34.
        /// </summary>
        private static void SpawnBloodEffect(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (prefab == null) return;

            GameObject instance = Instantiate(prefab, position, rotation);
            instance.transform.SetParent(parent, true);
            if (instance.TryGetComponent(out BloodFactory bloodFactory)) bloodFactory.Execute();
            Destroy(instance, Tunables.I.EnemyBloodEffectLifetime);
        }

        /// <summary>
        /// Debug-only: pops a Damage Numbers Pro popup at the hit point. Following the enemy's own
        /// transform (rather than leaving it pinned to the hit point) both keeps the number attached
        /// to a moving target and — via the DamageNumbersPro's own <c>SetFollowedTarget</c> — scopes
        /// its combine/"spam" grouping to this specific enemy, mirroring the vendor demo's own
        /// pattern (<c>DNP_Camera.Shoot()</c>): repeated hits on the same enemy combine into a
        /// running total while each chip number still shows, hits on different enemies never mix.
        /// </summary>
        private void SpawnDebugDamageNumber(Vector3 point, float amount)
        {
            if (!showDebugDamageNumbers || debugDamageNumberPrefab == null) return;

            DamageNumber popup = debugDamageNumberPrefab.Spawn(point, amount);
            popup.SetFollowedTarget(transform);
        }

        /// <summary>Faces a blood effect back along the hit's travel direction, so it reads as spraying outward from the wound rather than in an arbitrary default orientation.</summary>
        private static Quaternion HitEffectRotation(Vector3 hitDirection)
        {
            return hitDirection == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(hitDirection);
        }

        /// <summary>
        /// Closest bone to <paramref name="worldPosition"/> among this enemy's Gore Simulator bones,
        /// to parent a blood effect to so it tracks the body part it landed on. Falls back to this
        /// enemy's own root when there's no <see cref="GoreSimulator"/> (every enemy except the
        /// Spotter's gore prefab variant, for now) — never null, so callers don't have to guard it.
        /// </summary>
        private Transform NearestBone(Vector3 worldPosition)
        {
            if (_goreSimulator == null || _goreSimulator.bones == null || _goreSimulator.bones.Count == 0)
                return transform;

            Transform nearest = transform;
            float nearestSqrDistance = float.MaxValue;
            for (int i = 0; i < _goreSimulator.bones.Count; i++)
            {
                Transform bone = _goreSimulator.bones[i];

                // A bone just severed by ExecuteCut() gets reparented onto the detached, flying-away
                // piece — still the same Transform reference in this list, just no longer part of
                // this enemy's hierarchy. Skipping it keeps a dismemberment's blood effect pinned to
                // the body, not chasing the limb it's meant to stay behind.
                if (bone == null || !bone.IsChildOf(transform)) continue;

                float sqrDistance = (bone.position - worldPosition).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = bone;
                }
            }

            return nearest;
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
