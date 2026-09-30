using UnityEngine;

namespace MrMoonlight.Data
{
    /// <summary>
    /// Applies the saved vSync choice and lifts the frame-rate cap at boot, whichever scene the game starts in.
    /// The leftover PolymindGames <c>GraphicsOptions.Apply()</c> forces <c>QualitySettings.vSyncCount = 1</c>
    /// on every launch, and the only override (<c>SettingsPanel.ApplySavedDisplaySettings</c>) lives in the
    /// MainMenu scene, so any build or editor Play that starts elsewhere (Island, Island_Legion, the Lighting
    /// Test Scene) was locked to the monitor's refresh rate (75 fps on a 75 Hz screen, 2026-09-30).
    /// Polymind applies once at SubsystemRegistration, so this BeforeSceneLoad hook always runs after it.
    /// <see cref="GameSettings.VSyncEnabled"/> stays the single source of truth (default off). Owner: MRM-85
    /// </summary>
    public static class DisplayBootSettings
    {
        private const int UnlimitedFrameRate = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            QualitySettings.vSyncCount = GameSettings.VSyncEnabled ? 1 : 0;
            Application.targetFrameRate = UnlimitedFrameRate;
        }
    }
}
