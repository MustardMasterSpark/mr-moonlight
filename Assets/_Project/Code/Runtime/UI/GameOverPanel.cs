using DG.Tweening;
using MrMoonlight.Data;
using MrMoonlight.Events;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MrMoonlight.UI
{
    /// <summary>
    /// The end-of-level panel. Owner: MRM-17 (death landing), extended by MRM-11 (the win), and
    /// the seed of MRM-19's game over screen.
    ///
    /// <para><b>It serves both endings, despite the name.</b> The class started life as MRM-17's
    /// unstyled landing point for death, with its own note saying that whoever added buttons should
    /// extend it rather than build a second, parallel game-over canvas. MRM-11 needed a victory
    /// screen that is the same screen with different words, so that is what this is now. The name
    /// is kept only because the live scene and <c>DeathSequence</c> already reference it; renaming
    /// it is a cosmetic change to make when MRM-19 does the real styling pass.</para>
    ///
    /// <para><b>Two buttons</b>, per MRM-19: Restart (reload the scene) and Return to Main Menu.
    /// Restart-from-checkpoint is deliberately absent — MRM-45 has not built checkpoints yet, and
    /// a third button that silently does the same thing as the first would be worse than no button.
    /// <see cref="RestartFromCheckpoint"/> is the placeholder that issue asked for.</para>
    ///
    /// <para>Everything here runs on unscaled time and every exit resets <c>Time.timeScale</c> and
    /// <c>AudioListener.pause</c> before loading, because both endings arrive with the game frozen
    /// and the death path also arrives with audio suspended.</para>
    /// </summary>
    public sealed class GameOverPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        [Header("Text")]
        [Tooltip("Headline. Set per ending by the director; falls back to the text below for a plain death.")]
        [SerializeField] private TMP_Text titleText;

        [Tooltip("Shown when the panel is opened without an ending message — i.e. the player simply died.")]
        [SerializeField] private string defaultDeathTitle = "You died";

        [Header("Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Scenes")]
        [Tooltip("Scene the Main Menu button loads. Matches MainMenuController's own serialized scene name.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Transition")]
        [Tooltip("Optional full-screen black Image faded to opaque before either button loads. Assign the HUD's Black Screen.")]
        [SerializeField] private Image fadeImage;

        private bool _leaving;

        private void Awake()
        {
            if (root == null) root = gameObject;

            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);

            root.SetActive(false);
        }

        /// <summary>The death sequence's landing point (MRM-17). Shows the panel with its default title.</summary>
        public void Show() => ShowEnding(null, won: false);

        /// <summary>
        /// Shows the panel for a specific ending. Called by the event director's <c>win</c> and
        /// <c>lose</c> verbs, and by <see cref="Show"/> for an ordinary death.
        /// </summary>
        public void ShowEnding(string message, bool won)
        {
            if (titleText != null)
            {
                titleText.text = string.IsNullOrWhiteSpace(message) ? defaultDeathTitle : message;
            }

            root.SetActive(true);

            // Stop the world. Freeing the cursor is what makes the two buttons clickable at all —
            // the camera state machine locks it on Awake and never re-locks, so this sticks.
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            EventDirector director = EventDirector.Active;
            if (director != null) director.DisablePlayerControl();

            if (!won)
            {
                // The death sequence already left the screen black; nothing to fade.
                return;
            }

            SetFadeAlpha(0f);
        }

        /// <summary>Button hookup: restart the level from the beginning.</summary>
        public void OnRestartClicked()
        {
            LeaveTo(SceneManager.GetActiveScene().name);
        }

        /// <summary>Button hookup: back to the main menu.</summary>
        public void OnMainMenuClicked()
        {
            LeaveTo(mainMenuSceneName);
        }

        /// <summary>
        /// Placeholder MRM-19 explicitly asked for: restart from the last checkpoint. There are no
        /// checkpoints yet — MRM-45 owns them — so this restarts the level and says so, rather than
        /// pretending to do something it cannot.
        /// </summary>
        public void RestartFromCheckpoint()
        {
            Debug.LogWarning("[GameOverPanel] Restart-from-checkpoint is not implemented — MRM-45 owns checkpoints. Restarting the level instead.", this);
            OnRestartClicked();
        }

        private void LeaveTo(string sceneName)
        {
            if (_leaving) return;
            _leaving = true;

            if (restartButton != null) restartButton.interactable = false;
            if (mainMenuButton != null) mainMenuButton.interactable = false;

            if (fadeImage == null)
            {
                LoadNow(sceneName);
                return;
            }

            fadeImage.raycastTarget = true;
            DOTween.To(() => fadeImage.color.a, SetFadeAlpha, 1f, Tunables.I.EndScreenFadeDuration)
                .SetUpdate(isIndependentUpdate: true)
                .SetLink(gameObject)
                .OnComplete(() => LoadNow(sceneName));
        }

        private void LoadNow(string sceneName)
        {
            // Both must be undone here and not in the next scene's Awake: a scene that loads with
            // timeScale still 0 never runs a physics step, and one that loads with the listener
            // paused is silent with no obvious cause.
            Time.timeScale = 1f;
            AudioListener.pause = false;

            SceneManager.LoadScene(sceneName);
        }

        private void SetFadeAlpha(float alpha)
        {
            if (fadeImage == null) return;

            Color color = fadeImage.color;
            color.a = alpha;
            fadeImage.color = color;
        }
    }
}
