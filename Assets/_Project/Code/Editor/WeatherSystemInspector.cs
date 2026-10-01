using MrMoonlight.World.Weather;
using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// The Weather object's Inspector: Save / Revert buttons above the control board (the board itself is
    /// <see cref="WeatherSystem"/>'s serialized <c>live</c> profile, drawn by the default inspector).
    /// Owner: MRM-86.
    /// </summary>
    [CustomEditor(typeof(WeatherSystemBase), true)]
    public sealed class WeatherSystemInspector : Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            var weather = (WeatherSystemBase)target;

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Press Play. The control board below is tuned live in Play Mode and saved to the current "
                    + "weather profile with the buttons that appear here (or the P panel).",
                    MessageType.Info);
            }
            else
            {
                string now = "Weather: " + weather.CurrentName
                    + (weather.IsBlending ? "  ->  " + weather.TargetName + "  " + Mathf.RoundToInt(weather.Blend * 100f) + "%" : string.Empty);
                EditorGUILayout.LabelField(now, EditorStyles.boldLabel);

                bool canSave = weather.CanSave(out string reason);
                if (!canSave) EditorGUILayout.HelpBox(reason, MessageType.None);

                using (new EditorGUI.DisabledScope(!canSave))
                {
                    if (GUILayout.Button("Save everything to " + weather.CurrentName)) weather.SaveAll();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Save lighting")) weather.SaveLighting();
                        if (GUILayout.Button("Save fog")) weather.SaveFog();
                    }
                }

                using (new EditorGUI.DisabledScope(weather.IsBlending || !weather.IsReady))
                {
                    if (GUILayout.Button("Revert board to saved profile")) weather.RevertToProfile();
                }

                if (!string.IsNullOrEmpty(weather.LastSave)) EditorGUILayout.HelpBox(weather.LastSave, MessageType.None);
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
