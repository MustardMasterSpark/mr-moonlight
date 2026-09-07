using UnityEngine;

namespace MrMoonlight.World
{
    /// <summary>
    /// Live hue/intensity tint control over the Moon's neutral (white/grayscale) BaseColor
    /// texture, so the red mood can be dialed in directly in the Editor instead of baking a
    /// fixed tint into the material or sourcing a pre-tinted texture. Blends from the texture's
    /// own natural color (intensity 0) toward a fully saturated hue (intensity 1), written into
    /// RetroLit's HDR "_BaseColor" property, which the shader multiplies onto the sampled
    /// texture regardless of lighting mode.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("Mr. Moonlight/World/Moon Tint")]
    public sealed class MoonTint : MonoBehaviour
    {
        [Tooltip("Tint hue, 0-1 around the colour wheel. 0 and 1 are both red.")]
        [Range(0f, 1f)]
        [SerializeField] private float hue = 0f;

        [Tooltip("How strongly the hue tint is blended over the texture's natural white/grayscale colour. 0 = untinted (the plain moon texture), 1 = fully saturated hue.")]
        [Range(0f, 1f)]
        [SerializeField] private float intensity = 0.4f;

        private Renderer _renderer;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void OnEnable()
        {
            _renderer = GetComponent<Renderer>();
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        private void Update()
        {
            Apply();
        }

        private void Apply()
        {
            if (_renderer == null) _renderer = GetComponent<Renderer>();

            Material material = _renderer.sharedMaterial;
            if (material == null) return;

            Color hueColor = Color.HSVToRGB(hue, 1f, 1f);
            Color tint = Color.Lerp(Color.white, hueColor, intensity);
            tint.a = 1f;

            material.SetColor(BaseColorId, tint);
        }
    }
}
