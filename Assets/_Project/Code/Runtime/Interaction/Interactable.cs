using System;
using MrMoonlight.Data;
using UnityEngine;

namespace MrMoonlight.Interaction
{
    /// <summary>
    /// Which of MRM-16's three interaction kinds this object represents - purely descriptive here.
    /// MRM-16 only builds the hook; what's behind each kind (pickup, turret/stretcher use, a
    /// director-locked door) is other issues' scope. Owner: MRM-16
    /// </summary>
    public enum InteractionType
    {
        PickUp,
        Use,
        EventGated
    }

    /// <summary>
    /// Marks a world object as something Tracey can look at and press X on. Detection (proximity +
    /// aim), the prompt fade and the highlight fade all live in <see cref="InteractionDetector"/> -
    /// this component only carries the object's identity and raises <see cref="OnInteracted"/> when
    /// triggered. MRM-41's item pickup and future consumers (turret/stretcher use, an event-gated
    /// door) subscribe here; this component knows nothing about any of them. Owner: MRM-16
    /// </summary>
    public sealed class Interactable : MonoBehaviour
    {
        [Header("Interactable — MRM-16")]

        [SerializeField] private string displayName = "Object";
        [SerializeField] private InteractionType interactionType = InteractionType.PickUp;

        /// <summary>Aim target for the screen-centre angle test. Defaults to this transform if unassigned - set explicitly when the collider's centre isn't a good aim point (e.g. a tall prop).</summary>
        [SerializeField] private Transform aimPoint;

        /// <summary>Renderers this highlights. Auto-populated from this GameObject and its children in Awake if left empty.</summary>
        [SerializeField] private Renderer[] highlightRenderers;

        [Header("Highlight override (optional)")]
        [SerializeField] private bool overrideHighlightColor;
        [SerializeField] private Color highlightColorOverride = Color.white;
        [SerializeField] private bool overrideHighlightIntensity;
        [SerializeField] private float highlightIntensityOverride;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _propertyBlock;

        public string DisplayName => displayName;
        public InteractionType Type => interactionType;
        public Transform AimPoint => aimPoint != null ? aimPoint : transform;

        /// <summary>
        /// Whether this object currently registers with <see cref="InteractionDetector"/> at all.
        /// An event-gated door (hook #3) starts false and the level's Event Director sets it true
        /// once unlocked - no prompt or highlight shows while false. True by default, which is
        /// correct for pick-up/use interactables. Owner: MRM-16
        /// </summary>
        public bool IsInteractable { get; set; } = true;

        /// <summary>
        /// Raised when X is pressed while this is the current target. <paramref name="interactor"/>
        /// is the interacting GameObject (the player), passed through rather than looked up, so
        /// subscribers (MRM-41's item pickup) can reach the interactor's own components without a
        /// scene Find. Owner: MRM-16
        /// </summary>
        public event Action<Interactable, GameObject> OnInteracted;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();

            if (highlightRenderers == null || highlightRenderers.Length == 0)
            {
                highlightRenderers = GetComponentsInChildren<Renderer>();
            }

            // Emission must be enabled on the shared material for the keyword to evaluate at all -
            // the per-renderer property block set in SetHighlight only ever overrides this
            // instance's colour away from the material's own (default black) emission, so other
            // renderers sharing the same material asset stay visually unaffected until they're
            // highlighted themselves. Runtime-only mutation (not saved to the asset).
            foreach (var renderer in highlightRenderers)
            {
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    renderer.sharedMaterial.EnableKeyword("_EMISSION");
                }
            }
        }

        /// <summary>Called by <see cref="InteractionDetector"/> when X is pressed while this is the current target.</summary>
        public void Interact(GameObject interactor)
        {
            if (!IsInteractable)
            {
                return;
            }

            OnInteracted?.Invoke(this, interactor);
        }

        /// <summary>
        /// Blends this object's emission from none (0) to its configured highlight colour/intensity
        /// (1). Called every frame by <see cref="InteractionDetector"/> with its shared fade value -
        /// never jumps straight to full intensity, matching the prompt's own fade. Owner: MRM-16
        /// </summary>
        public void SetHighlight(float t)
        {
            if (highlightRenderers == null || highlightRenderers.Length == 0)
            {
                return;
            }

            Color color = overrideHighlightColor ? highlightColorOverride : Tunables.I.InteractionHighlightColor;
            float intensity = overrideHighlightIntensity ? highlightIntensityOverride : Tunables.I.InteractionHighlightIntensity;

            _propertyBlock.SetColor(EmissionColorId, color * (intensity * Mathf.Clamp01(t)));

            foreach (var renderer in highlightRenderers)
            {
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(_propertyBlock);
                }
            }
        }
    }
}
