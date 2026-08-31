using UnityEngine;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Sandbox-only debug tool: deducts from or restores a chosen pool (Health or Stamina) on
    /// whatever <see cref="PlayerStats"/> enters this trigger, re-applying at most once every
    /// <see cref="reTriggerDelay"/> seconds while it stays inside. Lets Carlos test Health
    /// damage/recovery and manually poke Stamina before real damage sources (enemies, traps,
    /// items) exist. Not part of the shipped game - same "debug tool, not shipped" category as
    /// MRM-8's InputDebugOverlay. Set the collider to Is Trigger. Owner: MRM-12
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class StatDebugPoolZone : MonoBehaviour
    {
        private enum TargetPool
        {
            Health,
            Stamina
        }

        [SerializeField] private TargetPool targetPool = TargetPool.Health;
        [SerializeField] private bool restores = false;
        [SerializeField] private float amountPerHit = 10f;
        [SerializeField] private float reTriggerDelay = 1f;

        private float _lastHitTime = float.NegativeInfinity;

        private void OnTriggerEnter(Collider other)
        {
            TryApply(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (Time.time - _lastHitTime >= reTriggerDelay)
            {
                TryApply(other);
            }
        }

        private void TryApply(Collider other)
        {
            // MRM-70 (2026-08-31): PlayerStats lives on the "MrMoonlight Systems" child, not on
            // an ancestor of the collider, so GetComponentInParent could never find it - look up
            // from the root instead, same pattern the bridges use. See
            // Docs/mrm9-burntwax-integration.md §3, "Our code sits on one child, deliberately".
            var stats = other.transform.root.GetComponentInChildren<PlayerStats>(true);
            if (stats == null)
            {
                return;
            }

            Stat stat = targetPool == TargetPool.Health ? stats.Health : stats.Stamina;

            if (restores)
            {
                stat.Restore(amountPerHit);
            }
            else
            {
                stat.Deplete(amountPerHit);
            }

            _lastHitTime = Time.time;
        }
    }
}
