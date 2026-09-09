using UnityEngine;

namespace MrMoonlight.World
{
    /// <summary>
    /// Fires a single ripple particle (the Cartoon Rain &amp; Blood Rain asset's collision-ripple
    /// system) at the exact spot and moment a <see cref="FeatherFall"/> lands (MRM-18 main menu
    /// ask) - independent of the ambient rain's own collision-triggered ripples, which keep
    /// working normally. Works by calling <see cref="ParticleSystem.Emit(ParticleSystem.EmitParams, int)"/>
    /// directly with an explicit world position, which bypasses the ripple system's own (disabled)
    /// shape module - the same mechanism Unity uses internally when the rain's collision module
    /// triggers this same system as a sub-emitter, so this does not disturb that behaviour.
    /// </summary>
    public sealed class FeatherLandingRipple : MonoBehaviour
    {
        [SerializeField] private FeatherFall feather;

        [Tooltip("The rain asset's Ripple particle system (LightRain/Light Rain/RainRipple). Must stay Simulation Space = World for the emitted position to land exactly where specified.")]
        [SerializeField] private ParticleSystem ripple;

        private void OnEnable()
        {
            if (feather != null) feather.OnLanded += HandleLanded;
        }

        private void OnDisable()
        {
            if (feather != null) feather.OnLanded -= HandleLanded;
        }

        private void HandleLanded()
        {
            if (ripple == null || feather == null) return;

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = feather.transform.position
            };
            ripple.Emit(emitParams, 1);
        }
    }
}
