using MrMoonlight.Audio;
using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace MrMoonlight.UI
{
    /// <summary>
    /// The Settings panel's own logic (MRM-18): the Conformist/Punk difficulty pick (two Toggles
    /// under one ToggleGroup - exclusive by Unity's own mechanism, not hand-rolled), and the
    /// Master/Voices/SFX sliders. Every change writes straight through to <see cref="GameSettings"/>
    /// (so it survives the scene load into the demo scene) and to <paramref name="mixer"/>'s
    /// matching exposed parameter, live, so moving a slider is audible immediately.
    ///
    /// <see cref="ApplySavedAudioSettings"/> is called by <see cref="MainMenuController"/> in its
    /// own Awake, independent of whether this panel's GameObject is active - a fresh launch must
    /// apply the saved (or default) volumes to the mixer before the opening reveal, not only once
    /// the player opens Settings. Owner: MRM-18
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

        private void Awake()
        {
            InitializeFromSavedSettings();

            conformistToggle.onValueChanged.AddListener(OnConformistToggled);
            punkToggle.onValueChanged.AddListener(OnPunkToggled);
            masterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            voicesSlider.onValueChanged.AddListener(OnVoicesVolumeChanged);
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        private void OnDestroy()
        {
            conformistToggle.onValueChanged.RemoveListener(OnConformistToggled);
            punkToggle.onValueChanged.RemoveListener(OnPunkToggled);
            masterSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            voicesSlider.onValueChanged.RemoveListener(OnVoicesVolumeChanged);
            sfxSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
        }

        /// <summary>Writes the saved (or default) volumes to the mixer. Safe to call before this panel's GameObject has ever been active - the serialized <see cref="mixer"/> reference is populated at scene load regardless. Owner: MRM-18</summary>
        public void ApplySavedAudioSettings()
        {
            AudioMixerVolume.Apply(mixer, MasterVolumeParam, GameSettings.MasterVolume);
            AudioMixerVolume.Apply(mixer, VoicesVolumeParam, GameSettings.VoicesVolume);
            AudioMixerVolume.Apply(mixer, SFXVolumeParam, GameSettings.SFXVolume);
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
    }
}
