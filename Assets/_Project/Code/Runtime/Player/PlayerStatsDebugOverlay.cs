using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Text readout of all six MRM-12 stats - health, stamina, speed, melee, defense, audio
    /// pitch. Hidden by default per the issue's debug HUD requirement (health/stamina get real
    /// bars in the Conformist/Punk difficulty issue, not here). Same on-screen overlay shape as
    /// MRM-8's InputDebugOverlay - not reused directly since it reports different data, and this
    /// one starts hidden where that one starts visible. Toggle with F2 (keyboard) or Start
    /// (gamepad), or the inspector checkbox. Positioned below InputDebugOverlay's rect so both
    /// can be visible in Sandbox without overlapping. Owner: MRM-12
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerStatsDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool visible = false;
        [SerializeField] private PlayerStats stats;

        private GUIStyle _style;

        private void Awake()
        {
            if (stats == null)
            {
                stats = GetComponent<PlayerStats>();
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                visible = !visible;
            }

            if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                normal = { textColor = Color.cyan }
            };

            var rect = new Rect(10, 70, 340, 170);
            GUI.Box(rect, GUIContent.none);

            string lockedSuffix = stats.Stamina.IsLocked ? " (locked)" : "";
            string text =
                "[MRM-12 stat debug]  (F2 / Start to hide)\n" +
                $"Health:   {stats.Health.Value:F1} / {Tunables.I.MaxHealth:F0}\n" +
                $"Stamina:  {stats.Stamina.Value:F1} / {Tunables.I.MaxStamina:F0}{lockedSuffix}\n" +
                $"Speed:    {stats.Speed.Value:F2} m/s\n" +
                $"Melee:    x{stats.MeleeDamage.Value:F2}\n" +
                $"Defense:  x{stats.Defense.Value:F2}\n" +
                $"Pitch:    {stats.CurrentAudioPitch:F2} (target {stats.AudioPitch.Value:F2})";

            GUI.Label(rect, text, _style);
        }
    }
}
