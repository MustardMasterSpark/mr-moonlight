using MrMoonlight.Enemies;
using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// Shows only the fields that matter for the chosen patrol mode. Carlos asked for the mode
    /// choice to "unlock" its own options rather than showing every field at once, and a route with
    /// a wander radius sitting next to it invites exactly the kind of "which of these is actually
    /// being used?" question the single dropdown is meant to remove. Owner: MRM-29.
    /// </summary>
    [CustomEditor(typeof(EnemyPatrolRoute))]
    public sealed class EnemyPatrolRouteInspector : Editor
    {
        private SerializedProperty _mode;
        private SerializedProperty _wanderRadius;
        private SerializedProperty _avoidPointsBehindObstacles;
        private SerializedProperty _waypoints;
        private SerializedProperty _routeMode;
        private SerializedProperty _groundMask;
        private SerializedProperty _groundSearchDistance;
        private SerializedProperty _navMeshSnapDistance;
        private SerializedProperty _drawRoute;

        private void OnEnable()
        {
            _mode = serializedObject.FindProperty("mode");
            _wanderRadius = serializedObject.FindProperty("wanderRadius");
            _avoidPointsBehindObstacles = serializedObject.FindProperty("avoidPointsBehindObstacles");
            _waypoints = serializedObject.FindProperty("waypoints");
            _routeMode = serializedObject.FindProperty("routeMode");
            _groundMask = serializedObject.FindProperty("groundMask");
            _groundSearchDistance = serializedObject.FindProperty("groundSearchDistance");
            _navMeshSnapDistance = serializedObject.FindProperty("navMeshSnapDistance");
            _drawRoute = serializedObject.FindProperty("drawRoute");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_mode);
            var mode = (EnemyPatrolRoute.PatrolMode)_mode.enumValueIndex;

            EditorGUILayout.Space();

            switch (mode)
            {
                case EnemyPatrolRoute.PatrolMode.Idle:
                    EditorGUILayout.HelpBox(
                        "Stands where he is dropped and sweeps his vision cone. He will still chase " +
                        "and search when he spots something, then return to this spot.",
                        MessageType.Info);
                    break;

                case EnemyPatrolRoute.PatrolMode.RandomWander:
                    EditorGUILayout.HelpBox(
                        "Roams at random around wherever he is dropped. Needs no setup — this is why " +
                        "it is the default on the prefab.",
                        MessageType.Info);
                    EditorGUILayout.PropertyField(_wanderRadius);
                    EditorGUILayout.PropertyField(_avoidPointsBehindObstacles);
                    break;

                case EnemyPatrolRoute.PatrolMode.Waypoints:
                    EditorGUILayout.HelpBox(
                        "Walks the ordered route below. Drop empty GameObjects in the scene and drag " +
                        "them in — only their horizontal position matters, the height is re-derived " +
                        "from the ground, so a marker left floating will not make him fly.",
                        MessageType.Info);
                    EditorGUILayout.PropertyField(_routeMode);
                    EditorGUILayout.PropertyField(_waypoints, true);
                    DrawWaypointWarnings();

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Ground snapping", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_groundMask);
                    EditorGUILayout.PropertyField(_groundSearchDistance);
                    EditorGUILayout.PropertyField(_navMeshSnapDistance);
                    break;
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_drawRoute);

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Catch the two mistakes that are easy to make and invisible until play mode: an empty
        /// slot in the list, and a one-point "route".
        /// </summary>
        private void DrawWaypointWarnings()
        {
            int empties = 0;
            for (int i = 0; i < _waypoints.arraySize; i++)
            {
                if (_waypoints.GetArrayElementAtIndex(i).objectReferenceValue == null) empties++;
            }

            if (_waypoints.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "No waypoints assigned — he will stand still until some are added.",
                    MessageType.Warning);
            }
            else if (_waypoints.arraySize == 1)
            {
                EditorGUILayout.HelpBox(
                    "One waypoint is the same as Idle, but at that point rather than his spawn.",
                    MessageType.Warning);
            }

            if (empties > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{empties} empty slot(s) in the route — they are skipped at startup.",
                    MessageType.Warning);
            }
        }
    }
}
