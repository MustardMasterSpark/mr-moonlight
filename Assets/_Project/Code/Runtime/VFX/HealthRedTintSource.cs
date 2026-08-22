using MrMoonlight.Data;
using MrMoonlight.Player;
using UnityEngine;

namespace MrMoonlight.VFX
{
    /// <summary>
    /// Continuously feeds the player's current health into the shared <see cref="ScreenTint"/>
    /// registry, so the screen reddens gradually as health drops - clear at full health, most
    /// visible near zero. This is MRM-53's health-damage tint, not MRM-17's - built ahead of
    /// schedule at Carlos's request on 2026-08-22 because the shared tint mechanism (MRM-17)
    /// already existed and this was a small addition on top of it. MRM-53 still owns retuning
    /// <see cref="MoonlightTunables.HealthRedTintCurve"/> and building the rest of its scope
    /// (radial blur, wound overlays, rear indicator, damage SFX) - none of that is here.
    /// Owner: MRM-53 (implemented during MRM-17)
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public sealed class HealthRedTintSource : MonoBehaviour
    {
        private const string RedTintSourceName = "HealthDamage";

        [SerializeField] private PlayerStats playerStats;

        private void Awake()
        {
            if (playerStats == null)
            {
                playerStats = GetComponent<PlayerStats>();
            }
        }

        private void Update()
        {
            float maxHealth = playerStats.Health.MaxValue;
            float healthLostFraction = maxHealth > 0f ? 1f - playerStats.Health.Value / maxHealth : 0f;
            ScreenTint.SetRed(RedTintSourceName, Tunables.I.HealthRedTintCurve.Evaluate(healthLostFraction));
        }
    }
}
