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

        public EnemyKind Kind => kind;

        /// <summary>False once this enemy is dead. A corpse must not count toward a "not alone" check.</summary>
        public bool IsAlive => _health == null || !_health.IsDead;

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();
        }

        /// <summary>Count of alive (non-corpse) enemies of <paramref name="kind"/> currently in the scene. Shared by <see cref="DemoSpotterPopulationManager"/>'s population floor and <see cref="EnemyReinforcementSpawner"/>'s per-kind wave cap, so both enforce the same population ceiling against the same live count. Owner: island-demo-wrapup, 2026-09-10</summary>
        public static int CountAlive(EnemyKind kind)
        {
            var identities = Object.FindObjectsByType<EnemyIdentity>(FindObjectsSortMode.None);
            int count = 0;

            for (int i = 0; i < identities.Length; i++)
            {
                if (identities[i].Kind == kind && identities[i].IsAlive) count++;
            }

            return count;
        }
    }
}
