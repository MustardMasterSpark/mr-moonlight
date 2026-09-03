using UnityEngine.InputSystem;
using UnityEngine;

namespace PolymindGames.InputSystem.Behaviours
{
    [AddComponentMenu("Input/Look Input")]
    [RequireCharacterComponent(typeof(ILookHandlerCC))]
    public class FPSLookInput : PlayerInputBehaviour
    {
        [SerializeField, Title("Actions")]
        private InputActionReference _lookAction;

        // --- MRM-9: gamepad look ---
        // The vendor fed every device straight into CharacterLookHandler, which multiplies by
        // InputOptions.MouseSensitivity. That is correct for a mouse, whose Look value is a
        // per-frame delta of tens of units, but a gamepad stick is a *rate* that maxes out at 1.0,
        // so the right stick moved the camera at a crawl. Stick input is converted to a per-frame
        // delta here, with an acceleration ramp; the mouse path is deliberately untouched so its
        // feel and its sensitivity option are unchanged.
        //
        // Values are pushed in by MoonlightPlayerRig from MoonlightTunables at startup - this
        // assembly cannot reference Mr. Moonlight's (that would be a circular asmdef reference),
        // which is the same reason StaminaManager.TrySetStateCosts exists.
        private float _gamepadLookSpeed = 1200f;
        private float _gamepadLookAcceleration = 9000f;
        private Vector2 _stickVelocity;

        /// <summary>MRM-9. Sets the gamepad look rate (mouse-delta-equivalent units per second) and
        /// how fast the stick ramps up to it. Called by <c>MoonlightPlayerRig</c>.</summary>
        public void SetGamepadLook(float speed, float acceleration)
        {
            _gamepadLookSpeed = Mathf.Max(0f, speed);
            _gamepadLookAcceleration = Mathf.Max(0f, acceleration);
        }

        private ILookHandlerCC _lookHandler;

        #region Initialization
        protected override void OnBehaviourStart(ICharacter character)
        {
            _lookHandler = character.GetCC<ILookHandlerCC>();
        }

        protected override void OnBehaviourEnable(ICharacter character)
        {
            _lookAction.EnableAction();
            _lookHandler.SetLookInput(GetInput);
        }

        protected override void OnBehaviourDisable(ICharacter character)
        {
            _lookAction.DisableAction();
            _lookHandler.SetLookInput(null);
        }
        #endregion

        #region Input Handling
        private Vector2 GetInput()
        {
#if UNITY_EDITOR
            if (Time.timeSinceLevelLoad < 0.15f)
                return Vector2.zero;
#endif
            
            Vector2 lookInput = _lookAction.action.ReadValue<Vector2>();

            // MRM-9: a stick is a rate, a mouse is already a delta. Only the stick gets converted.
            var control = _lookAction.action.activeControl;
            bool isStick = control != null && control.device is Gamepad;
            if (isStick)
            {
                Vector2 target = lookInput * _gamepadLookSpeed;
                _stickVelocity = Vector2.MoveTowards(_stickVelocity, target, _gamepadLookAcceleration * Time.deltaTime);
                lookInput = _stickVelocity * Time.deltaTime;
            }
            else
            {
                _stickVelocity = Vector2.zero;
            }

            (lookInput.x, lookInput.y) = (lookInput.y, lookInput.x);
            return lookInput;
        }
        #endregion
    }
}