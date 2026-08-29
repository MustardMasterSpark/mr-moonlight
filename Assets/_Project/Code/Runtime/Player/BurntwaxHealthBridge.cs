using Burntwax;
using UnityEngine;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Makes <see cref="PlayerStats"/> the single source of truth for health while keeping
    /// Burntwax's <see cref="PlayerHealth"/> component present and rendering.
    ///
    /// <para>Burntwax ships its own health float on <see cref="PlayerHealth"/>, but Mr. Moonlight
    /// already had health in <see cref="PlayerStats"/> from MRM-12 — where it participates in the
    /// Stat/StatModifier stack (defense, item effects, debuff pools) and raises the death event
    /// that MRM-17's <c>DeathSequence</c> listens to. Keeping two floats would desync the moment
    /// anything damaged one and not the other.</para>
    ///
    /// <para>So <see cref="PlayerHealth"/> is put into reporting-only mode: damage and heal calls
    /// from Burntwax pickups and hazards are forwarded here, applied to the stat, and the result
    /// is pushed back for the health bar. The component stays on the player because Burntwax's
    /// pickups and teleporter use it as their "is this the player?" marker.</para>
    ///
    /// Owner: MRM-9 (controller swap), MRM-12 (stats), MRM-17 (death).
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public sealed class BurntwaxHealthBridge : MonoBehaviour
    {
        [Tooltip("Burntwax health component. Found in children if left unassigned.")]
        [SerializeField] private PlayerHealth playerHealth;

        private PlayerStats _stats;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();

            if (playerHealth == null)
            {
                // From the prefab root - see BurntwaxPlayerBridge for why. (MRM-9)
                playerHealth = transform.root.GetComponentInChildren<PlayerHealth>(true);
            }

            if (playerHealth == null)
            {
                Debug.LogError($"{nameof(BurntwaxHealthBridge)} on '{name}' found no {nameof(PlayerHealth)}. Burntwax pickups will not detect the player.", this);
                return;
            }

            playerHealth.ExternalHealthAuthority = true;
        }

        private void OnEnable()
        {
            if (playerHealth == null) return;
            playerHealth.OnDamaged += HandleDamaged;
            playerHealth.OnHealed += HandleHealed;
        }

        private void OnDisable()
        {
            if (playerHealth == null) return;
            playerHealth.OnDamaged -= HandleDamaged;
            playerHealth.OnHealed -= HandleHealed;
        }

        private void HandleDamaged(float amount) => _stats.Health.Deplete(amount);

        private void HandleHealed(float amount) => _stats.Health.Restore(amount);

        private void LateUpdate()
        {
            if (playerHealth == null) return;

            playerHealth.SetDisplayedHealth(_stats.Health.Value, _stats.Health.MaxValue);
        }
    }
}
