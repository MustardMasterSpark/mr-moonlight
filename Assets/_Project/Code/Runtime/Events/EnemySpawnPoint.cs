using System.Collections;
using System.Collections.Generic;
using MrMoonlight.Data;
using MrMoonlight.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace MrMoonlight.Events
{
    /// <summary>
    /// A named place the event script can spawn enemies at: <c>spawn at=glade_ambush count=3</c>.
    /// Owner: MRM-11.
    ///
    /// <para><b>The prefab lives here, not in the script.</b> A text file cannot hold an asset
    /// reference, and threading one through a string lookup would just be Resources.Load wearing
    /// a hat. So the scene object carries the prefab and the script carries the name — which is
    /// also the division that lets Carlos re-point an ambush at a different enemy without
    /// touching the script.</para>
    ///
    /// <para>Deliberately <i>not</i> <see cref="EnemyReinforcementSpawner"/>. That one places a
    /// wave in an annulus <i>away</i> from its origin, because a flare call must not drop enemies
    /// in the player's lap; a placed spawn point wants the opposite — everyone as close to the
    /// marker as the NavMesh allows. Same idea, incompatible geometry.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Mr. Moonlight/Events/Enemy Spawn Point")]
    public sealed class EnemySpawnPoint : MonoBehaviour
    {
        private static readonly List<EnemySpawnPoint> Active = new List<EnemySpawnPoint>();

        [Tooltip("The name the event script spawns at. Defaults to the GameObject's name when left empty.")]
        [SerializeField] private string pointName;

        [Tooltip("Enemy prefab this point spawns. Normally Enemy_Spotter.")]
        [SerializeField] private GameObject enemyPrefab;

        [Tooltip("Enemies are scattered inside this radius, in metres, and nudged onto the NavMesh.")]
        [SerializeField] private float scatterRadius = 8f;

        [Tooltip("Closest two spawned enemies may start to each other, in metres. Stops a wave arriving stacked inside itself.")]
        [SerializeField] private float minSpacing = 2f;

        [Tooltip("Optional parent for spawned enemies, so the hierarchy does not fill with loose objects.")]
        [SerializeField] private Transform spawnParent;

        [Tooltip("How far a candidate point may be nudged onto the NavMesh before it is discarded.")]
        [SerializeField] private float navMeshSampleDistance = 4f;

        private readonly List<Vector3> _takenPoints = new List<Vector3>();

        public string PointName => string.IsNullOrWhiteSpace(pointName) ? name : pointName;

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        public static EnemySpawnPoint Find(string pointName)
        {
            if (string.IsNullOrWhiteSpace(pointName)) return null;

            for (int i = 0; i < Active.Count; i++)
            {
                if (string.Equals(Active[i].PointName, pointName.Trim(), System.StringComparison.OrdinalIgnoreCase))
                {
                    return Active[i];
                }
            }

            return null;
        }

        /// <summary>All spawn point names currently in the scene. For the event script validator's error messages.</summary>
        public static IReadOnlyList<EnemySpawnPoint> AllActive => Active;

        /// <summary>
        /// Spawns <paramref name="count"/> enemies and returns the ones that actually got placed.
        /// Fewer than asked for is a warning, not a failure — cluttered terrain legitimately runs
        /// out of NavMesh, and a short wave is better than a stuck level.
        /// </summary>
        public List<EnemyHealth> Spawn(int count, GameObject chaseTarget)
        {
            var spawned = new List<EnemyHealth>();

            if (enemyPrefab == null)
            {
                Debug.LogError($"[EventDirector] Spawn point '{PointName}' has no enemy prefab assigned — nothing spawned.", this);
                return spawned;
            }

            _takenPoints.Clear();

            for (int i = 0; i < count; i++)
            {
                if (!TryFindPoint(out Vector3 point)) continue;

                _takenPoints.Add(point);
                GameObject enemy = Instantiate(enemyPrefab, point, FacingRotation(point), spawnParent);

                if (chaseTarget != null && enemy.TryGetComponent(out BlazeAI blaze))
                {
                    StartCoroutine(EngageNextFrame(blaze, chaseTarget));
                }

                if (enemy.TryGetComponent(out EnemyHealth health)) spawned.Add(health);
            }

            if (spawned.Count < count)
            {
                Debug.LogWarning(
                    $"[EventDirector] Spawn point '{PointName}' wanted {count} enemies but placed {spawned.Count} — " +
                    $"not enough NavMesh within {scatterRadius}m. Widen the scatter radius, move the point, or check the bake.", this);
            }

            return spawned;
        }

        /// <summary>
        /// Deferred by a frame on purpose, copying <see cref="EnemyReinforcementSpawner"/>'s hard-won
        /// note: Instantiate runs Awake immediately but Start does not run until the end of the
        /// frame, and Blaze finishes wiring itself up in Start. Handing it a target before that is a
        /// null-reference waiting for a slow frame. <c>randomizePoint</c> asks Blaze to give each
        /// agent its own approach point, which is the second half of not arriving stacked.
        /// </summary>
        private static IEnumerator EngageNextFrame(BlazeAI blaze, GameObject target)
        {
            yield return null;

            if (blaze == null || target == null) yield break;
            blaze.SetEnemy(target, true, true);
        }

        private Quaternion FacingRotation(Vector3 point)
        {
            // Face outward from the marker, so a wave reads as fanning out of somewhere rather
            // than as objects appearing in arbitrary directions.
            Vector3 outward = point - transform.position;
            outward.y = 0f;
            return outward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(outward) : transform.rotation;
        }

        private bool TryFindPoint(out Vector3 point)
        {
            for (int attempt = 0; attempt < Tunables.I.EventSpawnPlacementAttempts; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * scatterRadius;
                Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas)) continue;
                if (IsCrowded(hit.position)) continue;

                point = hit.position;
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        private bool IsCrowded(Vector3 candidate)
        {
            float minSpacingSqr = minSpacing * minSpacing;
            for (int i = 0; i < _takenPoints.Count; i++)
            {
                if ((_takenPoints[i] - candidate).sqrMagnitude < minSpacingSqr) return true;
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, scatterRadius);
        }
    }
}
