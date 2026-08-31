using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Debug-only: locks <see cref="PlayerStats.Stamina"/> at max so sprinting never drains it,
    /// for testing traversal/sprint distance between blockout waypoints without stamina cutting
    /// the run short. Uses <see cref="Stat.Lock"/> rather than bypassing the drain/regen code,
    /// so the real stamina behaviour stays intact underneath - unlocking picks back up exactly
    /// where a normal playthrough would be. Toggle with F3 (keyboard) or the inspector checkbox.
    /// Same "debug tool, not shipped content" category as <see cref="PlayerStatsDebugOverlay"/>
    /// and <see cref="StatDebugPoolZone"/>. Owner: sprint-distance testing request, 2026-08-31.
    ///
    /// <para>Shows an always-visible on-screen label whenever active - there's no real stamina
    /// bar yet (that's the Conformist/Punk difficulty issue's job), so without this label the
    /// only sign anything changed is that sprint never slows to walk, which is easy to miss.</para>
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public sealed class InfiniteStaminaDebugToggle : MonoBehaviour
    {
        [SerializeField] private bool infiniteStamina = false;
        [SerializeField] private PlayerStats stats;

        private bool _appliedState;
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
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                infiniteStamina = !infiniteStamina;
            }

            if (infiniteStamina != _appliedState)
            {
                Apply(infiniteStamina);
            }
        }

        private void Apply(bool enable)
        {
            if (enable)
            {
                stats.Stamina.Lock(Tunables.I.MaxStamina);
            }
            else
            {
                stats.Stamina.Unlock();
            }

            _appliedState = enable;
        }

        private void OnGUI()
        {
            if (!_appliedState)
            {
                return;
            }

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.yellow }
            };

            var rect = new Rect(Screen.width / 2f - 200f, 10f, 400f, 30f);
            GUI.Label(rect, "INFINITE STAMINA ON (F3)", _style);
        }
    }
}
