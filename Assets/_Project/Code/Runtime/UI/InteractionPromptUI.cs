using MrMoonlight.Interaction;
using TMPro;
using UnityEngine;

namespace MrMoonlight.UI
{
    /// <summary>
    /// Unstyled placeholder for MRM-16's on-screen interaction prompt - "unstyled first", the same
    /// pattern already established for MRM-18/19 (see <see cref="GameOverPanel"/>). Driven entirely
    /// by <see cref="Interaction.InteractionDetector"/>: <see cref="SetVisibility"/> every frame for
    /// the smooth fade the issue calls out explicitly, <see cref="SetContent"/> only when the
    /// current target changes. No icon sprite yet - text-only until art exists. Owner: MRM-16
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text label;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>();
            }

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0f;
        }

        /// <summary>0-1, applied straight to the CanvasGroup's alpha - the caller already computes the smooth fade, this just displays it.</summary>
        public void SetVisibility(float t)
        {
            canvasGroup.alpha = Mathf.Clamp01(t);
        }

        /// <summary>Updates the prompt's text. Pass null to clear it (visibility will already be fading toward 0 in that case).</summary>
        public void SetContent(Interactable target)
        {
            if (label == null)
            {
                return;
            }

            label.SetText(target != null ? $"[X] {target.DisplayName}" : string.Empty);
        }
    }
}
