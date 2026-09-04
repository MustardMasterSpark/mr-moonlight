using MrMoonlight.Events;
using MrMoonlight.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Opens and closes the pause state on Escape (keyboard) or Start (gamepad), and halts/resumes
    /// the game cleanly. Owner: MRM-19.
    ///
    /// <para><b>Scope note.</b> This is the narrow slice of MRM-19 Carlos asked for first: the
    /// toggle and the halt/resume, no buttons and no UI panel yet — those, plus the game-over
    /// screen the issue also bundles, are a fast follow. <see cref="Paused"/> and <see cref="Toggle"/>
    /// are the seam a future <c>PauseMenu</c> UI hangs its Continue button off.</para>
    ///
    /// <para><b>Why <c>Time.timeScale = 0</c>, not a hand-rolled pause flag threaded through every
    /// system.</b> Every coroutine wait in the project (<c>WaitVerb</c>'s scripted cutscene waits,
    /// <see cref="DeathSequence"/>'s beats, VFX drift) already uses scaled <c>WaitForSeconds</c>, and
    /// every <c>Animator</c> runs in its default Normal update mode — so freezing the timescale
    /// freezes all of it for free, including an <see cref="EventDirector"/> sequence mid-<c>wait</c>.
    /// That answers the issue's "pausing during a cutscene" acceptance criterion: it is not blocked,
    /// it genuinely pauses and resumes exactly where it left off, because nothing here is on
    /// unscaled/real time. The one thing that keeps running regardless is the Input System itself
    /// (it polls devices on real time), which is what lets the same button close the menu it opened.</para>
    ///
    /// <para><b>Why two actions, not one.</b> <c>Gameplay/Pause</c> (Escape, Gamepad Start) opens the
    /// menu; closing it is <c>UI/Cancel</c> (Escape, Gamepad East/B) instead of the same action,
    /// because <see cref="InputMapController.SetMode"/> disables every map but the active one — while
    /// paused the Gameplay map, and Pause with it, goes quiet. Escape is bound in both maps so the
    /// keyboard is symmetric; the gamepad is not (Start opens, B closes), which matches the
    /// conventional pause-menu control scheme and is the existing binding already authored into
    /// <c>InputSystem_Actions</c> — not a new asymmetry introduced here.</para>
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Player/Pause Controller")]
    // Must Awake/OnEnable after MoonlightPlayerRig (order 100), whose Awake constructs the
    // InputMapController this component subscribes to in OnEnable.
    [DefaultExecutionOrder(110)]
    public sealed class PauseController : MonoBehaviour
    {
        [Header("References — leave empty to resolve from the player root")]
        [SerializeField] private MoonlightPlayerRig playerRig;

        /// <summary>
        /// The pause controller in the loaded scene. Same lookup-cache pattern as
        /// <see cref="EventDirector.Active"/> — not a singleton in the sense
        /// <c>Docs/csharp-conventions.md</c> forbids: it does not survive a scene load and is
        /// authored into the scene by hand.
        /// </summary>
        public static PauseController Active { get; private set; }

        public bool Paused { get; private set; }

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Debug.LogError($"[PauseController] A second Pause Controller ('{name}') is in the scene alongside '{Active.name}'. Only one may run; this one will not register.", this);
            }
            else
            {
                Active = this;
            }

            if (playerRig == null)
            {
                playerRig = transform.root.GetComponentInChildren<MoonlightPlayerRig>(true);
            }

            if (playerRig == null)
            {
                Debug.LogError("[PauseController] No MoonlightPlayerRig found under the player root — pause cannot halt/resume control.", this);
            }
        }

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
            }

            // Guarantee the game is never left frozen behind a destroyed pause controller — a
            // scene reload (Restart/Return-to-Menu, once those buttons exist) must not inherit a
            // stuck Time.timeScale = 0 from the scene it is leaving. Same class of bug as the
            // stuck-red-tint static registry found in MRM-11.
            if (Paused)
            {
                Time.timeScale = 1f;
                AudioListener.pause = false;
            }
        }

        private void OnEnable()
        {
            if (playerRig == null || playerRig.Input == null)
            {
                return;
            }

            playerRig.Input.Actions.Gameplay.Pause.started += OnToggleAction;
            playerRig.Input.Actions.UI.Cancel.started += OnToggleAction;
        }

        private void OnDisable()
        {
            if (playerRig == null || playerRig.Input == null)
            {
                return;
            }

            playerRig.Input.Actions.Gameplay.Pause.started -= OnToggleAction;
            playerRig.Input.Actions.UI.Cancel.started -= OnToggleAction;
        }

        private void OnToggleAction(InputAction.CallbackContext ctx) => Toggle();

        public void Toggle()
        {
            if (Paused) Resume();
            else Pause();
        }

        public void Pause()
        {
            // The level-ended screens (win/loss) already own the timescale and control state;
            // pausing on top of them would fight EventDirector.EndLevel and DeathSequence rather
            // than layer cleanly. Full game-over pause support is the fast follow, not this pass.
            if (Paused || playerRig == null || (EventDirector.Active != null && EventDirector.Active.LevelEnded))
            {
                return;
            }

            Paused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            playerRig.SetControlSuspended(true);
            playerRig.Input.SetMode(InputMode.UI);
        }

        public void Resume()
        {
            if (!Paused || playerRig == null)
            {
                return;
            }

            Paused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            playerRig.SetControlSuspended(false);
            playerRig.Input.SetMode(InputMode.Gameplay);
        }
    }
}
