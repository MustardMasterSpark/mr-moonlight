using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.UI;

namespace MrMoonlight.VFX
{
    /// <summary>
    /// The one thing that actually draws the shared red tint registered on <see cref="ScreenTint"/>.
    /// Sums every active contribution, clamps to <see cref="MoonlightTunables.RedTintCeiling"/> -
    /// the shared ceiling MRM-17's acceptance criteria call out so the death tint and MRM-53's
    /// future health tint can't blow past a sane maximum between them - and applies the result as
    /// this Image's alpha.
    ///
    /// Two layers, both driven by the same computed alpha: a flat, full-screen solid-colour
    /// <see cref="Image"/> underneath (the thing that actually floods the whole screen red - a
    /// textured overlay alone cannot, see below) and a <see cref="RawImage"/> on top using
    /// Carlos's Veins_1 art (Tex_UI preset - Default texture type, so RawImage rather than a
    /// Sprite-based Image) for detail/character. <b>The flat layer is required, not decorative:</b>
    /// Veins_1 is edge-concentrated with a transparent centre (a damage-vignette texture), so no
    /// amount of alpha on it alone can ever redden the middle of the screen - confirmed live,
    /// Carlos saw the veins intensify at the edges while the centre stayed sky-blue right up to
    /// death. The flat layer is what makes "extremely red before the blackout" actually happen.
    ///
    /// Neither is a URP Volume override: there is no post-processing profile in the project yet,
    /// and a UI overlay is the cheapest way to get this on WebGL today. If a shared-Volume
    /// approach lands later (see Docs/webgl-constraints.md §4), this is the only class that needs
    /// to change - contributors calling <see cref="ScreenTint.SetRed"/> never touch it either way.
    /// Owner: MRM-17
    /// </summary>
    public sealed class ScreenTintRenderer : MonoBehaviour
    {
        [SerializeField] private Image flatRedOverlay;
        [SerializeField] private RawImage redTintImage;

        private void Awake()
        {
            if (redTintImage == null)
            {
                redTintImage = GetComponent<RawImage>();
            }

            if (redTintImage == null)
            {
                Debug.LogError($"[ScreenTint] {name} has no RawImage to render the veins overlay onto. See MRM-17.");
            }

            if (flatRedOverlay == null)
            {
                Debug.LogError($"[ScreenTint] {name} has no flat Image to render the full-screen red wash onto. See MRM-17.");
            }
        }

        private void Update()
        {
            float total = 0f;
            foreach (float contribution in ScreenTint.RedContributions.Values)
            {
                total += contribution;
            }

            float alpha = Mathf.Min(total, Tunables.I.RedTintCeiling);

            if (redTintImage != null)
            {
                Color veinsColor = redTintImage.color;
                veinsColor.a = alpha;
                redTintImage.color = veinsColor;
            }

            if (flatRedOverlay != null)
            {
                Color flatColor = flatRedOverlay.color;
                flatColor.a = alpha;
                flatRedOverlay.color = flatColor;
            }
        }
    }
}
