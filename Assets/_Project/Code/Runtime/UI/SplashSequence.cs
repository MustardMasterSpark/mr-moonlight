using System.Collections;
using MrMoonlight.Data;
using UnityEngine;

namespace MrMoonlight.UI
{
    /// <summary>
    /// The pre-menu intro (MRM-18, requested 2026-08-26): a fixed sequence of black-screen title
    /// cards - studio name, then a disclaimer - each fading its text in, holding, then fading out,
    /// one after another. Lives entirely inside the MainMenu scene, riding on top of
    /// <see cref="FadeOverlay"/>'s already-opaque black background rather than owning one itself
    /// - <see cref="MainMenuController"/> runs this first and only starts its own opening reveal
    /// once every card has finished. Card text is Carlos's own placeholder, edited by hand later -
    /// same spirit as the credits roll's placeholder Lorem Ipsum. Owner: MRM-18
    /// </summary>
    public sealed class SplashSequence : MonoBehaviour
    {
        [SerializeField] private CanvasGroup[] cards = System.Array.Empty<CanvasGroup>();

        private void Awake()
        {
            foreach (CanvasGroup card in cards)
            {
                card.alpha = 0f;
            }
        }

        /// <summary>Plays every card in order, fade in / hold / fade out, and returns once the last one has faded out.</summary>
        public Coroutine Play() => StartCoroutine(RunSequence());

        private IEnumerator RunSequence()
        {
            foreach (CanvasGroup card in cards)
            {
                yield return Fade(card, 0f, 1f, Tunables.I.SplashCardFadeDuration);
                yield return new WaitForSeconds(Tunables.I.SplashCardHoldDuration);
                yield return Fade(card, 1f, 0f, Tunables.I.SplashCardFadeDuration);
            }
        }

        private static IEnumerator Fade(CanvasGroup card, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                card.alpha = Mathf.Lerp(from, to, duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            card.alpha = to;
        }
    }
}
