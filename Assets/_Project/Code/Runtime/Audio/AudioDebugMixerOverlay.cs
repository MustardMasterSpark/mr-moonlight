using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

namespace MrMoonlight.Audio
{
    /// <summary>
    /// Four temporary tuning sliders — Carlos's explicit ask for the island demo wrap-up audio pass
    /// (2026-09-10): "not the proper way to tune the sound," just a way to adjust levels himself
    /// before a build, without asking for a code change each time. Deliberately separate from the
    /// polished <c>SettingsPanel</c> (MRM-18, Master/Voices/SFX) — that one is the real UI; this is a
    /// debug overlay, same OnGUI shape as <c>PlayerStatsDebugOverlay</c> (F2). Toggle with F10.
    ///
    /// Slider 1 (Menu Music) and Slider 3 (Island Music) write to <see cref="moonlightMixer"/>'s
    /// <c>MenuMusicVolume</c>/<c>IslandMusicVolume</c> exposed parameters — new groups added under
    /// Master alongside Voices/SFX. Slider 4 (Spotter SFX) writes to
    /// <see cref="moonlightMixer"/>'s <c>SpotterVolume</c>, a new group every EnemyAudioHooks
    /// AudioSource is routed to. Slider 2 (Weapon Sounds) writes to <see cref="weaponsMixer"/>'s
    /// existing <c>EffectsVolume</c> — the Polymind FPS_AudioMixer group every weapon fire/reload/
    /// equip sound already routes through, so no new group was needed there. Values persist via
    /// <see cref="GameSettings"/> and are (re)applied in <see cref="Start"/> so a fresh launch always
    /// reflects whatever Carlos last set, not the mixer's checked-in defaults. Owner: island-demo-wrapup.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Audio/Audio Debug Mixer Overlay")]
    public sealed class AudioDebugMixerOverlay : MonoBehaviour
    {
        private const string MenuMusicParam = "MenuMusicVolume";
        private const string IslandMusicParam = "IslandMusicVolume";
        private const string SpotterParam = "SpotterVolume";
        private const string WeaponEffectsParam = "EffectsVolume";

        [Tooltip("MoonlightMixer.mixer — owns MenuMusicVolume, IslandMusicVolume and SpotterVolume.")]
        [SerializeField] private AudioMixer moonlightMixer;

        [Tooltip("FPS_AudioMixer.mixer (Polymind) — owns EffectsVolume, which every weapon sound already routes through.")]
        [SerializeField] private AudioMixer weaponsMixer;

        [SerializeField] private bool visible;
        [SerializeField] private Font font;

        private GUIStyle _labelStyle;
        private float _menuMusic;
        private float _weapon;
        private float _islandMusic;
        private float _spotter;

        private void Start()
        {
            _menuMusic = GameSettings.MenuMusicVolume;
            _weapon = GameSettings.WeaponVolume;
            _islandMusic = GameSettings.IslandMusicVolume;
            _spotter = GameSettings.SpotterVolume;

            ApplyAll();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame)
            {
                visible = !visible;
            }
        }

        private void ApplyAll()
        {
            AudioMixerVolume.Apply(moonlightMixer, MenuMusicParam, _menuMusic);
            AudioMixerVolume.Apply(weaponsMixer, WeaponEffectsParam, _weapon);
            AudioMixerVolume.Apply(moonlightMixer, IslandMusicParam, _islandMusic);
            AudioMixerVolume.Apply(moonlightMixer, SpotterParam, _spotter);
        }

        private void OnGUI()
        {
            if (!visible) return;

            _labelStyle ??= new GUIStyle(GUI.skin.label) { font = font, fontSize = 14, normal = { textColor = Color.white } };

            var rect = new Rect(10, 250, 320, 150);
            GUI.Box(rect, GUIContent.none);

            GUI.Label(new Rect(20, 258, 300, 20), "[Debug audio mix]  (F10 to hide)", _labelStyle);

            float newMenuMusic = Slider(298, "Menu music", _menuMusic);
            float newWeapon = Slider(320, "Weapon sounds", _weapon);
            float newIslandMusic = Slider(342, "Island music", _islandMusic);
            float newSpotter = Slider(364, "Spotter SFX", _spotter);

            if (newMenuMusic != _menuMusic)
            {
                _menuMusic = newMenuMusic;
                GameSettings.MenuMusicVolume = _menuMusic;
                AudioMixerVolume.Apply(moonlightMixer, MenuMusicParam, _menuMusic);
            }

            if (newWeapon != _weapon)
            {
                _weapon = newWeapon;
                GameSettings.WeaponVolume = _weapon;
                AudioMixerVolume.Apply(weaponsMixer, WeaponEffectsParam, _weapon);
            }

            if (newIslandMusic != _islandMusic)
            {
                _islandMusic = newIslandMusic;
                GameSettings.IslandMusicVolume = _islandMusic;
                AudioMixerVolume.Apply(moonlightMixer, IslandMusicParam, _islandMusic);
            }

            if (newSpotter != _spotter)
            {
                _spotter = newSpotter;
                GameSettings.SpotterVolume = _spotter;
                AudioMixerVolume.Apply(moonlightMixer, SpotterParam, _spotter);
            }
        }

        private float Slider(float y, string label, float value)
        {
            GUI.Label(new Rect(20, y, 120, 20), label, _labelStyle);
            return GUI.HorizontalSlider(new Rect(140, y + 3, 170, 20), value, 0f, 1f);
        }
    }
}
