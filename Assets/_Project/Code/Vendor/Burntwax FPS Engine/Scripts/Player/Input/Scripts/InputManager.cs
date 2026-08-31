using UnityEngine;

namespace Burntwax
{
    /// <summary>
    /// Passive input state holder for the Burntwax engine.
    /// <para>
    /// <b>Modified for Mr. Moonlight (MRM-9).</b> Originally this class owned its own
    /// <c>PlayerInput</c> generated wrapper and subscribed to Burntwax's own
    /// <c>PlayerInput.inputactions</c>. Mr. Moonlight already has a complete Input System setup
    /// from MRM-8 (<c>InputSystem_Actions</c>, with Keyboard&amp;Mouse and Gamepad control schemes
    /// that Burntwax's asset never had), so running a second, competing action asset would have
    /// meant two sets of bindings fighting over the same devices.
    /// </para>
    /// <para>
    /// Instead this is now a plain data holder: every field is written each frame by
    /// <c>MrMoonlight.Player.BurntwaxInputBridge</c>, which subscribes to MRM-8's actions and
    /// reproduces the original started/canceled semantics exactly. The public surface is
    /// unchanged, so all other Burntwax scripts compile and behave as shipped.
    /// </para>
    /// <para>
    /// The dependency deliberately runs one way only — <c>MrMoonlight.Runtime</c> references
    /// <c>Burntwax.Core</c>, never the reverse — because assembly definitions cannot reference
    /// each other circularly. That is why this class knows nothing about Mr. Moonlight.
    /// </para>
    /// </summary>
    // Awake order across GameObjects is arbitrary in Unity. The Burntwax demo scene happened to
    // initialise this before PlayerStateMachine.Awake() read InputManager.Instance; in the
    // Mr. Moonlight prefab it did not, and the state machine threw a NullReferenceException on
    // the first frame. An explicit early execution order makes that ordering guaranteed rather
    // than incidental. (MRM-9)
    [DefaultExecutionOrder(-100)]
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance;

        public Vector2 MoveInput = Vector2.zero;
        public Vector2 LookInput = Vector2.zero;
        public bool InvertMouseY = false;

        // Consumed by Interactor, which sets it back to false for non-continuous interactables.
        public bool interactIsPressed = false;

        public bool moveIsPressed = false;
        public bool crouchIsPressed = false;
        public bool jumpIsPressed = false;
        public bool pauseIsPressed = false;

        public float mouseScroll = 0f;
        public int weaponSelectID = -1;
        public bool HasWeaponSelectInput { get { return weaponSelectID >= 0; } }

        public bool aimIsPressed = false;
        public bool shootIsPressed = false;
        public bool reloadIsPressed = false;
        public bool sprintIsPressed = false;

        void Awake()
        {
            Instance = this;
        }

        public void ConsumeWeaponSwitchInput()
        {
            weaponSelectID = -1;
            mouseScroll = 0f;
        }
    }
}
