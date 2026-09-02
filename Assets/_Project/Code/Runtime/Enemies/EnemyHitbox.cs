using MrMoonlight.Combat;
using MrMoonlight.Data;
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
    /// Owner: MRM-34 (stub), MRM-76 (real zones).
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Enemies/Enemy Hitbox")]
    public sealed class EnemyHitbox : MonoBehaviour, IDamageable
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
