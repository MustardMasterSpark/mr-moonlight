using System.Collections;
using MrMoonlight.Data;
using UnityEngine;

namespace MrMoonlight.UI
{
    /// <summary>
    /// Music-anchored pre-menu title sequence (MRM-18, rebuilt 2026-09-07 to Carlos's exact
    /// breakpoint spec, replacing the old generic SplashSequence). Every visual beat is driven
    /// directly off <see cref="AudioSource.time"/> on the menu's music track rather than a
    /// fixed animation timeline, so re-timing anything is a <see cref="Tunables"/> edit, not a
    /// script or scene change - the breakpoints are literal seconds into the song.
    ///
    /// Three cards, all riding on top of FadeOverlay's already-opaque black background:
    /// - Cross + Greek text (<see cref="crossGroup"/>): appears instantly at
    ///   <see cref="Tunables.TitleBreakpointCross"/>, grows continuously, holds, fades out - see
    ///   <see cref="GrowThenFade"/>.
    /// - Studio logo (<see cref="logoGroup"/>): same grow/hold/fade shape at
    ///   <see cref="Tunables.TitleBreakpointLogo"/>, except "2026" sits outside
    ///   <see cref="logoGrowingElements"/> so only the parent group's alpha touches it, never
    ///   its scale.
    /// - Disclaimer: two paragraphs fade in independently at their own breakpoints (paragraph 1
    ///   stays visible while paragraph 2 fades in beside it), then both fade out together a
    ///   fixed window before the feather is meant to start falling.
    ///
    /// This script only owns what happens on screen up to (and including) the disclaimer's
    /// fade-out. It has no idea the sparrow feather exists - <see cref="Play"/>'s callback fires
    /// once, at the exact song-time the feather should start falling so it lands on
    /// <see cref="Tunables.TitleBreakpointWorldReveal"/>; <see cref="MainMenuController"/> owns
    /// everything about the feather/world-reveal handoff from there.
    /// </summary>
    public sealed class TitleSequenceController : MonoBehaviour
    {
        [Header("Audio (the timing anchor for every breakpoint)")]
        [SerializeField] private AudioSource musicSource;

        [Header("Screen 1 - Cross")]
        [SerializeField] private CanvasGroup crossGroup;
        [SerializeField] private RectTransform crossScaleRoot;

        [Header("Screen 2 - Logo")]
        [SerializeField] private CanvasGroup logoGroup;
        [Tooltip("Everything in the logo composition except the \"2026\" text - this is what scales, the year does not.")]
        [SerializeField] private RectTransform logoGrowingElements;

        [Header("Screen 3 - Disclaimer")]
        [SerializeField] private CanvasGroup disclaimerParagraph1;
        [SerializeField] private CanvasGroup disclaimerParagraph2;

        /// <summary>
        /// The exact song-time the feather should start falling so it lands on
        /// <see cref="Tunables.TitleBreakpointWorldReveal"/>, given how the disclaimer's own
        /// timing is laid out. Exposed so the fall duration can be set to match in the Inspector
        /// rather than guessed - see the class doc.
        /// </summary>
        public static float ComputeFeatherStartSongTime(MoonlightTunables t) =>
            t.TitleBreakpointDisclaimer2 + t.DisclaimerFadeInDuration + t.DisclaimerHoldAfterParagraph2 +
            t.DisclaimerFadeOutDuration + t.TitleGapBeforeFeatherStarts;

        /// <summary>Starts the music and runs the full sequence. <paramref name="onFeatherShouldStart"/> fires once, at the song-time computed by <see cref="ComputeFeatherStartSongTime"/>.</summary>
        public Coroutine Play(System.Action onFeatherShouldStart) => StartCoroutine(RunSequence(onFeatherShouldStart));

        private IEnumerator RunSequence(System.Action onFeatherShouldStart)
        {
            MoonlightTunables t = Tunables.I;

            crossGroup.alpha = 0f;
            logoGroup.alpha = 0f;
            disclaimerParagraph1.alpha = 0f;
            disclaimerParagraph2.alpha = 0f;
            crossScaleRoot.localScale = Vector3.one * t.TitleElementStartScale;
            logoGrowingElements.localScale = Vector3.one * t.TitleElementStartScale;

            musicSource.time = 0f;
            musicSource.Play();

            // Breakpoint 0 - instant, no fade-in.
            crossGroup.alpha = 1f;
            StartCoroutine(GrowThenFade(crossGroup, crossScaleRoot, t.TitleBreakpointCross, t.TitleBreakpointLogo));

            yield return WaitForSongTime(t.TitleBreakpointLogo);

            // Breakpoint 1 - instant, no fade-in.
            logoGroup.alpha = 1f;
            StartCoroutine(GrowThenFade(logoGroup, logoGrowingElements, t.TitleBreakpointLogo, t.TitleBreakpointDisclaimer1));

            yield return WaitForSongTime(t.TitleBreakpointDisclaimer1);
            StartCoroutine(FadeCanvasGroupBySongTime(disclaimerParagraph1, 0f, 1f, t.TitleBreakpointDisclaimer1, t.TitleBreakpointDisclaimer1 + t.DisclaimerFadeInDuration));

            yield return WaitForSongTime(t.TitleBreakpointDisclaimer2);
            StartCoroutine(FadeCanvasGroupBySongTime(disclaimerParagraph2, 0f, 1f, t.TitleBreakpointDisclaimer2, t.TitleBreakpointDisclaimer2 + t.DisclaimerFadeInDuration));

            float fadeOutStart = t.TitleBreakpointDisclaimer2 + t.DisclaimerFadeInDuration + t.DisclaimerHoldAfterParagraph2;
            float fadeOutEnd = fadeOutStart + t.DisclaimerFadeOutDuration;
            yield return WaitForSongTime(fadeOutStart);

            Coroutine fadeOut1 = StartCoroutine(FadeCanvasGroupBySongTime(disclaimerParagraph1, disclaimerParagraph1.alpha, 0f, fadeOutStart, fadeOutEnd));
            Coroutine fadeOut2 = StartCoroutine(FadeCanvasGroupBySongTime(disclaimerParagraph2, disclaimerParagraph2.alpha, 0f, fadeOutStart, fadeOutEnd));
            yield return fadeOut1;
            yield return fadeOut2;

            yield return WaitForSongTime(fadeOutEnd + t.TitleGapBeforeFeatherStarts);

            onFeatherShouldStart?.Invoke();
        }

        /// <summary>
        /// One card's full lifecycle: already visible by the time this starts (alpha set
        /// instantly outside this coroutine, per spec - no fade-in), scale grows from
        /// <see cref="Tunables.TitleElementStartScale"/> to 1 until
        /// <see cref="Tunables.TitleGrowStopBeforeNextBreakpoint"/> seconds before
        /// <paramref name="nextBreakpoint"/>, holds at full scale, then fades alpha to 0 over the
        /// final <see cref="Tunables.TitleFadeOutBeforeNextBreakpoint"/> seconds so it's fully
        /// gone exactly on <paramref name="nextBreakpoint"/>.
        /// </summary>
        private IEnumerator GrowThenFade(CanvasGroup group, RectTransform scaleRoot, float appearBreakpoint, float nextBreakpoint)
        {
            MoonlightTunables t = Tunables.I;
            float growStopAt = nextBreakpoint - t.TitleGrowStopBeforeNextBreakpoint;
            float fadeStartAt = nextBreakpoint - t.TitleFadeOutBeforeNextBreakpoint;
            float growDuration = Mathf.Max(0.0001f, growStopAt - appearBreakpoint);

            while (musicSource.time < growStopAt)
            {
                float t01 = Mathf.Clamp01((musicSource.time - appearBreakpoint) / growDuration);
                scaleRoot.localScale = Vector3.one * Mathf.Lerp(t.TitleElementStartScale, 1f, t01);
                yield return null;
            }
            scaleRoot.localScale = Vector3.one;

            yield return WaitForSongTime(fadeStartAt);
            yield return FadeCanvasGroupBySongTime(group, group.alpha, 0f, fadeStartAt, nextBreakpoint);
        }

        /// <summary>Waits until the music's own playback time reaches the given song second - the actual timing anchor for every breakpoint.</summary>
        private IEnumerator WaitForSongTime(float songSeconds)
        {
            while (musicSource.time < songSeconds)
            {
                yield return null;
            }
        }

        private IEnumerator FadeCanvasGroupBySongTime(CanvasGroup group, float from, float to, float startSongTime, float endSongTime)
        {
            float duration = Mathf.Max(0.0001f, endSongTime - startSongTime);
            while (musicSource.time < endSongTime)
            {
                float t01 = Mathf.Clamp01((musicSource.time - startSongTime) / duration);
                group.alpha = Mathf.Lerp(from, to, t01);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
