using MrMoonlight.Data;
using UnityEngine;

namespace MrMoonlight.UI
{
    /// <summary>
    /// Minimal proof that the Settings panel's difficulty pick actually reaches the demo scene,
    /// per MRM-18's acceptance criterion "difficulty selection reaches the game scene and is
    /// readable there." Deliberately just an OnGUI readout, same "unstyled, functional only"
    /// spirit as the rest of this issue - no difficulty-scaling systems exist yet for it to drive.
    /// Same debug-overlay shape as <see cref="MrMoonlight.Input.InputDebugOverlay"/> and
    /// <see cref="MrMoonlight.Player.PlayerStatsDebugOverlay"/>. Owner: MRM-18
    /// </summary>
    public sealed class DifficultyDebugOverlay : MonoBehaviour
    {
        [SerializeField] private Rect screenRect = new Rect(10f, 10f, 260f, 24f);

        private void OnGUI()
        {
            GUI.Label(screenRect, $"Difficulty: {GameSettings.Difficulty}");
        }
    }
}
