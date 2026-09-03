using MrMoonlight.Combat;
using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.Pool;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// One shot from an enemy gun: pellets, spread, hitscan, damage falloff, and the visible
    /// tracers that let the player read where the shot came from.
    ///
    /// Shared, not Spotter-specific — the Zealot and anything else that shoots reuses this and
    /// only changes numbers. The <i>rhythm</i> (aim, two barrels, reload) is deliberately not here;
    /// that is <see cref="EnemyRangedAttack"/>, because rhythm is per-enemy design and ballistics
    /// are not.
    ///
    /// Hitscan, not projectiles: MRM-34 asks for visible birdshot tracers, and a tracer that is
    /// only a visual makes the hit deterministic on the frame the shot fires. The trail then flies
    /// along the already-resolved path purely so the player can see it. This is the same shape as
    /// the player's own gun (Burntwax <c>GunScriptableObject.PlayTrail</c>), so both sides of a
    /// firefight read identically.
    ///
    /// Owner: MRM-34.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Enemies/Enemy Firearm")]
    public sealed class EnemyFirearm : MonoBehaviour
    {
        [Header("Muzzle")]
        [Tooltip("Empty GameObject at the barrel tip. Tracers start here and its forward axis is the barrel direction. Required.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Optional particle system flashed on every shot. Leave empty until the muzzle-flash VFX lands.")]
        [SerializeField] private ParticleSystem muzzleFlash;

        [Header("Ballistics — defaults come from MoonlightTunables")]
        [SerializeField] private bool overridePelletCount;
        [SerializeField] private int pelletCountOverride = 7;

        [SerializeField] private bool overridePelletDamage;
        [SerializeField] private float pelletDamageOverride = 4.5f;

        [SerializeField] private bool overrideSpreadAngle;
        [SerializeField] private float spreadAngleOverride = 4.5f;

        [SerializeField] private bool overrideRange;
        [SerializeField] private float rangeOverride = 45f;

        [Header("Targeting")]
        [Tooltip("What a pellet can hit. Must exclude the shooter's own layer or every shot hits the shooter.")]
        [SerializeField] private LayerMask hitMask = ~0;

        [Tooltip("Colliders under this transform are ignored, so a Spotter never shoots himself or his own lamp. Leave empty to use this component's root.")]
        [SerializeField] private Transform ignoreRoot;

        [Header("Tracers")]
        [Tooltip("Material for the tracer streak. An additive/unlit particle material — a lit one will read as a grey string.")]
        [SerializeField] private Material tracerMaterial;

        [Tooltip("Colour and alpha along the streak, head to tail. The default fades a hot yellow tip out to a dim red tail.")]
        [SerializeField] private Gradient tracerColour = BuildDefaultTracerGradient();

        private ObjectPool<TrailRenderer> _tracerPool;
        private GameObject _owner;

        private int PelletCount => overridePelletCount ? pelletCountOverride : Tunables.I.SpotterPelletCount;
        private float PelletDamage => overridePelletDamage ? pelletDamageOverride : Tunables.I.SpotterPelletDamage;
        private float SpreadAngle => overrideSpreadAngle ? spreadAngleOverride : Tunables.I.SpotterSpreadAngle;
        private float Range => overrideRange ? rangeOverride : Tunables.I.SpotterShotRange;

        /// <summary>Where shots come from. Behaviours use it to decide whether the barrel has line of sight.</summary>
        public Transform Muzzle => muzzle;

        private void Awake()
        {
            if (ignoreRoot == null) ignoreRoot = transform.root;
            _owner = ignoreRoot != null ? ignoreRoot.gameObject : gameObject;

            // Per-firearm pool rather than one shared static pool: a shell is at most a handful of
            // trails, ten Spotters is still under a hundred, and a per-instance pool keeps this
            // free of the static state the project otherwise avoids.
            _tracerPool = new ObjectPool<TrailRenderer>(
                CreateTracer,
                actionOnGet: t => t.gameObject.SetActive(true),
                actionOnRelease: t =>
                {
                    t.emitting = false;
                    t.Clear();
                    t.gameObject.SetActive(false);
                },
                actionOnDestroy: t => { if (t != null) Destroy(t.gameObject); },
                defaultCapacity: 8,
                maxSize: 32);
        }

        private void OnDestroy()
        {
            _tracerPool?.Dispose();
        }

        /// <summary>
        /// Fire one shell at <paramref name="aimPoint"/>. A deliberate miss still fires, still
        /// draws tracers and still hits the world — it is only aimed wide, so the player reads it
        /// as being shot at rather than as the enemy having lost them.
        /// </summary>
        public void Fire(Vector3 aimPoint, bool deliberateMiss, float missAngle)
        {
            if (muzzle == null) return;

            if (muzzleFlash != null) muzzleFlash.Play();

            Vector3 origin = muzzle.position;
            Vector3 baseDirection = (aimPoint - origin).normalized;
            if (baseDirection.sqrMagnitude < 0.0001f) baseDirection = muzzle.forward;

            if (deliberateMiss)
            {
                // One random roll around the barrel axis, applied to the whole shell rather than
                // per pellet — the cone has to stay a cone, just pointed somewhere else. Rolling
                // per pellet would only widen the spread and half of it would still connect.
                float roll = Random.Range(0f, 360f);
                baseDirection = Quaternion.AngleAxis(missAngle, Quaternion.AngleAxis(roll, baseDirection) * Vector3.up)
                                * baseDirection;
            }

            // One roll per shell, shared by every pellet in it — MRM-76: Carlos wants shot-to-shot
            // damage variance ("a little bit more fair"), not pellet-to-pellet noise. Rolling per
            // pellet would average out across all seven and the variance would vanish.
            float shotDamageMultiplier = Random.Range(
                Tunables.I.EnemyDamageVarianceMultiplierMin,
                Tunables.I.EnemyDamageVarianceMultiplierMax);

            for (int i = 0; i < PelletCount; i++)
            {
                FirePellet(origin, ConeDirection(baseDirection, SpreadAngle), shotDamageMultiplier);
            }
        }

        private void FirePellet(Vector3 origin, Vector3 direction, float shotDamageMultiplier)
        {
            Vector3 endPoint = origin + direction * Range;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, Range, hitMask, QueryTriggerInteraction.Ignore)
                && !IsOwnCollider(hit.collider))
            {
                endPoint = hit.point;

                // No friendly fire: enemies only ever hurt the player. hitMask already excludes most
                // of this by layer, but colliders aren't guaranteed to be laid out that cleanly on
                // every enemy variant (e.g. the gore prefab's added hitboxes/colliders) — checking
                // EnemyIdentity directly is the one check that can't drift out of sync with layers.
                if (hit.collider.GetComponentInParent<IDamageable>() is { IsDead: false } target
                    && hit.collider.GetComponentInParent<EnemyIdentity>() == null)
                {
                    target.TakeDamage(new DamageInfo(
                        DamageAtDistance(hit.distance) * shotDamageMultiplier, hit.point, direction, _owner));
                }
            }

            StartCoroutine(FlyTracer(origin, endPoint));
        }

        /// <summary>Full damage out to the falloff start, then a linear taper to zero at max range.</summary>
        private float DamageAtDistance(float distance)
        {
            float falloffStart = Tunables.I.SpotterDamageFalloffStart;
            if (distance <= falloffStart) return PelletDamage;

            float t = Mathf.InverseLerp(falloffStart, Range, distance);
            return Mathf.Lerp(PelletDamage, 0f, t);
        }

        private static Vector3 ConeDirection(Vector3 forward, float halfAngleDegrees)
        {
            // Uniform over the cone's spherical cap rather than over the angle — sampling the angle
            // directly bunches pellets at the centre and leaves the edge of the pattern empty.
            float cosMax = Mathf.Cos(halfAngleDegrees * Mathf.Deg2Rad);
            float z = Random.Range(cosMax, 1f);
            float theta = Random.Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(1f - z * z);

            Vector3 local = new Vector3(r * Mathf.Cos(theta), r * Mathf.Sin(theta), z);
            return Quaternion.LookRotation(forward) * local;
        }

        private bool IsOwnCollider(Collider other)
        {
            return ignoreRoot != null && other.transform.IsChildOf(ignoreRoot);
        }

        private System.Collections.IEnumerator FlyTracer(Vector3 start, Vector3 end)
        {
            TrailRenderer tracer = _tracerPool.Get();
            tracer.transform.position = start;

            // One frame with emitting off so the trail does not draw a line from wherever this
            // instance was last used. Same guard as the player's gun trail.
            yield return null;
            tracer.emitting = true;

            float distance = Vector3.Distance(start, end);
            float speed = Tunables.I.EnemyTracerSpeed;
            float travelled = 0f;

            while (travelled < distance)
            {
                travelled += speed * Time.deltaTime;
                tracer.transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(travelled / distance));
                yield return null;
            }

            tracer.transform.position = end;

            yield return new WaitForSeconds(Tunables.I.EnemyTracerDuration);
            _tracerPool.Release(tracer);
        }

        /// <summary>
        /// The default streak: a hot tip fading to a dim red tail. A field initializer rather than
        /// inspector setup, so a firearm added from script looks right without anyone touching it.
        /// </summary>
        private static Gradient BuildDefaultTracerGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.92f, 0.65f), 0f),
                    new GradientColorKey(new Color(1f, 0.35f, 0.12f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private TrailRenderer CreateTracer()
        {
            var go = new GameObject($"{name} Tracer");
            go.transform.SetParent(null);

            var trail = go.AddComponent<TrailRenderer>();
            trail.material = tracerMaterial;
            trail.colorGradient = tracerColour;
            trail.time = Tunables.I.EnemyTracerDuration;
            trail.startWidth = Tunables.I.EnemyTracerWidth;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.1f;
            trail.emitting = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            go.SetActive(false);
            return trail;
        }

        private void OnDrawGizmosSelected()
        {
            if (muzzle == null) return;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawRay(muzzle.position, muzzle.forward * 2f);
        }
    }
}
