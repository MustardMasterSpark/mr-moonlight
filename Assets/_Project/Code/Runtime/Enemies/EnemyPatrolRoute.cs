using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// How this enemy behaves when nobody has seen it yet: stand still, wander, or walk a route.
    /// MRM-29's idle/patrol spec, expressed as one component so the choice is a single dropdown on
    /// the prefab instead of six scattered Blaze fields.
    ///
    /// The three modes are exactly the three MRM-29 describes:
    /// <list type="bullet">
    /// <item><b>Idle</b> — fixed position, sweeping his cone. Blaze still lets him chase and search
    /// when he does see something, and his own spawn point is what he returns to afterwards.</item>
    /// <item><b>Random wander</b> — roams a radius around wherever he was dropped. Not in MRM-29,
    /// added because it makes the prefab work with zero setup, which is the drag-and-drop
    /// requirement. It is the default for that reason.</item>
    /// <item><b>Waypoints</b> — the authored route, either <see cref="RouteMode.Linear"/> (walk it
    /// once, then idle at the far end) or <see cref="RouteMode.Loop"/> (return to the first point
    /// and repeat).</item>
    /// </list>
    ///
    /// <b>A floating waypoint never makes an enemy fly</b> (MRM-29's acceptance criterion). Only a
    /// waypoint's horizontal position is used; the height is re-derived by dropping a ray onto the
    /// ground and then settling that point onto the NavMesh. Carlos can place markers roughly, at
    /// any height, and they still land on walkable ground — and a marker over water or off the mesh
    /// is reported by name at startup rather than silently producing a broken route.
    ///
    /// Owner: MRM-29, first used by MRM-34.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BlazeAI))]
    [AddComponentMenu("Mr. Moonlight/Enemies/Enemy Patrol Route")]
    public sealed class EnemyPatrolRoute : MonoBehaviour
    {
        public enum PatrolMode
        {
            /// <summary>Holds his spawn position and sweeps his vision cone.</summary>
            Idle,

            /// <summary>Roams randomly inside <see cref="wanderRadius"/> of where he started.</summary>
            RandomWander,

            /// <summary>Walks the ordered <see cref="waypoints"/> list.</summary>
            Waypoints
        }

        public enum RouteMode
        {
            /// <summary>Walk the route once, then idle at the last waypoint.</summary>
            Linear,

            /// <summary>Return to the first waypoint and repeat forever.</summary>
            Loop
        }

        [Tooltip("What this enemy does before anyone has seen him.")]
        [SerializeField] private PatrolMode mode = PatrolMode.RandomWander;

        [Header("Random wander")]
        [Tooltip("How far from his spawn point he will roam, in metres.")]
        [SerializeField] private float wanderRadius = 22f;

        [Tooltip("Skip candidate wander points that are behind an obstacle, so he does not repeatedly path around a rock to reach the far side of it.")]
        [SerializeField] private bool avoidPointsBehindObstacles = true;

        [Header("Waypoints")]
        [Tooltip("Ordered route. Empty GameObjects placed in the scene — only their horizontal position is used, the height is re-derived from the ground.")]
        [SerializeField] private List<Transform> waypoints = new List<Transform>();

        [Tooltip("Linear walks the route once and idles at the end. Loop returns to the first waypoint and repeats.")]
        [SerializeField] private RouteMode routeMode = RouteMode.Linear;

        [Header("Ground snapping")]
        [Tooltip("What counts as ground when re-deriving a waypoint's height.")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Tooltip("How far above and below a waypoint marker to look for ground, in metres. Generous by default so a marker dropped anywhere near the terrain still resolves.")]
        [SerializeField] private float groundSearchDistance = 200f;

        [Tooltip("How far a grounded waypoint may be nudged to land on the NavMesh, in metres.")]
        [SerializeField] private float navMeshSnapDistance = 5f;

        [Header("Debug")]
        [SerializeField] private bool drawRoute = true;

        private BlazeAI _blaze;
        private bool _holdPosition;

        public PatrolMode Mode => mode;

        private void Awake()
        {
            _blaze = GetComponent<BlazeAI>();
            Apply();
        }

        private void Update()
        {
            // Idle mode is held every frame rather than set once: Blaze clears stayIdle whenever it
            // returns to patrol after a search, and a static guard has to go back to standing still
            // rather than wandering off from wherever the search ended.
            if (!_holdPosition || _blaze == null) return;

            // Only in normal state. Holding through alert too would stop a static guard from ever
            // investigating, which MRM-29 explicitly wants him to do before returning to his spot.
            if (_blaze.state == BlazeAI.State.normal) _blaze.StayIdle();
        }

        /// <summary>
        /// Push this component's choice into Blaze's waypoint settings. Called at
        /// <see cref="Awake"/>; public so the custom inspector can preview a change without
        /// entering play mode.
        /// </summary>
        public void Apply()
        {
            if (_blaze == null) _blaze = GetComponent<BlazeAI>();
            if (_blaze == null || _blaze.waypoints == null) return;

            _holdPosition = false;

            switch (mode)
            {
                case PatrolMode.Idle:
                    // Blaze rejects an empty non-randomised waypoint list, so give it the one point
                    // he is standing on. StayIdle in Update is what actually keeps him there.
                    _blaze.waypoints.randomize = false;
                    _blaze.waypoints.loop = false;
                    _blaze.waypoints.waypoints = new List<Vector3> { transform.position };
                    _blaze.waypoints.waypointsRotation = new List<Vector2> { Vector2.zero };
                    _holdPosition = true;
                    break;

                case PatrolMode.RandomWander:
                    _blaze.waypoints.randomize = true;
                    _blaze.waypoints.loop = false;
                    _blaze.waypoints.randomizeRadius = wanderRadius;
                    _blaze.waypoints.preventPointsBehindObstacles = avoidPointsBehindObstacles;
                    break;

                case PatrolMode.Waypoints:
                    ApplyWaypoints();
                    break;
            }
        }

        private void ApplyWaypoints()
        {
            var resolved = new List<Vector3>();
            var rotations = new List<Vector2>();

            for (int i = 0; i < waypoints.Count; i++)
            {
                Transform marker = waypoints[i];
                if (marker == null)
                {
                    Debug.LogWarning($"{name}: waypoint {i} is empty and was skipped.", this);
                    continue;
                }

                if (!TryGround(marker.position, out Vector3 grounded))
                {
                    Debug.LogWarning(
                        $"{name}: waypoint '{marker.name}' could not be placed on walkable ground " +
                        $"(nothing found within {groundSearchDistance}m vertically, or no NavMesh within " +
                        $"{navMeshSnapDistance}m). It was skipped — move it over land, or re-bake.", this);
                    continue;
                }

                resolved.Add(grounded);
                rotations.Add(Vector2.zero);
            }

            if (resolved.Count == 0)
            {
                Debug.LogWarning($"{name}: no usable waypoints, falling back to standing still.", this);
                _blaze.waypoints.randomize = false;
                _blaze.waypoints.loop = false;
                _blaze.waypoints.waypoints = new List<Vector3> { transform.position };
                _blaze.waypoints.waypointsRotation = new List<Vector2> { Vector2.zero };
                _holdPosition = true;
                return;
            }

            // randomize and loop are mutually exclusive in Blaze's own validation, so clear
            // randomize before setting loop or the two silently fight.
            _blaze.waypoints.randomize = false;
            _blaze.waypoints.loop = routeMode == RouteMode.Loop;
            _blaze.waypoints.waypoints = resolved;
            _blaze.waypoints.waypointsRotation = rotations;
        }

        /// <summary>
        /// Take a marker's horizontal position, find the ground under (or over) it, and settle the
        /// result on the NavMesh. This is the whole "a waypoint contributes only X and Y" rule.
        /// </summary>
        public bool TryGround(Vector3 marker, out Vector3 grounded)
        {
            grounded = marker;

            Vector3 top = new Vector3(marker.x, marker.y + groundSearchDistance, marker.z);
            bool hitGround = Physics.Raycast(
                top, Vector3.down, out RaycastHit hit, groundSearchDistance * 2f, groundMask, QueryTriggerInteraction.Ignore);

            Vector3 candidate = hitGround ? hit.point : marker;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, navMeshSnapDistance, NavMesh.AllAreas))
            {
                return false;
            }

            grounded = navHit.position;
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawRoute) return;

            if (mode == PatrolMode.RandomWander)
            {
                Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
                Gizmos.DrawWireSphere(transform.position, wanderRadius);
                return;
            }

            if (mode == PatrolMode.Idle)
            {
                Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
                Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(0.6f, 2f, 0.6f));
                return;
            }

            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
            Vector3 previous = Vector3.zero;
            bool hasPrevious = false;

            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null) continue;

                Vector3 point = waypoints[i].position;
                Gizmos.DrawWireSphere(point, 0.4f);

                if (hasPrevious) Gizmos.DrawLine(previous, point);
                previous = point;
                hasPrevious = true;
            }

            // Close the loop visually, so Linear and Loop are distinguishable at a glance.
            if (routeMode == RouteMode.Loop && hasPrevious && waypoints.Count > 1 && waypoints[0] != null)
            {
                Gizmos.DrawLine(previous, waypoints[0].position);
            }
        }
    }
}
