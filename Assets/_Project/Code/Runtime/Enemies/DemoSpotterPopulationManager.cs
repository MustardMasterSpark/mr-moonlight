using System.Collections.Generic;
using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.AI;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// DEMO-ONLY, LEGACY — added 2026-09-10 for the class-demo wrap-up, on top of the MRM-34
    /// Spotter build that is itself already flagged legacy (see CLAUDE.md / glossary.md: Spotter
    /// the enemy type survives into the capstone, this AI/implementation does not). Do not build
    /// new systems that depend on this component; when the Spotter is rebuilt from scratch this
    /// whole file should go with it.
    ///
    /// Replaces the ~100 hand-placed Enemy_Spotter instances that used to sit scattered across the
    /// island (always simulating, regardless of where the player was — a real perf cost for a
    /// build this close to the deadline). Instead, keeps a floor of
    /// <see cref="MoonlightTunables.SpotterPopulationMin"/> Spotters alive near the player at all
    /// times: it counts live Spotters (including ones spawned by a flare or panic call — this
    /// doesn't own those, it just polls) and tops up the difference on a timer, placing new ones on
    /// the NavMesh in an annulus around the player between
    /// <see cref="MoonlightTunables.SpotterPopulationMinSpawnDistance"/> and
    /// <see cref="MoonlightTunables.SpotterPopulationSpawnRadius"/> metres out — close enough to
    /// matter, far enough not to pop in on screen. A flare wave can push the count past the floor
    /// (Carlos's explicit call — that's fine, the floor is a minimum, not a cap); this only ever
    /// adds enemies back down toward it, never removes any.
    ///
    /// <para><b>Relocation (2026-09-10 addendum).</b> The floor above is a <i>global</i> alive
    /// count, not a "how many are near the player right now" count — so if the player crosses the
    /// island fast enough, the existing 10 can all get left behind in the region he just quit,
    /// <c>TopUp</c> sees the floor already met and spawns nothing, and he walks into an empty area
    /// with none to encounter. A second, much slower loop (<see cref="MoonlightTunables.SpotterRelocationCheckInterval"/>
    /// — periodic on purpose, this does not need to run every frame or even every TopUp tick) checks
    /// whether enough Spotters are still within <see cref="MoonlightTunables.SpotterRelocationNearbyRadius"/>
    /// of the player, and if not, warps the farthest stragglers into a fresh spawn-ring point instead
    /// of instantiating more (the global floor is already met) — same NavMesh placement TopUp uses.
    /// A Spotter already mid-fight (Blaze state == attack) is never relocated out from under itself.</para>
    ///
    /// <para><b>Aggression escalation (2026-09-10 addendum).</b> Carlos's ask: keep the demo getting
    /// more dangerous, not flatter, as the player racks up kills. At
    /// <see cref="MoonlightTunables.SpotterAggressionKillThreshold1"/> total enemy kills the
    /// population floor rises and every subsequently placed Spotter (fresh spawn or relocated
    /// straggler) is handed straight into Blaze's attack state chasing the player
    /// (<c>BlazeAI.SetEnemy(player, true, true)</c> — the same call <see cref="EnemyReinforcementSpawner"/>
    /// uses for a flare wave) instead of waiting to spot him on its own. At
    /// <see cref="MoonlightTunables.SpotterAggressionKillThreshold2"/> both the floor and the top-up
    /// cadence escalate further on top of that. Kills are counted from <see cref="EnemyHealth.AnyDied"/>
    /// directly (not through <see cref="Events.ObjectiveTracker"/>) so this stays self-contained and
    /// works even if the level script's objective list changes.</para>
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Enemies/Demo Spotter Population Manager (LEGACY, demo-only)")]
    public sealed class DemoSpotterPopulationManager : MonoBehaviour
    {
        [Tooltip("Enemy_Spotter prefab to top up with.")]
        [SerializeField] private GameObject spotterPrefab;

        [Tooltip("Spawns are placed around this transform. Leave empty to auto-find the GameObject tagged Player.")]
        [SerializeField] private Transform player;

        [Tooltip("Optional parent for spawned Spotters, so the hierarchy does not fill with loose objects. Leave empty to spawn at scene root.")]
        [SerializeField] private Transform spawnParent;

        private Coroutine _populationLoop;
        private Coroutine _relocationLoop;
        private int _totalKills;

        private static readonly List<EnemyIdentity> ScratchStragglers = new List<EnemyIdentity>();

        private bool IsAggressiveTier1 => _totalKills >= Tunables.I.SpotterAggressionKillThreshold1;
        private bool IsAggressiveTier2 => _totalKills >= Tunables.I.SpotterAggressionKillThreshold2;

        /// <summary>The floor's raw target before the hard population cap is applied — see <see cref="CurrentPopulationFloor"/>.</summary>
        private int RawPopulationFloor
        {
            get
            {
                if (IsAggressiveTier2) return Tunables.I.SpotterPopulationMin + Tunables.I.SpotterAggressionPopulationBonusTier2;
                if (IsAggressiveTier1) return Tunables.I.SpotterPopulationMin + Tunables.I.SpotterAggressionPopulationBonusTier1;
                return Tunables.I.SpotterPopulationMin;
            }
        }

        /// <summary>Clamped by <see cref="MoonlightTunables.SpotterPopulationMax"/> so an aggression-tier bonus can never top the population up past the hard cap on its own — the cap is enforced here for top-up/relocation and separately in <see cref="EnemyReinforcementSpawner"/> for flare/panic waves.</summary>
        private int CurrentPopulationFloor => Mathf.Min(RawPopulationFloor, Tunables.I.SpotterPopulationMax);

        private float CurrentPopulationCheckInterval
        {
            get
            {
                if (IsAggressiveTier2) return Tunables.I.SpotterAggressionCheckIntervalTier2;
                if (IsAggressiveTier1) return Tunables.I.SpotterAggressionCheckIntervalTier1;
                return Tunables.I.SpotterPopulationCheckInterval;
            }
        }

        private void OnEnable()
        {
            if (player == null)
            {
                GameObject found = GameObject.FindGameObjectWithTag("Player");
                if (found != null) player = found.transform;
            }

            EnemyHealth.AnyDied += HandleAnyEnemyDied;
            _populationLoop = StartCoroutine(PopulationLoop());
            _relocationLoop = StartCoroutine(RelocationLoop());
        }

        private void OnDisable()
        {
            EnemyHealth.AnyDied -= HandleAnyEnemyDied;

            if (_populationLoop != null) StopCoroutine(_populationLoop);
            _populationLoop = null;

            if (_relocationLoop != null) StopCoroutine(_relocationLoop);
            _relocationLoop = null;
        }

        private void HandleAnyEnemyDied(EnemyHealth enemy) => _totalKills++;

        private System.Collections.IEnumerator PopulationLoop()
        {
            // Fill immediately so the demo never opens with zero Spotters on the island.
            TopUp();

            while (true)
            {
                // Interval is re-read every cycle (not cached into one WaitForSeconds) since it
                // shortens once an aggression tier is crossed mid-loop.
                yield return new WaitForSeconds(CurrentPopulationCheckInterval);
                TopUp();
            }
        }

        private System.Collections.IEnumerator RelocationLoop()
        {
            var wait = new WaitForSeconds(Tunables.I.SpotterRelocationCheckInterval);

            while (true)
            {
                yield return wait;
                RelocateStragglers();
            }
        }

        private void TopUp()
        {
            if (spotterPrefab == null || player == null) return;

            int needed = CurrentPopulationFloor - CountAliveSpotters();

            for (int i = 0; i < needed; i++)
            {
                if (TryFindSpawnPoint(out Vector3 point))
                {
                    GameObject spotter = Instantiate(spotterPrefab, point, Quaternion.identity, spawnParent);
                    if (IsAggressiveTier1) StartCoroutine(SendStraightToPlayer(spotter));
                }
            }
        }

        /// <summary>
        /// Finds every alive Spotter that has drifted more than <see cref="MoonlightTunables.SpotterRelocationNearbyRadius"/>
        /// from the player and is not currently mid-fight, and warps the farthest of them (up to
        /// however many are missing from the nearby count) into a fresh spawn-ring point around the
        /// player — reusing them instead of spawning more, since the global floor is already met.
        /// </summary>
        private void RelocateStragglers()
        {
            if (spotterPrefab == null || player == null) return;

            var identities = Object.FindObjectsByType<EnemyIdentity>(FindObjectsSortMode.None);
            float nearbyRadiusSqr = Tunables.I.SpotterRelocationNearbyRadius * Tunables.I.SpotterRelocationNearbyRadius;

            ScratchStragglers.Clear();
            int nearbyCount = 0;

            for (int i = 0; i < identities.Length; i++)
            {
                EnemyIdentity identity = identities[i];
                if (identity.Kind != EnemyKind.Spotter || !identity.IsAlive) continue;

                float sqrDistance = (identity.transform.position - player.position).sqrMagnitude;
                if (sqrDistance <= nearbyRadiusSqr)
                {
                    nearbyCount++;
                    continue;
                }

                // Never yank a Spotter out of a fight the player can see just because he wandered
                // off since — mid-attack means it already found him, relocating it would just look
                // like it teleported away from its own target.
                if (identity.TryGetComponent(out BlazeAI blaze) && blaze.state == BlazeAI.State.attack) continue;

                ScratchStragglers.Add(identity);
            }

            int deficit = CurrentPopulationFloor - nearbyCount;
            if (deficit <= 0 || ScratchStragglers.Count == 0) return;

            // Farthest-first, so the stragglers that would take longest to walk back on their own
            // are the ones pulled in.
            ScratchStragglers.Sort((a, b) =>
                (b.transform.position - player.position).sqrMagnitude
                    .CompareTo((a.transform.position - player.position).sqrMagnitude));

            int relocated = 0;
            for (int i = 0; i < ScratchStragglers.Count && relocated < deficit; i++)
            {
                if (!TryFindSpawnPoint(out Vector3 point)) continue;

                EnemyIdentity straggler = ScratchStragglers[i];
                if (straggler.TryGetComponent(out NavMeshAgent agent) && agent.isOnNavMesh)
                {
                    agent.Warp(point);
                }
                else
                {
                    straggler.transform.position = point;
                }

                if (IsAggressiveTier1) StartCoroutine(SendStraightToPlayer(straggler.gameObject));
                relocated++;
            }

            ScratchStragglers.Clear();
        }

        /// <summary>
        /// Puts a Spotter straight into Blaze's attack state on the player, one frame after it is
        /// placed. The one-frame delay matters even for a relocated (already-Start'd) Spotter, since
        /// it is also used right after <c>Instantiate</c>, where Blaze has not finished wiring itself
        /// up in <c>Start</c> yet — same trap <see cref="EnemyReinforcementSpawner.EngageNextFrame"/>
        /// documents.
        /// </summary>
        private System.Collections.IEnumerator SendStraightToPlayer(GameObject spotter)
        {
            yield return null;

            if (spotter == null || player == null) yield break;
            if (spotter.TryGetComponent(out BlazeAI blaze)) blaze.SetEnemy(player.gameObject, true, true);
        }

        private static int CountAliveSpotters() => EnemyIdentity.CountAlive(EnemyKind.Spotter);

        private bool TryFindSpawnPoint(out Vector3 point)
        {
            float minDistance = Tunables.I.SpotterPopulationMinSpawnDistance;
            float maxDistance = Tunables.I.SpotterPopulationSpawnRadius;

            for (int attempt = 0; attempt < Tunables.I.SpotterPopulationPlacementAttempts; attempt++)
            {
                // Annulus, not a full disc — matches EnemyReinforcementSpawner's approach: keeps
                // spawns out past the player's usual sightline instead of popping in beside them.
                float distance = Mathf.Sqrt(Random.Range(minDistance * minDistance, maxDistance * maxDistance));
                Vector2 disc = Random.insideUnitCircle.normalized * distance;
                Vector3 candidate = player.position + new Vector3(disc.x, 0f, disc.y);

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, Tunables.I.SpotterPopulationNavMeshSampleDistance, NavMesh.AllAreas))
                {
                    point = hit.position;
                    return true;
                }
            }

            point = default;
            return false;
        }
    }
}
