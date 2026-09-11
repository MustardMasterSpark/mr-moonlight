using System.Collections;
using DG.Tweening;
using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.UI;

namespace MrMoonlight.UI
{
    /// <summary>
    /// ELVTR-DEMO-ONLY. The two black cinematic bars shown at the start of the ELVTR demo (top +
    /// bottom), matching Carlos's reference still (menu images/letterbox.png) — visible the instant
    /// the level loads, held through <see cref="DemoIntroSubtitles"/>'s lines, then retracted once
    /// the intro is done. <see cref="KillLineDisplay"/> also reads <see cref="IsActive"/> to decide
    /// where its red line sits. See Assets/_Project/Data/Demo/README.txt for why this whole feature
    /// is marked demo-only. Owner: Carlos's ask, 2026-09-10.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/UI/Letterbox Controller (ELVTR demo only)")]
    public sealed class LetterboxController : MonoBehaviour
    {
        [Tooltip("Full-width black bar anchored to the top of the screen.")]
        [SerializeField] private RectTransform topBar;

        [Tooltip("Full-width black bar anchored to the bottom of the screen — the bar DemoIntroSubtitles, and KillLineDisplay while this is still up, show their text against.")]
        [SerializeField] private RectTransform bottomBar;

        [Tooltip("Image on topBar. Coloured from MoonlightTunables.LetterboxColor on Awake if set.")]
        [SerializeField] private Image topBarImage;

        [Tooltip("Image on bottomBar. Coloured from MoonlightTunables.LetterboxColor on Awake if set.")]
        [SerializeField] private Image bottomBarImage;

        /// <summary>The single letterbox live in the loaded scene, if any — set in <see cref="Awake"/>, cleared in <see cref="OnDestroy"/>. Lets <see cref="KillLineDisplay"/> find it without an inspector reference, same pattern as EventDirector.Active.</summary>
        public static LetterboxController Active { get; private set; }

        /// <summary>True from <see cref="ShowInstant"/> until <see cref="Retract"/>'s tween finishes.</summary>
        public bool IsActive { get; private set; }

        private void Awake()
        {
            Active = this;

            MoonlightTunables t = Tunables.I;
            if (topBarImage != null) topBarImage.color = t.LetterboxColor;
            if (bottomBarImage != null) bottomBarImage.color = t.LetterboxColor;
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        /// <summary>Snaps both bars to full height immediately — no animation, matching Carlos's ask that they're already on screen the instant the demo starts.</summary>
        public void ShowInstant()
        {
            gameObject.SetActive(true);
            SetBarHeight(topBar, Tunables.I.LetterboxBarHeight);
            SetBarHeight(bottomBar, Tunables.I.LetterboxBarHeight);
            IsActive = true;
        }

        /// <summary>Shrinks both bars to zero height over <see cref="MoonlightTunables.LetterboxRetractDuration"/>, then disables the letterbox.</summary>
        public Coroutine Retract() => StartCoroutine(RetractRoutine());

        private IEnumerator RetractRoutine()
        {
            float duration = Tunables.I.LetterboxRetractDuration;
            int pending = 0;

            pending++;
            DOTween.To(() => topBar.sizeDelta.y, h => SetBarHeight(topBar, h), 0f, duration)
                .SetEase(Ease.InOutSine).SetLink(gameObject).OnComplete(() => pending--);

            pending++;
            DOTween.To(() => bottomBar.sizeDelta.y, h => SetBarHeight(bottomBar, h), 0f, duration)
                .SetEase(Ease.InOutSine).SetLink(gameObject).OnComplete(() => pending--);

            while (pending > 0) yield return null;

            IsActive = false;
            gameObject.SetActive(false);
        }

        private static void SetBarHeight(RectTransform bar, float height)
        {
            Vector2 size = bar.sizeDelta;
            size.y = height;
            bar.sizeDelta = size;
        }
    }
}
