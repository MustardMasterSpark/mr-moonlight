using System;
using UnityEngine;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// Marks a GameObject as an enemy of a particular <see cref="EnemyKind"/>, and gives other
    /// systems one cheap component to find instead of walking the hierarchy.
    ///
    /// Goes on the enemy's root, next to <see cref="EnemyHealth"/>. Owner: MRM-34.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Mr. Moonlight/Enemies/Enemy Identity")]
    public sealed class EnemyIdentity : MonoBehaviour
    {
        [Tooltip("Which enemy this is. Used by same-kind proximity checks such as the Spotter's alone-check.")]
        [SerializeField] private EnemyKind kind = EnemyKind.Spotter;

        private EnemyHealth _health;

        /// <summary>
        /// Fired when an enemy becomes active (placed in the scene or instantiated by any spawner) and
        /// when it goes inactive or is destroyed. One hook for every spawn path, so tooling such as
        /// <c>SessionLog</c> can track the live population without touching each spawner.
        /// Subscribers unsubscribe in OnDisable/OnDestroy; the reset below covers Enter Play Mode with
        /// domain reload off. Owner: MRM-84
        /// </summary>
        public static event Action<EnemyIdentity> AnySpawned;

        /// <summary>See <see cref="AnySpawned"/>.</summary>
        public static event Action<EnemyIdentity> AnyDespawned;

        public EnemyKind Kind => kind;

        /// <summary>False once this enemy is dead. A corpse must not count toward a "not alone" check.</summary>
        public bool IsAlive => _health == null || !_health.IsDead;

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();
        }

        private void OnEnable() => AnySpawned?.Invoke(this);

        private void OnDisable() => AnyDespawned?.Invoke(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStaticSubscribers()
        {
            AnySpawned = null;
            AnyDespawned = null;
        }

        /// <summary>Count of alive (non-corpse) enemies of <paramref name="kind"/> currently in the scene. Shared by <see cref="DemoSpotterPopulationManager"/>'s population floor and <see cref="EnemyReinforcementSpawner"/>'s per-kind wave cap, so both enforce the same population ceiling against the same live count. Owner: island-demo-wrapup, 2026-09-10</summary>
        public static int CountAlive(EnemyKind kind)
        {
            var identities = UnityEngine.Object.FindObjectsByType<EnemyIdentity>(FindObjectsSortMode.None);
            int count = 0;

            for (int i = 0; i < identities.Length; i++)
            {
                if (identities[i].Kind == kind && identities[i].IsAlive) count++;
            }

            return count;
        }
    }
}
