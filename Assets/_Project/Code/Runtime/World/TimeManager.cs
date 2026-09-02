using System;
using System.Collections;
using System.Collections.Generic;
using MrMoonlight.Data;
using UnityEngine;

namespace MrMoonlight.World
{
    /// <summary>
    /// Drop-in replacement for a scene's default skybox and Sun: owns both (via
    /// <see cref="SkyboxSwitcher"/> and <see cref="SunController"/>) and switches between named
    /// presets at runtime. Skybox swaps are instant per MRM-47 (never blended — hidden behind a
    /// story beat instead); the Sun's rotation/color/intensity lerp smoothly over the given
    /// duration. Event Director hookup (MRM-11/MRM-62) is out of scope here — for now, call
    /// <see cref="ApplyPreset(int,float)"/> from the inspector or another script.
    /// Owner: MRM-69
    /// </summary>
    public sealed class TimeManager : MonoBehaviour
    {
        [Serializable]
        public sealed class Preset
        {
            public string Name = "New Preset";
            public Material Skybox;
            public SunState Sun = SunState.Default;
        }

        [SerializeField] private SkyboxSwitcher skyboxSwitcher;
        [SerializeField] private SunController sun;
        [SerializeField] private List<Preset> presets = new List<Preset>();

        [Header("Editor Test")]
        [Tooltip("Index into Presets above. Right-click this component's header (or the ⋮ menu) and choose \"Apply Test Preset\" to switch to it — works in Play Mode (smooth transition) and Edit Mode (instant, since coroutines don't run outside Play Mode).")]
        [SerializeField] private int testPresetIndex;

        private Coroutine _transition;

        /// <summary>How many presets are authored. Owner: MRM-69</summary>
        public int PresetCount => presets.Count;

        /// <summary>
        /// Index of the last preset applied, or -1 if none has been applied this session.
        ///
        /// <para>-1 rather than 0 on purpose: "nothing has been applied yet" and "Morning is
        /// showing" are different states, and a cycler that assumed 0 would skip the first preset
        /// on its first press. Added for MRM-11's F8 debug cycle.</para>
        /// </summary>
        public int CurrentPresetIndex { get; private set; } = -1;

        /// <summary>Name of a preset, for debug readouts. Empty string for an out-of-range index. Owner: MRM-69</summary>
        public string GetPresetName(int index) =>
            index >= 0 && index < presets.Count ? presets[index].Name : string.Empty;

        /// <summary>Applies Presets[testPresetIndex] — hook this up to the component's context menu ("Apply Test Preset") for one-click testing in the Inspector. Owner: MRM-69</summary>
        [ContextMenu("Apply Test Preset")]
        private void ApplyTestPreset() => ApplyPreset(testPresetIndex);

        /// <summary>Switches to a preset by index over MoonlightTunables' default duration. See <see cref="ApplyPreset(int,float)"/>.</summary>
        public void ApplyPreset(int index) => ApplyPreset(index, Tunables.I.TimeManagerDefaultTransitionSeconds);

        /// <summary>Switches to a preset by name over MoonlightTunables' default duration. See <see cref="ApplyPreset(int,float)"/>.</summary>
        public void ApplyPreset(string presetName) => ApplyPreset(presetName, Tunables.I.TimeManagerDefaultTransitionSeconds);

        /// <summary>Switches to a preset by index: the skybox swaps instantly, the Sun lerps to the preset's state over durationSeconds (0 = instant). Owner: MRM-69</summary>
        public void ApplyPreset(int index, float durationSeconds)
        {
            if (!TryGetPreset(index, out Preset preset)) return;

            CurrentPresetIndex = index;
            skyboxSwitcher.SetSkybox(preset.Skybox);

            if (_transition != null) StopCoroutine(_transition);

            // Coroutines don't run outside Play Mode - Edit Mode testing (the context menu
            // below) always applies instantly regardless of the requested duration.
            if (durationSeconds <= 0f || !Application.isPlaying)
            {
                sun.ApplyState(preset.Sun);
                _transition = null;
            }
            else
            {
                _transition = StartCoroutine(LerpSun(sun.GetState(), preset.Sun, durationSeconds));
            }
        }

        /// <summary>Switches to a preset by name. See <see cref="ApplyPreset(int,float)"/>.</summary>
        public void ApplyPreset(string presetName, float durationSeconds) => ApplyPreset(IndexOf(presetName), durationSeconds);

        private IEnumerator LerpSun(SunState from, SunState to, float durationSeconds)
        {
            float t = 0f;
            while (t < durationSeconds)
            {
                t += Time.deltaTime;
                sun.ApplyState(LerpState(from, to, Mathf.Clamp01(t / durationSeconds)));
                yield return null;
            }
            sun.ApplyState(to);
            _transition = null;
        }

        private static SunState LerpState(SunState a, SunState b, float t) => new SunState
        {
            Elevation = Mathf.LerpAngle(a.Elevation, b.Elevation, t),
            Azimuth = Mathf.LerpAngle(a.Azimuth, b.Azimuth, t),
            Color = Color.Lerp(a.Color, b.Color, t),
            Intensity = Mathf.Lerp(a.Intensity, b.Intensity, t),
            UseColorTemperature = t >= 1f ? b.UseColorTemperature : a.UseColorTemperature,
            ColorTemperature = Mathf.Lerp(a.ColorTemperature, b.ColorTemperature, t)
        };

        private int IndexOf(string presetName)
        {
            for (int i = 0; i < presets.Count; i++)
                if (presets[i].Name == presetName) return i;
            Debug.LogError($"[TimeManager] No preset named \"{presetName}\". See MRM-69.");
            return -1;
        }

        private bool TryGetPreset(int index, out Preset preset)
        {
            if (index < 0 || index >= presets.Count)
            {
                Debug.LogError($"[TimeManager] Preset index {index} out of range ({presets.Count} presets). See MRM-69.");
                preset = null;
                return false;
            }
            preset = presets[index];
            return true;
        }
    }
}
