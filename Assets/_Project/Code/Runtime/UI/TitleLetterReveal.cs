using DG.Tweening;
using TMPro;
using UnityEngine;

namespace MrMoonlight.UI
{
    /// <summary>
    /// Fades in the main menu title's letters in a custom, non-left-to-right order Carlos hand-
    /// picked (a jumbled cut-out-poster read on the Mustasurma logotype), each letter also pulling
    /// into focus from a soft blur as it fades in. The title used to be a single TMP_Text with
    /// per-character vertex alpha driven by a hand-rolled coroutine, but that read as an abrupt pop
    /// rather than a smooth fade - Carlos asked to rebuild it as one real GameObject (own TMP_Text
    /// + CanvasGroup) per letter instead, each positioned to match the original combined string
    /// exactly, so DOTween - this project's standard for smooth transitions - can animate real
    /// per-object alpha the same way every other fade in the game works.
    ///
    /// The blur-to-focus part doesn't need Text Animator, a render-texture blur, or any new asset:
    /// TMP's SDF shader ("TextMeshPro/Mobile/Distance Field", same one Mustasurma SDF uses) already
    /// exposes a <c>_Sharpness</c> float on the material (Range -1..1, default 0) that controls how
    /// crisply the signed-distance edge resolves - strongly negative reads as a soft blur, 0 is the
    /// normal crisp edge. Each letter gets its own material instance via <c>TMP_Text.fontMaterial</c>
    /// (a per-object instance, not the shared font asset), so animating it per letter doesn't affect
    /// any other letter or the font asset itself.
    ///
    /// Plain TMP has no built-in per-letter timed fade, and Text Animator's typewriter
    /// (Febucci.TextAnimatorCore, a compiled DLL) is hard-coded to reveal characters strictly by
    /// increasing string index - it cannot reproduce an order that jumps around (e.g. the 'R'
    /// revealing before the first 'M'), and its per-character effect system only works within a
    /// single TMP_Text's own rich-text characters, not across separate GameObjects like this now
    /// is - so it doesn't fit this architecture even for the blur/focus part.
    ///
    /// Call <see cref="Play"/> at the same moment the surrounding menu UI starts revealing itself
    /// (see MainMenuController.PlayFeatherAndWorldReveal) - not on Awake/OnEnable, since this
    /// object sits under mainButtonsGroup but carries its own CanvasGroup with
    /// ignoreParentGroups=true so its letters can be revealed on their own timeline instead of
    /// riding the parent group's single alpha fade.
    /// </summary>
    public sealed class TitleLetterReveal : MonoBehaviour
    {
        [Tooltip("One CanvasGroup per letter GameObject (each its own TMP_Text), in the same order as revealGroups. Positioned to reproduce the original combined-string layout exactly.")]
        [SerializeField] private CanvasGroup[] letters;

        [Tooltip("Gap between reveal groups, in seconds. Carlos's starting point: quick, a quarter second.")]
        [SerializeField] private float groupInterval = 0.25f;

        [Tooltip("How long each individual letter takes to fade from invisible to fully visible.")]
        [SerializeField] private float fadeDuration = 0.4f;

        [Tooltip("Ease curve for each letter's fade-in.")]
        [SerializeField] private Ease fadeEase = Ease.OutCubic;

        [Tooltip("Starting SDF sharpness (material's _Sharpness, range -1..1) each letter blurs in from - strongly negative. Animates up to focusedSharpness over the same reveal window as the alpha fade.")]
        [SerializeField] private float blurredSharpness = -1f;

        [Tooltip("SDF sharpness a letter settles at once fully revealed - 0 matches the font's normal crisp edge.")]
        [SerializeField] private float focusedSharpness = 0f;

        [Tooltip("Ease curve for the blur-to-focus pull. A slower settle than the alpha fade (InOutSine or similar) reads as the letter 'landing' rather than just snapping sharp.")]
        [SerializeField] private Ease focusEase = Ease.InOutSine;

        [Tooltip("1-based reveal group per entry in letters (index 0 = 'group 5' etc. per Carlos's order), same length/order as letters.")]
        [SerializeField] private int[] revealGroups = { 5, 1, 1, 2, 3, 1, 2, 4, 2, 3, 5, 4 };

        private void Awake()
        {
            // Hidden immediately so there's no one-frame flash of the full title before Play()
            // runs - harmless either way since FadeOverlay covers the whole screen until then,
            // but keeps this component correct in isolation too.
            HideAll();
        }

        /// <summary>Starts the reveal from a fully-hidden, unfocused title. Safe to call once per menu open.</summary>
        public void Play()
        {
            DOTween.Kill(this);
            HideAll();

            for (int i = 0; i < letters.Length; i++)
            {
                if (letters[i] == null) continue;

                CanvasGroup letter = letters[i];
                int group = i < revealGroups.Length ? revealGroups[i] : 1;
                float delay = Mathf.Max(0, group - 1) * groupInterval;

                // CanvasGroup.DOFade doesn't resolve in this project (same known gap as
                // AudioSource.DOFade - see Docs/debug-tools.md) - DOTween.To against the raw
                // property is exactly what the shortcut does internally, so nothing is lost.
                DOTween.To(() => letter.alpha, a => letter.alpha = a, 1f, fadeDuration)
                    .SetDelay(delay)
                    .SetEase(fadeEase)
                    .SetId(this);

                TMP_Text tmp = letter.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    Material material = tmp.fontMaterial; // per-object instance, not the shared font asset
                    DOTween.To(() => material.GetFloat(ShaderUtilities.ID_Sharpness),
                            s => material.SetFloat(ShaderUtilities.ID_Sharpness, s),
                            focusedSharpness, fadeDuration)
                        .SetDelay(delay)
                        .SetEase(focusEase)
                        .SetId(this);
                }
            }
        }

        private void HideAll()
        {
            for (int i = 0; i < letters.Length; i++)
            {
                if (letters[i] == null) continue;

                letters[i].alpha = 0f;

                TMP_Text tmp = letters[i].GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    tmp.fontMaterial.SetFloat(ShaderUtilities.ID_Sharpness, blurredSharpness);
                }
            }
        }
    }
}
