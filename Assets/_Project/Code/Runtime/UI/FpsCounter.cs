using TMPro;
using UnityEngine;

namespace MrMoonlight.UI
{
    /// <summary>
    /// On-screen frames-per-second readout, bottom-left.
    ///
    /// Lives on the `FPS Counter` prefab (`Assets/_Project/Prefabs/UI/`), which carries its own
    /// Canvas so it can be dropped into ANY scene on its own - no dependency on the HUD Canvas or
    /// on anything else in the scene. Drag it in, press play, delete it when done.
    ///
    /// Two deliberate choices, both because this thing exists to MEASURE performance and must not
    /// meaningfully change what it measures:
    ///
    /// - `TMP_Text.SetText` with format arguments is allocation-free. Building the string with
    ///   interpolation or concatenation would allocate every refresh, adding GC pressure to the
    ///   very frame times being reported - which matters more on WebGL than anywhere else.
    /// - Timing uses `Time.unscaledDeltaTime`, so the reading stays honest when `Time.timeScale`
    ///   changes (pause menus, the MRM-17 death sequence).
    ///
    /// Averaged over `refreshSeconds` rather than shown per-frame: a per-frame number flickers too
    /// fast to read and tells you less than a short average.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/FPS Counter")]
    public sealed class FpsCounter : MonoBehaviour
    {
        [Tooltip("Text element to write into. Auto-filled from children if left empty.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("How often the reading updates, in seconds. Lower = twitchier, harder to read.")]
        [SerializeField, Range(0.05f, 2f)] private float refreshSeconds = 0.25f;

        [Tooltip("Green at 50+, amber 30-50, red below 30.")]
        [SerializeField] private bool colourCode = true;

        private static readonly Color Good = new Color(0.55f, 0.95f, 0.45f);
        private static readonly Color Warn = new Color(1.00f, 0.80f, 0.25f);
        private static readonly Color Bad = new Color(1.00f, 0.35f, 0.30f);

        private float elapsed;
        private int frames;

        private void Reset() => label = GetComponentInChildren<TMP_Text>();

        private void Awake()
        {
            if (label == null) label = GetComponentInChildren<TMP_Text>();
            if (label == null)
            {
                Debug.LogError($"{nameof(FpsCounter)} on '{name}' has no TMP_Text to write to.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            frames++;
            elapsed += Time.unscaledDeltaTime;

            if (elapsed < refreshSeconds) return;

            float fps = frames / elapsed;
            label.SetText("{0:0} FPS   {1:0.0} ms", fps, 1000f / fps);

            if (colourCode)
                label.color = fps >= 50f ? Good : fps >= 30f ? Warn : Bad;

            frames = 0;
            elapsed = 0f;
        }
    }
}
