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
        [Tooltip("Populated at Awake from Screen.resolutions, deduplicated by width x height and filtered to 16:9 only - the project's UI is authored at 1920x1080 and does not lay out correctly at other aspect ratios.")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [Tooltip("Windowed / Borderless Fullscreen / Exclusive Fullscreen. Borderless is the project's committed display target (CLAUDE.md) - Exclusive is exposed here as a diagnostic/testing option for the DWM-compositor frame-cap investigation, 2026-09-16, not (yet) a default change.")]
        [SerializeField] private TMP_Dropdown fullscreenModeDropdown;
        [Tooltip("Minimal/Medium/High/Highest graphics quality preset. Options are fixed (not populated at runtime) - order must match MoonlightTunables' Quality* fields and GameSettings.GraphicsQualityLevel's 0-3 indexing.")]
        [SerializeField] private TMP_Dropdown qualityDropdown;

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
            fullscreenModeDropdown.onValueChanged.AddListener(OnFullscreenModeChanged);
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
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
            fullscreenModeDropdown.onValueChanged.RemoveListener(OnFullscreenModeChanged);
            qualityDropdown.onValueChanged.RemoveListener(OnQualityChanged);
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
            ApplyGraphicsQuality(GameSettings.GraphicsQualityLevel);
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
            InitializeFullscreenModeDropdown();
            InitializeQualityDropdown();

            ApplySavedDisplaySettings();
        }

        /// <summary>16:9 aspect ratio, with a small tolerance for the rounding a monitor's actual pixel counts introduce (e.g. 1366x768 is 1.779, not the exact 1.778 of 1920x1080).</summary>
        private const float SixteenByNine = 16f / 9f;
        private const float AspectRatioTolerance = 0.02f;

        /// <summary>
        /// Deduplicates <see cref="Screen.resolutions"/> by width x height (ignoring refresh rate -
        /// a per-resolution refresh choice isn't exposed here) and selects whichever entry matches
        /// the saved resolution, falling back to the current screen size. Filtered to 16:9 only -
        /// the project's Canvas Scaler reference resolution and every HUD/menu layout is authored
        /// at 1920x1080 (CLAUDE.md's display target); a 4:3 or other-ratio resolution stretches
        /// that layout instead of just scaling it, which is what Carlos saw break at 1024x768.
        /// Owner: settings-menu, 2026-09-16
        /// </summary>
        private void InitializeResolutionDropdown()
        {
            availableResolutions = Screen.resolutions
                .Select(r => new Vector2Int(r.width, r.height))
                .Where(r => Mathf.Abs((float)r.x / r.y - SixteenByNine) < AspectRatioTolerance)
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

        /// <summary>Fixed 4-entry list - see <see cref="qualityDropdown"/>'s tooltip for the indexing contract.</summary>
        private void InitializeQualityDropdown()
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string> { "Minimal", "Medium", "High", "Highest" });
            qualityDropdown.SetValueWithoutNotify(Mathf.Clamp(GameSettings.GraphicsQualityLevel, 0, 3));
        }

        /// <summary>
        /// Applies one of the four <see cref="MoonlightTunables"/> Quality* preset groups (shadow
        /// distance/cascades, MSAA, LOD bias, global texture mip limit) to the active URP asset and
        /// <see cref="QualitySettings"/>. No-ops (with a warning) if the active pipeline isn't URP,
        /// same guard as <see cref="ApplyUpscaling"/>.
        /// </summary>
        private static void ApplyGraphicsQuality(int level)
        {
            if (GraphicsSettings.defaultRenderPipeline is not UniversalRenderPipelineAsset urpAsset)
            {
                Debug.LogWarning("SettingsPanel: active render pipeline isn't URP - cannot apply graphics quality setting.");
                return;
            }

            MoonlightTunables tunables = Tunables.I;
            float shadowDistance;
            int shadowCascades;
            int msaaSamples;
            float lodBias;
            int textureMipLimit;

            switch (level)
            {
                case 0:
                    shadowDistance = tunables.QualityMinimalShadowDistance;
                    shadowCascades = tunables.QualityMinimalShadowCascades;
                    msaaSamples = tunables.QualityMinimalMsaaSamples;
                    lodBias = tunables.QualityMinimalLodBias;
                    textureMipLimit = tunables.QualityMinimalTextureMipLimit;
                    break;
                case 1:
                    shadowDistance = tunables.QualityMediumShadowDistance;
                    shadowCascades = tunables.QualityMediumShadowCascades;
                    msaaSamples = tunables.QualityMediumMsaaSamples;
                    lodBias = tunables.QualityMediumLodBias;
                    textureMipLimit = tunables.QualityMediumTextureMipLimit;
                    break;
                case 2:
                    shadowDistance = tunables.QualityHighShadowDistance;
                    shadowCascades = tunables.QualityHighShadowCascades;
                    msaaSamples = tunables.QualityHighMsaaSamples;
                    lodBias = tunables.QualityHighLodBias;
                    textureMipLimit = tunables.QualityHighTextureMipLimit;
                    break;
                default:
                    shadowDistance = tunables.QualityHighestShadowDistance;
                    shadowCascades = tunables.QualityHighestShadowCascades;
                    msaaSamples = tunables.QualityHighestMsaaSamples;
                    lodBias = tunables.QualityHighestLodBias;
                    textureMipLimit = tunables.QualityHighestTextureMipLimit;
                    break;
            }

            urpAsset.shadowDistance = shadowDistance;
            urpAsset.shadowCascadeCount = shadowCascades;
            urpAsset.msaaSampleCount = msaaSamples;
            QualitySettings.lodBias = lodBias;
            QualitySettings.globalTextureMipmapLimit = textureMipLimit;
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

        /// <summary>Fixed 3-entry list, in the same index order <see cref="FullscreenModeToIndex"/>/<see cref="IndexToFullscreenMode"/> use. Owner: settings-menu, 2026-09-16</summary>
        private void InitializeFullscreenModeDropdown()
        {
            fullscreenModeDropdown.ClearOptions();
            fullscreenModeDropdown.AddOptions(new List<string> { "Windowed", "Fullscreen (Borderless)", "Fullscreen (Exclusive)" });
            fullscreenModeDropdown.SetValueWithoutNotify(FullscreenModeToIndex(GameSettings.FullscreenMode));
        }

        private static int FullscreenModeToIndex(FullScreenMode mode) => mode switch
        {
            FullScreenMode.Windowed => 0,
            FullScreenMode.ExclusiveFullScreen => 2,
            _ => 1, // FullScreenWindow (borderless) - the project's committed default
        };

        private static FullScreenMode IndexToFullscreenMode(int index) => index switch
        {
            0 => FullScreenMode.Windowed,
            2 => FullScreenMode.ExclusiveFullScreen,
            _ => FullScreenMode.FullScreenWindow,
        };

        private void OnFullscreenModeChanged(int index)
        {
            FullScreenMode mode = IndexToFullscreenMode(index);
            GameSettings.FullscreenMode = mode;
            Screen.SetResolution(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight, mode);
        }

        private void OnQualityChanged(int level)
        {
            GameSettings.GraphicsQualityLevel = level;
            ApplyGraphicsQuality(level);
        }
    }
}
