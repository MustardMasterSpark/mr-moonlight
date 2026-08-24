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
    ///
    /// <para>Optionally linked to a sibling <see cref="PlayerStats"/> (MRM-12, unified per
    /// Carlos's call on that issue): when present, this controller feeds its computed base speed
    /// into <see cref="PlayerStats.Speed"/> and moves at the modifier-stacked result instead of
    /// its own raw number, and refuses to jump or sprint at exactly empty stamina. Falls back to
    /// its original MRM-9-only behaviour when no <see cref="PlayerStats"/> is attached, so this
    /// stays optional rather than a hard requirement on every prefab using this controller.</para>
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

        /// <summary>Optional. Auto-found via GetComponent if left unassigned and present on this GameObject. See the class doc comment — MRM-12.</summary>
        [SerializeField] private PlayerStats stats;

        private CharacterController _controller;
        private InputMapController _input;

        private float _standingHeight;
        private float _standingCenterY;
        private float _standingCameraPivotY;

        private bool _isCrouched;
        private float _crouchBlend;
        private bool _controlDisabled;

        private float _pitchDegrees;
        private Vector2 _stickLookVelocity;
        private float _verticalVelocity;
        private int _groundLayerMask;
        private readonly RaycastHit[] _groundHitsBuffer = new RaycastHit[8];

        /// <summary>
        /// Raised once per jump, immediately after takeoff. When a sibling <see cref="PlayerStats"/>
        /// is present, this controller also refuses to jump at exactly empty stamina (MRM-12) —
        /// MRM-12's stat framework subscribes here to apply the jump's actual stamina cost.
        /// Owner: MRM-9, consumed by MRM-12.
        /// </summary>
        public event Action OnJumped;

        /// <summary>
        /// Raised every frame the player is actively sprinting (Sprint held, not crouched, moving
        /// forward, and — when a sibling <see cref="PlayerStats"/> is present — stamina above
        /// zero; sprint silently falls back to a walk once stamina empties, MRM-12), with
        /// deltaTime, so MRM-12 can drain stamina proportionally to time spent sprinting.
        /// Owner: MRM-9, consumed by MRM-12.
        /// </summary>
        public event Action<float> OnSprinting;

        /// <summary>The pitch pivot MRM-9 built for the camera - exposed read-only so MRM-17's DeathSequence can drive its own fall tilt and shake directly once <see cref="DisableControl"/> has stopped this controller's own Update. Owner: MRM-9, exposed for MRM-17</summary>
        public Transform CameraPivot => cameraPivot;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = new InputMapController();
            _groundLayerMask = Tunables.I.GroundCheckMask;

            if (stats == null)
            {
                stats = GetComponent<PlayerStats>();
            }

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
            if (_controlDisabled)
            {
                return;
            }

            HandleCrouchToggle();
            UpdateCrouchTransition();
            UpdateLook();
            UpdateMove();
        }

        /// <summary>
        /// Stops this controller's Update entirely - no more look, move, crouch or jump input is
        /// read. Whatever crouch height/camera state was live at the moment this is called stays
        /// frozen exactly as it was, which is what MRM-17's "keep her stance through the fall"
        /// acceptance criterion needs for free. There is no re-enable: the only caller is the
        /// death sequence, and death does not currently un-happen. Owner: MRM-17
        /// </summary>
        public void DisableControl()
        {
            _controlDisabled = true;
        }

        /// <summary>Snaps the camera pivot back to level, facing forward - MRM-17's "return the camera to facing forward" cleanup step, called before the death fall's own tilt takes over. Owner: MRM-17</summary>
        public void ResetCameraPitch()
        {
            _pitchDegrees = 0f;
            if (cameraPivot != null)
            {
                cameraPivot.localRotation = Quaternion.identity;
            }
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
            // Empty stamina blocks sprint the same way it blocks jump below — falls back to a
            // walk rather than refusing movement outright. See MRM-12.
            bool hasStamina = stats == null || stats.Stamina.Value > 0f;
            // Sprint only applies moving forward — holding Sprint while backing up or strafing
            // just walks. Flagged by Carlos during MRM-9 testing: sprinting backwards felt wrong.
            bool isSprinting = sprintHeld && !_isCrouched && moveInput.y > 0f && hasStamina;

            float baseSpeed = _isCrouched
                ? Tunables.I.CrouchSpeed
                : isSprinting ? Tunables.I.SprintSpeed : Tunables.I.WalkSpeed;

            // Feed this frame's base speed into PlayerStats.Speed and move at its modifier-stacked
            // result instead of the raw number, so boots/weapon/substance modifiers (MRM-12)
            // actually affect movement rather than sitting on an unread stat. See the class doc
            // comment.
            float speed = baseSpeed;
            if (stats != null)
            {
                stats.Speed.BaseValue = baseSpeed;
                speed = stats.Speed.Value;
            }

            Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            bool grounded = CheckGrounded(out Vector3 groundNormal);
            bool onSteepSlope = grounded && Vector3.Angle(groundNormal, Vector3.up) > _controller.slopeLimit;

            if (grounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = 0f;
            }

            // hasStamina also gates jump (MRM-12) — refuses at exactly empty rather than letting
            // OnJumped's stamina cost drain below zero. Jump is not blocked on steep ground —
            // sliding below stops the jump-climb instead, while still letting Tracey jump off a
            // slope back toward flat ground.
            if (_input.Actions.Gameplay.Jump.WasPerformedThisFrame() && grounded && !_isCrouched && hasStamina)
            {
                _verticalVelocity = ComputeJumpVelocity();
                OnJumped?.Invoke();
            }

            _verticalVelocity += Tunables.I.Gravity * Time.deltaTime;

            Vector3 velocity = moveDirection * speed + Vector3.up * _verticalVelocity;

            // Ground steeper than SlopeLimit slides Tracey back down it instead of just refusing
            // to climb — CharacterController.slopeLimit alone only blocks the walking Move()
            // resolution, not repeated jump+land hops that "ram" up a slope a step at a time
            // (found live during MRM-58 terrain blockout). Basic constant-speed slide per Carlos's
            // call; no friction/acceleration curve yet — flagged for polish, see SlideSpeed's doc
            // comment on MoonlightTunables.
            if (onSteepSlope)
            {
                Vector3 slideDirection = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
                velocity += slideDirection * Tunables.I.SlideSpeed;
            }

            _controller.Move(velocity * Time.deltaTime);

            if (isSprinting)
            {
                OnSprinting?.Invoke(Time.deltaTime);
            }
        }

        // CharacterController.isGrounded is unreliable while stationary — confirmed live during
        // MRM-9 testing, it reads false even at rest on flat ground, which silently blocked
        // jumping whenever the player wasn't moving. A short downward SphereCast replaces it as
        // the authoritative check for jump eligibility and the vertical-velocity reset. It hits
        // any solid collider by default (Tunables.GroundCheckMask) rather than requiring objects
        // to be tagged Ground — widened during MRM-58 once blockout props (location markers, mine
        // obstacles) needed to be standable without per-object layer setup. Narrow GroundCheckMask
        // in the Inspector if a specific layer misbehaves.
        //
        // Uses the NonAlloc/multi-hit overload and skips any hit on the player's own GameObject,
        // rather than excluding the player's layer from the mask — the project has no dedicated
        // Player layer set up (confirmed live during MRM-58: Player sits on Default, the same
        // layer as ordinary level geometry, so layer-based self-exclusion silently zeroed out
        // Default and broke jumping on anything else that shared it). Filtering by identity here
        // is correct regardless of what layer the player or the ground ends up on.
        //
        // groundNormal is reported separately from the grounded bool so UpdateMove can derive the
        // slope angle for its own sliding logic — see SlideSpeed's doc comment on MoonlightTunables.
        private bool CheckGrounded(out Vector3 groundNormal)
        {
            var origin = transform.position + _controller.center + Vector3.down * (_controller.height * 0.5f - _controller.radius);
            int hitCount = Physics.SphereCastNonAlloc(origin, _controller.radius * 0.95f, Vector3.down, _groundHitsBuffer, Tunables.I.GroundCheckDistance, _groundLayerMask, QueryTriggerInteraction.Ignore);

            float closestDistance = float.MaxValue;
            RaycastHit? closestHit = null;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _groundHitsBuffer[i];
                if (candidate.collider.gameObject == gameObject)
                {
                    continue;
                }

                if (candidate.distance < closestDistance)
                {
                    closestDistance = candidate.distance;
                    closestHit = candidate;
                }
            }

            groundNormal = closestHit?.normal ?? Vector3.up;
            return closestHit.HasValue;
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
