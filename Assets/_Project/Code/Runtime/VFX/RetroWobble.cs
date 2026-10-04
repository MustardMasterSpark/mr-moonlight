using MrMoonlight.Data;
using UnityEngine;

namespace MrMoonlight.VFX
{
    /// <summary>
    /// Drives the <c>_RetroWobbleScale</c> shader global that scales the PS1 vertex snapping and affine
    /// texture swim on every RetroLit material at once (see RetroSurfaceInput.hlsl and
    /// Docs/retro-wobble-global.md). It is a shader global, so changing it is live and free: no material
    /// is swapped or touched. 0 = off, 1 = the full per-material effect. The boot value comes from
    /// <see cref="MoonlightTunables.RetroWobbleScale"/> (0 today); gameplay code such as a mushroom or
    /// drug effect calls <see cref="Set"/> to ramp it. Owner: MRM-88
    /// </summary>
    public static class RetroWobble
    {
        private static readonly int ScaleId = Shader.PropertyToID("_RetroWobbleScale");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyBootValue() => Set(Tunables.I.RetroWobbleScale);

        /// <summary>Sets the wobble scale live, clamped to 0..1.</summary>
        public static void Set(float scale) => Shader.SetGlobalFloat(ScaleId, Mathf.Clamp01(scale));
    }
}
