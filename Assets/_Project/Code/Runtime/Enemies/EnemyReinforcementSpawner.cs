using System.Collections.Generic;
using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// Spawns a scattered wave of enemies around a point and hands them straight into the chase.
    /// Shared, because two different Spotter triggers use it (the flare and the panic call) and
    /// the Zealot's alarm will want the same thing.
    ///
    /// The hard requirement from MRM-34 is that they <b>arrive spread out and never stack</b>. That
    /// is handled at spawn time rather than left to local avoidance: candidate points are sampled
    /// on the NavMesh, rejected if they are within <see cref="MoonlightTunables.SpotterReinforcementMinSpacing"/>
    /// of a point already taken, and each agent is given its own approach point. Avoidance alone
    /// does not fix a wave that spawned on top of itself.
    ///
    /// Owner: MRM-34, reused by MRM-57 (Vernon's distraction flares).
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Enemies/Enemy Reinforcement Spawner")]
    public sealed class EnemyReinforcementSpawner : MonoBehaviour
    {
        [Header("What to spawn")]
        [Tooltip("Enemy prefab. Normally the same prefab this component sits on — a Spotter calls Spotters.")]
        [SerializeField] private GameObject enemyPrefab;

        [Tooltip("Optional parent for spawned enemies, so the hierarchy does not fill with loose objects. Leave empty to spawn at scene root.")]
        [SerializeField] private Transform spawnParent;

        [Header("Wave size — defaults come from MoonlightTunables")]
        [SerializeField] private bool overrideCount;
        [SerializeField] private int countMinOverride = 3;
        [SerializeField] private int countMaxOverride = 10;

        [Header("Scatter")]
        [SerializeField] private bool overrideScatterRadius;
        [SerializeField] private float scatterRadiusOverride = 55f;

        [Tooltip("Nearest a reinforcement may spawn, in metres. Defaults come from MoonlightTunables.SpotterReinforcementMinDistance.")]
        [SerializeField] private bool overrideMinDistance;
        [SerializeField] private float minDistanceOverride = 45f;

        [Tooltip("How far a candidate point may be nudged onto the NavMesh before it is discarded.")]
        [SerializeField] private float navMeshSampleDistance = 4f;

        [Tooltip("Attempts per spawn before giving up on finding a valid, well-spaced point. Raise it only if waves come up short on cluttered terrain.")]
        [SerializeField] private int placementAttemptsPerSpawn = 12;

        [Header("Runaway guard")]
        [Tooltip("Stops reinforcements from calling reinforcements of their own. Ten Spotters each summoning ten is an exponential wave and a guaranteed frame-rate cliff; MRM-34's worst case is ten total, not ten per Spotter.")]
        [SerializeField] private bool suppressCallsOnSpawned = true;

        [Header("Events")]
        [Tooltip("Fires once per wave, with the number actually spawned (which can be lower than the roll if the terrain had no room).")]
        public UnityEvent<int> WaveSpawned;

        private readonly List<Vector3> _takenPoints = new List<Vector3>();

        private int CountMin => overrideCount ? countMinOverride : Tunables.I.SpotterReinforcementMin;
        private int CountMax => overrideCount ? countMaxOverride : Tunables.I.SpotterReinforcementMax;
        private float ScatterRadius => overrideScatterRadius ? scatterRadiusOverride : Tunables.I.SpotterReinforcementScatterRadius;
        private float MinDistance => overrideMinDistance ? minDistanceOverride : Tunables.I.SpotterReinforcementMinDistance;

        /// <summary>
        /// Spawn a wave around <paramref name="origin"/>. If <paramref name="target"/> is given the
        /// spawned enemies start already chasing it; if it is null they converge on the origin and
        /// fall into Blaze's own alert/search behaviour when they get there — which is MRM-34's
        /// "arriving without finding the player → they idle near the flare origin".
        /// </summary>
        public int SpawnWave(Vector3 origin, GameObject target)
        {
            return SpawnWave(origin, target, Random.Range(CountMin, CountMax + 1));
        }

        /// <summary>Spawn an exact number, for the panic call and for debug tooling.</summary>
        public int SpawnWave(Vector3 origin, GameObject target, int count)
        {
            if (enemyPrefab == null)
            {
                Debug.LogWarning($"{name}: reinforcement spawn skipped — no enemy prefab assigned.", this);
                return 0;
            }

            _takenPoints.Clear();
            int spawned = 0;

            for (int i = 0; i < count; i++)
            {
                if (!TryFindSpawnPoint(origin, out Vector3 point)) continue;

                _takenPoints.Add(point);
                SpawnOne(point, origin, target);
                spawned++;
            }

            if (spawned < count)
            {
                Debug.LogWarning(
                    $"{name}: wanted {count} reinforcements but only placed {spawned} — not enough " +
                    $"NavMesh inside {ScatterRadius}m of the call. Widen the scatter radius or check the bake.", this);
            }

            WaveSpawned?.Invoke(spawned);
            return spawned;
        }

        private void SpawnOne(Vector3 point, Vector3 origin, GameObject target)
        {
            // Face the call, so a wave that spawns behind the player still reads as converging on
            // the flare rather than as enemies popping in facing arbitrary directions.
            Vector3 toOrigin = origin - point;
            toOrigin.y = 0f;
            Quaternion rotation = toOrigin.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toOrigin)
                : Quaternion.identity;

            GameObject spawned = Instantiate(enemyPrefab, point, rotation, spawnParent);

            if (suppressCallsOnSpawned)
            {
                foreach (var caller in spawned.GetComponentsInChildren<IReinforcementCaller>(true))
                {
                    caller.SuppressCall();
                }
            }

            var blaze = spawned.GetComponent<BlazeAI>();
            if (blaze == null) return;

            // Deferred by a frame on purpose. Instantiate runs Awake immediately but Start does not
            // run until the end of the frame, and Blaze finishes wiring itself up in Start —
            // handing it a target before that is a null-reference waiting for a slow frame. One
            // frame of delay is invisible; the crash is not.
            StartCoroutine(EngageNextFrame(blaze, target));
        }

        private System.Collections.IEnumerator EngageNextFrame(BlazeAI blaze, GameObject target)
        {
            yield return null;

            if (blaze == null) yield break;

            if (target != null)
            {
                // randomizePoint asks Blaze to give this agent its own approach point around the
                // target instead of the same one every agent gets — the second half of not stacking.
                blaze.SetEnemy(target, true, true);
            }
            else if (blaze.waypoints != null)
            {
                blaze.waypoints.randomize = true;
                blaze.waypoints.randomizeRadius = ScatterRadius;
            }
        }

        private bool TryFindSpawnPoint(Vector3 origin, out Vector3 point)
        {
            for (int attempt = 0; attempt < placementAttemptsPerSpawn; attempt++)
            {
                // Sampled over an annulus (MinDistance..ScatterRadius), not a full disc — MRM-76:
                // reinforcements must land out past the player's usual sightline and run in, not pop
                // in beside the flare. Square-rooting the squared-radius range keeps the distribution
                // uniform over the ring's area instead of bunching near the inner edge.
                float distance = Mathf.Sqrt(Random.Range(MinDistance * MinDistance, ScatterRadius * ScatterRadius));
                Vector2 disc = Random.insideUnitCircle.normalized * distance;
                Vector3 candidate = origin + new Vector3(disc.x, 0f, disc.y);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                {
                    continue;
                }

                if (IsTooCloseToTakenPoint(hit.position)) continue;

                point = hit.position;
                return true;
            }

            point = default;
            return false;
        }

        private bool IsTooCloseToTakenPoint(Vector3 candidate)
        {
            float minSpacingSqr = Tunables.I.SpotterReinforcementMinSpacing * Tunables.I.SpotterReinforcementMinSpacing;

            for (int i = 0; i < _takenPoints.Count; i++)
            {
                if ((_takenPoints[i] - candidate).sqrMagnitude < minSpacingSqr) return true;
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, ScatterRadius);
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, MinDistance);
        }
    }
}
