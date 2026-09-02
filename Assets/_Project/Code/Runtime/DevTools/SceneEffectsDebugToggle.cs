using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.Runtime
{
    /// <summary>
    /// Debug-only: flip HAZE fog (<b>F6</b>) and the CRT post effect (<b>F7</b>) while playing, so
    /// a look can be judged with and without them without leaving play mode. Same shape as the
    /// player's F3/F4/F5 toggles — one key each, an on-screen label while the scene is in a
    /// modified state. Owner: MRM-11, requested 2026-09-02.
    ///
    /// <para><b>It drives <see cref="SceneEffectsToggle"/> rather than reimplementing it.</b> That
    /// component already knows the two awkward parts — that HAZE and Retro Shaders Pro live in
    /// assemblies this one cannot reference (hence its lookup by type name), and that fog has two
    /// independent sources, a global Volume override <i>and</i> every <c>HazeDensityVolume</c> in
    /// the scene. Duplicating either would mean this key looked like it worked while fog kept
    /// rendering.</para>
    ///
    /// <para><b>Why it restores on exit.</b> <see cref="SceneEffectsToggle"/> writes to the shared
    /// Volume <i>profile asset</i>, by design, so its inspector checkboxes persist like any other
    /// manual edit. An asset edited during play mode is <b>not</b> rolled back when play mode ends
    /// — unlike a scene object — so without this a cheat key press would quietly change the
    /// project's shipping look. The state found at startup is put back in
    /// <see cref="OnDisable"/>, and only if a key was actually pressed, so this component never
    /// fights a deliberate inspector setting.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Mr. Moonlight/Dev Tools/Scene Effects Debug Toggle")]
    public sealed class SceneEffectsDebugToggle : MonoBehaviour
    {
        [Tooltip("The toggle that owns fog and CRT. Found on this GameObject, then anywhere in the scene, if left empty.")]
        [SerializeField] private SceneEffectsToggle sceneEffects;

        [Tooltip("Put the startup fog/CRT state back when play mode ends. Leave on — the toggle writes to a shared profile asset, which play mode does not roll back.")]
        [SerializeField] private bool restoreOnExit = true;

        [Tooltip("Debug overlay font, so it doesn't read as generic default-Unity text. Matches the F3/F4/F5 toggles.")]
        [SerializeField] private Font font;

        private bool _fogAtStart;
        private bool _crtAtStart;
        private bool _touched;
        private GUIStyle _style;

        private void Awake()
        {
            if (sceneEffects == null) sceneEffects = GetComponent<SceneEffectsToggle>();
            if (sceneEffects == null) sceneEffects = FindFirstObjectByType<SceneEffectsToggle>(FindObjectsInactive.Include);

            if (sceneEffects == null)
            {
                Debug.LogWarning($"[{nameof(SceneEffectsDebugToggle)}] No {nameof(SceneEffectsToggle)} in the scene — F6 and F7 will do nothing.", this);
                enabled = false;
                return;
            }

            _fogAtStart = sceneEffects.FogEnabled;
            _crtAtStart = sceneEffects.CrtEnabled;
        }

        private void OnDisable()
        {
            if (!restoreOnExit || !_touched || sceneEffects == null) return;

            sceneEffects.FogEnabled = _fogAtStart;
            sceneEffects.CrtEnabled = _crtAtStart;
            _touched = false;
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                sceneEffects.FogEnabled = !sceneEffects.FogEnabled;
                _touched = true;
            }

            if (Keyboard.current.f7Key.wasPressedThisFrame)
            {
                sceneEffects.CrtEnabled = !sceneEffects.CrtEnabled;
                _touched = true;
            }
        }

        private void OnGUI()
        {
            // Silent until a key is pressed, so it costs nothing on a normal playtest — but once
            // the scene is in a modified state it says so permanently, because "why does this look
            // wrong" is exactly the question a forgotten toggle causes.
            if (!_touched || sceneEffects == null) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.cyan }
            };

            string fog = sceneEffects.FogEnabled ? "ON" : "OFF";
            string crt = sceneEffects.CrtEnabled ? "ON" : "OFF";

            // Below the F3/F4 labels so all of them can be on screen at once.
            var rect = new Rect(Screen.width / 2f - 250f, 100f, 500f, 30f);
            GUI.Label(rect, $"FOG {fog} (F6)   ·   CRT {crt} (F7)", _style);
        }
    }
}
