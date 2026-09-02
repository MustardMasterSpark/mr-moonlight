using System.Collections;
using BlazeAISpace;
using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.Events;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// The firing rhythm of a ranged enemy: hold the aim, fire, pause, fire, then lock into a
    /// reload the player can exploit. MRM-34 specifies it for the Spotter; it is written shared
    /// because it is the same shape for any gun-carrying enemy and only the numbers change.
    ///
    /// <b>Why the rhythm lives here and not in Blaze's own timers.</b> Blaze can pace shots itself
    /// (<c>attackInIntervals</c>, and the cover shooter's <c>totalShootTime</c> /
    /// <c>delayBetweenEachShot</c>), but both express the cadence as randomised windows, and
    /// MRM-34 asks for something exact — <i>two</i> shots, then a reload, every time. Deriving
    /// "exactly two" from a randomised total-time window is arithmetic that silently breaks the
    /// moment a tunable moves. So Blaze's <c>attackEvent</c> is treated as "you may open fire" and
    /// the burst itself is one readable coroutine here. Blaze keeps everything it is genuinely
    /// better at: closing to engagement distance, facing the target, line-of-sight checks, and
    /// deciding when to attack at all.
    ///
    /// <b>Why AttackStateBehaviour and not CoverShooterBehaviour.</b> Blaze's cover shooter is the
    /// obvious fit on paper and MRM-34's triage note points at it — but it needs real cover
    /// objects registered with <c>BlazeAICoverManager</c>, and the island has none placed. With no
    /// cover in the world it degrades to a plain ranged attacker anyway. The plain attack state
    /// gets the same result today with far fewer moving parts; swapping the cover shooter in later
    /// only changes which UnityEvent calls <see cref="RequestFire"/>. Owner: MRM-34.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Mr. Moonlight/Enemies/Enemy Ranged Attack")]
    public sealed class EnemyRangedAttack : MonoBehaviour
    {
        [Header("Weapon")]
        [SerializeField] private EnemyFirearm firearm;

        [Header("Animator state names")]
        [Tooltip("Animator state played per shot. Must match a state name in the enemy's Animator Controller — Blaze crossfades by name.")]
        [SerializeField] private string shootState = "Shoot";

        [Tooltip("Animator state played during the reload lock.")]
        [SerializeField] private string reloadState = "Reload";

        [SerializeField] private float animationBlendTime = 0.12f;

        [Header("Rhythm — defaults come from MoonlightTunables")]
        [SerializeField] private bool overrideShotsBeforeReload;
        [SerializeField] private int shotsBeforeReloadOverride = 2;

        [SerializeField] private bool overrideMissChance;
        [Range(0f, 1f)]
        [SerializeField] private float missChanceOverride = 0.3f;

        [Header("Events")]
        [Tooltip("Fires as the burst begins, before the aim delay. Hook the muzzle-flash windup or an aim audio cue here.")]
        public UnityEvent BurstStarted;

        [Tooltip("Fires on each shot actually leaving the barrel.")]
        public UnityEvent Fired;

        [Tooltip("Fires when the reload lock begins — the player's window to close or reposition.")]
        public UnityEvent ReloadStarted;

        private BlazeAI _blaze;
        private EnemyAudioHooks _audio;
        private EnemyHealth _health;
        private Coroutine _burst;

        private int ShotsBeforeReload =>
            overrideShotsBeforeReload ? shotsBeforeReloadOverride : Tunables.I.SpotterShotsBeforeReload;

        private float MissChance =>
            overrideMissChance ? missChanceOverride : Tunables.I.SpotterMissChance;

        /// <summary>True from the first trigger pull until the reload lock ends.</summary>
        public bool IsBursting => _burst != null;

        /// <summary>
        /// How long one complete aim → fire → pause → fire → reload cycle takes. Written into
        /// Blaze's attack duration at <see cref="Awake"/> so the two can never drift apart when a
        /// tunable moves.
        /// </summary>
        public float CycleDuration =>
            Tunables.I.SpotterAimDelay
            + Mathf.Max(0, ShotsBeforeReload - 1) * Tunables.I.SpotterInterShotDelay
            + Tunables.I.SpotterReloadDuration;

        private void Awake()
        {
            _blaze = GetComponent<BlazeAI>();
            _audio = GetComponent<EnemyAudioHooks>();
            _health = GetComponent<EnemyHealth>();

            if (firearm == null) firearm = GetComponentInChildren<EnemyFirearm>(true);

            SyncBlazeAttackDuration();
        }

        /// <summary>
        /// Blaze's <c>attackEvent</c> (or the cover shooter's <c>shootEvent</c>) is wired here.
        /// Repeat calls while a burst or reload is already running are ignored, which is what
        /// makes the "exactly two shots" guarantee hold no matter how Blaze paces its cycles.
        /// </summary>
        public void RequestFire()
        {
            if (IsBursting) return;
            if (_health != null && _health.IsDead) return;
            if (firearm == null) return;

            _burst = StartCoroutine(RunBurst());
        }

        /// <summary>Abandon the burst immediately — used on death so a corpse does not keep firing.</summary>
        public void CancelBurst()
        {
            if (_burst == null) return;
            StopCoroutine(_burst);
            _burst = null;
        }

        private IEnumerator RunBurst()
        {
            BurstStarted?.Invoke();

            // The aim hold is the whole tell. Without it the player has no frame in which to break
            // line of sight, and being shot stops reading as a decision the enemy made.
            PlayState(shootState);
            yield return new WaitForSeconds(Tunables.I.SpotterAimDelay);

            for (int shot = 0; shot < ShotsBeforeReload; shot++)
            {
                if (_health != null && _health.IsDead) break;

                // Replay the shoot state per barrel. overplay clears Blaze's cached state name so
                // the same animation can be crossfaded to twice in a row instead of being skipped.
                if (shot > 0) PlayState(shootState, overplay: true);

                FireOnce();

                if (shot < ShotsBeforeReload - 1)
                {
                    yield return new WaitForSeconds(Tunables.I.SpotterInterShotDelay);
                }
            }

            ReloadStarted?.Invoke();
            PlayState(reloadState);
            _audio?.PlayReload();

            yield return new WaitForSeconds(Tunables.I.SpotterReloadDuration);

            _burst = null;
        }

        private void FireOnce()
        {
            Vector3 aimPoint = ResolveAimPoint();

            // Rolled per shot, not per burst: MRM-34 asks for "roughly 30% of shots miss", and
            // rolling once per burst would make both barrels miss together, which reads as the
            // enemy being broken rather than as a near miss.
            bool miss = Random.value < MissChance;

            firearm.Fire(aimPoint, miss, Tunables.I.SpotterMissAngle);

            _audio?.PlayFire();
            Fired?.Invoke();
        }

        private Vector3 ResolveAimPoint()
        {
            // enemyColPoint is Blaze's own resolved aim point on the target's collider — using it
            // rather than the target's origin keeps the shot aimed at the body, not at the feet.
            if (_blaze != null && _blaze.enemyToAttack != null)
            {
                return _blaze.enemyColPoint != Vector3.zero
                    ? _blaze.enemyColPoint
                    : _blaze.enemyToAttack.transform.position;
            }

            return firearm.Muzzle != null
                ? firearm.Muzzle.position + firearm.Muzzle.forward * Tunables.I.SpotterShotRange
                : transform.position + transform.forward * Tunables.I.SpotterShotRange;
        }

        private void PlayState(string state, bool overplay = false)
        {
            if (string.IsNullOrEmpty(state) || _blaze == null || _blaze.animManager == null) return;
            _blaze.animManager.Play(state, animationBlendTime, overplay);
        }

        /// <summary>
        /// Push the derived cycle length into Blaze's attack state so Blaze does not re-trigger an
        /// attack in the middle of our burst. Also pushes the engagement distance, for the same
        /// reason: one number, one owner.
        /// </summary>
        private void SyncBlazeAttackDuration()
        {
            if (_blaze == null) return;

            if (_blaze.attackStateBehaviour is AttackStateBehaviour attack)
            {
                if (attack.attacks != null && attack.attacks.Length > 0)
                {
                    attack.attacks[0].attackDuration = CycleDuration;
                }

                attack.distanceFromEnemy = Tunables.I.SpotterEngagementDistance;
                attack.attackDistance = Tunables.I.SpotterEngagementDistance;
            }

            if (_blaze.coverShooterBehaviour is CoverShooterBehaviour cover)
            {
                cover.distanceFromEnemy = Tunables.I.SpotterEngagementDistance;
                cover.attackDistance = Tunables.I.SpotterEngagementDistance;
            }
        }
    }
}
