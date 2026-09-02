using MrMoonlight.Events;
using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// Adds a live "what is the director doing right now" readout under the Event Director's
    /// normal inspector. Owner: MRM-11 (acceptance criterion: "Current step is visible in the
    /// inspector at runtime").
    ///
    /// <para>Repaints every editor frame while playing, so the step line updates as the level
    /// runs rather than only when the inspector happens to be redrawn.</para>
    /// </summary>
    [CustomEditor(typeof(EventDirector))]
    public sealed class EventDirectorInspector : Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (!Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    "Press play to see the running sequences here.\n\n" +
                    "Tools → Mr. Moonlight → Validate Event Scripts checks every script without entering play mode.",
                    MessageType.None);
                return;
            }

            var director = (EventDirector)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Running now", EditorStyles.boldLabel);

            if (director.Running.Count == 0)
            {
                EditorGUILayout.LabelField(director.LevelEnded ? "Level ended." : "Nothing running.");
            }
            else
            {
                foreach (RunningSequence sequence in director.Running)
                {
                    EditorGUILayout.SelectableLabel(sequence.Describe(), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
            }

            ObjectiveTracker tracker = director.Objectives;
            if (tracker == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Objectives", EditorStyles.boldLabel);

            if (tracker.All.Count == 0)
            {
                EditorGUILayout.LabelField("None set yet.");
                return;
            }

            foreach (Objective objective in tracker.All)
            {
                string state = objective.Completed ? "DONE" : "open";
                EditorGUILayout.LabelField($"[{state}] {objective.Id}", objective.ProgressText);
            }
        }
    }
}
