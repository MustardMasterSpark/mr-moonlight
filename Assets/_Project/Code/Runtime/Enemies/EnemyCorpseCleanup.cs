using System.Collections;
using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.AI;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// Strips a dead enemy down to an inert corpse: every collider disabled (so the player walks
    /// straight through it instead of getting blocked) and every behaviour that cost CPU while it
    /// was alive — the Blaze state machine, ranged attack, reinforcement/flare/panic calls, patrol,
    /// debug tools — turned off with it. The GameObject itself is never touched; Carlos's ask was a
    /// corpse that stays around looking like a corpse, not one that vanishes.
    ///
    /// Runs on a delay (<see cref="MoonlightTunables.EnemyCorpseCleanupDelay"/>) rather than
    /// instantly on death, deliberately: this fires from <see cref="EnemyHealth.Died"/>, which runs
    /// <i>before</i> Blaze's own death handling (ragdoll settle, the keyframed death animation) —
    /// disabling BlazeAI/NavMeshAgent out from under that sequence while it's still running would
    /// visibly glitch the death. Leaves Animator, AudioSource, GoreSimulator, EnemyIdentity,
    /// EnemyHealth and EnemyDeathDrop alone — the last one may still have its own ground-snap
    /// coroutine in flight for a dropped weapon.
    /// Owner: island-demo-wrapup, 2026-09-10.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Enemies/Enemy Corpse Cleanup")]
    public sealed class EnemyCorpseCleanup : MonoBehaviour
    {
        /// <summary>Wired to <see cref="EnemyHealth.Died"/>.</summary>
        public void Cleanup()
        {
            StartCoroutine(CleanupAfterDelay());
        }

        private IEnumerator CleanupAfterDelay()
        {
            yield return new WaitForSeconds(Tunables.I.EnemyCorpseCleanupDelay);

            foreach (var col in GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            if (TryGetComponent(out NavMeshAgent agent)) agent.enabled = false;
            if (TryGetComponent(out BlazeAI blaze)) blaze.enabled = false;
            if (TryGetComponent(out BlazeAISpareState spareState)) spareState.enabled = false;

            foreach (var behaviour in GetComponents<BlazeAISpace.BlazeBehaviour>())
            {
                behaviour.enabled = false;
            }

            if (TryGetComponent(out EnemyRangedAttack rangedAttack)) rangedAttack.enabled = false;
            if (TryGetComponent(out EnemyFirearm firearm)) firearm.enabled = false;
            if (TryGetComponent(out EnemyReinforcementSpawner spawner)) spawner.enabled = false;
            if (TryGetComponent(out Spotter.SpotterFlareCall flareCall)) flareCall.enabled = false;
            if (TryGetComponent(out Spotter.SpotterPanicCall panicCall)) panicCall.enabled = false;
            if (TryGetComponent(out EnemyPatrolRoute patrolRoute)) patrolRoute.enabled = false;
            if (TryGetComponent(out EnemyAudioHooks audioHooks)) audioHooks.enabled = false;
            if (TryGetComponent(out MrMoonlight.Runtime.EnemyDebugControls debugControls)) debugControls.enabled = false;
        }
    }
}
