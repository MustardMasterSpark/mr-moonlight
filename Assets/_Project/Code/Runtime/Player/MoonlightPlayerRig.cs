using System;
using MrMoonlight.Data;
using MrMoonlight.Input;
using PolymindGames;
using PolymindGames.InputSystem.Behaviours;
using PolymindGames.MovementSystem;
using UnityEngine;

namespace MrMoonlight.Player
{
    /// <summary>
    /// The one place Mr. Moonlight meets PolymindGames' FPS character (MRM-9).
    ///
    /// <para>Carlos's instruction on the HQ FPS swap was explicit: "We're just gonna have one
    /// character controller... We don't want functionality split in several places related to the
    /// character controller." So the movement, look, crouch, sprint, jump, stamina and weapon
    /// handling all belong to PolymindGames now, and everything Mr. Moonlight needs *from* the
    /// controller comes through this single component. It replaces all four Burntwax bridges
    /// (<c>BurntwaxPlayerBridge</c>, <c>BurntwaxInputBridge</c>, <c>BurntwaxHealthBridge</c>,
    /// <c>BurntwaxStartingLoadout</c>) rather than porting them one-for-one.</para>
    ///
    /// <para><b>Who owns what.</b> PolymindGames' <c>HealthManager</c> and <c>StaminaManager</c> are
    /// the runtime source of truth — Carlos's call, and structurally forced anyway, since the
    /// vendor's stamina, movement-blocking and weapon systems all subscribe to them directly.
    /// <see cref="PlayerStats"/> stays as the game-facing mirror so MRM-12's stat modifiers, MRM-41's
    /// consumables and the HUD keep working unchanged. The mirror is deliberately one-directional
    /// per frame plus an explicit push-back for item effects, rather than a two-way binding that
    /// would fight itself.</para>
    ///
    /// <para><b>Scale conversion.</b> PolymindGames keeps stamina and health normalised 0-1 and
    /// 0-<c>MaxHealth</c> respectively; Mr. Moonlight's stats are 0-100. Everything MRM-41 already
    /// tuned (CrackersStaminaAmount, SodaStaminaAmount, ...) is written against 0-100, so the
    /// conversion happens here and those tunables stay valid.</para>
    ///
    /// Owner: MRM-9.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Player/Moonlight Player Rig")]
    [DefaultExecutionOrder(100)]
    public sealed class MoonlightPlayerRig : MonoBehaviour
    {
        [Header("References — leave empty to resolve from children")]
        [Tooltip("The camera transform the death sequence tips over. Resolved from the character's Head body point when empty.")]
        [SerializeField] private Transform cameraPivot;

        private ICharacter _character;
        private IMovementControllerCC _movement;
        private IStaminaManagerCC _stamina;
        private IHealthManager _health;
        private PlayerStats _stats;

        private FPSMovementInput _movementInput;
        private FPSLookInput _lookInput;
        private FPSWieldablesInput _wieldablesInput;
        private FPSInteractionInput _interactionInput;

        private InputMapController _input;
        private bool _movementLocked;
        private bool _controlDisabled;
        private bool _controlSuspended;
        private bool _pitchResetActive;

        /// <summary>Mr. Moonlight's own action wrapper, kept for the UI and interaction code that
        /// polls it directly (MRM-16, MRM-41). The character controller itself does not read this —
        /// it binds to the same asset through InputActionReferences, so there is exactly one set of
        /// bindings and one place to rebind them.</summary>
        public InputMapController Input => _input;

        /// <summary>Where the death sequence tips the view from (MRM-17).</summary>
        public Transform CameraPivot => cameraPivot;

        /// <summary>Fires once per jump. Kept because MRM-12's stat code and the audio hooks listen
        /// for it; the stamina cost itself is no longer applied here — PolymindGames' StaminaManager
        /// charges it on the Jump state's enter transition.</summary>
        public event Action OnJumped;

        /// <summary>Fires each frame the player is sprinting, with the frame's delta time. Same note
        /// as <see cref="OnJumped"/>: signal only, the drain is the StaminaManager's job.</summary>
        public event Action<float> OnSprinting;

        private void Awake()
        {
            _character = GetComponentInParent<ICharacter>() ?? GetComponent<ICharacter>();
            _stats = GetComponentInChildren<PlayerStats>(true);

            _movementInput = GetComponentInChildren<FPSMovementInput>(true);
            _lookInput = GetComponentInChildren<FPSLookInput>(true);
            _wieldablesInput = GetComponentInChildren<FPSWieldablesInput>(true);
            _interactionInput = GetComponentInChildren<FPSInteractionInput>(true);

            _input = new InputMapController(InputMode.Gameplay);
        }

        private void Start()
        {
            if (_character == null)
            {
                Debug.LogError("[MRM-9] MoonlightPlayerRig found no ICharacter — it must sit on, or under, the PolymindGames player.", this);
                enabled = false;
                return;
            }

            _movement = _character.GetCC<IMovementControllerCC>();
            _stamina = _character.GetCC<IStaminaManagerCC>();
            _health = _character.HealthManager;

            if (cameraPivot == null)
            {
                cameraPivot = _character.GetTransformOfBodyPoint(BodyPoint.Head);
            }

            ApplyStaminaTunables();
            ApplyGamepadLookTunables();
            ApplyCursorState();
            HookSpeedStat();
            HookHealth();
            SyncStatsFromCharacter();

            if (_movement != null)
            {
                _movement.AddStateTransitionListener(MovementStateType.Jump, OnJumpStateEntered);
            }
        }

        private void OnDestroy()
        {
            if (_movement != null)
            {
                _movement.RemoveStateTransitionListener(MovementStateType.Jump, OnJumpStateEntered);
                _movement.SpeedModifier.RemoveModifier(EvaluateSpeedStat);
            }

            if (_health != null)
            {
                _health.DamageReceived -= OnDamageReceived;
                _health.Death -= OnCharacterDeath;
            }

            if (_input != null)
            {
                _input.Dispose();
                _input = null;
            }
        }

        private void Update()
        {
            MirrorPoolsToStats();

            if (_movement != null && _movement.ActiveState == MovementStateType.Run)
            {
                OnSprinting?.Invoke(Time.deltaTime);
            }
        }

        #region Stamina

        /// <summary>
        /// Pushes the two stamina knobs Carlos asked to have exposed into the vendor's per-state
        /// table. The rest of the curve — idle and crouch regen, the 1.35s regen pause, the slide
        /// cost — is left exactly as the asset shipped it, because that is the feel he signed off on
        /// ("I like how it behaves right now, like the ratio at which it recovers").
        /// </summary>
        private void ApplyStaminaTunables()
        {
            var manager = _stamina as StaminaManager;
            if (manager == null)
            {
                return;
            }

            // Jump is a one-off cost on entering the state; sprint is a per-second drain.
            if (!manager.TrySetStateCosts(MovementStateType.Jump, -Mathf.Abs(Tunables.I.JumpStaminaCostNormalised), null))
            {
                Debug.LogWarning("[MRM-9] StaminaManager has no Jump state configured — JumpStaminaCostNormalised had nowhere to go.", this);
            }

            if (!manager.TrySetStateCosts(MovementStateType.Run, null, -Mathf.Abs(Tunables.I.SprintStaminaDrainPerSecond)))
            {
                Debug.LogWarning("[MRM-9] StaminaManager has no Run state configured — SprintStaminaDrainPerSecond had nowhere to go.", this);
            }
        }

        /// <summary>
        /// Hands the gamepad look tuning to <see cref="FPSLookInput"/>. Same reason as
        /// <see cref="ApplyStaminaTunables"/>: the vendor assembly cannot reference Mr. Moonlight's,
        /// so tunables are pushed in from this side rather than read from that one.
        /// </summary>
        private void ApplyGamepadLookTunables()
        {
            if (_lookInput != null)
            {
                _lookInput.SetGamepadLook(Tunables.I.GamepadLookSpeed, Tunables.I.GamepadLookAcceleration);
            }
        }

        #endregion

        #region Speed stat

        /// <summary>
        /// Registers MRM-12's Speed stat as a multiplier on the controller's own speed rather than
        /// overwriting it.
        ///
        /// <para>This is why the Speed stat survived the controller swap. PolymindGames exposes
        /// <c>SpeedModifier</c> as a list of multiplier delegates, so the walk/run/crouch speeds the
        /// asset was tuned with stay authoritative and boots, drugs and weapon weight still scale
        /// them — where the old controller had the stat *replace* the base speed outright.</para>
        /// </summary>
        private void HookSpeedStat()
        {
            if (_movement == null || _stats == null)
            {
                return;
            }

            _movement.SpeedModifier.AddModifier(EvaluateSpeedStat);
        }

        /// <summary>Speed as a ratio against the unmodified walk speed, so an unmodified stat is
        /// exactly 1 and changes nothing.</summary>
        private float EvaluateSpeedStat()
        {
            if (_stats == null || _stats.Speed == null)
            {
                return 1f;
            }

            float baseline = Tunables.I.WalkSpeed;
            if (baseline <= 0.001f)
            {
                return 1f;
            }

            return Mathf.Max(0f, _stats.Speed.Value / baseline);
        }

        #endregion

        #region Health

        private void HookHealth()
        {
            if (_health == null)
            {
                return;
            }

            _health.DamageReceived += OnDamageReceived;
            _health.Death += OnCharacterDeath;
        }

        private void OnDamageReceived(float damage, in DamageArgs args)
        {
            // Nothing to subtract here: HealthManager has already applied it. The mirror in Update
            // carries the new value across to PlayerStats.
        }

        private void OnCharacterDeath(in DamageArgs args)
        {
            if (_stats != null)
            {
                _stats.Health.Deplete(_stats.Health.Value);
            }
        }

        /// <summary>
        /// Applies MRM-12's Defense stat to an incoming hit and hands it to the HealthManager.
        /// Enemy weapons call this rather than the HealthManager directly, so defence is applied in
        /// exactly one place.
        /// </summary>
        public void ApplyIncomingDamage(float rawDamage, in DamageArgs args)
        {
            if (_health == null || rawDamage <= 0f)
            {
                return;
            }

            float defense = _stats != null && _stats.Defense != null ? _stats.Defense.Value : 1f;
            float mitigated = defense > 0.001f ? rawDamage / defense : rawDamage;
            _health.ReceiveDamage(mitigated, args);
        }

        #endregion

        #region Stat mirror

        private void SyncStatsFromCharacter()
        {
            if (_stats == null)
            {
                return;
            }

            MirrorPoolsToStats();
        }

        /// <summary>
        /// Copies the controller's pools into <see cref="PlayerStats"/> each frame so the HUD, the
        /// breathing thresholds and MRM-17's death check all keep reading one number.
        /// <see cref="Stat.BaseValue"/> is written directly rather than through Deplete/Restore -
        /// this is a mirror of an authoritative value, not a gameplay change to it, and the stat's
        /// own modifier stack still layers on top via <see cref="Stat.Value"/>.
        /// </summary>
        private void MirrorPoolsToStats()
        {
            if (_stats == null)
            {
                return;
            }

            // A locked stat is deliberately held at a value by something else - the F-key debug
            // cheats use Stat.Lock for exactly this. Mirroring over a locked stat would silently
            // defeat it, which is what broke infinite stamina after the swap. Locked stats are
            // pushed the other way instead: the lock wins, and the controller is told to match.
            if (_health != null)
            {
                if (_stats.Health.IsLocked)
                {
                    float missing = _stats.Health.Value - _health.Health;
                    if (missing > 0f) _health.RestoreHealth(missing);
                }
                else
                {
                    _stats.Health.BaseValue = Mathf.Clamp(_health.Health, _stats.Health.MinValue, _stats.Health.MaxValue);
                }
            }

            if (_stamina != null)
            {
                float max = Mathf.Max(0.001f, Tunables.I.MaxStamina);
                if (_stats.Stamina.IsLocked)
                {
                    _stamina.Stamina = Mathf.Clamp01(_stats.Stamina.Value / max);
                }
                else
                {
                    float onHundredScale = _stamina.Stamina * max;
                    _stats.Stamina.BaseValue = Mathf.Clamp(onHundredScale, _stats.Stamina.MinValue, _stats.Stamina.MaxValue);
                }
            }
        }

        /// <summary>
        /// Push-back for MRM-41's consumables, which restore stamina on the 0-100 scale. Writing to
        /// <see cref="PlayerStats"/> alone would be overwritten by the next mirror tick, so the
        /// restore has to land on the controller's pool.
        /// </summary>
        public void RestoreStamina(float amountOnHundredScale)
        {
            if (_stamina == null || amountOnHundredScale <= 0f)
            {
                return;
            }

            float max = Mathf.Max(0.001f, Tunables.I.MaxStamina);
            _stamina.Stamina = Mathf.Clamp01(_stamina.Stamina + amountOnHundredScale / max);
        }

        /// <summary>Push-back for MRM-41's healing items, same reasoning as
        /// <see cref="RestoreStamina"/>.</summary>
        public void RestoreHealth(float amount)
        {
            if (_health == null || amount <= 0f)
            {
                return;
            }

            _health.RestoreHealth(amount);
        }

        #endregion

        #region Control surface

        private void OnJumpStateEntered(MovementStateType state)
        {
            OnJumped?.Invoke();
        }

        /// <summary>
        /// Reversible movement lock, used while a full-screen UI is open (MRM-41's inventory). Look
        /// is deliberately left alive — the player can still glance around behind the panel, which
        /// is what the old controller did.
        /// </summary>
        public void SetMovementLocked(bool locked)
        {
            _movementLocked = locked;
            ApplyInputEnabledState();
        }

        /// <summary>
        /// One-way shutdown for the death sequence (MRM-17). Kills movement, look, weapons and
        /// interaction; unlike <see cref="SetMovementLocked"/> nothing turns this back on, because
        /// death is followed by a scene reload.
        /// </summary>
        public void DisableControl()
        {
            _controlDisabled = true;
            ApplyInputEnabledState();
        }

        /// <summary>
        /// Reversible full-stop, used by <see cref="PauseController"/> (MRM-19). Unlike
        /// <see cref="SetMovementLocked"/> this also kills look — the pause menu takes the screen
        /// over entirely rather than sharing it the way the inventory does — and unlike
        /// <see cref="DisableControl"/> it turns back on when the game resumes.
        /// </summary>
        public void SetControlSuspended(bool suspended)
        {
            _controlSuspended = suspended;
            ApplyInputEnabledState();
        }

        private void ApplyInputEnabledState()
        {
            bool everythingElseOn = !_controlDisabled && !_controlSuspended;
            bool movementOn = everythingElseOn && !_movementLocked;

            if (_movementInput != null) _movementInput.enabled = movementOn;
            if (_wieldablesInput != null) _wieldablesInput.enabled = movementOn;
            if (_lookInput != null) _lookInput.enabled = everythingElseOn;
            if (_interactionInput != null) _interactionInput.enabled = movementOn;

            ApplyCursorState();
        }

        /// <summary>
        /// Locks and hides the cursor during gameplay, and releases it whenever control is taken
        /// away (a full-screen UI, or death).
        ///
        /// <para>PolymindGames does this from its <c>GameMode</c> component, which lives on the
        /// vendor's <c>FPS_GameMode</c> prefab — a whole game-flow object Mr. Moonlight does not use
        /// and did not migrate. Without it nothing ever called <c>Cursor.lockState</c>, so in play
        /// mode the pointer stayed free and clicks landed outside the Game view, on the Inspector or
        /// Hierarchy. Owning it here keeps it in the same single seam as the rest of the control
        /// surface rather than pulling in the vendor's game-flow machinery for one line.</para>
        /// </summary>
        private void ApplyCursorState()
        {
            bool gameplay = !_controlDisabled && !_movementLocked && !_controlSuspended;
            Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplay;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Re-assert the lock when the window regains focus: Unity drops it on focus loss, so
            // without this the cursor stays free after alt-tabbing back into the Game view.
            if (hasFocus)
            {
                ApplyCursorState();
            }
        }

        /// <summary>
        /// Levels the view for the game-over screen (MRM-17).
        ///
        /// <para>PolymindGames' look handler has no public setter for the view angles, only a hook
        /// for supplying additive look input. So this installs an additive delegate that feeds back
        /// the negative of the current pitch: it drives pitch to zero and then, being zero itself,
        /// holds it there. Safe because it is only ever called after
        /// <see cref="DisableControl"/>, so it is not fighting live player input.</para>
        /// </summary>
        public void ResetCameraPitch()
        {
            var look = _character != null ? _character.GetCC<ILookHandlerCC>() : null;
            if (look == null || _pitchResetActive)
            {
                return;
            }

            _pitchResetActive = true;
            look.SetAdditiveLookInput(() => new Vector2(-look.ViewAngles.x, 0f));
        }

        #endregion
    }
}
