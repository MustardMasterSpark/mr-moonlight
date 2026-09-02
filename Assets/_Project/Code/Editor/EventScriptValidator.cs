using System.Collections.Generic;
using System.Text;
using MrMoonlight.Events;
using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// Parses and checks every event script in the project without entering play mode. Owner: MRM-11.
    ///
    /// <para>The director validates its own script at load too, but that means one play session per
    /// round of typos on a scene that takes a while to open. This is the fast loop: edit the text
    /// file, hit the menu item, read the console.</para>
    ///
    /// <para>It checks everything that can be known from the file alone — grammar, unknown verbs,
    /// missing required arguments, <c>run</c>/<c>wait sequence=</c> pointing at sequences that do
    /// not exist. It cannot check names that live in the scene (spawn points, zones, signal
    /// receivers); those are reported at runtime, by the line that used them.</para>
    /// </summary>
    public static class EventScriptValidator
    {
        private const string ScriptFolder = "Assets/_Project/Data/Events";

        [MenuItem("Tools/Mr. Moonlight/Validate Event Scripts")]
        public static void ValidateAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { ScriptFolder });

            if (guids.Length == 0)
            {
                Debug.LogWarning($"[EventDirector] No event scripts found in {ScriptFolder}.");
                return;
            }

            int totalErrors = 0;
            var summary = new StringBuilder();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null) continue;

                EventScript script = EventScriptParser.Parse(asset.text, asset.name);
                EventDirector.ValidateVerbs(script);

                totalErrors += script.Errors.Count;
                summary.AppendLine($"  {asset.name}: {CountSteps(script)} steps in {script.Sequences.Count} sequence(s), {script.Errors.Count} error(s), {script.Warnings.Count} warning(s)");

                foreach (string warning in script.Warnings) Debug.LogWarning($"[EventDirector] {warning}", asset);
                foreach (string error in script.Errors) Debug.LogError($"[EventDirector] {error}", asset);
            }

            string header = totalErrors == 0
                ? $"[EventDirector] Validated {guids.Length} event script(s) — no errors."
                : $"[EventDirector] Validated {guids.Length} event script(s) — {totalErrors} error(s), listed above.";

            Debug.Log(header + "\n" + summary);
        }

        [MenuItem("Tools/Mr. Moonlight/Print Event Verb Reference")]
        public static void PrintVerbReference()
        {
            var lines = new List<string>();
            foreach (EventVerb verb in EventVerbRegistry.All) lines.Add("  " + verb.Summary);
            lines.Sort(System.StringComparer.OrdinalIgnoreCase);

            Debug.Log("[EventDirector] Verbs the event script understands:\n" + string.Join("\n", lines));
        }

        private static int CountSteps(EventScript script)
        {
            int count = 0;
            foreach (EventSequence sequence in script.Sequences) count += sequence.Steps.Count;
            return count;
        }
    }
}
