using UnityEngine;

namespace MrMoonlight.World
{
    /// <summary>
    /// Drives the shared RetroLit wind-sway globals (see RetroWind.hlsl and
    /// Docs/retrolit-wind-sway.md) for every RetroLit material that has opted in
    /// (<c>_WindEnabled</c> on). Built for the MainMenu's staged trees/flowers, but the
    /// underlying shader feature is not menu-specific - drop this same component into any scene
    /// (Island included) to control wind there too.
    ///
    /// One shared direction/speed drives all wind-enabled materials; two independent intensity
    /// sliders scale materials tagged as the Trees or Flowers wind category (per-material
    /// <c>_WindCategory</c> property), so trees and flowers can react differently to the same
    /// wind without touching individual materials.
    /// </summary>
    [ExecuteAlways]
    public sealed class RetroLitWindController : MonoBehaviour
    {
        [Header("Wind")]
        [Tooltip("Compass-style direction the wind blows toward, in degrees (0 = +Z, 90 = +X).")]
        [Range(0f, 360f)] public float windDirection = 0f;

        [Tooltip("How fast the sway oscillates. Low values = slow, gentle drift.")]
        [Range(0f, 3f)] public float windSpeed = 0.6f;

        [Header("Intensity")]
        [Tooltip("Master sway amplitude for materials set to the Trees wind category.")]
        [Range(0f, 3f)] public float treeIntensity = 1f;

        [Tooltip("Master sway amplitude for materials set to the Flowers wind category.")]
        [Range(0f, 3f)] public float flowerIntensity = 1f;

        private static readonly int DirectionID = Shader.PropertyToID("_MoonlightWindDirection");
        private static readonly int SpeedID = Shader.PropertyToID("_MoonlightWindSpeed");
        private static readonly int TreeIntensityID = Shader.PropertyToID("_MoonlightWindTreeIntensity");
        private static readonly int FlowerIntensityID = Shader.PropertyToID("_MoonlightWindFlowerIntensity");

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();
        private void Update() => Apply();

        private void Apply()
        {
            float rad = windDirection * Mathf.Deg2Rad;
            Vector4 direction = new Vector4(Mathf.Sin(rad), 0f, Mathf.Cos(rad), 0f);

            Shader.SetGlobalVector(DirectionID, direction);
            Shader.SetGlobalFloat(SpeedID, windSpeed);
            Shader.SetGlobalFloat(TreeIntensityID, treeIntensity);
            Shader.SetGlobalFloat(FlowerIntensityID, flowerIntensity);
        }
    }
}
