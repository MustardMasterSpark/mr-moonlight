using System.Collections.Generic;
using System.Linq;
using MrMoonlight.Audio;
using MrMoonlight.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MrMoonlight.UI
{
    /// <summary>
    /// The Settings panel's own logic (MRM-18): the Conformist/Punk difficulty pick (two Toggles
    /// under one ToggleGroup - exclusive by Unity's own mechanism, not hand-rolled), the
    /// Master/Voices/SFX sliders, and (MRM-78, 2026-09-15) the Display group - upscaling,
    /// resolution, fullscreen/windowed. Every change writes straight through to
    /// <see cref="GameSettings"/> (so it survives the scene load into the demo scene) and applies
    /// live - a slider is audible immediately, a display change takes effect immediately too.
    ///
    /// <see cref="ApplySavedAudioSettings"/> and <see cref="ApplySavedDisplaySettings"/> are
    /// called by <see cref="MainMenuController"/> in its own Awake, independent of whether this
    /// panel's GameObject is active - a fresh launch must apply saved (or default) audio/display
    /// state before the opening reveal, not only once the player opens Settings. Owner: MRM-18,
    /// Display group: MRM-78
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        private const string MasterVolumeParam = "MasterVolume";
        private const string VoicesVolumeParam = "VoicesVolume";
        private const string SFXVolumeParam = "SFXVolume";

        [Header("Difficulty")]
        [SerializeField] private Toggle conformistToggle;
        [SerializeField] private Toggle punkToggle;

        [Header("Volume")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider voicesSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private AudioMixer mixer;

        [Header("Display — MRM-78")]
        [Tooltip("Applies the active URP asset's Render Scale (Tunables.UpscalingRenderScale) + FSR Upscaling Filter when on; native (1.0, Auto) when off.")]
        [SerializeField] private Toggle upscalingToggle;
        [Tooltip("Populated at Awake from Screen.resolutions, deduplicated by width x height.")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [Tooltip("On = FullScreenWindow (borderless fullscreen, the project's display target). Off = Windowed.")]
        [SerializeField] private Toggle fullscreenToggle;

        private List<Vector2Int> availableResolutions;

        private void Awake()
        {
            InitializeFromSavedSettings();

            conformistToggle.onValueChanged.AddListener(OnConformistToggled);
            punkToggle.onValueChanged.AddListener(OnPunkToggled);
            masterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            voicesSlider.onValueChanged.AddListener(OnVoicesVolumeChanged);
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            upscalingToggle.onValueChanged.AddListener(OnUpscalingToggled);
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
        }

        private void OnDestroy()
        {
            conformistToggle.onValueChanged.RemoveListener(OnConformistToggled);
            punkToggle.onValueChanged.RemoveListener(OnPunkToggled);
            masterSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            voicesSlider.onValueChanged.RemoveListener(OnVoicesVolumeChanged);
            sfxSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
            upscalingToggle.onValueChanged.RemoveListener(OnUpscalingToggled);
            resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
            fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenToggled);
        }

        /// <summary>Writes the saved (or default) volumes to the mixer. Safe to call before this panel's GameObject has ever been active - the serialized <see cref="mixer"/> reference is populated at scene load regardless. Owner: MRM-18</summary>
        public void ApplySavedAudioSettings()
        {
            AudioMixerVolume.Apply(mixer, MasterVolumeParam, GameSettings.MasterVolume);
            AudioMixerVolume.Apply(mixer, VoicesVolumeParam, GameSettings.VoicesVolume);
            AudioMixerVolume.Apply(mixer, SFXVolumeParam, GameSettings.SFXVolume);
        }

        /// <summary>Applies the saved (or default) upscaling/resolution/fullscreen state to the URP asset and the OS window. Safe to call before this panel's GameObject has ever been active, same reasoning as <see cref="ApplySavedAudioSettings"/>. Owner: MRM-78</summary>
        public void ApplySavedDisplaySettings()
        {
            ApplyUpscaling(GameSettings.UpscalingEnabled);
            Screen.SetResolution(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight, GameSettings.FullscreenMode);
        }

        private void InitializeFromSavedSettings()
        {
            bool isPunk = GameSettings.Difficulty == Difficulty.Punk;
            punkToggle.SetIsOnWithoutNotify(isPunk);
            conformistToggle.SetIsOnWithoutNotify(!isPunk);

            masterSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            voicesSlider.SetValueWithoutNotify(GameSettings.VoicesVolume);
            sfxSlider.SetValueWithoutNotify(GameSettings.SFXVolume);

            ApplySavedAudioSettings();

            InitializeResolutionDropdown();
            upscalingToggle.SetIsOnWithoutNotify(GameSettings.UpscalingEnabled);
            fullscreenToggle.SetIsOnWithoutNotify(GameSettings.FullscreenMode == FullScreenMode.FullScreenWindow);

            ApplySavedDisplaySettings();
        }

        /// <summary>Deduplicates <see cref="Screen.resolutions"/> by width x height (ignoring refresh rate - a per-resolution refresh choice isn't exposed here) and selects whichever entry matches the saved resolution, falling back to the current screen size.</summary>
        private void InitializeResolutionDropdown()
        {
            availableResolutions = Screen.resolutions
                .Select(r => new Vector2Int(r.width, r.height))
                .Distinct()
                .OrderByDescending(r => r.x * r.y)
                .ToList();

            if (availableResolutions.Count == 0)
            {
                availableResolutions.Add(new Vector2Int(Screen.width, Screen.height));
            }

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(availableResolutions.Select(r => $"{r.x} x {r.y}").ToList());

            int savedIndex = availableResolutions.FindIndex(r => r.x == GameSettings.ResolutionWidth && r.y == GameSettings.ResolutionHeight);
            resolutionDropdown.SetValueWithoutNotify(savedIndex >= 0 ? savedIndex : 0);
        }

        /// <summary>Sets the active URP asset's Render Scale + Upscaling Filter. <c>enabled</c> on applies <see cref="MoonlightTunables.UpscalingRenderScale"/> with the FSR filter; off restores native 1.0/Auto. No-ops (with a warning) if the active pipeline isn't URP.</summary>
        private static void ApplyUpscaling(bool enabled)
        {
            if (GraphicsSettings.defaultRenderPipeline is not UniversalRenderPipelineAsset urpAsset)
            {
                Debug.LogWarning("SettingsPanel: active render pipeline isn't URP - cannot apply upscaling setting.");
                return;
            }

            urpAsset.renderScale = enabled ? Tunables.I.UpscalingRenderScale : 1f;
            urpAsset.upscalingFilter = enabled ? UpscalingFilterSelection.FSR : UpscalingFilterSelection.Auto;
        }

        private void OnConformistToggled(bool isOn)
        {
            if (isOn)
            {
                GameSettings.Difficulty = Difficulty.Conformist;
            }
        }

        private void OnPunkToggled(bool isOn)
        {
            if (isOn)
            {
                GameSettings.Difficulty = Difficulty.Punk;
            }
        }

        private void OnMasterVolumeChanged(float value01)
        {
            GameSettings.MasterVolume = value01;
            AudioMixerVolume.Apply(mixer, MasterVolumeParam, value01);
        }

        private void OnVoicesVolumeChanged(float value01)
        {
            GameSettings.VoicesVolume = value01;
            AudioMixerVolume.Apply(mixer, VoicesVolumeParam, value01);
        }

        private void OnSFXVolumeChanged(float value01)
        {
            GameSettings.SFXVolume = value01;
            AudioMixerVolume.Apply(mixer, SFXVolumeParam, value01);
        }

        private void OnUpscalingToggled(bool isOn)
        {
            GameSettings.UpscalingEnabled = isOn;
            ApplyUpscaling(isOn);
        }

        private void OnResolutionChanged(int index)
        {
            Vector2Int resolution = availableResolutions[index];
            GameSettings.ResolutionWidth = resolution.x;
            GameSettings.ResolutionHeight = resolution.y;
            Screen.SetResolution(resolution.x, resolution.y, GameSettings.FullscreenMode);
        }

        private void OnFullscreenToggled(bool isOn)
        {
            FullScreenMode mode = isOn ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            GameSettings.FullscreenMode = mode;
            Screen.SetResolution(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight, mode);
        }
    }
}
