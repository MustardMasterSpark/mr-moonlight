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

        /// <summary>Field of view for the hip-fire camera. The Burntwax engine expresses aim-down-sights as a blend between two Cinemachine cameras rather than a single lerped FOV, so both ends live here. Owner: MRM-9</summary>
        public float CameraFovDefault = 60f;

        /// <summary>Field of view for the aim-down-sights camera. Lower than <see cref="CameraFovDefault"/>; the difference is the zoom amount. Owner: MRM-9</summary>
        public float CameraFovAimDownSights = 40f;

        /// <summary>Seconds the Cinemachine blend takes between hip-fire and aim-down-sights. Owner: MRM-9</summary>
        public float CameraAimBlendDuration = 0.15f;

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

        [Header("Enemies — shared, MRM-29 / MRM-34")]

        /// <summary>Health fraction at or below which <see cref="Enemies.EnemyHealth"/> raises its one-shot low-health event. Shared by every enemy; the Spotter uses it for its panic reinforcement call, which is a separate trigger from the flare (see <see cref="SpotterFlareTimer"/>). Owner: MRM-34</summary>
        public float EnemyLowHealthThreshold = 0.35f;

        /// <summary>How fast an enemy bullet tracer travels along its already-resolved path, in metres per second. The shot itself is hitscan — this only controls how fast the visible streak crosses the gap, so the player can read the firing direction. Mirrors the Burntwax player-gun trail pattern (<c>GunScriptableObject.PlayTrail</c>). Owner: MRM-34</summary>
        public float EnemyTracerSpeed = 140f;

        /// <summary>How long an enemy tracer streak lingers after arriving, in seconds. Owner: MRM-34</summary>
        public float EnemyTracerDuration = 0.12f;

        /// <summary>Width of an enemy tracer streak, in metres. Birdshot is meant to read as a spray of thin lines, not one fat beam. Owner: MRM-34</summary>
        public float EnemyTracerWidth = 0.035f;

        /// <summary>Seconds a detached death drop (lamp, shotgun) survives before it is cleaned up. Zero means it is never cleaned up. Owner: MRM-34</summary>
        public float EnemyDropLifetime = 0f;

        /// <summary>Impulse applied to a detached death drop so it falls and rolls rather than dropping straight down. Owner: MRM-34</summary>
        public float EnemyDropScatterImpulse = 1.5f;

        [Header("Combat — hitbox zones & damage variance, MRM-76")]

        /// <summary>
        /// Damage multiplier for a limb hit (arms/legs) — the baseline, 1.0. Carlos's design target
        /// (2026-09-02): four to five pistol shots to kill a Spotter through a limb. Owner: MRM-76
        /// </summary>
        public float EnemyHitboxLimbMultiplier = 1.0f;

        /// <summary>Damage multiplier for a torso hit. Target: two to three pistol shots. Owner: MRM-76</summary>
        public float EnemyHitboxTorsoMultiplier = 2.0f;

        /// <summary>
        /// Damage multiplier for a head hit. MRM-32 documents ×3, but at the pistol's tuned damage
        /// range (24-33) that reliably takes two hits, not one — Carlos's explicit call 2026-09-02
        /// was to deviate from that spec number so headshots read as "almost always one shot,
        /// occasionally two". At 4.0, only the single lowest damage roll (24) survives a headshot;
        /// every roll of 25 or above (9 of the 10 possible integer values) kills in one. Owner:
        /// MRM-76, deliberately off MRM-32's ×3
        /// </summary>
        public float EnemyHitboxHeadMultiplier = 4.0f;

        /// <summary>
        /// How far below an enemy's head bone (in metres, world-space) the head hitbox zone starts.
        /// <see cref="Enemies.EnemyHitbox"/> classifies a hit by comparing its height against the
        /// Animator's own Head and Hips bones, cached once at Awake, so this needs no per-species
        /// tuning as long as the rig is Humanoid. Owner: MRM-76
        /// </summary>
        public float EnemyHitboxHeadBandMargin = 0.15f;

        /// <summary>Lower bound of the per-shot damage randomiser, as a multiplier (0.8 = -20%). Carlos's ask: shots should vary "so we are a little bit more fair" rather than always landing the exact same number. Applies to the player's pistol (via each gun's DamageConfig, set to TwoConstants mode) and to enemy firearms (<see cref="EnemyDamageVarianceMultiplierMin"/> mirrors this for the enemy side since Burntwax's DamageConfig isn't ours to share). Owner: MRM-76</summary>
        public float CombatDamageVarianceMultiplierMin = 0.80f;

        /// <summary>Upper bound of the per-shot damage randomiser, as a multiplier (1.1 = +10%). Owner: MRM-76</summary>
        public float CombatDamageVarianceMultiplierMax = 1.10f;

        /// <summary>Lower bound of an enemy shot's damage randomiser. Same idea as <see cref="CombatDamageVarianceMultiplierMin"/>, kept as its own field because <see cref="Enemies.EnemyFirearm"/> rolls it once per shot and applies it to every pellet, not once per pellet — otherwise seven independent rolls average out and the shot-to-shot variance disappears. Owner: MRM-76</summary>
        public float EnemyDamageVarianceMultiplierMin = 0.80f;

        /// <summary>Upper bound of an enemy shot's damage randomiser. Owner: MRM-76</summary>
        public float EnemyDamageVarianceMultiplierMax = 1.10f;

        [Header("Enemy — Spotter, MRM-34")]

        /// <summary>The Spotter's maximum health. "Medium health" in MRM-34's scope — sturdier than a Zealot, well under the Furman. Owner: MRM-34</summary>
        public float SpotterMaxHealth = 100f;

        /// <summary>Shots fired before the Spotter locks into its reload state. MRM-34 specifies a double-barrel, so two. Owner: MRM-34</summary>
        public int SpotterShotsBeforeReload = 2;

        /// <summary>Probability (0-1) that a given shot is deliberately thrown wide. MRM-34 asks for 30%. This is a <i>deliberate</i> miss — the shot still fires and still draws tracers, it is just aimed off-target by <see cref="SpotterMissAngle"/>, so the player reads it as being shot at rather than as the enemy failing to notice them. Owner: MRM-34</summary>
        public float SpotterMissChance = 0.30f;

        /// <summary>How far off-target a deliberate miss is aimed, in degrees. Owner: MRM-34</summary>
        public float SpotterMissAngle = 9f;

        /// <summary>Seconds the Spotter holds his aim before the first shot of a burst. This is the tell that gives the player time to break line of sight. Lowered from 0.65, then 0.45 (Carlos, 2026-09-02 — MRM-76: still takes too long to start firing). Owner: MRM-34</summary>
        public float SpotterAimDelay = 0.35f;

        /// <summary>Seconds between the two barrels. Lowered from 0.85 (Carlos, 2026-09-02 — MRM-76). Owner: MRM-34</summary>
        public float SpotterInterShotDelay = 0.55f;

        /// <summary>Seconds the Spotter is locked in his reload state after emptying both barrels. This is the player's window to close distance or reposition. Lowered from 2.6 (Carlos, 2026-09-02 — MRM-76: a shorter window keeps the fight faster without removing it). Owner: MRM-34</summary>
        public float SpotterReloadDuration = 1.8f;

        /// <summary>Distance the Spotter tries to hold from the player while shooting, in metres. Fed into Blaze's attack-state <c>distanceFromEnemy</c>. Owner: MRM-34</summary>
        public float SpotterEngagementDistance = 12f;

        /// <summary>Damage a single birdshot pellet deals inside <see cref="SpotterDamageFalloffStart"/>. A full-hit shot is this times <see cref="SpotterPelletCount"/>. Owner: MRM-34</summary>
        public float SpotterPelletDamage = 4.5f;

        /// <summary>Pellets per shot. Owner: MRM-34</summary>
        public int SpotterPelletCount = 7;

        /// <summary>Half-angle of the birdshot cone, in degrees. Owner: MRM-34</summary>
        public float SpotterSpreadAngle = 4.5f;

        /// <summary>Maximum range of a Spotter shot, in metres. Beyond this a pellet does nothing. Owner: MRM-34</summary>
        public float SpotterShotRange = 45f;

        /// <summary>Distance, in metres, at which pellet damage starts falling off. It reaches zero at <see cref="SpotterShotRange"/>. Owner: MRM-34</summary>
        public float SpotterDamageFalloffStart = 15f;

        /// <summary>Radius of the "am I the only Spotter here?" sphere, in metres. If another Spotter is inside it, this one does not flare. Owner: MRM-34</summary>
        public float SpotterAloneCheckRadius = 25f;

        /// <summary>Seconds a lone, engaged Spotter must survive before he fires the flare. Owner: MRM-34</summary>
        public float SpotterFlareTimer = 8f;

        /// <summary>Seconds the flare animation and its recovery occupy before normal behaviour resumes. Owner: MRM-34</summary>
        public float SpotterFlareAnimationDuration = 2.2f;

        /// <summary>Fewest reinforcements a flare summons. Owner: MRM-34</summary>
        public int SpotterReinforcementMin = 3;

        /// <summary>Most reinforcements a flare summons. MRM-34 calls 10 simultaneous Spotters the game's worst case — measure the frame cost against MRM-64 before raising it. Owner: MRM-34</summary>
        public int SpotterReinforcementMax = 10;

        /// <summary>
        /// Nearest a reinforcement is allowed to spawn from the flaring Spotter, in metres. Added
        /// 2026-09-02 (MRM-76): Carlos's report was that reinforcements popped in right next to the
        /// player — "comical, like a little devil spawning other little devils." Combined with
        /// <see cref="SpotterReinforcementScatterRadius"/> (now the outer bound, not the only bound),
        /// <see cref="Enemies.EnemyReinforcementSpawner"/> samples an annulus between the two so every
        /// reinforcement lands out past the player's usual sightline and has to run in. Owner: MRM-76
        /// </summary>
        public float SpotterReinforcementMinDistance = 45f;

        /// <summary>Farthest a reinforcement may spawn from the flaring Spotter, in metres. Raised from 20 to 55 (Carlos, 2026-09-02 — MRM-76) alongside <see cref="SpotterReinforcementMinDistance"/> so the whole spawn band sits well outside the player's visual range instead of landing on top of them. Owner: MRM-34</summary>
        public float SpotterReinforcementScatterRadius = 55f;

        /// <summary>Minimum spacing between two reinforcement spawn points, in metres. Owner: MRM-34</summary>
        public float SpotterReinforcementMinSpacing = 2.5f;

        /// <summary>Fewest reinforcements the <i>panic</i> call summons when a Spotter drops below <see cref="EnemyLowHealthThreshold"/>. Deliberately a separate, smaller trigger from the flare (Carlos, 2026-09-01) — the flare is proactive and fires when isolated, this one is reactive and fires when hurt. Owner: MRM-34</summary>
        public int SpotterPanicReinforcementMin = 1;

        /// <summary>Most reinforcements the panic call summons. Owner: MRM-34</summary>
        public int SpotterPanicReinforcementMax = 3;

        /// <summary>Intensity of the Spotter's hand lamp. A real Light, never an emission map (standing project rule) — it is what makes him readable at range, so it is gameplay, not decoration. Owner: MRM-34</summary>
        public float SpotterLampIntensity = 2.5f;

        /// <summary>Range of the Spotter's hand lamp, in metres. Owner: MRM-34</summary>
        public float SpotterLampRange = 14f;

        /// <summary>Peak angle the lamp tilts to on its local X axis at full sway, in degrees — the "hanging and swinging" read. Owner: MRM-34 (2026-09-03, lamp moved from hand to hip socket)</summary>
        public float SpotterLampSwayMaxAngle = 12f;

        /// <summary>Full swing cycles per second at full sway. Owner: MRM-34</summary>
        public float SpotterLampSwayFrequency = 1.6f;

        /// <summary>Spotter speed, in m/s, at which the lamp reaches full sway angle and frequency; scales down proportionally below it. Matches the Spotter's own NavMeshAgent speed (3) so a normal walk is already full sway. Owner: MRM-34</summary>
        public float SpotterLampSwayReferenceSpeed = 3f;

        [Header("Flare VFX — MRM-34 (reused by MRM-57)")]

        /// <summary>
        /// How high the flare climbs before gravity turns it around, in metres. Replaces the old
        /// launch-speed/launch-pitch pair (45° then 78°, Carlos, 2026-09-02 — MRM-76: still read as
        /// "shot sideways"). The launch is now a real 90°-from-ground cheat — see
        /// <see cref="VFX.FlareProjectile.Launch"/> — so this height is the only thing that decides
        /// how strong the vertical shove is; <see cref="FlareGravityScale"/> decides how long it
        /// takes to get there and fall back. Owner: MRM-76
        /// </summary>
        public float FlareApexHeight = 50f;

        /// <summary>
        /// Small horizontal speed, in metres per second, added on top of the vertical launch — the
        /// only thing that curves the flare at all now that the launch itself is purely vertical.
        /// Deliberately tiny next to the vertical speed so the shot still reads as "straight up" at
        /// the muzzle; the drift only becomes visually obvious near the apex, once the vertical
        /// speed has bled off — which is exactly the "goes up, hangs, then curves forward like a
        /// mortar" read Carlos asked for. Owner: MRM-76
        /// </summary>
        public float FlareForwardDrift = 3f;

        /// <summary>Gravity multiplier applied to the flare in flight. Below 1 so it hangs in the air and reads as a signal rather than a mortar round. Owner: MRM-34</summary>
        public float FlareGravityScale = 0.45f;

        /// <summary>Seconds the flare burns at full brightness before it starts dying. Owner: MRM-34</summary>
        public float FlareBurnDuration = 11f;

        /// <summary>Seconds the flare takes to fade out once its burn time is up. Owner: MRM-34</summary>
        public float FlareFadeDuration = 2.5f;

        /// <summary>Lower bound of the flare light's flicker, in intensity units. Raised from 4, then 5 (Carlos, 2026-09-02 — MRM-76: more illumination). Owner: MRM-34</summary>
        public float FlareLightIntensityMin = 7f;

        /// <summary>Upper bound of the flare light's flicker. Raised from 9, then 11 (Carlos, 2026-09-02 — MRM-76: more illumination). Owner: MRM-34</summary>
        public float FlareLightIntensityMax = 14f;

        /// <summary>How many times per second the flare light re-randomises its intensity. Per-frame randomisation reads as noise; this slower rate reads as a burning chemical flame. Owner: MRM-34</summary>
        public float FlareFlickerFrequency = 14f;

        /// <summary>Range of the flare light, in metres. Raised from 20 (Carlos, 2026-09-02 — MRM-76: more illumination). Owner: MRM-34</summary>
        public float FlareLightRange = 26f;

        [Header("Dropped lamp fire — MRM-76")]

        /// <summary>Linear speed, in m/s, below which a dropped lamp counts as "settled" for <see cref="Enemies.LampFireEffect"/>. Owner: MRM-76</summary>
        public float LampFireSettleLinearThreshold = 0.15f;

        /// <summary>Angular speed, in rad/s, below which a dropped lamp counts as "settled". Owner: MRM-76</summary>
        public float LampFireSettleAngularThreshold = 0.3f;

        /// <summary>Seconds the lamp must stay under both settle thresholds before the fire ignites — a short grace period so a lamp that's merely paused mid-roll doesn't light early. Owner: MRM-76</summary>
        public float LampFireSettleGraceDuration = 0.4f;

        /// <summary>Fire VFX size as a multiplier of the lamp's own largest rendered dimension. Carlos's ask: size it relative to the lamp, not a fixed number. Owner: MRM-76</summary>
        public float LampFireVfxScaleFactor = 2.5f;

        /// <summary>Seconds the fire burns at full strength once it ignites. Lowered from 40 (Carlos, 2026-09-02 — read as too long). Owner: MRM-76</summary>
        public float LampFireBurnDuration = 15f;

        /// <summary>Seconds the fire VFX (particles, its own flicker light, audio) takes to die out once <see cref="LampFireBurnDuration"/> elapses. Raised from 2 (Carlos, 2026-09-02). Owner: MRM-76</summary>
        public float LampFireVfxFadeDuration = 2.5f;

        /// <summary>Seconds the lamp's own gameplay <c>Light</c> takes to dim to zero, starting at the same moment as the VFX fade. Deliberately longer than <see cref="LampFireVfxFadeDuration"/> — Carlos's ask keeps the lamp glowing a beat after the flame itself dies down. Owner: MRM-76</summary>
        public float LampLightFadeDuration = 5f;

        [Header("Event Director — MRM-11 (2026-09-02)")]

        /// <summary>
        /// Seconds a <c>wait</c> on a <i>world</i> condition (a zone, a signal, another sequence)
        /// gives up after, logs an error, and carries on. Deadlock safety: a mis-typed zone name or
        /// a volume the player squeezed past would otherwise leave the level stuck forever with
        /// nothing on screen to say why. Waits on <i>player progress</i> (an objective, a kill
        /// count, a spawned group) ignore this and wait forever on purpose — timing those out would
        /// hand out a win nobody earned. Any line can override with <c>timeout=</c>. Owner: MRM-11
        /// </summary>
        public float EventWaitDefaultTimeout = 180f;

        /// <summary>Attempts an <see cref="Events.EnemySpawnPoint"/> makes to find a valid, well-spaced NavMesh point per enemy before giving up on that one. Raise it only if scripted waves come up short on cluttered terrain. Owner: MRM-11</summary>
        public int EventSpawnPlacementAttempts = 12;

        /// <summary>Seconds the end-of-level screen takes to fade to black before Restart or Main Menu loads. Owner: MRM-11</summary>
        public float EndScreenFadeDuration = 0.5f;

        [Header("System messages + objectives — MRM-14 (seeded by MRM-11)")]

        /// <summary>Seconds a <c>message</c> line stays on screen when it does not say <c>for=</c>. Owner: MRM-14</summary>
        public float SystemMessageDefaultDuration = 5f;

        /// <summary>Seconds an objective announcement stays on screen. Longer than a plain message — it is the thing the player has to remember. Owner: MRM-14</summary>
        public float ObjectiveAnnounceDuration = 6f;

        /// <summary>Seconds the centre-bottom message fades in and out over. Runs on unscaled time so a pause does not freeze it mid-fade. Owner: MRM-14</summary>
        public float SystemMessageFadeDuration = 0.4f;

        /// <summary>Point size of the centre-bottom message text at the 1920×1080 reference resolution. Owner: MRM-14</summary>
        public float SystemMessageFontSize = 34f;

        /// <summary>Colour of the centre-bottom message text. MRM-14 calls for blue; this is the pale, slightly cold blue that survives the CRT filter without glowing. Owner: MRM-14</summary>
        public Color SystemMessageColor = new Color(0.72f, 0.85f, 1f, 1f);
    }
}
