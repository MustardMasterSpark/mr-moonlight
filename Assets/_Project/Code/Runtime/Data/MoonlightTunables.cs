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

        /// <summary>Steepest ground angle the player can climb, in degrees — fed straight into <see cref="CharacterController.slopeLimit"/>, no custom slide/stick logic on top. Owner: MRM-9</summary>
        public float SlopeLimit = 45f;

        /// <summary>Downward acceleration applied to the player, in metres per second squared. Owner: MRM-9</summary>
        public float Gravity = -20f;

        /// <summary>How far below the capsule's base the grounded check casts, in metres. Not in MRM-9's original tunables list — added because CharacterController.isGrounded proved unreliable while stationary (confirmed live: it read false while resting motionless on flat ground), which silently blocked jumping whenever the player wasn't moving. See Docs/changelog.md. Owner: MRM-9</summary>
        public float GroundCheckDistance = 0.2f;

        /// <summary>Layers the grounded SphereCast can land on. Defaults to Everything — jump/landing works on any solid collider, not just objects tagged <c>Ground</c> (that per-object tagging requirement was dropped during MRM-58 once blockout props like location markers and mine obstacles needed to be standable without manual layer setup). The player's own collider is excluded by identity, not by layer, in <see cref="MrMoonlight.Player.PlayerController"/> — the project has no dedicated Player layer, so a layer-based exclusion would risk zeroing out a layer ordinary level geometry also sits on. Narrow this mask in the Inspector if a specific layer (e.g. a trigger-only volume) turns out to cause false positives. Owner: MRM-9, widened MRM-58</summary>
        public LayerMask GroundCheckMask = ~0;

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

        [Header("World Lighting — Sun — MRM-47")]

        /// <summary>Default seconds the cabin's fast interior dim-out/restore takes, when SunController.SetIndoorDim() runs without a per-instance override. Deliberately separate from the slower per-story-beat dimming TimeManager drives - "step indoors" reads as fast, not a scene-wide fade. Owner: MRM-47</summary>
        public float SunIndoorDimTransitionSeconds = 1.5f;

        [Header("Time Manager — MRM-69")]

        /// <summary>Default seconds a TimeManager preset switch takes when ApplyPreset() is called without an explicit duration. Owner: MRM-69</summary>
        public float TimeManagerDefaultTransitionSeconds = 3f;

        [Header("Main Menu — MRM-18")]

        /// <summary>How long the opening black screen takes to reveal the staged background scenario when the menu first loads, in seconds. Owner: MRM-18</summary>
        public float MenuOpeningFadeDuration = 1.5f;

        /// <summary>Duration of every other main menu fade transition - Settings/Credits opening and closing, Start's fade to black before loading the demo scene, and Quit's fade to black. One shared value keeps every transition feeling consistent, per the issue's "every transition a fade, never a hard cut" requirement. Owner: MRM-18</summary>
        public float MenuTransitionFadeDuration = 0.6f;

        /// <summary>Scroll speed of the credits roll, in pixels per second at the menu's 1920x1080 reference resolution. Owner: MRM-18</summary>
        public float CreditsScrollSpeed = 60f;

        /// <summary>Default Master volume slider value, 0-1 linear, used the first time the game runs before any PlayerPrefs value exists. Owner: MRM-18</summary>
        public float DefaultMasterVolume = 1f;

        /// <summary>Default Voices (character dialogue only) volume slider value, 0-1 linear. Owner: MRM-18</summary>
        public float DefaultVoicesVolume = 1f;

        /// <summary>Default SFX (everything that is not character dialogue) volume slider value, 0-1 linear. Owner: MRM-18</summary>
        public float DefaultSFXVolume = 1f;

        /// <summary>Decibel value written to an AudioMixer group's exposed volume parameter when its slider sits at 0 (fully muted). Mixer volume is logarithmic and linear 0 has no finite dB equivalent, so this is the floor used instead of -infinity. Owner: MRM-18</summary>
        public float MixerMuteDecibels = -80f;

        /// <summary>How long each pre-menu splash card (studio name, then the disclaimer) takes to fade its text in or out, in seconds - the same duration is used for both the fade-in and fade-out of every card. Requested by Carlos on 2026-08-26. Owner: MRM-18</summary>
        public float SplashCardFadeDuration = 1f;

        /// <summary>How long each splash card's text stays fully visible before fading out, in seconds. Owner: MRM-18</summary>
        public float SplashCardHoldDuration = 2f;

        [Header("Interaction — MRM-16")]

        /// <summary>How close Tracey must be to an interactable for it to register at all, in metres. Owner: MRM-16</summary>
        public float InteractionNearbyDistance = 2.5f;

        /// <summary>Maximum angle, in degrees, between the camera's forward direction and an interactable's aim point for it to count as "looked at". Owner: MRM-16</summary>
        public float InteractionAngleTolerance = 15f;

        /// <summary>Layers <see cref="Interaction.InteractionDetector"/>'s proximity query checks. Defaults to Everything, same reasoning as <see cref="GroundCheckMask"/> - confirmed live (see project memory) the "Interactable" layer named in Docs/unity-conventions.md was never actually created, so detection can't depend on it existing. Narrow this once/if that layer gets set up. Owner: MRM-16</summary>
        public LayerMask InteractionLayerMask = ~0;

        /// <summary>How long the prompt takes to fade fully in once an interactable is being looked at, in seconds. Owner: MRM-16</summary>
        public float InteractionPromptFadeInDuration = 0.15f;

        /// <summary>How long the prompt takes to fade fully out once Tracey looks away - "never a dry pop" per the issue. Owner: MRM-16</summary>
        public float InteractionPromptFadeOutDuration = 0.25f;

        /// <summary>Default emission colour for the current interactable's highlight. Per-object override available on <see cref="Interaction.Interactable"/>. Owner: MRM-16</summary>
        public Color InteractionHighlightColor = Color.white;

        /// <summary>Default emission intensity (HDR multiplier) for the current interactable's highlight at full fade-in. Per-object override available on <see cref="Interaction.Interactable"/>. Owner: MRM-16</summary>
        public float InteractionHighlightIntensity = 1.5f;

        [Header("Items — MRM-41")]

        /// <summary>Distinct item types Tracey can carry before the cabin event, per the issue's "counts types, not total items" rule. Owner: MRM-41</summary>
        public int PocketStorageCap = 4;

        /// <summary>Distinct item types Tracey can carry once <see cref="Items.Inventory.UnlockBackpack"/> has run (the cabin event). Owner: MRM-41</summary>
        public int BackpackStorageCap = 10;

        /// <summary>Health restored by eating Crackers. Owner: MRM-41</summary>
        public float CrackersHealAmount = 5f;

        /// <summary>Stamina restored by eating Crackers. Owner: MRM-41</summary>
        public float CrackersStaminaAmount = 10f;

        /// <summary>Health restored by drinking a Soda. Owner: MRM-41</summary>
        public float SodaHealAmount = 5f;

        /// <summary>Stamina restored by drinking a Soda. Owner: MRM-41</summary>
        public float SodaStaminaAmount = 10f;

        /// <summary>Health restored by applying Bandages. Owner: MRM-41</summary>
        public float BandagesHealAmount = 15f;

        /// <summary>Drunkenness added by a Vodka bottle. Owner: MRM-41</summary>
        public float VodkaDrunkennessAmount = 30f;

        /// <summary>Drunkenness added by a Beer can. Owner: MRM-41</summary>
        public float BeerDrunkennessAmount = 15f;

        /// <summary>Weed-high added by a Marijuana blunt. Owner: MRM-41</summary>
        public float WeedHighAmount = 25f;

        /// <summary>Morphine-high added by a Morphine vial. Owner: MRM-41</summary>
        public float MorphineHighAmount = 40f;

        /// <summary>Ceiling for <see cref="Player.PlayerStats.Drunkenness"/>. Not in MRM-41's original tunables list - added because the pool needs a max the same way Health/Stamina do (MRM-12's MaxHealth/MaxStamina precedent). Owner: MRM-41</summary>
        public float MaxDrunkenness = 100f;

        /// <summary>Ceiling for <see cref="Player.PlayerStats.WeedHigh"/>. Owner: MRM-41</summary>
        public float MaxWeedHigh = 100f;

        /// <summary>Ceiling for <see cref="Player.PlayerStats.MorphineHigh"/>. Owner: MRM-41</summary>
        public float MaxMorphineHigh = 100f;

        /// <summary>How long the "storage full" refusal message takes to fade in/out, in seconds (used for both directions). Not in MRM-41's original tunables list - added because the "refused with clear feedback" AC needs a displayed message, not just a returned bool. Owner: MRM-41</summary>
        public float InventoryFullFeedbackFadeDuration = 0.3f;

        /// <summary>How long the "storage full" message holds fully visible before fading out, in seconds. Owner: MRM-41</summary>
        public float InventoryFullFeedbackHoldDuration = 2f;

        [Header("Inventory UI — MRM-42")]

        /// <summary>How long the inventory's open entry animation takes, in seconds. Drives the model half only (MRM-42's actual animation clips are Carlos's handoff - see the issue). Owner: MRM-42</summary>
        public float InventoryOpenAnimationDuration = 0.4f;

        /// <summary>How long the inventory's close/return animation takes, in seconds. Owner: MRM-42</summary>
        public float InventoryCloseAnimationDuration = 0.4f;

        /// <summary>Spin speed of the displayed 3D item, in degrees per second. Owner: MRM-42</summary>
        public float InventoryItemSpinSpeed = 60f;

        /// <summary>Fade duration for the inventory panel itself (distinct from the open/close animation clips), in seconds. Owner: MRM-42</summary>
        public float InventoryFadeDuration = 0.2f;
    }
}
