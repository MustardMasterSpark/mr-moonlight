using UnityEngine;

namespace MrMoonlight.Data
{
    /// <summary>
    /// Persisted player-facing settings written by the main menu's Settings panel (MRM-18):
    /// difficulty and the three mixer volume sliders. Backed by PlayerPrefs rather than a scene
    /// object, so the choice survives the fade/load into the demo scene without needing a
    /// DontDestroyOnLoad object or a second singleton - same reasoning as <see cref="Tunables"/>
    /// being the project's only sanctioned one (see Docs/csharp-conventions.md). Not a
    /// MonoBehaviour, same static-class shape as <see cref="MrMoonlight.VFX.ScreenTint"/>.
    ///
    /// Volumes are 0-1 linear (what a UI Slider wants); converting to the mixer's logarithmic
    /// decibel scale is <see cref="MrMoonlight.Audio.AudioMixerVolume"/>'s job, not this class's -
    /// this class only ever stores/retrieves plain settings values. Owner: MRM-18
    /// </summary>
    public static class GameSettings
    {
        private const string DifficultyKey = "MrMoonlight.Difficulty";
        private const string MasterVolumeKey = "MrMoonlight.MasterVolume";
        private const string VoicesVolumeKey = "MrMoonlight.VoicesVolume";
        private const string SFXVolumeKey = "MrMoonlight.SFXVolume";

        // Display — MRM-78 (FSR spike, settings menu 2026-09-15).
        private const string UpscalingEnabledKey = "MrMoonlight.UpscalingEnabled";
        private const string FullscreenModeKey = "MrMoonlight.FullscreenMode";
        private const string ResolutionWidthKey = "MrMoonlight.ResolutionWidth";
        private const string ResolutionHeightKey = "MrMoonlight.ResolutionHeight";

        // Graphics quality preset dropdown, settings menu 2026-09-16.
        private const string GraphicsQualityLevelKey = "MrMoonlight.GraphicsQualityLevel";

        // VSync checkbox, MRM-78 FPS-cap investigation, 2026-09-16.
        private const string VSyncEnabledKey = "MrMoonlight.VSyncEnabled";

        // Island demo wrap-up debug mixer overlay (F10) — temporary tuning sliders, not the
        // Settings panel above. Owner: island-demo-wrapup, 2026-09-10.
        private const string MenuMusicVolumeKey = "MrMoonlight.MenuMusicVolume";
        private const string WeaponVolumeKey = "MrMoonlight.WeaponVolume";
        private const string IslandMusicVolumeKey = "MrMoonlight.IslandMusicVolume";
        private const string SpotterVolumeKey = "MrMoonlight.SpotterVolume";

        public static Difficulty Difficulty
        {
            get => (Difficulty)PlayerPrefs.GetInt(DifficultyKey, (int)Difficulty.Punk);
            set
            {
                PlayerPrefs.SetInt(DifficultyKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(MasterVolumeKey, Tunables.I.DefaultMasterVolume);
            set
            {
                PlayerPrefs.SetFloat(MasterVolumeKey, value);
                PlayerPrefs.Save();
            }
        }

        public static float VoicesVolume
        {
            get => PlayerPrefs.GetFloat(VoicesVolumeKey, Tunables.I.DefaultVoicesVolume);
            set
            {
                PlayerPrefs.SetFloat(VoicesVolumeKey, value);
                PlayerPrefs.Save();
            }
        }

        public static float SFXVolume
        {
            get => PlayerPrefs.GetFloat(SFXVolumeKey, Tunables.I.DefaultSFXVolume);
            set
            {
                PlayerPrefs.SetFloat(SFXVolumeKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Debug mixer overlay (F10), island demo wrap-up 2026-09-10 — not part of the Settings panel above. Owner: island-demo-wrapup</summary>
        public static float MenuMusicVolume
        {
            get => PlayerPrefs.GetFloat(MenuMusicVolumeKey, Tunables.I.DefaultMenuMusicVolume);
            set
            {
                PlayerPrefs.SetFloat(MenuMusicVolumeKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Debug mixer overlay (F10). Owner: island-demo-wrapup</summary>
        public static float WeaponVolume
        {
            get => PlayerPrefs.GetFloat(WeaponVolumeKey, Tunables.I.DefaultWeaponVolume);
            set
            {
                PlayerPrefs.SetFloat(WeaponVolumeKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Debug mixer overlay (F10). Owner: island-demo-wrapup</summary>
        public static float IslandMusicVolume
        {
            get => PlayerPrefs.GetFloat(IslandMusicVolumeKey, Tunables.I.DefaultIslandMusicVolume);
            set
            {
                PlayerPrefs.SetFloat(IslandMusicVolumeKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Debug mixer overlay (F10). Owner: island-demo-wrapup</summary>
        public static float SpotterVolume
        {
            get => PlayerPrefs.GetFloat(SpotterVolumeKey, Tunables.I.DefaultSpotterVolume);
            set
            {
                PlayerPrefs.SetFloat(SpotterVolumeKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Whether the active URP asset's Render Scale + Upscaling Filter (FSR) are applied. Default off (Carlos, 2026-09-15) - upscaling is opt-in, not the out-of-the-box experience. Owner: MRM-78/MRM-79</summary>
        public static bool UpscalingEnabled
        {
            get => PlayerPrefs.GetInt(UpscalingEnabledKey, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(UpscalingEnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Default matches the project's display target (see CLAUDE.md): 1920x1080 borderless fullscreen. Owner: MRM-78</summary>
        public static FullScreenMode FullscreenMode
        {
            get => (FullScreenMode)PlayerPrefs.GetInt(FullscreenModeKey, (int)FullScreenMode.FullScreenWindow);
            set
            {
                PlayerPrefs.SetInt(FullscreenModeKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Owner: MRM-78</summary>
        public static int ResolutionWidth
        {
            get => PlayerPrefs.GetInt(ResolutionWidthKey, 1920);
            set
            {
                PlayerPrefs.SetInt(ResolutionWidthKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Owner: MRM-78</summary>
        public static int ResolutionHeight
        {
            get => PlayerPrefs.GetInt(ResolutionHeightKey, 1080);
            set
            {
                PlayerPrefs.SetInt(ResolutionHeightKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Graphics quality preset index: 0 = Minimal, 1 = Medium, 2 = High, 3 = Highest. Default
        /// is Highest, which is defined to exactly match <c>PC_RPAsset</c>'s own committed values
        /// (see <see cref="MoonlightTunables"/>'s Graphics Quality Presets header) - a fresh install
        /// looks identical to today's build until the player deliberately lowers it. Owner:
        /// settings-menu, 2026-09-16
        /// </summary>
        public static int GraphicsQualityLevel
        {
            get => PlayerPrefs.GetInt(GraphicsQualityLevelKey, 3);
            set
            {
                PlayerPrefs.SetInt(GraphicsQualityLevelKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Default off (Carlos, 2026-09-16) - found during the FPS-cap investigation that a
        /// leftover PolymindGames vendor asset (GraphicsOptions.Apply(), auto-run on every boot)
        /// was force-setting QualitySettings.vSyncCount = 1 with no UI ever exposing it. This is
        /// now the single source of truth for vSyncCount; SettingsPanel applies it every launch
        /// (ApplySavedDisplaySettings) so it always wins regardless of the vendor asset's own boot
        /// timing. Owner: MRM-78
        /// </summary>
        public static bool VSyncEnabled
        {
            get => PlayerPrefs.GetInt(VSyncEnabledKey, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(VSyncEnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
