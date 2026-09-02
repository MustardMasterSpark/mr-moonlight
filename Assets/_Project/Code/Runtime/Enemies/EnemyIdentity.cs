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
    }
}
