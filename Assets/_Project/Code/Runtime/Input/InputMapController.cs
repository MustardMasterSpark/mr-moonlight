using System;
using MrMoonlight.Data;
using UnityEngine.InputSystem;

namespace MrMoonlight.Input
{
    /// <summary>
    /// Which action map is currently live. Modes that strip control (Turret, Stretcher, Cutscene)
    /// switch maps here rather than being handled with scattered <c>if</c> checks elsewhere. Owner: MRM-8
    /// </summary>
    public enum InputMode
    {
        Gameplay,
        UI,
        Turret,
        Stretcher,
        Cutscene
    }

    /// <summary>
    /// Owns the generated <see cref="InputSystem_Actions"/> asset and switches between its action
    /// maps. Not a MonoBehaviour and not a singleton (see Docs/csharp-conventions.md — the only
    /// sanctioned singleton is <see cref="Tunables"/>) — whoever needs input (the player controller,
    /// a future menu system) constructs one in Awake and calls <see cref="Dispose"/> in OnDestroy.
    ///
    /// Both the Keyboard&amp;Mouse and Gamepad control schemes stay bound simultaneously — nothing
    /// here restricts the asset to a single device. That is what makes "plug in a controller
    /// mid-session" work with no extra code: whichever device fires, its bound action fires.
    /// Owner: MRM-8
    /// </summary>
    public sealed class InputMapController : IDisposable
    {
        private readonly InputSystem_Actions _actions;

        public InputMode CurrentMode { get; private set; }
        public InputSystem_Actions Actions => _actions;

        public InputMapController(InputMode startingMode = InputMode.Gameplay)
        {
            _actions = new InputSystem_Actions();
            ApplyInputTunables();
            SetMode(startingMode);
        }

        /// <summary>
        /// Disables every action map, then enables only <paramref name="mode"/>'s — the previous
        /// map is always fully disabled, never left running underneath the new one. Owner: MRM-8
        /// </summary>
        public void SetMode(InputMode mode)
        {
            _actions.Gameplay.Disable();
            _actions.UI.Disable();
            _actions.Turret.Disable();
            _actions.Stretcher.Disable();
            _actions.Cutscene.Disable();

            switch (mode)
            {
                case InputMode.Gameplay:
                    _actions.Gameplay.Enable();
                    break;
                case InputMode.UI:
                    _actions.UI.Enable();
                    break;
                case InputMode.Turret:
                    _actions.Turret.Enable();
                    break;
                case InputMode.Stretcher:
                    _actions.Stretcher.Enable();
                    break;
                case InputMode.Cutscene:
                    _actions.Cutscene.Enable();
                    break;
            }

            CurrentMode = mode;
        }

        public void Dispose()
        {
            _actions.Dispose();
        }

        /// <summary>
        /// Stick deadzone is a device-wide default (Gamepad's stick controls fall back to it, see
        /// <c>Gamepad.cs</c>), so it's set once here rather than as a per-binding processor —
        /// adding a second deadzone processor on top of the device default would just clamp twice.
        /// Invert-Y is a binding-level processor on the Look action instead, since it is specific
        /// to this action, not every stick in the project. Owner: MRM-8
        /// </summary>
        private void ApplyInputTunables()
        {
            InputSystem.settings.defaultDeadzoneMin = Tunables.I.StickDeadzone;
            _actions.Gameplay.Look.ApplyParameterOverride("invertVector2:invertY", Tunables.I.InvertYAxis);
        }
    }
}
