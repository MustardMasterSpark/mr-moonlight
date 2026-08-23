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

        /// <summary>Speed Tracey slides downhill at while standing on ground steeper than <see cref="SlopeLimit"/>, in metres per second. Not in MRM-9's original tunables list — added during MRM-58 terrain blockout because SlopeLimit alone only blocks CharacterController's walking Move() resolution, not repeated jump+land hops that "ram" up a steep slope a step at a time. Basic constant-speed slide for now; flagged for a friction/acceleration curve as a polish pass. Owner: MRM-9</summary>
        public float SlideSpeed = 4f;

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

        [Header("Player Stats — MRM-12")]

        /// <summary>Health pool ceiling. Health drops from attacks (0 from a punji trap or a Zealot backstab) and is recoverable by items. Owner: MRM-12</summary>
        public float MaxHealth = 100f;

        /// <summary>Stamina pool ceiling. Owner: MRM-12</summary>
        public float MaxStamina = 100f;

        /// <summary>Stamina drain rate while sprinting, in stamina/second, as a function of the fraction of stamina remaining (X: 1 = full, 0 = empty). "Drains on a curve, slower at first, faster as it empties" per MRM-12 — the default eases from a ~10/sec drain near full up to a ~25/sec drain near empty. Owner: MRM-12</summary>
        public AnimationCurve StaminaDrainCurve = AnimationCurve.EaseInOut(0f, 25f, 1f, 10f);

        /// <summary>Flat stamina regeneration rate once regen is active, in stamina/second. Owner: MRM-12</summary>
        public float StaminaRegenRate = 15f;

        /// <summary>Seconds after sprinting stops before stamina regen begins. Owner: MRM-12</summary>
        public float StaminaRegenDelayAfterSprint = 1.5f;

        /// <summary>Flat stamina cost of a single jump, deducted once per <see cref="Player.PlayerController.OnJumped"/>. Owner: MRM-12</summary>
        public float JumpStaminaCost = 8f;

        /// <summary>Flat stamina cost of a single Pickaxe swing. Consumed by the Pickaxe issue via <see cref="Player.PlayerStats.ConsumeSwingStamina"/>; not yet called by anything since the Pickaxe isn't built. Owner: MRM-12</summary>
        public float SwingStaminaCost = 12f;

        /// <summary>Stamina percentage (0-100) at or below which tired-sprint breathing triggers. Owner: MRM-12</summary>
        public float StaminaTiredThreshold = 50f;

        /// <summary>Stamina percentage (0-100) at or below which hyperventilating breathing triggers. Owner: MRM-12</summary>
        public float StaminaHyperventilateThreshold = 20f;

        /// <summary>Melee damage multiplier with no items or statuses applied. Owner: MRM-12</summary>
        public float BaseMeleeMultiplier = 1.0f;

        /// <summary>Defense (damage reduction) multiplier with no items or statuses applied. Owner: MRM-12</summary>
        public float BaseDefenseMultiplier = 1.0f;

        /// <summary>How fast the audibly-applied pitch chases its modifier-stack target, in pitch units/second. Keeps pitch changes smooth, never instant, per MRM-12. Owner: MRM-12</summary>
        public float AudioPitchTransitionSpeed = 2.0f;

        [Header("Death Sequence — MRM-17")]

        /// <summary>How long the fall-and-shake plays while the red tint rises to its ceiling, in seconds. Owner: MRM-17</summary>
        public float DeathFallDuration = 1.2f;

        /// <summary>How long the screen holds at full red tint before the instant cut to black, in seconds. Owner: MRM-17</summary>
        public float DeathHoldBeforeBlackDuration = 0.3f;

        /// <summary>Normalized (0-1 time over DeathFallDuration, 0-1 tint contribution) curve driving the death tint's rise. Owner: MRM-17</summary>
        public AnimationCurve DeathRedTintCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        /// <summary>Amplitude of the death-fall camera shake, in degrees. Owner: MRM-17</summary>
        public float DeathCameraShakeAmplitude = 4f;

        /// <summary>Frequency of the death-fall camera shake's Perlin noise sampling, in Hz. Not in MRM-17's original tunables list - added because the shake needs a rate, not just an amplitude, same reasoning as MRM-9's GroundCheckDistance addition. Owner: MRM-17</summary>
        public float DeathCameraShakeFrequency = 18f;

        /// <summary>How long the death scream keeps playing into the cut-to-black silence before it too is cut, in seconds. Owner: MRM-17</summary>
        public float DeathScreamTailDuration = 1.0f;

        [Header("Screen Red Tint — shared, MRM-17 / MRM-53")]

        /// <summary>Ceiling on the summed red tint from every active <see cref="MrMoonlight.VFX.ScreenTint"/> contributor (the death tint, the health damage tint below), 0-1. Keeps the additive contributors from together blowing past a sane maximum - MRM-17's acceptance criteria call this out explicitly. Owner: MRM-17, shared by MRM-53</summary>
        public float RedTintCeiling = 0.85f;

        /// <summary>Normalized (0-1 = fraction of health lost, 0-1 tint contribution) curve driving the continuous health-damage tint - clear at full health, most visible near zero. This is MRM-53's feature, not MRM-17's; built ahead of schedule on 2026-08-22 at Carlos's request since the shared tint mechanism above already existed. Caps at 0.4, deliberately below RedTintCeiling (0.85) - capping at 1.0 (DeathRedTintCurve's own shape) let the health tint alone saturate the shared ceiling by the time health reached 0, leaving the death tint's own rise with zero visible headroom to add anything (confirmed live: death was invisible, indistinguishable from the pre-existing health tint). MRM-53 should still retune this for its own feel, but must keep some headroom under the ceiling for the death tint to remain a visible escalation. Owner: MRM-53 (implemented during MRM-17)</summary>
        public AnimationCurve HealthRedTintCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0.4f);
    }
}
