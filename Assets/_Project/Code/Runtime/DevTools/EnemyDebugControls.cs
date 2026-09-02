using MrMoonlight.Combat;
using MrMoonlight.Enemies;
using MrMoonlight.Enemies.Spotter;
using UnityEngine;

namespace MrMoonlight.Runtime
{
    /// <summary>
    /// Right-click menu on any enemy to trigger the things that normally need systems that do not
    /// exist yet.
    ///
    /// This is not decoration — it is the only way to test half of MRM-34 right now. The player has
    /// no way to deal damage (MRM-32 is Backlog), so without this there is no way to reach the
    /// low-health panic call, the death drop, or the "lamp keeps burning after it falls" acceptance
    /// criterion at all. Delete it once the player can shoot back.
    ///
    /// Add it to an enemy in the scene, enter play mode, then use the component's context menu
    /// (the three dots on its header, or right-click the header). Owner: MRM-34.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Dev Tools/Enemy Debug Controls")]
    public sealed class EnemyDebugControls : MonoBehaviour
    {
        [Tooltip("Damage applied by the \"Damage\" context menu item.")]
        [SerializeField] private float debugDamage = 25f;

        [Tooltip("Who the enemy is told to attack. Leave empty to find the object tagged Player.")]
        [SerializeField] private GameObject target;

        [ContextMenu("Damage")]
        public void Damage()
        {
            if (!RequirePlayMode()) return;

            var health = GetComponent<EnemyHealth>();
            if (health == null) { Debug.LogWarning($"{name}: no EnemyHealth.", this); return; }

            health.TakeDamage(new DamageInfo(debugDamage, transform.position + Vector3.up, transform.forward, ResolveTarget()));
            Debug.Log($"{name}: took {debugDamage} — health now {health.CurrentHealth}/{health.MaxHealth} " +
                      $"({health.HealthFraction:P0})", this);
        }

        [ContextMenu("Kill")]
        public void Kill()
        {
            if (!RequirePlayMode()) return;

            var health = GetComponent<EnemyHealth>();
            if (health == null) { Debug.LogWarning($"{name}: no EnemyHealth.", this); return; }

            health.Kill(ResolveTarget());
            Debug.Log($"{name}: killed — lamp and shotgun should now be detached and falling.", this);
        }

        [ContextMenu("Attack Player")]
        public void AttackPlayer()
        {
            if (!RequirePlayMode()) return;

            GameObject player = ResolveTarget();
            if (player == null) { Debug.LogWarning($"{name}: no object tagged Player.", this); return; }

            var blaze = GetComponent<BlazeAI>();
            if (blaze == null) { Debug.LogWarning($"{name}: no BlazeAI.", this); return; }

            blaze.SetEnemy(player, true, true);
            Debug.Log($"{name}: engaging {player.name}.", this);
        }

        [ContextMenu("Fire Flare Now")]
        public void FireFlare()
        {
            if (!RequirePlayMode()) return;

            var flare = GetComponent<SpotterFlareCall>();
            if (flare == null) { Debug.LogWarning($"{name}: no SpotterFlareCall.", this); return; }

            if (flare.HasFlared)
            {
                Debug.Log($"{name}: already used its one flare — that is the rule, not a bug.", this);
                return;
            }

            flare.FireFlare();
            Debug.Log($"{name}: flare fired; reinforcements land shortly after the animation.", this);
        }

        [ContextMenu("Log State")]
        public void LogState()
        {
            var blaze = GetComponent<BlazeAI>();
            var health = GetComponent<EnemyHealth>();
            var flare = GetComponent<SpotterFlareCall>();
            var ranged = GetComponent<EnemyRangedAttack>();

            Debug.Log(
                $"{name}\n" +
                $"  blaze state = {(blaze == null ? "-" : blaze.state.ToString())}\n" +
                $"  target      = {(blaze == null || blaze.enemyToAttack == null ? "-" : blaze.enemyToAttack.name)}\n" +
                $"  animation   = {(blaze == null || blaze.animManager == null ? "-" : blaze.animManager.currentState)}\n" +
                $"  health      = {(health == null ? "-" : $"{health.CurrentHealth}/{health.MaxHealth}")}\n" +
                $"  alone       = {(flare == null ? "-" : flare.IsAlone().ToString())}\n" +
                $"  has flared  = {(flare == null ? "-" : flare.HasFlared.ToString())}\n" +
                $"  bursting    = {(ranged == null ? "-" : ranged.IsBursting.ToString())}", this);
        }

        private GameObject ResolveTarget()
        {
            if (target != null) return target;
            target = GameObject.FindGameObjectWithTag("Player");
            return target;
        }

        private bool RequirePlayMode()
        {
            if (Application.isPlaying) return true;
            Debug.LogWarning($"{name}: enter play mode first — this needs the runtime running.", this);
            return false;
        }
    }
}
