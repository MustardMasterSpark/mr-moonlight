using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.Events;

namespace MrMoonlight.Enemies.Spotter
{
    /// <summary>
    /// A wounded Spotter shouts for help. Hooked to <see cref="EnemyHealth.LowHealthReached"/>, it
    /// summons a small wave the first time he drops below the low-health threshold.
    ///
    /// <b>This is deliberately a separate trigger from</b> <see cref="SpotterFlareCall"/>, not a
    /// second entry point into it (Carlos, 2026-09-01). The two read as different things and should
    /// stay different: the flare is <i>proactive</i> — a Spotter who finds himself isolated decides
    /// to call the others, whether or not he is hurt. This is <i>reactive</i> — a Spotter who is
    /// about to die panics, whether or not he is alone. They have their own counts, their own
    /// once-only latches, and either can fire without the other. Folding them into one code path
    /// would make both harder to tune and would quietly couple "hurt" to "isolated".
    ///
    /// Owner: MRM-34.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    [AddComponentMenu("Mr. Moonlight/Enemies/Spotter/Spotter Panic Call")]
    public sealed class SpotterPanicCall : MonoBehaviour, IReinforcementCaller
    {
        [Header("Wiring")]
        [SerializeField] private EnemyReinforcementSpawner spawner;

        [Header("Wave size — defaults come from MoonlightTunables")]
        [SerializeField] private bool overrideCount;
        [SerializeField] private int countMinOverride = 1;
        [SerializeField] private int countMaxOverride = 3;

        [Header("Events")]
        public UnityEvent PanicCalled;

        private BlazeAI _blaze;
        private EnemyHealth _health;
        private EnemyAudioHooks _audio;
        private bool _suppressed;

        /// <summary>True once he has panicked. Like the flare, it only happens once.</summary>
        public bool HasCalled { get; private set; }

        private int CountMin => overrideCount ? countMinOverride : Tunables.I.SpotterPanicReinforcementMin;
        private int CountMax => overrideCount ? countMaxOverride : Tunables.I.SpotterPanicReinforcementMax;

        public void SuppressCall() => _suppressed = true;

        private void Awake()
        {
            _blaze = GetComponent<BlazeAI>();
            _health = GetComponent<EnemyHealth>();
            _audio = GetComponent<EnemyAudioHooks>();

            if (spawner == null) spawner = GetComponent<EnemyReinforcementSpawner>();
        }

        private void OnEnable()
        {
            if (_health != null) _health.LowHealthReached.AddListener(OnLowHealth);
        }

        private void OnDisable()
        {
            if (_health != null) _health.LowHealthReached.RemoveListener(OnLowHealth);
        }

        private void OnLowHealth()
        {
            if (_suppressed || HasCalled) return;
            if (_health != null && _health.IsDead) return;

            HasCalled = true;

            _audio?.PlayAlerted();
            PanicCalled?.Invoke();

            if (spawner == null) return;

            // No flare goes up here — this is a shout, not a signal. The wave still converges on
            // him and still picks up whoever he was fighting.
            spawner.SpawnWave(
                transform.position,
                _blaze != null ? _blaze.enemyToAttack : null,
                Random.Range(CountMin, CountMax + 1));
        }
    }
}
