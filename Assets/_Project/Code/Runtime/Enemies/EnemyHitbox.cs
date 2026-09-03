using MrMoonlight.Combat;
using MrMoonlight.Data;
using PolymindGames;
using UnityEngine;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// A collider on an enemy that forwards hits to the root <see cref="EnemyHealth"/>, scaled by a
    /// zone multiplier — MRM-76's per-body-part damage (Carlos: pistol should mostly one-shot a
    /// headshot, take 2-3 to the torso, 4-5 to a limb).
    ///
    /// Two ways a collider gets its zone, picked per instance via <see cref="mode"/>:
    /// <list type="bullet">
    /// <item><b>HeightBands</b> — for the enemy's existing root capsule, which already spans the
    /// whole body. Rather than adding competing geometry a raycast could get shadowed behind (a
    /// small head sphere sitting inside the capsule's own rounded cap would rarely be the first
    /// surface hit), this classifies the hit by comparing <c>DamageInfo.Point</c>'s height against
    /// the Animator's own Head and Hips bones, cached once at Awake. Top band = head, bottom band
    /// (leg height) = limb, everything between = torso.</item>
    /// <item><b>FixedZone</b> — for a small collider parented directly to an arm bone (the one part
    /// of "limbs" the capsule's silhouette does not cover — legs are inside it, arms are not). No
    /// classification needed; anything that lands here is a limb hit by construction.</item>
    /// </list>
    ///
    /// <para>Also implements PolymindGames' <see cref="PolymindGames.IDamageHandler"/> (MRM-9), which
    /// is how the HQ FPS weapons deliver hits: <c>FirearmStandardImpactEffect</c> does a
    /// <c>collider.TryGetComponent(out IDamageHandler)</c>, so the handler has to live on the
    /// collider's own GameObject. Rather than bolt a second bridge component onto all fifteen
    /// hitboxes of every Spotter, this one component answers to both damage systems and funnels
    /// them through the same zone-multiplier path.</para>
    ///
    /// Owner: MRM-34 (stub), MRM-76 (real zones), MRM-9 (HQ FPS damage interop).
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Enemies/Enemy Hitbox")]
    public sealed class EnemyHitbox : MonoBehaviour, IDamageable, PolymindGames.IDamageHandler
    {
        public enum HitboxZone { Limb, Torso, Head }

        private enum ClassificationMode { HeightBands, FixedZone }

        [Tooltip("HeightBands classifies by hit height against the Animator's Head/Hips bones — use this on the root capsule. FixedZone always reports the same zone — use this on a collider parented to a specific bone (e.g. an arm).")]
        [SerializeField] private ClassificationMode mode = ClassificationMode.HeightBands;

        [Tooltip("Only used in FixedZone mode.")]
        [SerializeField] private HitboxZone fixedZone = HitboxZone.Limb;

        [Tooltip("Leave empty to find the health on a parent automatically.")]
        [SerializeField] private EnemyHealth health;

        [Tooltip("Only used in HeightBands mode. Leave empty to find it on a parent automatically.")]
        [SerializeField] private Animator animator;

        private float _headBandStartY;
        private float _legBandEndY;
        private bool _bandsReady;

        public bool IsDead => health == null || health.IsDead;

        public Transform DamageTransform => health != null ? health.transform : transform;

        private void Awake()
        {
            if (health == null) health = GetComponentInParent<EnemyHealth>();

            if (mode == ClassificationMode.HeightBands)
            {
                if (animator == null) animator = GetComponentInParent<Animator>();
                CacheBandsIfReady();
            }
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (health == null) return;

            float multiplier = mode == ClassificationMode.FixedZone
                ? ZoneMultiplier(fixedZone)
                : ZoneMultiplier(ClassifyByHeight(info.Point));

            var scaled = new DamageInfo(info.Amount * multiplier, info.Point, info.Direction, info.Source);
            health.TakeDamage(scaled);
        }

        /// <summary>
        /// PolymindGames entry point (MRM-9). The HQ FPS weapons call this on whatever collider
        /// their ray hit.
        ///
        /// <para><b>One hitbox per ray, by construction.</b> Carlos's rule is that a shot must never
        /// stack damage across several hitboxes of the same enemy. Every HQ FPS fire system uses
        /// <c>Physics.Raycast</c>, which returns only the first collider along the ray, so a single
        /// ray can only ever reach one hitbox. The shotgun's eight pellets are eight separate rays
        /// and are each meant to land on their own - that is the spread, not double damage.</para>
        /// </summary>
        public DamageResult HandleDamage(float damage, in DamageArgs args)
        {
            if (health == null || health.IsDead)
            {
                return DamageResult.Ignored;
            }

            // args.HitForce is the shot's impulse; its direction is the travel direction our own
            // DamageInfo wants. Falls back to the shooter's facing when a source gave no force.
            Vector3 direction = args.HitForce.sqrMagnitude > 0.0001f
                ? args.HitForce.normalized
                : transform.forward;

            Vector3 point = args.HitPoint != Vector3.zero ? args.HitPoint : transform.position;
            GameObject source = args.Source != null ? args.Source.transform.gameObject : null;

            TakeDamage(new DamageInfo(damage, point, direction, source));

            return health.IsDead ? DamageResult.Fatal : DamageResult.Normal;
        }

        /// <summary>
        /// Mr. Moonlight enemies are Blaze AI agents, not PolymindGames <c>ICharacter</c>s, so there
        /// is no character to hand back. Only the vendor's own hitmarker UI reads this, and it
        /// null-checks first.
        /// </summary>
        ICharacter PolymindGames.IDamageHandler.Character => null;

        private HitboxZone ClassifyByHeight(Vector3 worldPoint)
        {
            CacheBandsIfReady();
            if (!_bandsReady) return HitboxZone.Torso;

            if (worldPoint.y >= _headBandStartY) return HitboxZone.Head;
            if (worldPoint.y <= _legBandEndY) return HitboxZone.Limb;
            return HitboxZone.Torso;
        }

        private void CacheBandsIfReady()
        {
            if (_bandsReady || animator == null || !animator.isHuman) return;

            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (head == null || hips == null) return;

            _headBandStartY = head.position.y - Tunables.I.EnemyHitboxHeadBandMargin;
            _legBandEndY = hips.position.y;
            _bandsReady = true;
        }

        private static float ZoneMultiplier(HitboxZone zone) => zone switch
        {
            HitboxZone.Head => Tunables.I.EnemyHitboxHeadMultiplier,
            HitboxZone.Torso => Tunables.I.EnemyHitboxTorsoMultiplier,
            _ => Tunables.I.EnemyHitboxLimbMultiplier
        };
    }
}
