using UnityEngine;

namespace MrMoonlight.Data
{
    /// <summary>
    /// The single source of truth for every tunable value in the game. No hardcoded values —
    /// see Docs/csharp-conventions.md. Every field below carries an XML doc comment naming the
    /// issue that owns it.
    ///
    /// Access via <see cref="Tunables.I"/>. Do not drag this asset into per-scene fields —
    /// that lets copies drift out of sync.
    ///
    /// Per-instance override pattern (documented once, here, per MRM-7 — reuse this shape
    /// anywhere a value needs a shared default plus a per-component override, e.g. a per-enemy
    /// vision cone distance):
    /// <code>
    /// [SerializeField] private bool overrideConeDistance = false;
    /// [SerializeField] private float coneDistanceOverride = 0f;
    ///
    /// private float ConeDistance =>
    ///     overrideConeDistance ? coneDistanceOverride : Tunables.I.DefaultConeDistance;
    /// </code>
    /// The tunables value is the default; the component may override it; the inspector shows
    /// both fields so the override is visible, not hidden behind a checkbox elsewhere.
    /// </summary>
    [CreateAssetMenu(menuName = "MrMoonlight/Tunables", fileName = "MoonlightTunables")]
    public sealed class MoonlightTunables : ScriptableObject
    {
        [Header("Player Movement — MRM-9")]

        /// <summary>Walking speed, in metres per second. Owner: MRM-9</summary>
        public float WalkSpeed = 3.0f;

        /// <summary>Sprinting speed, in metres per second. Consumes stamina (hook only for now). Owner: MRM-9</summary>
        public float SprintSpeed = 5.5f;

        /// <summary>Crouched movement speed, in metres per second. Owner: MRM-9</summary>
        public float CrouchSpeed = 1.5f;

        /// <summary>Mouse look sensitivity, in degrees per accumulated pixel of mouse delta. Owner: MRM-9</summary>
        public float LookSpeedMouse = 0.15f;

        /// <summary>Gamepad stick look speed, in degrees per second at full stick deflection. Owner: MRM-9</summary>
        public float LookSpeedStick = 180f;

        /// <summary>How quickly look input ramps toward its target speed, in degrees per second squared. Softens the snap of stick input. Owner: MRM-9</summary>
        public float LookAcceleration = 900f;

        /// <summary>Peak jump height, in metres. Owner: MRM-9</summary>
        public float JumpHeight = 1.2f;

        /// <summary>Initial upward velocity applied on jump, in metres per second. Kept as its own field, separate from <see cref="JumpHeight"/>, so Carlos can tune takeoff feel directly instead of back-solving it from gravity. Owner: MRM-9</summary>
        public float JumpSpeed = 6.0f;

        /// <summary>How much the capsule and camera drop when crouched, in metres. Owner: MRM-9</summary>
        public float CrouchHeightDelta = 0.5f;

        /// <summary>Duration of the crouch/stand transition, in seconds. Owner: MRM-9</summary>
        public float CrouchTransitionDuration = 0.25f;

        /// <summary>Steepest ground angle the player can climb, in degrees. Owner: MRM-9</summary>
        public float SlopeLimit = 45f;

        /// <summary>Downward acceleration applied to the player, in metres per second squared. Owner: MRM-9</summary>
        public float Gravity = -20f;

        /// <summary>How far below the capsule's base the grounded check casts, in metres. Not in MRM-9's original tunables list — added because CharacterController.isGrounded proved unreliable while stationary (confirmed live: it read false while resting motionless on flat ground), which silently blocked jumping whenever the player wasn't moving. See Docs/changelog.md. Owner: MRM-9</summary>
        public float GroundCheckDistance = 0.2f;

        /// <summary>Maximum degrees the camera can pitch downward. Set high enough that Tracey can see her own placeholder body underfoot, not just her arms, per MRM-9's look-down requirement. Not in MRM-9's original tunables list — added because the requirement can't be met without a clamp. Owner: MRM-9</summary>
        public float LookPitchDownMax = 85f;

        /// <summary>Maximum degrees the camera can pitch upward. Not in MRM-9's original tunables list, added alongside <see cref="LookPitchDownMax"/> for the same clamp. Owner: MRM-9</summary>
        public float LookPitchUpMax = 80f;

        [Header("Input System — MRM-8")]

        /// <summary>Minimum stick displacement, as a fraction of full deflection, before gamepad stick input registers. Applied as a runtime override on the Move and Look stick bindings so a worn or drifting stick doesn't creep the player. Owner: MRM-8</summary>
        public float StickDeadzone = 0.125f;

        /// <summary>When true, look-Y input is inverted (stick and mouse alike). Applied as a runtime override on the Look action's invert processor. Owner: MRM-8</summary>
        public bool InvertYAxis = false;

        [Header("Pathfinding — MRM-27")]

        /// <summary>Hard per-frame time budget for time-sliced A*, in milliseconds. WebGL is single-threaded (confirmed live on-device, MRM-6), so pathfinding cannot be offloaded and must yield within this budget. Owner: MRM-27</summary>
        public float PathfindingMillisecondsPerFrame = 2.0f;

        /// <summary>Cap on agents pathing at once. The Spotter flare's worst case spawns 10 reinforcements simultaneously — the number MRM-27 must measure against. Owner: MRM-27</summary>
        public int PathfindingMaxConcurrentAgents = 10;

        /// <summary>Seconds between repaths for a chasing agent. The lever that keeps the frame budget affordable. Owner: MRM-27</summary>
        public float PathfindingRepathInterval = 0.5f;

        [Header("Mine Lighting — MRM-60")]

        /// <summary>Cap on concurrent real-time lights active in the mine. Every Spotter carries a lamp, so a group of them in the mine's confined space is URP's worst case for real-time lighting. Owner: MRM-60</summary>
        public int MineMaxRealtimeLights = 8;
    }
}
