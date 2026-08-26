using System.Collections;
using MrMoonlight.Data;
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
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Fades")]
        [SerializeField] private FadeOverlay fadeOverlay;
        [SerializeField] private SplashSequence splashSequence;

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
            SetGroupState(mainButtonsGroup, alpha: 1f, interactable: true);
            SetGroupState(settingsGroup, alpha: 0f, interactable: false);

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

            yield return fadeOverlay.FadeToClear(Tunables.I.MenuOpeningFadeDuration);
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
