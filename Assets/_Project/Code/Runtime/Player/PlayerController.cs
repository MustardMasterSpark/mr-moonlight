using System;
using MrMoonlight.Data;
using MrMoonlight.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Tracey's first-person controller: move, look, jump, crouch (toggle) and sprint, every
    /// value read live from <see cref="Tunables"/>. Owns its own <see cref="InputMapController"/>
    /// per MRM-8's construction pattern — built in Awake, disposed in OnDestroy. Owner: MRM-9
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("References")]

        /// <summary>
        /// Pitch pivot: yaw rotates this GameObject's transform, pitch and the crouch height
        /// offset are applied to this pivot. Carlos parents the Camera under it and sets the
        /// near-clip plane so Tracey can see her own placeholder body — see MRM-9.
        /// </summary>
        [SerializeField] private Transform cameraPivot;

        private CharacterController _controller;
        private InputMapController _input;

        private float _standingHeight;
        private float _standingCenterY;
        private float _standingCameraPivotY;

        private bool _isCrouched;
        private float _crouchBlend;

        private float _pitchDegrees;
        private Vector2 _stickLookVelocity;
        private float _verticalVelocity;
        private int _groundLayerMask;

        /// <summary>
        /// Raised once per jump, immediately after takeoff. This controller has no notion of
        /// stamina — MRM-12's stat framework subscribes here to apply the jump's cost.
        /// Owner: MRM-9, consumed by MRM-12.
        /// </summary>
        public event Action OnJumped;

        /// <summary>
        /// Raised every frame the player is actively sprinting (Sprint held, not crouched, and
        /// moving), with deltaTime, so MRM-12 can drain stamina proportionally to time spent
        /// sprinting. Owner: MRM-9, consumed by MRM-12.
        /// </summary>
        public event Action<float> OnSprinting;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = new InputMapController();
            _groundLayerMask = LayerMask.GetMask("Ground");

            if (cameraPivot == null)
            {
                Debug.LogError($"[Player] {name} is missing its camera pivot reference. See MRM-9.");
            }

            _standingHeight = _controller.height;
            _standingCenterY = _controller.center.y;
            _standingCameraPivotY = cameraPivot != null ? cameraPivot.localPosition.y : 0f;
        }

        private void OnDestroy()
        {
            _input.Dispose();
        }

        private void Update()
        {
            HandleCrouchToggle();
            UpdateCrouchTransition();
            UpdateLook();
            UpdateMove();
        }

        private void HandleCrouchToggle()
        {
            if (_input.Actions.Gameplay.Crouch.WasPerformedThisFrame())
            {
                _isCrouched = !_isCrouched;
            }
        }

        private void UpdateCrouchTransition()
        {
            float target = _isCrouched ? 1f : 0f;
            _crouchBlend = Mathf.MoveTowards(_crouchBlend, target, Time.deltaTime / Tunables.I.CrouchTransitionDuration);

            float heightDelta = Tunables.I.CrouchHeightDelta * _crouchBlend;

            _controller.height = _standingHeight - heightDelta;
            _controller.center = new Vector3(0f, _standingCenterY - heightDelta * 0.5f, 0f);

            if (cameraPivot != null)
            {
                Vector3 pivotPosition = cameraPivot.localPosition;
                pivotPosition.y = _standingCameraPivotY - heightDelta;
                cameraPivot.localPosition = pivotPosition;
            }
        }

        private void UpdateLook()
        {
            Vector2 lookDelta = ComputeLookDelta();

            transform.Rotate(Vector3.up, lookDelta.x);

            // Positive pitch = looking down (Unity's local X-axis convention), so the down clamp
            // is the positive bound and the up clamp is the negative bound.
            _pitchDegrees = Mathf.Clamp(_pitchDegrees - lookDelta.y, -Tunables.I.LookPitchUpMax, Tunables.I.LookPitchDownMax);

            if (cameraPivot != null)
            {
                cameraPivot.localRotation = Quaternion.Euler(_pitchDegrees, 0f, 0f);
            }
        }

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

            // Mouse delta is already a per-frame pixel delta, not a rate, so it's applied
            // directly with no acceleration smoothing — matches LookSpeedMouse's doc comment.
            _stickLookVelocity = Vector2.zero;
            return rawLook * Tunables.I.LookSpeedMouse;
        }

        private void UpdateMove()
        {
            _controller.slopeLimit = Tunables.I.SlopeLimit;

            Vector2 moveInput = _input.Actions.Gameplay.Move.ReadValue<Vector2>();
            bool sprintHeld = _input.Actions.Gameplay.Sprint.IsPressed();
            // Sprint only applies moving forward — holding Sprint while backing up or strafing
            // just walks. Flagged by Carlos during MRM-9 testing: sprinting backwards felt wrong.
            bool isSprinting = sprintHeld && !_isCrouched && moveInput.y > 0f;

            float speed = _isCrouched
                ? Tunables.I.CrouchSpeed
                : isSprinting ? Tunables.I.SprintSpeed : Tunables.I.WalkSpeed;

            Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            bool grounded = CheckGrounded();

            if (grounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = 0f;
            }

            if (_input.Actions.Gameplay.Jump.WasPerformedThisFrame() && grounded && !_isCrouched)
            {
                _verticalVelocity = ComputeJumpVelocity();
                OnJumped?.Invoke();
            }

            _verticalVelocity += Tunables.I.Gravity * Time.deltaTime;

            Vector3 velocity = moveDirection * speed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);

            if (isSprinting)
            {
                OnSprinting?.Invoke(Time.deltaTime);
            }
        }

        // CharacterController.isGrounded is unreliable while stationary — confirmed live during
        // MRM-9 testing, it reads false even at rest on flat ground, which silently blocked
        // jumping whenever the player wasn't moving. A short downward SphereCast against the
        // Ground layer replaces it as the authoritative check for jump eligibility and the
        // vertical-velocity reset. Any floor/terrain placed in the scene must be on the Ground
        // layer for this to work — see Docs/unity-conventions.md's layers table.
        private bool CheckGrounded()
        {
            var origin = transform.position + _controller.center + Vector3.down * (_controller.height * 0.5f - _controller.radius);
            return Physics.SphereCast(origin, _controller.radius * 0.95f, Vector3.down, out _, Tunables.I.GroundCheckDistance, _groundLayerMask, QueryTriggerInteraction.Ignore);
        }

        // JumpSpeed is the applied takeoff velocity (Carlos tunes feel directly, per its own doc
        // comment on MoonlightTunables). JumpHeight caps the apex that velocity can reach, derived
        // from Gravity via the standard v = sqrt(2gh) relationship, so both fields stay live and
        // meaningful instead of JumpHeight sitting unused. Flagged for Carlos to confirm — see MRM-9.
        private float ComputeJumpVelocity()
        {
            float heightCappedVelocity = Mathf.Sqrt(2f * Mathf.Abs(Tunables.I.Gravity) * Tunables.I.JumpHeight);
            return Mathf.Min(Tunables.I.JumpSpeed, heightCappedVelocity);
        }
    }
}
