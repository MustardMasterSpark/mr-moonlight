using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MrMoonlight.UI
{
    /// <summary>
    /// Drives the "punk" jagged outline (Assets/_Project/Art/UI/Shaders/UI_PunkOutline.shader) on
    /// a dedicated <see cref="overlayImage"/>, plus a white-tint brighten on this button's own
    /// paper sprite, when this button is hovered or gamepad/keyboard-selected. Same visual
    /// language as the Highlight Plus 2 stylized outline tuned in the Playground project
    /// (Docs/highlight-plus-2-punk-outline.md) - that component only supports
    /// MeshRenderer/SpriteRenderer/SkinnedMeshRenderer, not uGUI CanvasRenderer, so this
    /// reimplements the look directly as UI shaders instead of porting it. Carlos's ask
    /// (2026-09-08): "for now" - a fallback pending a real port, not a permanent design decision.
    ///
    /// The outline shader marches rings outward from the button sprite's own alpha silhouette (the
    /// torn paper cutout shape), so it hugs whatever shape the current background sprite is - it is
    /// NOT a rectangle border. It runs on a SEPARATE, larger overlay quad rather than this button's
    /// own Image: these torn-paper source textures have almost no transparent margin around the
    /// paper shape (confirmed by inspecting one directly), so there's no room inside the texture's
    /// own UV range for an outward-growing outline - the overlay is sized bigger than the button
    /// (by <see cref="overlayPaddingPx"/> on every side, recomputed at Awake from the button's
    /// actual current RectTransform size) purely so the outline has somewhere to draw into, while
    /// the sprite itself stays the same size/position (the real button Image is untouched other
    /// than the tint below). Carlos's explicit call (2026-09-08): fine for this to spill over onto
    /// neighboring buttons for now.
    ///
    /// The white tint uses a second small shader (UI_WhiteTint.shader) rather than
    /// UnityEngine.UI.Graphic.color: Image.color is packed into the mesh as a byte-precision vertex
    /// color, so any channel above 1.0 silently clamps to 1.0 with zero visual effect - and 1.0 is
    /// already a button's normal color, so a plain ">1 multiply" trick (which works on a real
    /// Renderer/Material) can never actually brighten a uGUI Image, confirmed live (Image.color
    /// read back as set, but rendered pixels were unchanged from a non-highlighted button). The
    /// tint shader instead lerps the sampled texture color toward white by a material float
    /// uniform, which isn't byte-packed and genuinely brightens.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class ButtonPunkOutlineHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [Tooltip("Shared punk-outline material to instantiate for this button (Assets/_Project/Art/UI/ButtonHighlight/M_PunkOutline.mat). Instantiated per-button so each button's fade doesn't fight the others.")]
        [SerializeField] private Material outlineMaterialSource;

        [Tooltip("Shared white-tint material to instantiate for this button's own Image (Assets/_Project/Art/UI/ButtonHighlight/M_ButtonWhiteTint.mat).")]
        [SerializeField] private Material tintMaterialSource;

        [Tooltip("How long the outline/tint takes to fade in/out, in seconds.")]
        [SerializeField] private float fadeDuration = 0.15f;

        [Tooltip("How far beyond this button's own rect (in canvas pixels, each side) the highlight overlay extends - the room the outline has to draw into. Recomputed against the button's current rect size at Awake, so resizing the button later keeps this consistent.")]
        [SerializeField] private float overlayPaddingPx = 20f;

        [Tooltip("Dedicated child Image the outline actually renders on (bigger rect than this button, same sprite). Not this button's own Image.")]
        [SerializeField] private Image overlayImage;

        [Tooltip("How far this button's own paper sprite blends toward white while highlighted (0 = no change, 1 = fully white). Carlos's ask (2026-09-08): a slight white tint on the paper itself so the outline reads as a bigger impact.")]
        [Range(0f, 1f)]
        [SerializeField] private float highlightTintAmount = 0.45f;

        [SerializeField] private Button button;
        [SerializeField] private Image image;

        private Material _outlineInstanceMaterial;
        private Material _tintInstanceMaterial;
        private static readonly int HighlightAmountId = Shader.PropertyToID("_HighlightAmount");
        private static readonly int SpriteTexelSizeId = Shader.PropertyToID("_SpriteTexelSize");
        private static readonly int PaddingUVId = Shader.PropertyToID("_PaddingUV");
        private static readonly int TintAmountId = Shader.PropertyToID("_TintAmount");

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (image == null) image = GetComponent<Image>();

            if (outlineMaterialSource != null && overlayImage != null)
            {
                _outlineInstanceMaterial = new Material(outlineMaterialSource);
                overlayImage.material = _outlineInstanceMaterial;
                overlayImage.sprite = image.sprite;
                ApplyOverlaySizing();
            }

            if (tintMaterialSource != null)
            {
                _tintInstanceMaterial = new Material(tintMaterialSource);
                _tintInstanceMaterial.SetFloat(TintAmountId, 0f);
                image.material = _tintInstanceMaterial;
            }
        }

        // Sizes the overlay rect to overlayPaddingPx bigger than this button's own rect on every
        // side, and tells the shader what fraction of the overlay's own UV range that padding
        // works out to (see _PaddingUV in UI_PunkOutline.shader) so it can remap back down to
        // where the real sprite content sits. Re-applied on every fade-in (not just Awake) in case
        // the button gets resized later.
        private void ApplyOverlaySizing()
        {
            if (_outlineInstanceMaterial == null || overlayImage == null) return;

            RectTransform buttonRect = image.rectTransform;
            RectTransform overlayRect = overlayImage.rectTransform;
            float width = Mathf.Max(buttonRect.rect.width, 1f);
            float height = Mathf.Max(buttonRect.rect.height, 1f);

            overlayRect.anchorMin = new Vector2(0f, 0f);
            overlayRect.anchorMax = new Vector2(1f, 1f);
            overlayRect.offsetMin = new Vector2(-overlayPaddingPx, -overlayPaddingPx);
            overlayRect.offsetMax = new Vector2(overlayPaddingPx, overlayPaddingPx);

            Vector2 paddingUV = new Vector2(overlayPaddingPx / (width + 2f * overlayPaddingPx), overlayPaddingPx / (height + 2f * overlayPaddingPx));
            _outlineInstanceMaterial.SetVector(PaddingUVId, paddingUV);

            // CanvasRenderer doesn't populate the shader's usual auto "_MainTex_TexelSize" - push
            // the real sprite texture size in manually so the outline shader's pixel-radius math
            // is correct (confirmed at runtime: it read back as the (1,1,1,1) default otherwise).
            Texture spriteTexture = overlayImage.sprite != null ? overlayImage.sprite.texture : overlayImage.mainTexture;
            if (spriteTexture != null)
            {
                _outlineInstanceMaterial.SetVector(SpriteTexelSizeId, new Vector4(1f / spriteTexture.width, 1f / spriteTexture.height, spriteTexture.width, spriteTexture.height));
            }
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
            if (_outlineInstanceMaterial != null) Destroy(_outlineInstanceMaterial);
            if (_tintInstanceMaterial != null) Destroy(_tintInstanceMaterial);
        }

        public void OnPointerEnter(PointerEventData eventData) => FadeTo(1f);
        public void OnPointerExit(PointerEventData eventData) => FadeTo(0f);
        public void OnSelect(BaseEventData eventData) => FadeTo(1f);
        public void OnDeselect(BaseEventData eventData) => FadeTo(0f);

        private void FadeTo(float target)
        {
            if (button != null && !button.interactable && target > 0f) return;

            if (target > 0f) ApplyOverlaySizing();

            DOTween.Kill(this);

            if (_outlineInstanceMaterial != null)
            {
                DOTween.To(() => _outlineInstanceMaterial.GetFloat(HighlightAmountId),
                        v => _outlineInstanceMaterial.SetFloat(HighlightAmountId, v),
                        target, fadeDuration)
                    .SetId(this);
            }

            if (_tintInstanceMaterial != null)
            {
                float targetTint = target > 0f ? highlightTintAmount : 0f;
                DOTween.To(() => _tintInstanceMaterial.GetFloat(TintAmountId),
                        v => _tintInstanceMaterial.SetFloat(TintAmountId, v),
                        targetTint, fadeDuration)
                    .SetId(this);
            }
        }
    }
}
