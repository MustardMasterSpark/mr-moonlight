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
    /// - Cross + Greek text (<see cref="crossGroup"/>): visible from the moment the scene loads
    ///   (through <see cref="MainMenuController"/>'s pre-music black-screen delay too, per
    ///   Carlos 2026-09-08 - no fade-in, no wait), static at its baked size the whole time, then
    ///   fades out before <see cref="Tunables.TitleBreakpointLogo"/> - see
    ///   <see cref="HoldThenFade"/>.
    /// - Studio logo (<see cref="logoGroup"/>): fades in at
    ///   <see cref="Tunables.TitleBreakpointLogo"/> (<see cref="Tunables.TitleLogoFadeInDuration"/>),
    ///   grows from <see cref="Tunables.TitleElementStartScale"/> to
    ///   <see cref="Tunables.TitleElementEndScale"/> (Carlos's baked "natural" pose plus a subtle
    ///   overshoot, tuned 2026-09-08), holds, then fades out
    ///   (<see cref="Tunables.TitleLogoFadeOutDuration"/>) - except "2026" sits outside
    ///   <see cref="logoGrowingElements"/> so only the parent group's alpha touches it, never
    ///   its scale.
    /// - Disclaimer: both paragraphs fade in together as one slide at
    ///   <see cref="Tunables.TitleBreakpointDisclaimer1"/> (changed 2026-09-08 from staggered
    ///   per-paragraph timing, at Carlos's request), hold, then fade out together starting at
    ///   <see cref="Tunables.TitleBreakpointDisclaimerFadeOutStart"/>.
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
            t.TitleBreakpointDisclaimerFadeOutStart + t.DisclaimerFadeOutDuration + t.TitleGapBeforeFeatherStarts;

        /// <summary>
        /// Visible from frame one, through the whole pre-music black-screen delay - see the
        /// class doc. Nothing else resets this; the card only ever goes back to 0 via its own
        /// fade-out in <see cref="HoldThenFade"/>.
        /// </summary>
        private void Awake()
        {
            crossGroup.alpha = 1f;
        }

        /// <summary>Starts the music and runs the full sequence. <paramref name="onFeatherShouldStart"/> fires once, at the song-time computed by <see cref="ComputeFeatherStartSongTime"/>.</summary>
        public Coroutine Play(System.Action onFeatherShouldStart) => StartCoroutine(RunSequence(onFeatherShouldStart));

        private IEnumerator RunSequence(System.Action onFeatherShouldStart)
        {
            MoonlightTunables t = Tunables.I;

            logoGroup.alpha = 0f;
            disclaimerParagraph1.alpha = 0f;
            disclaimerParagraph2.alpha = 0f;
            logoGrowingElements.localScale = Vector3.one * t.TitleElementStartScale;

            musicSource.time = 0f;
            musicSource.Play();

            // Cross is already visible (Awake) and static - just holds, then fades out.
            StartCoroutine(HoldThenFade(crossGroup, t.TitleBreakpointLogo));

            yield return WaitForSongTime(t.TitleBreakpointLogo);

            StartCoroutine(GrowThenFade(logoGroup, logoGrowingElements, t.TitleBreakpointLogo, t.TitleBreakpointDisclaimer1));

            yield return WaitForSongTime(t.TitleBreakpointDisclaimer1);
            StartCoroutine(FadeCanvasGroupBySongTime(disclaimerParagraph1, 0f, 1f, t.TitleBreakpointDisclaimer1, t.TitleBreakpointDisclaimer1 + t.DisclaimerFadeInDuration));
            StartCoroutine(FadeCanvasGroupBySongTime(disclaimerParagraph2, 0f, 1f, t.TitleBreakpointDisclaimer1, t.TitleBreakpointDisclaimer1 + t.DisclaimerFadeInDuration));

            float fadeOutStart = t.TitleBreakpointDisclaimerFadeOutStart;
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
        /// Logo card lifecycle: fades in from 0 over <see cref="Tunables.TitleLogoFadeInDuration"/>
        /// (running concurrently with the grow, not sequentially before it), scale grows from
        /// <see cref="Tunables.TitleElementStartScale"/> to <see cref="Tunables.TitleElementEndScale"/>
        /// until <see cref="Tunables.TitleGrowStopBeforeNextBreakpoint"/> seconds before
        /// <paramref name="nextBreakpoint"/>, holds, then fades alpha to 0 over
        /// <see cref="Tunables.TitleLogoFadeOutDuration"/> so it's fully gone exactly on
        /// <paramref name="nextBreakpoint"/>.
        /// </summary>
        private IEnumerator GrowThenFade(CanvasGroup group, RectTransform scaleRoot, float appearBreakpoint, float nextBreakpoint)
        {
            MoonlightTunables t = Tunables.I;
            float growStopAt = nextBreakpoint - t.TitleGrowStopBeforeNextBreakpoint;
            float fadeOutStartAt = nextBreakpoint - t.TitleLogoFadeOutDuration;
            float growDuration = Mathf.Max(0.0001f, growStopAt - appearBreakpoint);

            StartCoroutine(FadeCanvasGroupBySongTime(group, 0f, 1f, appearBreakpoint, appearBreakpoint + t.TitleLogoFadeInDuration));

            while (musicSource.time < growStopAt)
            {
                float t01 = Mathf.Clamp01((musicSource.time - appearBreakpoint) / growDuration);
                scaleRoot.localScale = Vector3.one * Mathf.Lerp(t.TitleElementStartScale, t.TitleElementEndScale, t01);
                yield return null;
            }
            scaleRoot.localScale = Vector3.one * t.TitleElementEndScale;

            yield return WaitForSongTime(fadeOutStartAt);
            yield return FadeCanvasGroupBySongTime(group, group.alpha, 0f, fadeOutStartAt, nextBreakpoint);
        }

        /// <summary>
        /// Cross-card lifecycle: already fully visible and at its baked static size (no grow),
        /// holds until <see cref="Tunables.TitleFadeOutBeforeNextBreakpoint"/> seconds before
        /// <paramref name="nextBreakpoint"/>, then fades alpha to 0 so it's fully gone exactly on
        /// <paramref name="nextBreakpoint"/>.
        /// </summary>
        private IEnumerator HoldThenFade(CanvasGroup group, float nextBreakpoint)
        {
            MoonlightTunables t = Tunables.I;
            float fadeStartAt = nextBreakpoint - t.TitleFadeOutBeforeNextBreakpoint;
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
