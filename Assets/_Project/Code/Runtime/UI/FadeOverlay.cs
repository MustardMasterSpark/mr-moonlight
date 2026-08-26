using System.Collections;
using UnityEngine;

namespace MrMoonlight.UI
{
    /// <summary>
    /// A full-screen solid-colour CanvasGroup that fades to opaque or clear over a given
    /// duration, blocking raycasts whenever it is even partially visible. The main menu's
    /// (MRM-18) shared black overlay - the opening reveal, Start's fade-to-black before loading
    /// the demo scene, and Quit's fade-to-black all drive this same component. Settings and
    /// Credits do not use this - they crossfade their own panel CanvasGroups instead, since their
    /// spec keeps the staged background visible underneath (Settings) or scrolls credits on their
    /// own opaque panel (Credits). Owner: MRM-18
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class FadeOverlay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        private Coroutine _routine;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        /// <summary>Starts (immediately, before the opening reveal runs) fully opaque and blocking - the "black screen" the menu opens on.</summary>
        public void SetOpaqueInstant()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        public Coroutine FadeToOpaque(float duration) => Run(1f, duration);

        public Coroutine FadeToClear(float duration) => Run(0f, duration);

        private Coroutine Run(float target, float duration)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(FadeRoutine(target, duration));
            return _routine;
        }

        private IEnumerator FadeRoutine(float target, float duration)
        {
            float start = canvasGroup.alpha;
            canvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(start, target, duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            canvasGroup.alpha = target;
            canvasGroup.blocksRaycasts = target > 0f;
            _routine = null;
        }
    }
}
