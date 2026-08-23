using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Debug-only free-fly mode for inspecting terrain in Play mode - flies in whatever direction
    /// the camera is looking, full 3D, with no collision, gravity, stamina or slope handling.
    /// Toggle <see cref="flyModeEnabled"/> in the Inspector, live during Play mode, to switch
    /// between this and the normal <see cref="PlayerController"/>. The two never run at the same
    /// time: this component only flips PlayerController's and CharacterController's
    /// <c>enabled</c> flags, it never reaches into PlayerController's own fields, so normal player
    /// movement (slope, slide, jump, stamina) stays exactly as built. Reads the mouse/keyboard and
    /// gamepad directly rather than through the shared Gameplay action map, on purpose, so this
    /// stays fully isolated from the real input pipeline - both device types stay live
    /// simultaneously, same "whichever fires" philosophy as MRM-8's InputMapController. Not part
    /// of the shipped game - same "debug tool, not shipped" category as MRM-8's InputDebugOverlay.
    /// Added for MRM-58 terrain blockout at Carlos's request; not an acceptance-criteria item on
    /// any issue.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(CharacterController))]
    public sealed class DebugFlyController : MonoBehaviour
    {
        [Header("Toggle - live in Play mode")]
        [Tooltip("On: fly freely, no collision, PlayerController disabled. Off: hand control back to PlayerController.")]
        [SerializeField] private bool flyModeEnabled;

        [Header("References")]
        [Tooltip("Look/fly direction pivot. Defaults to PlayerController's own camera pivot if left unassigned.")]
        [SerializeField] private Transform cameraPivot;

        [Header("Feel - debug tool only, deliberately local fields rather than MoonlightTunables")]
        [SerializeField] private float flySpeed = 6f;
        [SerializeField] private float flySpeedFast = 18f;
        [SerializeField] private float lookSpeed = 0.15f;
        [Tooltip("Right stick look speed, degrees/second at full deflection.")]
        [SerializeField] private float stickLookSpeed = 180f;
        [Tooltip("How quickly stick look ramps to its target speed, degrees/second^2.")]
        [SerializeField] private float stickLookAcceleration = 900f;

        private PlayerController _playerController;
        private CharacterController _characterController;
        private bool _flyModeActive;
        private float _pitchDegrees;
        private Vector2 _stickLookVelocity;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _characterController = GetComponent<CharacterController>();

            if (cameraPivot == null)
            {
                cameraPivot = _playerController.CameraPivot;
            }
        }

        private void Update()
        {
            if (flyModeEnabled != _flyModeActive)
            {
                SetFlyModeActive(flyModeEnabled);
            }

            if (_flyModeActive)
            {
                UpdateLook();
                UpdateFly();
            }
        }

        private void SetFlyModeActive(bool active)
        {
            _flyModeActive = active;
            _playerController.enabled = !active;
            _characterController.enabled = !active;

            if (active && cameraPivot != null)
            {
                // Pick up whatever pitch PlayerController left the camera pivot at, so entering
                // fly mode doesn't snap the view level.
                _pitchDegrees = cameraPivot.localEulerAngles.x;
                if (_pitchDegrees > 180f)
                {
                    _pitchDegrees -= 360f;
                }
            }
        }

        // Mirrors PlayerController's two look paths (mouse: instant per-frame delta; stick:
        // accelerate toward a target velocity) - both stay live at once, same as the real
        // controller, so switching devices mid-session just works.
        private void UpdateLook()
        {
            if (cameraPivot == null)
            {
                return;
            }

            Vector2 delta = Vector2.zero;

            if (Mouse.current != null)
            {
                delta += Mouse.current.delta.ReadValue() * lookSpeed;
            }

            if (Gamepad.current != null)
            {
                Vector2 targetVelocity = Gamepad.current.rightStick.ReadValue() * stickLookSpeed;
                _stickLookVelocity = Vector2.MoveTowards(_stickLookVelocity, targetVelocity, stickLookAcceleration * Time.deltaTime);
                delta += _stickLookVelocity * Time.deltaTime;
            }

            transform.Rotate(Vector3.up, delta.x);

            _pitchDegrees = Mathf.Clamp(_pitchDegrees - delta.y, -89f, 89f);
            cameraPivot.localRotation = Quaternion.Euler(_pitchDegrees, 0f, 0f);
        }

        // Full 3D fly: forward/back follows the camera pivot's pitch too, so looking down and
        // pressing forward descends. Space/Ctrl (keyboard) and the triggers (gamepad) add a
        // look-independent vertical nudge on top; left shift or a left-stick click boosts speed.
        private void UpdateFly()
        {
            if (cameraPivot == null)
            {
                return;
            }

            float forwardInput = 0f;
            float strafeInput = 0f;
            float verticalInput = 0f;
            bool boost = false;

            if (Keyboard.current != null)
            {
                var keyboard = Keyboard.current;
                forwardInput += (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
                strafeInput += (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                verticalInput += (keyboard.spaceKey.isPressed ? 1f : 0f) - (keyboard.leftCtrlKey.isPressed ? 1f : 0f);
                boost |= keyboard.leftShiftKey.isPressed;
            }

            if (Gamepad.current != null)
            {
                var gamepad = Gamepad.current;
                Vector2 moveStick = gamepad.leftStick.ReadValue();
                forwardInput += moveStick.y;
                strafeInput += moveStick.x;
                verticalInput += gamepad.rightTrigger.ReadValue() - gamepad.leftTrigger.ReadValue();
                boost |= gamepad.leftStickButton.isPressed;
            }

            Vector3 direction = cameraPivot.forward * forwardInput + cameraPivot.right * strafeInput + Vector3.up * verticalInput;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            float speed = boost ? flySpeedFast : flySpeed;
            transform.position += direction * speed * Time.deltaTime;
        }
    }
}
