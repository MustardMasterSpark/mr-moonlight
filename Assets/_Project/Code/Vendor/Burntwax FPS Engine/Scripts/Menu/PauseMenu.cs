using UnityEngine;
using UnityEngine.SceneManagement;

namespace Burntwax
{
    /// <summary>
    /// The project's canonical pause authority (adopted for Mr. Moonlight, MRM-9).
    ///
    /// <para><b>The pause contract.</b> Pausing sets <see cref="Time.timeScale"/> to 0 and
    /// <see cref="AudioListener.pause"/> to true. Every other system — enemy AI, the event
    /// system, animation, timers, VFX — must be consistent with that, which in practice means:
    /// drive logic from <see cref="Time.deltaTime"/> (never <c>unscaledDeltaTime</c>), leave
    /// Animators on <c>AnimatorUpdateMode.Normal</c>, use <c>WaitForSeconds</c> rather than
    /// <c>WaitForSecondsRealtime</c> in coroutines, and gate any <c>Update</c> that must not run
    /// while paused on <c>Time.timeScale == 0f</c>. UI that must stay live while paused (this
    /// menu, and only this menu) is the sole exception. See Docs/pause-contract.md.</para>
    ///
    /// <para><b>Changes from the shipped Burntwax version.</b> The wall-charge camera was removed
    /// with the wall-running mechanics; <c>Save()</c> was removed with the save system, which
    /// Mr. Moonlight does not use; and the main-menu return now loads the scene by name rather
    /// than by build index 1, which is not Mr. Moonlight's build order. Input-map switching is
    /// handled on the Mr. Moonlight side by <c>BurntwaxInputBridge</c>, which watches
    /// <see cref="Paused"/> — Burntwax code cannot reference Mr. Moonlight types, because the
    /// assembly reference runs one way only.</para>
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance;

        [Header("Camera stuff")]
        public CamStateMachine camStateMachine;

        [Tooltip("Scene loaded by MainMenu(). Named rather than indexed so build order can change freely.")]
        public string mainMenuSceneName = "MainMenu";

        public GameObject pauseMenuUI;
        public bool Paused;

        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("Found more than one Pause Menu in the scene");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Update()
        {
            if (InputManager.Instance != null && InputManager.Instance.pauseIsPressed)
            {
                InputManager.Instance.pauseIsPressed = false;

                if (Paused)
                {
                    Resume();
                }
                else
                {
                    Pause();
                }
            }
        }

        public void Resume()
        {
            if (pauseMenuUI != null) pauseMenuUI.SetActive(false);
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Paused = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetCamerasActive(true);
        }

        private void Pause()
        {
            if (pauseMenuUI != null) pauseMenuUI.SetActive(true);
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Paused = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetCamerasActive(false);
        }

        private void SetCamerasActive(bool active)
        {
            if (camStateMachine == null) return;
            if (camStateMachine.fpsCam != null) camStateMachine.fpsCam.gameObject.SetActive(active);
            if (camStateMachine.aimCam != null) camStateMachine.aimCam.gameObject.SetActive(active);
        }

        // Quit to the main menu.
        public void MainMenu()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadSceneAsync(mainMenuSceneName);
        }

        public void Options()
        {
        }
    }
}
