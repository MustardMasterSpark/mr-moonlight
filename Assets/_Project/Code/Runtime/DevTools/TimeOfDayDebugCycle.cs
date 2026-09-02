using MrMoonlight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.Runtime
{
    /// <summary>
    /// Debug-only: <b>F8</b> steps to the next <see cref="TimeManager"/> preset and wraps around at
    /// the end of the list — Morning → Sunset → Night → Apocalypse → Morning. Owner: MRM-11,
    /// requested 2026-09-02.
    ///
    /// <para>Reads <see cref="TimeManager.PresetCount"/> rather than assuming four, so adding a
    /// fifth preset in the inspector needs no code change here.</para>
    ///
    /// <para><b>Transitions are instant, not the usual smooth lerp.</b> The Sun normally eases into
    /// a preset over <c>TimeManagerDefaultTransitionSeconds</c>, which is right for a story beat and
    /// wrong for a cheat key — pressing F8 four times in a row would otherwise queue four
    /// overlapping lerps and land somewhere between two skies. <see cref="transitionSeconds"/> is
    /// exposed anyway for the times the transition itself is the thing being judged.</para>
    ///
    /// <para>Unlike F6/F7 this needs no restore-on-exit: <see cref="TimeManager"/> drives scene
    /// objects — the Sun light and the skybox — and play mode rolls those back on its own. Only the
    /// shared Volume profile asset behind the fog and CRT toggles survives, which is why that one
    /// puts itself back and this one does not.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Mr. Moonlight/Dev Tools/Time Of Day Debug Cycle")]
    public sealed class TimeOfDayDebugCycle : MonoBehaviour
    {
        [Tooltip("The manager whose presets F8 cycles. Found on this GameObject, then anywhere in the scene, if left empty.")]
        [SerializeField] private TimeManager timeManager;

        [Tooltip("Seconds the Sun takes to reach the next preset. 0 = instant, which is what a cheat key wants.")]
        [SerializeField] private float transitionSeconds;

        [Tooltip("Seconds the preset name stays on screen after a press.")]
        [SerializeField] private float labelDuration = 2.5f;

        [Tooltip("Debug overlay font, so it doesn't read as generic default-Unity text. Matches the F3/F4/F5 toggles.")]
        [SerializeField] private Font font;

        private float _lastChangeAt = -99f;
        private string _lastLabel = string.Empty;
        private GUIStyle _style;

        private void Awake()
        {
            if (timeManager == null) timeManager = GetComponent<TimeManager>();
            if (timeManager == null) timeManager = FindFirstObjectByType<TimeManager>(FindObjectsInactive.Include);

            if (timeManager == null)
            {
                Debug.LogWarning($"[{nameof(TimeOfDayDebugCycle)}] No {nameof(TimeManager)} in the scene — F8 will do nothing.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.f8Key.wasPressedThisFrame) return;

            int count = timeManager.PresetCount;
            if (count <= 0)
            {
                Debug.LogWarning($"[{nameof(TimeOfDayDebugCycle)}] {timeManager.name} has no presets to cycle.", this);
                return;
            }

            // CurrentPresetIndex is -1 until something applies a preset, so the first press lands
            // on 0 rather than skipping past it.
            int next = (timeManager.CurrentPresetIndex + 1) % count;
            timeManager.ApplyPreset(next, transitionSeconds);

            _lastLabel = $"{timeManager.GetPresetName(next)}  ({next + 1}/{count})";
            _lastChangeAt = Time.unscaledTime;
        }

        private void OnGUI()
        {
            if (Time.unscaledTime - _lastChangeAt > labelDuration) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.cyan }
            };

            // Below the F6/F7 line so the debug labels stack rather than overlap.
            var rect = new Rect(Screen.width / 2f - 250f, 130f, 500f, 30f);
            GUI.Label(rect, $"TIME OF DAY (F8) — {_lastLabel}", _style);
        }
    }
}
