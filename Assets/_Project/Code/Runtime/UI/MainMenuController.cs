using System.Collections;
using MrMoonlight.Data;
using MrMoonlight.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MrMoonlight.UI
{
    /// <summary>
    /// Orchestrates the MainMenu scene (MRM-18): the pre-menu splash cards
    /// (<see cref="SplashSequence"/>), the opening reveal, and the four buttons - Start,
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
    /// one landing.</para>
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Fades")]
        [SerializeField] private FadeOverlay fadeOverlay;
        [SerializeField] private SplashSequence splashSequence;

        [Header("Feather Intro")]
        [Tooltip("Plays once the splash cards finish; the buttons group and the rest of the 3D scene stay hidden until this reports OnLanded. Its own Loop must be off.")]
        [SerializeField] private FeatherFall introFeatherFall;

        [Tooltip("Second camera, culled to the feather's WorldOverlayFX layer only, rendering into introFeatherCanvas's RawImage while the rest of the scene stays hidden behind fadeOverlay.")]
        [SerializeField] private Camera introFeatherCamera;

        [Tooltip("Screen-Space-Overlay canvas (sorting order above the main menu canvas) whose RawImage displays introFeatherCamera's output. Active only while the feather is falling.")]
        [SerializeField] private GameObject introFeatherCanvas;

        [Header("Panels")]
        [SerializeField] private CanvasGroup mainButtonsGroup;
        [SerializeField] private CanvasGroup settingsGroup;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private CreditsController creditsController;

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
            StartCoroutine(CrossfadePanels(settingsGroup, mainButtonsGroup));
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
        }

        private IEnumerator PlayIntroThenReveal()
        {
            if (splashSequence != null)
            {
                yield return splashSequence.Play();
            }

            yield return PlayOpeningReveal();
        }

        private IEnumerator PlayOpeningReveal()
        {
            if (menuMusicSource != null)
            {
                menuMusicSource.Play();
            }

            if (introFeatherFall == null)
            {
                // No feather wired up - original MRM-18 behavior: the overlay clearing alone
                // reveals everything, buttons already visible from Awake.
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

            if (introFeatherCanvas != null) introFeatherCanvas.SetActive(false);
            if (introFeatherCamera != null) introFeatherCamera.enabled = false;

            // The world and the UI reveal together, as one beat - the main camera already
            // renders the feather resting exactly where the overlay left it.
            Coroutine worldReveal = fadeOverlay.FadeToClear(Tunables.I.MenuOpeningFadeDuration);
            yield return FadeInGroup(mainButtonsGroup, Tunables.I.MenuOpeningFadeDuration);
            yield return worldReveal;

            mainButtonsGroup.interactable = true;
            mainButtonsGroup.blocksRaycasts = true;
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

        private static IEnumerator CrossfadePanels(CanvasGroup from, CanvasGroup to)
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
        }

        private static void SetGroupState(CanvasGroup group, float alpha, bool interactable)
        {
            group.alpha = alpha;
            group.interactable = interactable;
            group.blocksRaycasts = interactable;
        }
    }
}
