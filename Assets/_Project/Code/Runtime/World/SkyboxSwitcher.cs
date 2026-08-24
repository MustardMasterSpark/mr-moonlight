using UnityEngine;

namespace MrMoonlight.World
{
    /// <summary>
    /// Swaps RenderSettings.skybox between a fixed, inspector-authored list of skybox
    /// materials and exposes the active skybox's real-time rotation. Per MRM-47, skybox swaps
    /// happen instantly, never blended — a smooth cross-fade would need a custom blend shader,
    /// and the story hides every swap behind a beat instead (the cabin interior, the mine
    /// entrance). Owner: MRM-47
    /// </summary>
    public sealed class SkyboxSwitcher : MonoBehaviour
    {
        [Tooltip("The skybox materials available to switch between. All 6 imported from AllSky live here during development; only 4 ship in the build (Docs/webgl-budget.md §4.12) — trim this list once the final 4 are chosen.")]
        [SerializeField] private Material[] skyboxes;

        /// <summary>Sets RenderSettings.skybox to the material at the given index and refreshes ambient/reflection probes to match. Logs and no-ops if the index is out of range.</summary>
        public void SetSkybox(int index)
        {
            if (skyboxes == null || index < 0 || index >= skyboxes.Length)
            {
                Debug.LogError($"[SkyboxSwitcher] Index {index} out of range ({skyboxes?.Length ?? 0} skyboxes). See MRM-47.");
                return;
            }
            SetSkybox(skyboxes[index]);
        }

        /// <summary>Sets RenderSettings.skybox directly (does not need to be in the inspector list) and refreshes ambient/reflection probes to match. Passing null is a no-op, matching MRM-47's "if no skybox is passed, the skybox does not change" rule.</summary>
        public void SetSkybox(Material skybox)
        {
            if (skybox == null) return;
            RenderSettings.skybox = skybox;
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>Sets the active skybox material's _Rotation property, in degrees. No-op if the current skybox has no such property (only Skybox/Cubemap and Skybox/Panoramic expose it) — the future in-game skybox rotation/polish ask goes through here.</summary>
        public void SetRotation(float degrees)
        {
            if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Rotation"))
                RenderSettings.skybox.SetFloat("_Rotation", degrees);
        }
    }
}
