using System;
using Burntwax;
using MrMoonlight.Data;
using MrMoonlight.Input;
using UnityEngine;

namespace MrMoonlight.Player
{
    /// <summary>
    /// The player's control surface, replacing the retired <c>PlayerController</c> (MRM-9).
    ///
    /// <para><b>What this is.</b> Movement itself now belongs to the Burntwax FPS Engine's
    /// Rigidbody state machine. This component is what the rest of Mr. Moonlight talks to: it
    /// pushes <see cref="MoonlightTunables"/> values into the engine, upholds the MRM-12 stat
    /// contract, and re-exposes the exact API the old controller offered — <see cref="OnJumped"/>,
    /// <see cref="OnSprinting"/>, <see cref="Input"/>, <see cref="SetMovementLocked"/>,
    /// <see cref="DisableControl"/>, <see cref="ResetCameraPitch"/> and <see cref="CameraPivot"/>.
    /// That keeps MRM-12 (stats), MRM-16 (interaction), MRM-17 (death) and MRM-41/42 (inventory)
    /// working against an unchanged contract instead of being rewritten around a new engine.</para>
    ///
    /// <para><b>Speeds come from MoonlightTunables.</b> Burntwax ships walk/sprint/crouch as
    /// serialized inspector fields; project rule is that no gameplay number is hardcoded
    /// (CLAUDE.md), so they are pushed in from the tunables asset every frame.</para>
    ///
    /// <para><b>The MRM-12 speed contract is preserved exactly.</b> Whichever mode speed applies
    /// is written into <c>PlayerStats.Speed.BaseValue</c>, and the engine then moves at the
    /// modifier-stacked <c>Speed.Value</c> — so a speed item or debuff affects movement without
    /// movement code knowing items exist. The mode is read from the state machine's current
    /// substate rather than by reading <c>PlayerVelocity</c> back, because the Burntwax states
    /// assign that only in <c>EnterState</c>; reading it back and rewriting it would feed the
    /// modified value in as next frame's base and compound every frame.</para>
    ///
    /// Owner: MRM-9 (controller swap), MRM-12 (stat contract), MRM-17 (death hooks).
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public sealed class BurntwaxPlayerBridge : MonoBehaviour
    {
        [Tooltip("The Burntwax movement state machine. Found in children if left unassigned.")]
        [SerializeField] private PlayerStateMachine stateMachine;

        [Tooltip("Input bridge, for look reset and the shared InputMapController. Found in children if left unassigned.")]
        [SerializeField] private BurntwaxInputBridge inputBridge;

        [Tooltip("Transform the camera pitches around. Used by MRM-17's death sequence.")]
        [SerializeField] private Transform cameraPivot;

        private PlayerStats _stats;
        private PlayerBaseState _previousRootState;

        /// <summary>Raised once per jump. Consumed by <see cref="PlayerStats"/> for the jump stamina cost (MRM-12).</summary>
        public event Action OnJumped;

        /// <summary>Raised every frame while sprinting, with that frame's delta time. Drives stamina drain (MRM-12).</summary>
        public event Action<float> OnSprinting;

        /// <summary>The shared MRM-8 map controller. Consumed by MRM-16's detector and MRM-41's inventory UI.</summary>
        public InputMapController Input => inputBridge != null ? inputBridge.Input : null;

        /// <summary>Transform the camera pitches around. Consumed by MRM-17's death sequence.</summary>
        public Transform CameraPivot => cameraPivot;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();

            // Searched from the prefab root, not from this object: the Mr. Moonlight components
            // live on their own child of the Burntwax prefab, so the engine's components are in a
            // sibling branch rather than below us. (MRM-9)
            if (stateMachine == null) stateMachine = transform.root.GetComponentInChildren<PlayerStateMachine>(true);
            if (inputBridge == null) inputBridge = transform.root.GetComponentInChildren<BurntwaxInputBridge>(true);

            if (stateMachine == null)
            {
                Debug.LogError($"{nameof(BurntwaxPlayerBridge)} on '{name}' found no {nameof(PlayerStateMachine)}. Movement will not respond to tunables or stat modifiers.", this);
            }
        }

        private void Update()
        {
            RaiseMovementEvents();
        }

        private void LateUpdate()
        {
            if (stateMachine == null || _stats == null) return;

            PushTunables();
            ApplySpeedStat();
        }

        /// <summary>
        /// Reproduces the retired controller's two hooks against the state machine. Jump is
        /// edge-detected on the transition into the jump state; sprint is raised continuously
        /// while the sprint substate is active, matching the old per-frame drain.
        /// </summary>
        private void RaiseMovementEvents()
        {
            if (stateMachine == null || stateMachine.currentState == null) return;

            PlayerBaseState root = stateMachine.currentState;
            if (root == stateMachine.states.Jump() && _previousRootState != stateMachine.states.Jump())
            {
                OnJumped?.Invoke();
            }
            _previousRootState = root;

            if (root.CurrentSubstate() == stateMachine.states.Sprint())
            {
                OnSprinting?.Invoke(Time.deltaTime);
            }
        }

        private void PushTunables()
        {
            stateMachine.WalkVelocity = Tunables.I.WalkSpeed;
            stateMachine.SprintVelocity = Tunables.I.SprintSpeed;
            stateMachine.CrouchVelocity = Tunables.I.CrouchSpeed;
        }

        private void ApplySpeedStat()
        {
            _stats.Speed.BaseValue = CurrentModeSpeed();
            stateMachine.PlayerVelocity = _stats.Speed.Value;
        }

        private float CurrentModeSpeed()
        {
            PlayerBaseState substate = stateMachine.currentState == null ? null : stateMachine.currentState.CurrentSubstate();
            if (substate == null) return Tunables.I.WalkSpeed;
            if (substate == stateMachine.states.Sprint()) return Tunables.I.SprintSpeed;
            if (substate == stateMachine.states.Crouch()) return Tunables.I.CrouchSpeed;

            // Walk and Idle both use walk speed; idle has no move input so the value is moot.
            return Tunables.I.WalkSpeed;
        }

        /// <summary>
        /// Reversible movement lock, used while the inventory is open (MRM-41). Look is left
        /// alone deliberately — the old controller behaved the same way.
        /// </summary>
        public void SetMovementLocked(bool locked)
        {
            if (stateMachine != null) stateMachine.DisableMovement = locked;
        }

        /// <summary>Irreversible control shutdown for the death sequence (MRM-17).</summary>
        public void DisableControl()
        {
            SetMovementLocked(true);
            if (inputBridge != null) inputBridge.enabled = false;
        }

        /// <summary>Levels the camera before the death blackout (MRM-17).</summary>
        public void ResetCameraPitch()
        {
            if (inputBridge != null) inputBridge.ResetCameraPitch();
        }
    }
}
