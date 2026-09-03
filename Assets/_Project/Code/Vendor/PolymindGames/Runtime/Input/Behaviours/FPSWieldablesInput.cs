using PolymindGames.WieldableSystem;
using UnityEngine.InputSystem;
using PolymindGames.Options;
using UnityEngine;

namespace PolymindGames.InputSystem.Behaviours
{
    [AddComponentMenu("Input/Wieldables Input")]
    [RequireCharacterComponent(typeof(IWieldablesControllerCC), typeof(IWieldableInventoryCC))]
    [OptionalCharacterComponent(typeof(IWieldableHealingHandlerCC), typeof(IWieldableThrowableHandlerCC))]
    public class FPSWieldablesInput : PlayerInputBehaviour
    {
        [SerializeField, Title("Actions")]
        private InputActionReference _useAction;

        /// <summary>MRM-9: tracks whether the use/fire input was pressed last frame, so the End
        /// phase is emitted exactly once on release even when an analog trigger decays gradually.</summary>
        private bool _useHeld;

        [SerializeField]
        private InputActionReference _reloadAction;

        [SerializeField]
        private InputActionReference _dropAction;

        [SerializeField]
        private InputActionReference _aimAction;

        [SerializeField]
        private InputActionReference _selectAction;

        [SerializeField]
        private InputActionReference _holsterAction;

        [SerializeField]
        private InputActionReference _healAction;

        [SerializeField]
        private InputActionReference _throwAction;

        [SerializeField]
        private InputActionReference _firemodeAction;

        [SerializeField]
        private InputActionReference _throwableScrollAction;

        private IWieldableThrowableHandlerCC _throwableHandler;
        private IWieldableHealingHandlerCC _healingHandler;
        private IWieldableInventoryCC _selection;
        private IWieldablesControllerCC _controller;
        private IReloadInputHandler _reloadInputHandler;
        private IUseInputHandler _useInputHandler;
        private IAimInputHandler _aimInputHandler;
        private IWieldable _activeWieldable;

        #region Initialization
        protected override void OnBehaviourStart(ICharacter character)
        {
            _controller = character.GetCC<IWieldablesControllerCC>();
            _selection = character.GetCC<IWieldableInventoryCC>();
            _healingHandler = character.GetCC<IWieldableHealingHandlerCC>();
            _throwableHandler = character.GetCC<IWieldableThrowableHandlerCC>();
            
            _controller.EquippingStopped += OnEquip;
            _controller.HolsteringStarted += OnHolster;
        }

        protected override void OnBehaviourDestroy(ICharacter character)
        {
            if (_controller != null)
            {
                _controller.EquippingStopped -= OnEquip;
                _controller.HolsteringStarted -= OnHolster;
            }
        }

        protected override void OnBehaviourEnable(ICharacter character)
        {
            _holsterAction.RegisterStarted(OnHolsterAction);
            _selectAction.RegisterStarted(OnSelectAction);
            _dropAction.RegisterStarted(OnDropAction);

            if (_healingHandler != null)
                _healAction.RegisterStarted(OnHealAction);

            if (_throwableHandler != null)
            {
                _throwAction.RegisterStarted(OnThrowAction);
                _throwableScrollAction.RegisterPerformed(OnThrowableScrollAction);
            }

            _useAction.EnableAction();
            _aimAction.EnableAction();

            _firemodeAction.RegisterStarted(OnFiremodeAction);
            _reloadAction.RegisterStarted(OnReloadAction);
            
            if (_controller.State == WieldableControllerState.None)
                OnEquip(_controller.ActiveWieldable);
        }

        protected override void OnBehaviourDisable(ICharacter character)
        {
            _holsterAction.UnregisterStarted(OnHolsterAction);
            _selectAction.UnregisterStarted(OnSelectAction);
            _dropAction.UnregisterStarted(OnDropAction);

            if (_healingHandler != null)
                _healAction.UnregisterStarted(OnHealAction);

            if (_throwableHandler != null)
            {
                _throwAction.UnregisterStarted(OnThrowAction);
                _throwableScrollAction.UnregisterPerformed(OnThrowableScrollAction);
            }

            _firemodeAction.UnregisterStarted(OnFiremodeAction);
            _reloadAction.UnregisterStarted(OnReloadAction);
            
            _useAction.DisableAction();
            _aimAction.DisableAction();

            OnHolster(_activeWieldable);
        }

        private void OnHolster(IWieldable wieldable)
        {
            _useInputHandler?.Use(WieldableInputPhase.End);
            _useInputHandler = null;
            
            _aimInputHandler?.Aim(WieldableInputPhase.End);
            _aimInputHandler = null;
            
            _reloadInputHandler?.Reload(WieldableInputPhase.End);
            _reloadInputHandler = null;
        }

        private void OnEquip(IWieldable wieldable)
        {
            _activeWieldable = wieldable;
            _useInputHandler = wieldable as IUseInputHandler;
            _aimInputHandler = wieldable as IAimInputHandler;
            _reloadInputHandler = wieldable as IReloadInputHandler;
        }
        #endregion

        #region Input Handling
        private void Update()
        {
            if (_useInputHandler != null)
            {
                // MRM-9: released is decided by the button press point, not by "value > 0.001".
                //
                // The vendor test was fine for a mouse, which snaps 1 -> 0 in a single frame. A
                // gamepad analog trigger decays over several frames, so on the frame it was
                // released the value was still above 0.001, the chain took the Hold branch, and
                // WasReleasedThisFrame - true for exactly one frame - was missed. End never fired,
                // so FirearmTriggerBehaviour.IsTriggerHeld stayed true and the weapon refused to
                // shoot again until something else reset it. That is the "trigger takes ages to
                // register a second shot" bug.
                bool isPressed = _useAction.action.enabled && _useAction.action.IsPressed();
                if (_useAction.action.triggered)
                {
                    _useHeld = true;
                    _useInputHandler.Use(WieldableInputPhase.Start);
                }
                else if (isPressed)
                {
                    _useHeld = true;
                    _useInputHandler.Use(WieldableInputPhase.Hold);
                }
                else if (_useHeld || !_useAction.action.enabled)
                {
                    _useHeld = false;
                    _useInputHandler.Use(WieldableInputPhase.End);
                }
            }

            if (_aimInputHandler != null)
            {
                if (InputOptions.Instance.AimToggle)
                {
                    if (_aimAction.action.WasPressedThisFrame())
                    {
                        _aimInputHandler.Aim(_aimInputHandler.IsAiming ? WieldableInputPhase.End : WieldableInputPhase.Start);
                    }
                }
                else
                {
                    // MRM-9: same analog-trigger fix as the use action above - the gamepad left
                    // trigger decays rather than snapping to zero, so a "> 0.001" test kept the
                    // player stuck in aim-down-sights. IsPressed() honours the button press point.
                    if (_aimAction.action.IsPressed())
                    {
                        if (!_aimInputHandler.IsAiming)
                            _aimInputHandler.Aim(WieldableInputPhase.Start);
                    }
                    else if (_aimInputHandler.IsAiming)
                        _aimInputHandler.Aim(WieldableInputPhase.End);
                }
            }
        }

        private void OnSelectAction(InputAction.CallbackContext context)
        {
            int index = (int)context.ReadValue<float>() - 1;
            _selection.SelectAtIndex(index);
        }

        private void OnFiremodeAction(InputAction.CallbackContext context)
        {
            if (_activeWieldable is IFirearm && _activeWieldable.gameObject.TryGetComponent<IFirearmIndexModeHandler>(out var modeHandler))
                modeHandler.ToggleNextMode();
        }

        private void OnReloadAction(InputAction.CallbackContext context) => _reloadInputHandler?.Reload(WieldableInputPhase.Start);
        private void OnDropAction(InputAction.CallbackContext context) => _selection.DropWieldable();
        private void OnHealAction(InputAction.CallbackContext context) => _healingHandler?.TryHeal();
        private void OnThrowAction(InputAction.CallbackContext context) => _throwableHandler?.TryThrow();
        private void OnThrowableScrollAction(InputAction.CallbackContext context) => _throwableHandler.SelectNext(context.ReadValue<float>() > 0);
        private void OnHolsterAction(InputAction.CallbackContext context) => _selection.SelectAtIndex(_selection.SelectedIndex != -1 ? -1 : _selection.PreviousIndex);
        #endregion
    }
}