using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.DevTools
{
    /// <summary>
    /// On-screen readout of the ACTUAL runtime <see cref="Screen.fullScreenMode"/> and
    /// <see cref="QualitySettings.vSyncCount"/>, plus a smoothed FPS counter — shows what the game
    /// really applied at runtime instead of trusting the Settings menu's displayed selection. Born
    /// as a one-off diagnostic during the MRM-78 FPS-cap investigation (2026-09-16) - promoted to a
    /// permanent F12 cheat toggle afterward since it stayed useful. Lives on a persistent
    /// (DontDestroyOnLoad) GameObject in MainMenu.unity, same convention as
    /// InvulnerableDebugToggle/InfiniteAmmoDebugToggle. Starts hidden. Owner: MRM-78
    /// </summary>
    public sealed class DisplayModeDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool visible = false;

        private float _smoothedDeltaTime;
        private GUIStyle _style;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f12Key.wasPressedThisFrame)
                visible = !visible;

            _smoothedDeltaTime += (Time.unscaledDeltaTime - _smoothedDeltaTime) * 0.1f;
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.green }
            };

            float fps = 1f / _smoothedDeltaTime;
            string text =
                $"{fps:0.} FPS ({_smoothedDeltaTime * 1000f:0.0} ms)   " +
                $"Screen.fullScreenMode={Screen.fullScreenMode}   " +
                $"QualitySettings.vSyncCount={QualitySettings.vSyncCount}   " +
                $"Application.targetFrameRate={Application.targetFrameRate}   " +
                $"currentResolution={Screen.currentResolution}\n" +
                "(F12 to hide)";

            var rect = new Rect(20, 20, 1200, 60);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, text, _style);
        }
    }
}
