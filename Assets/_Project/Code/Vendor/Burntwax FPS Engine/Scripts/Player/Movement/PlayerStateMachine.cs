using System;
using System.Collections;
using UnityEngine;

namespace Burntwax
{
    public class PlayerStateMachine : MonoBehaviour
    {

        [Header("Essentials")]
        public string currentStateText;
        public string currentSubStateText;
        public Transform player;
        public Rigidbody rb;
        public CapsuleCollider capsuleCollider;


        [Header("State Machines")]
        public GunStateMachine gunMachine;



        [Header("Movement")]

        [SerializeField] bool disableMovement;
        [SerializeField] float playerVelocity;
        float moveMultiplier;
        [SerializeField] float groundMultiplier = 1f;
        [SerializeField] float airMultiplier = .85f;

        [SerializeField] float walkVelocity = 4f;
        [SerializeField] float sprintVelocity = 6f;
        [SerializeField] float crouchVelocity = 2f;
        [SerializeField] float groundDrag = 10f;
        [SerializeField] float airDrag = 0.5f;
        float horizontalInput;
        float verticalInput;
        [HideInInspector] public Vector3 moveDirection;

        [Header("Crouching")]
        [SerializeField] float crouchYScale = 0.5f;
        float startYScale;

        [Header("Jumping")]
        [SerializeField] float jumpForce;
        [SerializeField] int coyoteTimer;
        [SerializeField] int coyoteTime;


        [Header("Ground Checking")]
        public bool playerIsGrounded = true;
        [HideInInspector] public RaycastHit groundCheckHit = new RaycastHit();

        [Header("Spring Capsule Settings")]
        [SerializeField] float rideHeight = 0f;
        [SerializeField] float normalHeight = 1.2f;
        [SerializeField] float crouchHeight = 0.73f;
        [SerializeField] float slopeHeight = 1.4f;
        [SerializeField] float crouchSlopeHeight = 0.93f;
        [SerializeField] float rideSpringStrength = 50f; // How hard it pushes up
        [SerializeField] float rideSpringDampener = 5f;  // Prevents infinite bouncing
        [SerializeField] float rayDistance = 2.0f;       // Length of the check ray
        // MRM-9 (2026-08-29): sub-radius of capsuleCollider.radius (0.4) for the ground-check
        // SphereCast. See ApplyFloatingForce() and Docs/mrm9-groundcheck-spherecast-fix.md.
        [SerializeField] float groundCheckRadius = 0.3f;
        // MRM-9 (2026-08-30): safety cap for upward velocity, not a normal jump-height limit -
        // jumpForce (8, mass 1) gives ~8 u/s vertical velocity on a clean jump, so 12 leaves
        // headroom for a jump off a slope/ledge while still catching a runaway spike. See
        // SpeedControl() and Docs/mrm9-groundcheck-spherecast-fix.md.
        [SerializeField] float maxUpwardVelocity = 12f;





        [Header("Slope Handling")]
        public bool playerIsSloped;
        public int currentAngle;

        public PlayerBaseState currentState;
        public PlayerStateFactory states;
        // For states
        public PlayerBaseState CurrentState { get { return currentState; } set { currentState = value; } }


        // Movement

        public bool DisableMovement { get { return disableMovement; } set { disableMovement = value; } }
        public float PlayerVelocity { get { return playerVelocity; } set { playerVelocity = value; } }
        public float MoveMultiplier { get { return moveMultiplier; } set { moveMultiplier = value; } }
        public float VerticalInput { get { return verticalInput; } set { verticalInput = value; } }
        public float HorizontalInput { get { return horizontalInput; } set { horizontalInput = value; } }

        public float GroundMultiplier { get { return groundMultiplier; } }
        public float AirMultiplier { get { return airMultiplier; } }

        // Setters added for the MrMoonlight bridge so walk/sprint/crouch speeds come from
        // MoonlightTunables instead of these serialized defaults. See BurntwaxPlayerBridge (MRM-9).
        public float SprintVelocity { get { return sprintVelocity; } set { sprintVelocity = value; } }
        public float WalkVelocity { get { return walkVelocity; } set { walkVelocity = value; } }
        public float GroundDrag { get { return groundDrag; } }
        public float AirDrag { get { return airDrag; } }


        // Spring Capsule
        public float NormalHeight { get { return normalHeight; } }
        public float CrouchHeight { get { return crouchHeight; } }
        public float SlopeHeight { get { return slopeHeight; } }
        public float CrouchSlopeHeight { get { return crouchSlopeHeight; } }
        public float RideHeight { get { return rideHeight; } set { rideHeight = value; } }
        public float RideSpringStrength { get { return rideSpringStrength; } set { rideSpringStrength = value; } }
        public float RideSpringDampener { get { return rideSpringDampener; } set { rideSpringDampener = value; } }
        public float RayDistance { get { return rayDistance; } set { rayDistance = value; } }
        public float GroundCheckRadius { get { return groundCheckRadius; } set { groundCheckRadius = value; } }


        // Crouching

        // MRM-9: crouch squashes the VISUAL BODY, not the whole player.
        // Burntwax scaled its own transform (the root). That was safe in their demo because the
        // camera rig lived loose in the scene, so only the capsule squashed. In the Mr. Moonlight
        // prefab the camera rig, arms viewmodel and HUD canvas are all children of the root, so
        // scaling it squeezed the entire player including the view. Targeting `player` (the Body)
        // reproduces their intent exactly - the capsule mesh and its CapsuleCollider still squash -
        // while leaving the camera, weapon and UI alone.
        public Vector3 LocalScale { get { return player.localScale; } set { player.localScale = value; } }
        public float CrouchVelocity { get { return crouchVelocity; } set { crouchVelocity = value; } }
        public float StartYScale { get { return startYScale; } }
        public float CrouchYScale { get { return crouchYScale; } }


        // Jumping State
        public float JumpForce { get { return jumpForce; } }
        public int CoyoteTime { get { return coyoteTime; } set { coyoteTime = value; } }
        public int CoyoteTimer { get { return coyoteTimer; } }


        private void Awake()
        {
            states = new PlayerStateFactory(this);
            currentState = states.Grounded();
            currentState.EnterState();
            currentState.InitializeSubState();
            rideHeight = normalHeight;
            startYScale = player.localScale.y;

        }



        private void LateUpdate()
        {
            SetRotation();
        }

        private void Update()
        {
            GetMoveInput();
            currentState.UpdateStates();
            currentStateText = currentState.GetType().Name;
            currentSubStateText = currentState.CurrentSubstate().GetType().Name;
            playerIsSloped = PlayerSlopeCheck();
            playerIsGrounded = PlayerGroundCheck();
        }

        private void FixedUpdate()
        {

            ApplyFloatingForce();
            PlayerMove();
            SpeedControl();
        }



        private void GetMoveInput()
        {
            horizontalInput = InputManager.Instance.MoveInput.x;
            verticalInput = InputManager.Instance.MoveInput.y;
        }

        private void SetRotation()
        {
            player.transform.forward = new Vector3(Camera.main.transform.forward.x, 0, Camera.main.transform.forward.z).normalized;
        }

        private void ApplyFloatingForce()
        {
            // MRM-9 (2026-08-29): was Physics.Raycast - a single point sample straight down from
            // rb.position. Fragile at seams between adjacent static colliders (e.g. a vegetation
            // prop's collider meeting the Ground plane): landing exactly on a seam could make
            // groundCheckHit.distance jump sharply between frames, and since springForce below
            // scales that jump by rideSpringStrength with no clamp, one bad sample launched the
            // player ("super jump"). SphereCast bridges small seams/edges the point ray fell into.
            // See Docs/mrm9-groundcheck-spherecast-fix.md.
            if ((playerIsGrounded || playerIsSloped) && Physics.SphereCast(rb.position, groundCheckRadius, Vector3.down, out groundCheckHit, rayDistance))
            {
                Vector3 vel = rb.linearVelocity;
                Vector3 rayDir = Vector3.down;

                Vector3 otherVel = Vector3.zero;
                Rigidbody hitBody = groundCheckHit.rigidbody;
                if (hitBody != null)
                {
                    otherVel = hitBody.linearVelocity;
                }
                float rayDirVel = Vector3.Dot(Vector3.down, vel);
                float otherDirVel = Vector3.Dot(Vector3.down, otherVel);

                float relVel = rayDirVel - otherDirVel;
                float x = groundCheckHit.distance - rideHeight;
                float springForce = (x * rideSpringStrength) - (relVel * rideSpringDampener);
                rb.AddForce(rayDir * springForce);

                if (hitBody != null)
                {
                    hitBody.AddForceAtPosition(rayDir * -springForce, groundCheckHit.point);
                }

            }
        }

        private void PlayerMove()
        {
            if (!disableMovement)
            {
                moveDirection = player.forward * verticalInput + player.right * horizontalInput;

                if (PlayerSlopeCheck())
                {
                    moveDirection = GetSlopeMoveDirection();

                }

                rb.AddForce(moveDirection.normalized * 20f * playerVelocity * moveMultiplier);
            }
            else
            {
                Debug.Log("Locked");
            }
        }

        public void YFlat()
        {

            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

        private bool PlayerGroundCheck()
        {
            // MRM-9 (2026-08-30): was Physics.Raycast(..., rideHeight) - max range EXACTLY equal
            // to the floating-capsule spring's equilibrium target. The spring oscillates around
            // that target every physics step (it's damped, not rigid), so the true distance kept
            // ticking a hair past rideHeight and this ray - whose range stopped exactly there -
            // missed on those frames. That flipped playerIsGrounded false while standing still on
            // flat ground, which flickered PlayerGroundedState/PlayerFallState and groundDrag/
            // airDrag (10/0.5) every few frames, visible live in the Inspector. With airDrag briefly
            // active, ApplyFloatingForce()'s spring force went nearly undamped, which is the more
            // likely source of the "super jump" than the seam theory in the 2026-08-29 SphereCast
            // fix. Widened to SphereCast at rayDistance (2.0, same range ApplyFloatingForce already
            // uses) - matches that method's proven-working sensor range instead of a second,
            // narrower one tied to the spring's own target. See Docs/mrm9-groundcheck-spherecast-fix.md.
            Debug.DrawRay(rb.position, Vector3.down * rayDistance, Color.red);
            if (Physics.SphereCast(rb.position, groundCheckRadius, Vector3.down, out groundCheckHit, rayDistance))
            {
                return true;
            }
            return false;
        }



        private void SpeedControl()
        {

            if (currentState == states.Slope())
            {
                if (rb.linearVelocity.magnitude > playerVelocity)
                {
                    rb.linearVelocity = rb.linearVelocity.normalized * playerVelocity;
                }
            }

            else
            {
                Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                if (flatVel.magnitude > playerVelocity)
                {
                    Vector3 limitedVel = flatVel.normalized * playerVelocity;
                    rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
                }
            }

            // MRM-9 (2026-08-30): only the horizontal speed was ever clamped above - vertical
            // velocity was unbounded. A PhysX depenetration burst (pressing into a wall while
            // continuously adding movement force overlaps the capsule slightly; the solver pushes
            // it back out at up to Physics.defaultMaxDepenetrationVelocity, 10 u/s here, and if the
            // contact normal at a wall/ground seam has any vertical component that push is partly
            // vertical) can hand the rigidbody a large upward velocity outside any of this script's
            // own force math, which is why it wasn't caught by the ground-check fixes above. This
            // clamps that spike down one physics step after it happens instead of letting it carry
            // for the rest of the launch. Only clamps upward - falling is untouched.
            if (rb.linearVelocity.y > maxUpwardVelocity)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, maxUpwardVelocity, rb.linearVelocity.z);
            }

        }

        private bool PlayerSlopeCheck()
        {
            // MRM-9 (2026-08-30): same boundary-range bug as PlayerGroundCheck() above - widened
            // to SphereCast at rayDistance for the same reason.
            if (Physics.SphereCast(rb.position, groundCheckRadius, Vector3.down, out groundCheckHit, rayDistance))
            {
                Vector3 localGroundCheckHitNormal = rb.transform.InverseTransformDirection(groundCheckHit.normal);
                currentAngle = (int)Math.Round(Vector3.Angle(localGroundCheckHitNormal, rb.transform.up));
                if (groundCheckHit.collider == null)
                {
                    currentAngle = 0;
                    return false;
                }

                return Math.Abs(currentAngle) > 0.03f;
            }
            return false;

        }

        private Vector3 GetSlopeMoveDirection()
        {
            return Vector3.ProjectOnPlane(moveDirection, groundCheckHit.normal).normalized;
        }




    }



}