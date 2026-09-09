using System.Collections;
using MrMoonlight.Data;
using MrMoonlight.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MrMoonlight.UI
{
    /// <summary>
    /// Orchestrates the MainMenu scene (MRM-18): the pre-menu title sequence
    /// (<see cref="TitleSequenceController"/>), the opening reveal, and the four buttons - Start,
    /// Settings, Credits, Quit. Every transition is a fade, never a hard cut, per the issue. Two
    /// fade mechanisms are in play, matching the issue's own wording for each:
    /// <see cref="fadeOverlay"/> (a shared full-screen black CanvasGroup) for the opening reveal,
    /// Start's fade-to-black-then-load, and Quit's fade-to-black-then-quit; a direct crossfade
    /// between <see cref="mainButtonsGroup"/> and <see cref="settingsGroup"/> for Settings/Back,
    /// since that spec keeps the staged background visible underneath instead of going through
    /// black. Credits fades itself (<see cref="CreditsController"/>) on its own opaque panel.
    /// Owner: MRM-18
    ///
    /// <para><b>Feather intro (added post-MRM-18, Carlos's ask):</b> while <see cref="introFeatherFall"/>
    /// falls, everything else in the 3D scene (the ground plane, other props, eventually a full
    /// staged background) must read as pure black - only the feather itself is visible. A
    /// Screen-Space-Overlay canvas (what <see cref="fadeOverlay"/> is) always renders above every
    /// camera's 3D output, so there is no way to make the feather draw "through" it - instead,
    /// <see cref="introFeatherCamera"/> is a second camera, culled to only the feather's own layer
    /// (<c>WorldOverlayFX</c>), rendering into a RenderTexture that <see cref="introFeatherCanvas"/>
    /// displays on a second Screen-Space-Overlay canvas with a higher sorting order than the main
    /// menu canvas - so it draws above <see cref="fadeOverlay"/> while that overlay stays fully
    /// opaque and hides the normal camera's rendering of everything else underneath. Once the
    /// feather lands (<see cref="FeatherFall.OnLanded"/>), the overlay camera/canvas switch off and
    /// <see cref="fadeOverlay"/> fades to clear at the same time <see cref="mainButtonsGroup"/>
    /// fades in - "fade out everything and reveal the world and the UI" as one beat. The feather's
    /// own layer stays included in the main camera's culling mask throughout, so once revealed it
    /// simply continues existing as a normal part of the scene, already resting where it landed.
    /// <see cref="introFeatherFall"/>'s own Loop must stay off - this sequence expects exactly
    /// one landing.
    ///
    /// <b>2026-09-07/08 flicker investigation:</b> switching the overlay off happens immediately
    /// on landing, in the same statement block as starting the reveal fade (no yield between
    /// them, so there is no separately-rendered frame where only one has taken effect) - this can
    /// still show a very brief, faint dimming on the feather for a single frame (the fade's first
    /// tiny alpha step), which reads as an almost imperceptible flicker. A different approach was
    /// tried and reverted: keeping the overlay active for the *entire* reveal fade duration,
    /// switching it off only once fully faded. That was meant to close even that single-frame gap,
    /// but caused a much worse regression - Carlos: "it takes like 1 or 2 seconds and then it
    /// abruptly shows... it doesn't show the fading I wanted." Root cause traced to
    /// <see cref="introFeatherCamera"/>'s render pipeline: its clear flags/color and target
    /// texture are correctly configured for a transparent background (SolidColor, alpha 0,
    /// ARGB32) but URP does not reliably preserve that alpha through its color/post-processing
    /// pass - the overlay's rendered output ends up opaque across the whole frame regardless, so
    /// keeping it active blocked the entire reveal from ever being visible until it switched off,
    /// which then showed the already-fully-faded result all at once. Do not re-attempt "keep the
    /// overlay alive through the fade" without first fixing that URP alpha behavior (a dedicated
    /// unlit/transparent shader on the RawImage's material forcing straight-alpha blending is the
    /// likely fix, not attempted here) - the immediate-switch-off approach above is the current,
    /// working behavior.</para>
    ///
    /// <para><b>Title sequence handoff (2026-09-07 rebuild):</b> <see cref="titleSequence"/> owns
    /// the music-anchored cross/logo/disclaimer cards and calls back the instant the song reaches
    /// the exact second the feather should start falling (see
    /// <see cref="TitleSequenceController.ComputeFeatherStartSongTime"/>) so it lands precisely on
    /// <see cref="Tunables.TitleBreakpointWorldReveal"/> - <see cref="introFeatherFall"/>'s
    /// <c>fallDuration</c> must be set to match that computed window in the Inspector; this class
    /// does not re-derive or enforce it.</para>
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Fades")]
        [SerializeField] private FadeOverlay fadeOverlay;
        [SerializeField] private TitleSequenceController titleSequence;

        [Header("Feather Intro")]
        [Tooltip("Plays once the splash cards finish; the buttons group and the rest of the 3D scene stay hidden until this reports OnLanded. Its own Loop must be off.")]
        [SerializeField] private FeatherFall introFeatherFall;

        [Tooltip("Second camera, culled to the feather's WorldOverlayFX layer only, rendering into introFeatherCanvas's RawImage while the rest of the scene stays hidden behind fadeOverlay.")]
        [SerializeField] private Camera introFeatherCamera;

        [Tooltip("Screen-Space-Overlay canvas (sorting order above the main menu canvas) whose RawImage displays introFeatherCamera's output. Active only while the feather is falling.")]
        [SerializeField] private GameObject introFeatherCanvas;

        [Header("Panels")]
        [Tooltip("2026-09-08: repointed from the whole MainButtons block to ButtonGroup specifically (Carlos: only wants this fade/gate behavior on the buttons - title and description are unaffected, title already ignores this via its own CanvasGroup's ignoreParentGroups). Still gates click/raycast for all five buttons, same as before.")]
        [SerializeField] private CanvasGroup mainButtonsGroup;
        [SerializeField] private CanvasGroup settingsGroup;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private CreditsController creditsController;

        [Tooltip("Fades the title's letters in on their own custom-order timeline the instant the world/buttons reveal starts - see the class doc's Feather intro section for that moment. Optional; skipped if not wired up.")]
        [SerializeField] private TitleLetterReveal titleLetterReveal;

        [Tooltip("Drives mainButtonsGroup's fade-in with an editable duration/curve (Carlos's tuning tool, 2026-09-08) instead of a hardcoded lerp. Lives on the same GameObject as mainButtonsGroup (ButtonGroup). Optional; falls back to the old linear FadeInGroup if not wired up.")]
        [SerializeField] private GroupFadeReveal mainButtonsFadeReveal;

        [Header("Gamepad Navigation")]
        [Tooltip("Selected automatically whenever mainButtonsGroup becomes interactable - the EventSystem has no selection at all until something sets one, so without this a gamepad's D-pad does nothing on first reveal.")]
        [SerializeField] private Button startButton;
        [Tooltip("Re-selected when Credits closes, so focus returns to the button that opened it rather than being lost.")]
        [SerializeField] private Button creditsButton;
        [Tooltip("Re-selected when Settings' Back button returns to the main buttons, so focus returns to the button that opened it rather than being lost.")]
        [SerializeField] private Button settingsButton;

        [Tooltip("Gates every selection call below to Gamepad scheme only - see its own doc comment. Keyboard & Mouse scheme never auto-selects a button.")]
        [SerializeField] private MenuInputSchemeController inputScheme;

        [Header("Audio")]
        [SerializeField] private AudioSource menuMusicSource;

        [Header("Scene")]
        [Tooltip("Scene asset Start loads. Currently \"Island\" - the demo scene's actual asset name; Docs/unity-conventions.md still calls it \"Demo\" conceptually.")]
        [SerializeField] private string demoSceneName = "Island";

        private void Awake()
        {
            // Apply saved (or default) volumes before the reveal even starts - independent of
            // whether the player ever opens Settings this session. See SettingsPanel's own doc.
            settingsPanel.ApplySavedAudioSettings();

            fadeOverlay.SetOpaqueInstant();
            // Hidden (not just covered by the overlay) until the feather intro lands - see the
            // class doc's "Feather intro" section. If introFeatherFall isn't wired up, fall back
            // to the original MRM-18 behavior of showing immediately with the overlay.
            bool hasFeatherIntro = introFeatherFall != null;
            SetGroupState(mainButtonsGroup, alpha: hasFeatherIntro ? 0f : 1f, interactable: !hasFeatherIntro);
            SetGroupState(settingsGroup, alpha: 0f, interactable: false);

            if (introFeatherCanvas != null) introFeatherCanvas.SetActive(false);
            if (introFeatherCamera != null) introFeatherCamera.enabled = false;

            creditsController.OnClosed += HandleCreditsClosed;
        }

        private void OnDestroy()
        {
            creditsController.OnClosed -= HandleCreditsClosed;
        }

        private void Start()
        {
            StartCoroutine(PlayIntroThenReveal());
        }

        /// <summary>Button hookup: Start game.</summary>
        public void OnStartGameClicked()
        {
            StartCoroutine(RunStartGame());
        }

        /// <summary>Button hookup: Settings.</summary>
        public void OnSettingsClicked()
        {
            StartCoroutine(CrossfadePanels(mainButtonsGroup, settingsGroup));
        }

        /// <summary>Button hookup: Settings' Back button.</summary>
        public void OnSettingsBackClicked()
        {
            StartCoroutine(CrossfadePanels(settingsGroup, mainButtonsGroup, settingsButton));
        }

        /// <summary>Button hookup: Credits.</summary>
        public void OnCreditsClicked()
        {
            mainButtonsGroup.interactable = false;
            creditsController.Show();
        }

        /// <summary>Button hookup: Quit.</summary>
        public void OnQuitClicked()
        {
            StartCoroutine(RunQuit());
        }

        private void HandleCreditsClosed()
        {
            mainButtonsGroup.interactable = true;
            SelectForGamepad(creditsButton);
        }

        private IEnumerator PlayIntroThenReveal()
        {
            // Black screen with the cross already visible (TitleSequenceController.Awake sets
            // it, per Carlos 2026-09-08 - no fade-in, no wait) but nothing else ticking yet.
            // Gives Play Mode's own load/settle time somewhere safe to happen before the music
            // starts, since that's the timing anchor every breakpoint reads off of.
            yield return new WaitForSeconds(Tunables.I.TitleSequenceStartDelay);

            if (titleSequence == null)
            {
                // No title sequence wired up - fall back to just playing music and revealing
                // everything, same spirit as the original MRM-18 behavior.
                if (menuMusicSource != null) menuMusicSource.Play();
                yield return PlayFeatherAndWorldReveal();
                yield break;
            }

            // TitleSequenceController starts the music itself (it's the timing anchor for every
            // breakpoint) and invokes this callback the instant the song reaches the exact
            // second the feather should start falling - see the class doc's "Title sequence
            // handoff" note. Play()'s own coroutine finishes right after invoking the callback,
            // so by the time the first yield returns, featherReveal is already assigned.
            Coroutine featherReveal = null;
            yield return titleSequence.Play(() => featherReveal = StartCoroutine(PlayFeatherAndWorldReveal()));
            yield return featherReveal;
        }

        private IEnumerator PlayFeatherAndWorldReveal()
        {
            if (introFeatherFall == null)
            {
                // No feather wired up - original MRM-18 behavior: the overlay clearing alone
                // reveals everything, buttons already visible from Awake.
                if (titleLetterReveal != null) titleLetterReveal.Play();
                yield return fadeOverlay.FadeToClear(Tunables.I.MenuOpeningFadeDuration);
                yield break;
            }

            // fadeOverlay stays fully opaque here - it's still covering the main camera's
            // rendering of everything else. The feather is the only thing visible, via the
            // separate overlay camera/canvas drawn above it. See the class doc.
            if (introFeatherCanvas != null) introFeatherCanvas.SetActive(true);
            if (introFeatherCamera != null) introFeatherCamera.enabled = true;

            bool landed = false;
            System.Action onLanded = () => landed = true;
            introFeatherFall.OnLanded += onLanded;
            introFeatherFall.StartFalling();

            while (!landed)
            {
                yield return null;
            }

            introFeatherFall.OnLanded -= onLanded;

            // 2026-09-08 real fix: the overlay's camera renders into a RenderTexture through
            // URP, which does NOT reliably output a usable alpha channel (confirmed empirically -
            // tried disabling post-processing, disabling HDR, adding a proper
            // UniversalAdditionalCameraData, a freshly-created RenderTexture, and the pipeline
            // asset's own "Allow Post-process Alpha Output" toggle; every one of them still read
            // back alpha=1 everywhere, including empty background pixels that were cleared to
            // alpha 0). Two things were tried and abandoned because of this:
            // (1) switch the overlay off immediately at landing, before the fade starts - the
            //     fully-opaque overlay is gone before the reveal begins, but since the main
            //     camera's own rendering of the feather is what's then covered by fadeOverlay's
            //     fade like everything else, the feather visibly dims along with the rest of the
            //     world (Carlos: "the sparrow feather gets impacted by this black fade-in").
            // (2) keep the overlay active for the whole fade instead - since its render is opaque
            //     regardless of the intended transparency, this blocks the ENTIRE reveal from
            //     ever being visible, then reveals the already-fully-faded result all at once the
            //     instant it's switched off (Carlos: "it takes like 1 or 2 seconds and then it
            //     abruptly shows").
            // The actual fix: the feather is essentially static the instant it lands (resting on
            // the water), so instead of relying on the camera's continuous, broken-alpha render,
            // capture ONE snapshot right now via difference matting (render on black, render on
            // white, recover true per-pixel alpha from the two - see
            // CaptureMattedFeatherSnapshot) into a plain Texture2D. Regular UI Image/RawImage
            // alpha blending against a Texture2D works perfectly normally (it's exactly how every
            // other UI sprite in this menu already renders) - the RenderTexture was the only
            // broken part, and this sidesteps it entirely rather than working around its timing.
            // The overlay can then safely stay active for the whole reveal fade (no more "abrupt"
            // bug, since it now has real alpha) and switches off only once fully faded, so the
            // handoff to the main camera's live (already-landed, matching) feather is seamless.
            RawImage featherOverlayImage = introFeatherCanvas != null ? introFeatherCanvas.GetComponentInChildren<RawImage>(true) : null;
            Texture2D featherSnapshot = null;
            if (featherOverlayImage != null && introFeatherCamera != null)
            {
                featherSnapshot = CaptureMattedFeatherSnapshot(introFeatherCamera);
                featherOverlayImage.texture = featherSnapshot;
            }

            // 2026-09-08, Carlos: the world reveal and the UI (title letters + buttons) used to
            // start at the exact same instant - he wants a beat to look at the revealed 3D scene
            // alone first. worldReveal still starts immediately; the UI's own fade-in is deferred
            // by Tunables.UiElementsRevealDelay via RevealUiElementsAfterDelay below.
            Coroutine worldReveal = fadeOverlay.FadeToClear(Tunables.I.MenuOpeningFadeDuration);
            Coroutine buttonsFadeIn = StartCoroutine(RevealUiElementsAfterDelay());

            // 2026-09-08: switch off the static snapshot once the world fade is mostly through
            // instead of waiting for it to fully finish - see Tunables.FeatherOverlaySwitchAlphaThreshold's
            // doc. The live feather (already wobbling since the instant it landed) sat hidden
            // behind this one static, matted frame for the entire 1.5s fade otherwise, which read
            // as "frozen" to Carlos. Safe to cut early here (unlike at the very start of the fade
            // - see the flicker-investigation doc above) because the world is already mostly
            // visible by this alpha.
            while (fadeOverlay.Alpha > Tunables.I.FeatherOverlaySwitchAlphaThreshold)
            {
                yield return null;
            }

            if (introFeatherCanvas != null) introFeatherCanvas.SetActive(false);
            if (introFeatherCamera != null) introFeatherCamera.enabled = false;
            if (featherSnapshot != null) Destroy(featherSnapshot);

            yield return buttonsFadeIn;
            yield return worldReveal;

            mainButtonsGroup.interactable = true;
            mainButtonsGroup.blocksRaycasts = true;
            SelectForGamepad(startButton);
        }

        /// <summary>
        /// Renders <paramref name="cam"/>'s current view twice - once against a black background,
        /// once against white - and recovers true per-pixel straight alpha from the difference
        /// (standard "difference matting": a pixel's rendered color is
        /// <c>alpha * trueColor + (1 - alpha) * background</c>, so subtracting the white-background
        /// render from the black-background one isolates <c>(1 - alpha)</c> directly, independent
        /// of whatever the render pipeline does or doesn't do with the alpha channel itself - only
        /// RGB needs to survive the render, which URP does correctly). One-time cost (a couple of
        /// full-resolution renders plus a CPU pixel loop), meant to be called once at a single
        /// moment (the feather landing), not per-frame.
        /// </summary>
        private static Texture2D CaptureMattedFeatherSnapshot(Camera cam)
        {
            RenderTexture rt = cam.targetTexture;
            int width = rt.width;
            int height = rt.height;

            Color originalBackground = cam.backgroundColor;
            CameraClearFlags originalClearFlags = cam.clearFlags;
            cam.clearFlags = CameraClearFlags.SolidColor;

            cam.backgroundColor = Color.black;
            cam.Render();
            Color[] onBlack = ReadRenderTexturePixels(rt, width, height);

            cam.backgroundColor = Color.white;
            cam.Render();
            Color[] onWhite = ReadRenderTexturePixels(rt, width, height);

            cam.backgroundColor = originalBackground;
            cam.clearFlags = originalClearFlags;

            // 2026-09-08: alpha < FeatherMatteAlphaCutoff is treated as fully transparent rather
            // than un-premultiplied - dividing by a very small alpha amplifies ordinary render
            // noise/dither into stray fully-saturated pixels (Carlos: "red pixels around the
            // sparrow feather"), which the old 0.003f cutoff was too permissive to catch. See the
            // tunable's doc.
            float alphaCutoff = Tunables.I.FeatherMatteAlphaCutoff;
            Color[] result = new Color[onBlack.Length];
            for (int i = 0; i < result.Length; i++)
            {
                Color b = onBlack[i];
                Color w = onWhite[i];
                float alpha = 1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f;
                alpha = Mathf.Clamp01(alpha);
                result[i] = alpha > alphaCutoff
                    ? new Color(Mathf.Clamp01(b.r / alpha), Mathf.Clamp01(b.g / alpha), Mathf.Clamp01(b.b / alpha), alpha)
                    : Color.clear;
            }

            Texture2D snapshot = new Texture2D(width, height, TextureFormat.RGBA32, false);
            snapshot.SetPixels(result);
            snapshot.Apply();
            return snapshot;
        }

        private static Color[] ReadRenderTexturePixels(RenderTexture rt, int width, int height)
        {
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D temp = new Texture2D(width, height, TextureFormat.RGBA32, false);
            temp.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            temp.Apply();
            RenderTexture.active = previousActive;

            Color[] pixels = temp.GetPixels();
            Destroy(temp);
            return pixels;
        }

        /// <summary>
        /// Waits <see cref="Tunables.UiElementsRevealDelay"/>, then plays the title letters, then
        /// (2026-09-08, Carlos: buttons should only start appearing once the title is fully
        /// revealed rather than at the same time) waits for that reveal to finish before fading
        /// <see cref="mainButtonsGroup"/> in - see the call site's comment. Runs as its own
        /// coroutine, in parallel with <c>worldReveal</c> and the feather-overlay-switch-off wait,
        /// so this delay doesn't hold up either of those.
        /// </summary>
        private IEnumerator RevealUiElementsAfterDelay()
        {
            yield return new WaitForSeconds(Tunables.I.UiElementsRevealDelay);

            if (titleLetterReveal != null)
            {
                yield return titleLetterReveal.Play();
            }

            if (mainButtonsFadeReveal != null)
            {
                yield return mainButtonsFadeReveal.FadeIn();
            }
            else
            {
                yield return FadeInGroup(mainButtonsGroup, Tunables.I.MenuOpeningFadeDuration);
            }
        }

        private static IEnumerator FadeInGroup(CanvasGroup group, float duration)
        {
            float start = group.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(start, 1f, duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            group.alpha = 1f;
        }

        private IEnumerator RunStartGame()
        {
            mainButtonsGroup.interactable = false;

            // Kick the load off now, in the background, rather than after the fade - a
            // synchronous LoadScene on the demo scene's terrain/vegetation freezes the whole app
            // for the load duration, black screen or not. allowSceneActivation stays false until
            // both the load and the fade are done, so activation itself is instant and the
            // player never sees a hitch. Owner: MRM-18
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(demoSceneName);
            loadOp.allowSceneActivation = false;

            float duration = Tunables.I.MenuTransitionFadeDuration;
            Coroutine musicFade = menuMusicSource != null ? StartCoroutine(FadeMusicOut(duration)) : null;
            yield return fadeOverlay.FadeToOpaque(duration);
            if (musicFade != null)
            {
                yield return musicFade;
            }

            // AsyncOperation.progress caps at 0.9 until activation is allowed - that ceiling is
            // Unity's own API contract, not a tunable value.
            while (loadOp.progress < 0.9f)
            {
                yield return null;
            }

            loadOp.allowSceneActivation = true;
        }

        private IEnumerator RunQuit()
        {
            mainButtonsGroup.interactable = false;

            float duration = Tunables.I.MenuTransitionFadeDuration;
            Coroutine musicFade = menuMusicSource != null ? StartCoroutine(FadeMusicOut(duration)) : null;
            yield return fadeOverlay.FadeToOpaque(duration);
            if (musicFade != null)
            {
                yield return musicFade;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator FadeMusicOut(float duration)
        {
            float startVolume = menuMusicSource.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                menuMusicSource.volume = Mathf.Lerp(startVolume, 0f, duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            menuMusicSource.volume = 0f;
        }

        private IEnumerator CrossfadePanels(CanvasGroup from, CanvasGroup to, Button selectOnComplete = null)
        {
            from.interactable = false;
            from.blocksRaycasts = false;

            float duration = Tunables.I.MenuTransitionFadeDuration;
            float fromStart = from.alpha;
            float toStart = to.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                from.alpha = Mathf.Lerp(fromStart, 0f, t);
                to.alpha = Mathf.Lerp(toStart, 1f, t);
                yield return null;
            }

            from.alpha = 0f;
            to.alpha = 1f;
            to.interactable = true;
            to.blocksRaycasts = true;
            SelectForGamepad(selectOnComplete);
        }

        private static void SetGroupState(CanvasGroup group, float alpha, bool interactable)
        {
            group.alpha = alpha;
            group.interactable = interactable;
            group.blocksRaycasts = interactable;
        }

        /// <summary>
        /// Sets the EventSystem's selected object so a gamepad's D-pad has something to move
        /// from. Nothing is selected by default (Unity's EventSystem starts with no selection at
        /// all), so without this, D-pad input silently does nothing the first time the menu - or
        /// any panel within it - becomes interactable. Delegates the actual scheme check to
        /// <see cref="inputScheme"/> (a no-op in Keyboard &amp; Mouse scheme) rather than
        /// selecting unconditionally and relying on <see cref="MenuInputSchemeController"/> to
        /// clean it up afterwards - see that class's <c>SelectIfGamepad</c> doc for why that
        /// distinction matters (a description-text flash on selection, not just the highlight).
        /// No-ops if target or <see cref="inputScheme"/> is null.
        /// </summary>
        private void SelectForGamepad(Button target)
        {
            if (target == null || inputScheme == null) return;
            inputScheme.SelectIfGamepad(target);
        }
    }
}
