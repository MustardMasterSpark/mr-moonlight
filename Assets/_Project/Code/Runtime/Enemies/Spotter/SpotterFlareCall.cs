using BlazeAISpace;
using MrMoonlight.Data;
using MrMoonlight.VFX;
using UnityEngine;
using UnityEngine.Events;

namespace MrMoonlight.Enemies.Spotter
{
    /// <summary>
    /// The Spotter's signature move (MRM-34). A Spotter who is fighting the player <b>and has no
    /// other Spotter nearby</b> runs a timer; if he survives it, he fires a flare and 3–10 more
    /// Spotters converge on where it went up. He can only ever do it once.
    ///
    /// <b>Why this is not a</b> <c>BlazeBehaviour</c><b> subclass.</b> MRM-29 records the intended
    /// pattern as "write Carlos's spec as a <c>BlazeBehaviour</c> subclass", and that is right for
    /// anything that <i>replaces</i> a state. It does not fit here: reading Blaze's source,
    /// <c>RunBehaviour</c> only ever ticks the single behaviour assigned to the current state slot
    /// (<c>BlazeAI.cs</c> ~line 2314), so a <c>BlazeBehaviour</c> can substitute for a state but
    /// cannot run <i>alongside</i> one — and the flare condition has to be watched while the normal
    /// attack state is driving. So this is a plain component that monitors, and the flare itself is
    /// raised through Blaze's own sanctioned custom-state hook, <c>SetSpareState</c>, which
    /// interrupts cleanly, plays the animation, and hands control back on a timer.
    ///
    /// The spare state is registered from code rather than left for the inspector, so dropping the
    /// prefab into a scene is genuinely all the setup there is.
    ///
    /// Owner: MRM-34. The flare projectile itself is reused by MRM-57 (Vernon's distraction).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BlazeAI))]
    [AddComponentMenu("Mr. Moonlight/Enemies/Spotter/Spotter Flare Call")]
    public sealed class SpotterFlareCall : MonoBehaviour, IReinforcementCaller
    {
        /// <summary>Name of the Blaze spare state this component registers and drives.</summary>
        private const string FlareSpareStateName = "MoonlightSpotterFlare";

        [Header("Wiring")]
        [SerializeField] private EnemyReinforcementSpawner spawner;

        [Tooltip("Empty GameObject the flare is fired from — normally on the flare gun's muzzle. Falls back to this transform.")]
        [SerializeField] private Transform flareMuzzle;

        [Tooltip("The flare projectile prefab. Leave empty and the reinforcements still spawn — only the visual is skipped.")]
        [SerializeField] private FlareProjectile flarePrefab;

        [Header("Animation")]
        [Tooltip("Animator state name for the flare-firing animation. Must exist in the enemy's Animator Controller.")]
        [SerializeField] private string flareState = "Flare";

        [Tooltip("Seconds into the flare state at which the projectile actually leaves the gun.")]
        [SerializeField] private float launchDelay = 0.9f;

        [Header("Conditions — defaults come from MoonlightTunables")]
        [SerializeField] private bool overrideAloneRadius;
        [SerializeField] private float aloneRadiusOverride = 25f;

        [SerializeField] private bool overrideFlareTimer;
        [SerializeField] private float flareTimerOverride = 8f;

        [Tooltip("Layers other Spotters live on. Used by the alone-check sphere.")]
        [SerializeField] private LayerMask enemyLayers = ~0;

        [Header("Debug")]
        [SerializeField] private bool showAloneRadius;

        [Header("Events")]
        [Tooltip("Fires the moment the flare leaves the gun.")]
        public UnityEvent FlareFired;

        private BlazeAI _blaze;
        private EnemyHealth _health;
        private EnemyAudioHooks _audio;
        private readonly Collider[] _aloneCheckBuffer = new Collider[16];

        private float _engagedTimer;
        private bool _suppressed;
        private bool _launchScheduled;

        /// <summary>True once this Spotter has used his one flare. MRM-34: each Spotter flares only once.</summary>
        public bool HasFlared { get; private set; }

        private float AloneRadius => overrideAloneRadius ? aloneRadiusOverride : Tunables.I.SpotterAloneCheckRadius;
        private float FlareTimer => overrideFlareTimer ? flareTimerOverride : Tunables.I.SpotterFlareTimer;

        public void SuppressCall() => _suppressed = true;

        private void Awake()
        {
            _blaze = GetComponent<BlazeAI>();
            _health = GetComponent<EnemyHealth>();
            _audio = GetComponent<EnemyAudioHooks>();

            if (spawner == null) spawner = GetComponent<EnemyReinforcementSpawner>();
            if (flareMuzzle == null) flareMuzzle = transform;

            RegisterSpareState();
        }

        private void Update()
        {
            if (_suppressed || HasFlared) return;
            if (_health != null && _health.IsDead) return;
            if (_blaze == null) return;

            // The timer only runs while he is actually fighting. A Spotter who is merely alone on
            // patrol has no reason to burn his one flare.
            if (_blaze.state != BlazeAI.State.attack || _blaze.enemyToAttack == null)
            {
                _engagedTimer = 0f;
                return;
            }

            if (!IsAlone())
            {
                _engagedTimer = 0f;
                return;
            }

            _engagedTimer += Time.deltaTime;
            if (_engagedTimer < FlareTimer) return;

            FireFlare();
        }

        /// <summary>
        /// Is this the only living Spotter inside the check sphere? Own colliders are skipped by
        /// identity, and corpses do not count — a Spotter standing over his dead partner is alone,
        /// which is exactly the moment the flare should go up.
        /// </summary>
        public bool IsAlone()
        {
            int found = Physics.OverlapSphereNonAlloc(
                transform.position, AloneRadius, _aloneCheckBuffer, enemyLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < found; i++)
            {
                Collider other = _aloneCheckBuffer[i];
                if (other == null) continue;

                var identity = other.GetComponentInParent<EnemyIdentity>();
                if (identity == null) continue;
                if (identity.gameObject == gameObject) continue;
                if (identity.Kind != EnemyKind.Spotter) continue;
                if (!identity.IsAlive) continue;

                return false;
            }

            return true;
        }

        /// <summary>Fire the flare now, ignoring the timer. Public so a scripted beat or debug tool can trigger it.</summary>
        public void FireFlare()
        {
            if (HasFlared || _blaze == null) return;

            HasFlared = true;
            _engagedTimer = 0f;

            // Blaze refuses a spare state while already in one, and while dead. Guarding here keeps
            // the "flared once" latch honest even if the call is refused.
            _blaze.SetSpareState(FlareSpareStateName);

            if (!_launchScheduled)
            {
                _launchScheduled = true;
                Invoke(nameof(LaunchAndCall), launchDelay);
            }
        }

        /// <summary>The payload half, split out so it lands mid-animation rather than on the first frame.</summary>
        private void LaunchAndCall()
        {
            _launchScheduled = false;

            Vector3 origin = flareMuzzle != null ? flareMuzzle.position : transform.position;

            if (flarePrefab != null)
            {
                Vector3 aim = flareMuzzle != null ? flareMuzzle.forward : transform.forward;
                FlareProjectile flare = Instantiate(flarePrefab, origin, Quaternion.LookRotation(aim));
                flare.Launch(aim);
            }

            _audio?.PlayFlareFire();
            FlareFired?.Invoke();

            if (spawner != null)
            {
                // The reinforcements converge on the Spotter, not on the flare's landing point —
                // MRM-34 says they run "toward where the flare was fired".
                spawner.SpawnWave(transform.position, _blaze != null ? _blaze.enemyToAttack : null);
            }
        }

        /// <summary>
        /// Add the flare spare state to Blaze at runtime if it is not already configured, so the
        /// prefab needs no inspector setup to work when dropped into a scene.
        /// </summary>
        private void RegisterSpareState()
        {
            var spare = GetComponent<BlazeAISpareState>();
            if (spare == null) spare = gameObject.AddComponent<BlazeAISpareState>();

            spare.spareStates ??= new SpareState[0];

            foreach (SpareState existing in spare.spareStates)
            {
                if (existing != null && existing.stateName == FlareSpareStateName) return;
            }

            var flare = new SpareState
            {
                stateName = FlareSpareStateName,
                animsToPlay = new[] { flareState },
                animT = 0.2f,
                playAudio = false,
                exitMethod = SpareState.ExitMethod.ExitAfterTime,
                exitTimer = Tunables.I.SpotterFlareAnimationDuration,
                enterEvent = new UnityEvent(),
                exitEvent = new UnityEvent()
            };

            var grown = new SpareState[spare.spareStates.Length + 1];
            spare.spareStates.CopyTo(grown, 0);
            grown[^1] = flare;
            spare.spareStates = grown;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showAloneRadius) return;
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, AloneRadius);
        }
    }
}
