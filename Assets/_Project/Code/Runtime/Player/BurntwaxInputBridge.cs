using Burntwax;
using MrMoonlight.Data;
using MrMoonlight.Input;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Feeds Mr. Moonlight's MRM-8 input into the Burntwax engine, and drives the Cinemachine
    /// look axes with MRM-9's look maths.
    ///
    /// <para><b>Why this exists.</b> The Burntwax FPS Engine shipped its own
    /// <c>PlayerInput.inputactions</c> asset with no control schemes at all — keyboard and mouse
    /// bindings only. Mr. Moonlight already had a complete Input System setup from MRM-8, with
    /// both a Keyboard&amp;Mouse and a Gamepad scheme bound simultaneously. Running both assets
    /// would have put two sets of bindings on the same devices, and would have silently dropped
    /// gamepad support. So Burntwax's <see cref="InputManager"/> was reduced to a passive state
    /// holder and this component writes every one of its fields each frame.</para>
    ///
    /// <para><b>Why the dependency runs this way.</b> Assembly definitions cannot reference each
    /// other circularly. <c>MrMoonlight.Runtime</c> references <c>Burntwax.Core</c>, so Burntwax
    /// code can never see Mr. Moonlight types — every bridge lives on this side of the line.</para>
    ///
    /// <para><b>Look handling.</b> Cinemachine's own <c>CinemachineInputAxisController</c> is
    /// deliberately <i>not</i> used. It would take ownership of look input and discard MRM-9's
    /// stick acceleration ramp and separate mouse/stick sensitivities — the tuning Carlos tests
    /// by hand on both control schemes. Instead this component writes
    /// <see cref="CinemachinePanTilt"/>'s axes directly, reusing the exact maths from the
    /// retired <c>PlayerController.ComputeLookDelta</c>. Owner: MRM-9
    /// </para>
    /// </summary>
    // Must write this frame's input into Burntwax's InputManager before the movement, camera and
    // gun state machines read it in their own Update. Runs after InputManager (-100) and
    // InputPrioritySorter (-90), before everything at the default order. (MRM-9)
    [DefaultExecutionOrder(-80)]
    [RequireComponent(typeof(InputManager))]
    public sealed class BurntwaxInputBridge : MonoBehaviour
    {
        [Tooltip("Every virtual camera's pan/tilt. ALL of them are driven with the same values - see ApplyLook. Found under the prefab root if left empty.")]
        [SerializeField] private CinemachinePanTilt[] panTilts;

        [Tooltip("Optional. Gates sprint and jump on stamina, as the retired controller did (MRM-12).")]
        [SerializeField] private PlayerStats stats;

        private InputManager _burntwax;
        private InputMapController _input;
        private Vector2 _stickLookVelocity;
        private bool _isPaused;
        private bool _sprintHeld;
        private float _yaw;
        private float _pitch;

        /// <summary>The MRM-8 map controller, so other systems can switch maps (pause, cutscenes) without building a second one.</summary>
        public InputMapController Input => _input;

        private void Awake()
        {
            _burntwax = GetComponent<InputManager>();
            _input = new InputMapController(InputMode.Gameplay);

            if (stats == null) stats = transform.root.GetComponentInChildren<PlayerStats>(true);

            if (panTilts == null || panTilts.Length == 0)
            {
                // From the prefab root - the camera rig is a sibling branch. (MRM-9)
                panTilts = transform.root.GetComponentsInChildren<CinemachinePanTilt>(true);
            }

            // Seed from whatever the rig was authored at, so the first frame does not snap the
            // view to zero.
            if (panTilts != null && panTilts.Length > 0 && panTilts[0] != null)
            {
                _yaw = panTilts[0].PanAxis.Value;
                _pitch = panTilts[0].TiltAxis.Value;
            }
        }

        private void OnEnable()
        {
            InputSystem_Actions.GameplayActions g = _input.Actions.Gameplay;

            // Mirrors Burntwax's original started/canceled model exactly. Polling IsPressed()
            // instead would break Interactor's consume pattern, which sets interactIsPressed
            // back to false while the button is still physically held.
            g.Sprint.started += OnSprint; g.Sprint.canceled += OnSprint;
            g.AimDownSights.started += OnAim; g.AimDownSights.canceled += OnAim;
            g.Crouch.started += OnCrouch; g.Crouch.canceled += OnCrouch;
            g.Jump.started += OnJump; g.Jump.canceled += OnJump;
            g.Fire.started += OnFire; g.Fire.canceled += OnFire;
            g.Reload.started += OnReload; g.Reload.canceled += OnReload;
            g.Interact.started += OnInteract; g.Interact.canceled += OnInteract;
            g.Pause.started += OnPause; g.Pause.canceled += OnPause;

            // The Pause action lives in the Gameplay map, which goes quiet while paused, so the
            // UI map's Cancel is what resumes. Without this the menu could be opened but never
            // closed from a controller.
            _input.Actions.UI.Cancel.started += OnPause;
            _input.Actions.UI.Cancel.canceled += OnPause;
        }

        private void OnDisable()
        {
            InputSystem_Actions.GameplayActions g = _input.Actions.Gameplay;

            g.Sprint.started -= OnSprint; g.Sprint.canceled -= OnSprint;
            g.AimDownSights.started -= OnAim; g.AimDownSights.canceled -= OnAim;
            g.Crouch.started -= OnCrouch; g.Crouch.canceled -= OnCrouch;
            g.Jump.started -= OnJump; g.Jump.canceled -= OnJump;
            g.Fire.started -= OnFire; g.Fire.canceled -= OnFire;
            g.Reload.started -= OnReload; g.Reload.canceled -= OnReload;
            g.Interact.started -= OnInteract; g.Interact.canceled -= OnInteract;
            g.Pause.started -= OnPause; g.Pause.canceled -= OnPause;
            _input.Actions.UI.Cancel.started -= OnPause;
            _input.Actions.UI.Cancel.canceled -= OnPause;
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        // Sprint and jump both refuse at empty stamina, and sprint only applies moving forward.
        // Both rules come from the retired PlayerController: the stamina gate is MRM-12, and the
        // forward-only rule was Carlos's own MRM-9 playtest note (sprinting backwards felt wrong).
        // They are enforced here rather than inside Burntwax so its engine stays unmodified.
        private bool HasStamina => stats == null || stats.Stamina.Value > 0f;

        private void OnSprint(InputAction.CallbackContext ctx) => _sprintHeld = ctx.started;
        private void OnAim(InputAction.CallbackContext ctx) => _burntwax.aimIsPressed = ctx.started;
        private void OnCrouch(InputAction.CallbackContext ctx) => _burntwax.crouchIsPressed = ctx.started;
        private void OnJump(InputAction.CallbackContext ctx) => _burntwax.jumpIsPressed = ctx.started && HasStamina;
        private void OnFire(InputAction.CallbackContext ctx) => _burntwax.shootIsPressed = ctx.started;
        private void OnReload(InputAction.CallbackContext ctx) => _burntwax.reloadIsPressed = ctx.started;
        private void OnInteract(InputAction.CallbackContext ctx) => _burntwax.interactIsPressed = ctx.started;
        private void OnPause(InputAction.CallbackContext ctx) => _burntwax.pauseIsPressed = ctx.started;

        private void Update()
        {
            SyncPauseState();

            InputSystem_Actions.GameplayActions g = _input.Actions.Gameplay;

            // While paused the Gameplay map is disabled, so Move/Look already read zero. Bail out
            // anyway so mouse look — which does not scale by Time.deltaTime — cannot creep the
            // camera behind the pause menu.
            if (_isPaused)
            {
                return;
            }

            Vector2 move = g.Move.ReadValue<Vector2>();
            _burntwax.MoveInput = move;
            _burntwax.moveIsPressed = move != Vector2.zero;
            _burntwax.sprintIsPressed = _sprintHeld && move.y > 0f && HasStamina;

            Vector2 lookDelta = ComputeLookDelta();
            _burntwax.LookInput = lookDelta;

            // Weapon selection: MRM-8's SwitchWeapon is a single action rather than Burntwax's
            // nine separate number-key bindings, so it drives the scroll path instead of the
            // weaponSelectID path. GunStateMachine handles both.
            _burntwax.mouseScroll = g.InventoryScroll.ReadValue<float>();
            if (g.SwitchWeapon.WasPressedThisFrame())
            {
                _burntwax.mouseScroll = 1f;
            }

            ApplyLook(lookDelta);
        }

        /// <summary>
        /// Unchanged from the retired <c>PlayerController</c> (MRM-9): mouse delta is already a
        /// per-frame delta so it is applied directly, while stick input is a rate that ramps
        /// through <see cref="MoonlightTunables.LookAcceleration"/>.
        /// </summary>
        private Vector2 ComputeLookDelta()
        {
            InputAction lookAction = _input.Actions.Gameplay.Look;
            Vector2 rawLook = lookAction.ReadValue<Vector2>();
            bool isStick = lookAction.activeControl != null && lookAction.activeControl.device is Gamepad;

            if (isStick)
            {
                Vector2 targetVelocity = rawLook * Tunables.I.LookSpeedStick;
                _stickLookVelocity = Vector2.MoveTowards(_stickLookVelocity, targetVelocity, Tunables.I.LookAcceleration * Time.deltaTime);
                return _stickLookVelocity * Time.deltaTime;
            }

            _stickLookVelocity = Vector2.zero;
            return rawLook * Tunables.I.LookSpeedMouse;
        }

        /// <summary>
        /// Applies look to <b>every</b> virtual camera on the rig, from a single yaw/pitch owned
        /// here.
        ///
        /// <para><b>Why every camera, and why the value is owned here.</b> Aim-down-sights swaps
        /// the active camera from <c>vcam_FPS</c> to <c>vcam_Aim</c> by Cinemachine priority.
        /// Driving only the hip-fire camera meant the aim camera's axes were never written: the
        /// right stick appeared dead while aiming, and blending between one camera whose yaw kept
        /// moving and one frozen at its old value made the view spin. Keeping one authoritative
        /// value here and pushing it to all of them means the swap is seamless and there is no
        /// component to read stale state back from.</para>
        /// </summary>
        private void ApplyLook(Vector2 lookDelta)
        {
            _yaw += lookDelta.x;

            // Positive tilt = looking down in Cinemachine's convention, same as the retired
            // controller's pitch, so the down clamp is the positive bound.
            _pitch = Mathf.Clamp(_pitch - lookDelta.y, -Tunables.I.LookPitchUpMax, Tunables.I.LookPitchDownMax);

            PushLookToCameras();
        }

        private void PushLookToCameras()
        {
            if (panTilts == null) return;

            for (int i = 0; i < panTilts.Length; i++)
            {
                if (panTilts[i] == null) continue;
                panTilts[i].PanAxis.Value = _yaw;
                panTilts[i].TiltAxis.Value = _pitch;
            }
        }

        /// <summary>
        /// Follows <see cref="PauseMenu"/>'s state and swaps MRM-8 action maps to match, so the
        /// pause menu is navigable with the gamepad and gameplay bindings go quiet. Pause itself
        /// stays owned by <see cref="PauseMenu"/> (Time.timeScale + AudioListener.pause) — this
        /// only mirrors it, because Burntwax code cannot call into Mr. Moonlight.
        /// </summary>
        private void SyncPauseState()
        {
            bool paused = PauseMenu.Instance != null && PauseMenu.Instance.Paused;
            if (paused == _isPaused)
            {
                return;
            }

            _isPaused = paused;
            _input.SetMode(paused ? InputMode.UI : InputMode.Gameplay);

            if (paused)
            {
                // Clear held state so a button still down when the menu opened does not fire the
                // instant it closes.
                _burntwax.MoveInput = Vector2.zero;
                _burntwax.LookInput = Vector2.zero;
                _burntwax.moveIsPressed = false;
                _burntwax.sprintIsPressed = false;
                _sprintHeld = false;
                _burntwax.shootIsPressed = false;
                _burntwax.aimIsPressed = false;
                _burntwax.jumpIsPressed = false;
                _burntwax.crouchIsPressed = false;
                _burntwax.interactIsPressed = false;
                _burntwax.reloadIsPressed = false;
                _stickLookVelocity = Vector2.zero;
            }
        }

        /// <summary>Levels the camera. Replaces <c>PlayerController.ResetCameraPitch()</c> for MRM-17's death sequence.</summary>
        public void ResetCameraPitch()
        {
            _pitch = 0f;
            PushLookToCameras();
        }
    }
}
