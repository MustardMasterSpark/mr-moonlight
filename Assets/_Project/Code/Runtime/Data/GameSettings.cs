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
    }
}
