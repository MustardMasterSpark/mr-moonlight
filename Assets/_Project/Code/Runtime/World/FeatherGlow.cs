using DG.Tweening;
using MrMoonlight.Data;
using UnityEngine;

namespace MrMoonlight.World
{
    /// <summary>
    /// Drives a decorative Light that makes a falling feather glow, then fades it out once
    /// <see cref="FeatherFall"/> reports landing - RetroLit has no emission channel to fake this
    /// with (BaseColor + Normal only, see Docs/3d-prop-pipeline-wizard.md), so a real Light plus
    /// Bloom is the project's standing approach for anything that should glow.
    ///
    /// <para>Built for the MainMenu's sparrow feather intro (MRM-18, requested 2026-09-07): dark
    /// screen, glowing feather falls, lands on the water, glow dims out as the rest of the
    /// scenario is revealed. <see cref="DimOut"/> is exposed publicly so an external sequencer can
    /// trigger the fade against its own timing instead of relying on the landing event, if the
    /// reveal ever needs to be authored separately from the physical landing.</para>
    /// </summary>
    [RequireComponent(typeof(FeatherFall))]
    [AddComponentMenu("Mr. Moonlight/World/Feather Glow")]
    public sealed class FeatherGlow : MonoBehaviour
    {
        [Tooltip("Light to drive. Leave empty to use a Light found on this object or a child.")]
        [SerializeField] private Light glowLight;

        [Tooltip("Automatically call DimOut() the moment FeatherFall reports landing. Turn off to drive DimOut() manually from an external sequencer instead.")]
        [SerializeField] private bool dimOnLanded = true;

        private FeatherFall _fall;
        private Tween _dimTween;

        private void Awake()
        {
            _fall = GetComponent<FeatherFall>();
            if (glowLight == null) glowLight = GetComponentInChildren<Light>();
        }

        private void OnEnable()
        {
            if (glowLight != null) glowLight.intensity = Tunables.I.FeatherGlowIntensity;
            if (_fall != null) _fall.OnLanded += HandleLanded;
        }

        private void OnDisable()
        {
            if (_fall != null) _fall.OnLanded -= HandleLanded;
            _dimTween?.Kill();
        }

        private void HandleLanded()
        {
            if (dimOnLanded) DimOut();
        }

        /// <summary>Fades the glow down to <see cref="MoonlightTunables.FeatherGlowDimmedIntensity"/> over <see cref="MoonlightTunables.FeatherGlowDimDuration"/>. Safe to call from an external sequencer instead of waiting on the automatic landing hook.</summary>
        public void DimOut()
        {
            if (glowLight == null) return;

            _dimTween?.Kill();
            _dimTween = glowLight.DOIntensity(Tunables.I.FeatherGlowDimmedIntensity, Tunables.I.FeatherGlowDimDuration)
                .SetLink(gameObject);
        }

        /// <summary>Restores the glow to full intensity instantly - e.g. if the fall is restarted from <see cref="FeatherFall.StartFalling"/>.</summary>
        public void ResetGlow()
        {
            _dimTween?.Kill();
            if (glowLight != null) glowLight.intensity = Tunables.I.FeatherGlowIntensity;
        }
    }
}
