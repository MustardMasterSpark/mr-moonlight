using System;
using MrMoonlight.Data;
using UnityEngine;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Owns Tracey's six core stats - health, stamina, speed, melee damage, defense and audio
    /// pitch - each a <see cref="Stat"/> so later systems (drugs, difficulty, equipped weapon,
    /// boots) can layer modifiers without fighting over direct assignment. See <see cref="Stat"/>
    /// for the stacking rule and MRM-12 for the issue this implements.
    ///
    /// Consumes <see cref="PlayerController.OnJumped"/> and
    /// <see cref="PlayerController.OnSprinting"/> (MRM-9's hooks, built for this issue) to apply
    /// jump and sprint stamina costs. Owner: MRM-12
    ///
    /// <para><b>Speed</b> is unified with movement per Carlos's call: when this component sits
    /// alongside a <see cref="PlayerController"/>, that controller writes its computed base speed
    /// (walk/sprint/crouch) into <see cref="Speed"/>'s <see cref="Stat.BaseValue"/> every frame
    /// and moves at <see cref="Speed"/>'s modifier-stacked <see cref="Stat.Value"/>, so
    /// boots/weapon/substance modifiers attached here actually affect movement. See
    /// <see cref="PlayerController"/>'s class doc comment.</para>
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerStats : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController playerController;

        private float _currentAudioPitch;
        private float _lastSprintTime = float.NegativeInfinity;
        private bool _wasTired;
        private bool _wasHyperventilating;

        public Stat Health { get; private set; }
        public Stat Stamina { get; private set; }
        public Stat Speed { get; private set; }
        public Stat MeleeDamage { get; private set; }
        public Stat Defense { get; private set; }
        public Stat AudioPitch { get; private set; }

        /// <summary>The smoothed pitch other systems should actually apply - chases <see cref="AudioPitch"/>'s modifier-stack target at <see cref="MoonlightTunables.AudioPitchTransitionSpeed"/>, never jumping instantly.</summary>
        public float CurrentAudioPitch => _currentAudioPitch;

        /// <summary>Fires once when stamina crosses at-or-below <see cref="MoonlightTunables.StaminaTiredThreshold"/> from above. Edge-triggered - fires again next time stamina dips below after recovering. Empty for now; Carlos wires the tired-sprint breathing sound pool. Owner: MRM-12</summary>
        public event Action OnStaminaTired;

        /// <summary>Fires once when stamina crosses at-or-below <see cref="MoonlightTunables.StaminaHyperventilateThreshold"/> from above. Edge-triggered, same as <see cref="OnStaminaTired"/>. Empty for now; Carlos wires the hyperventilating breathing sound pool. Owner: MRM-12</summary>
        public event Action OnStaminaHyperventilating;

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            Health = new Stat(Tunables.I.MaxHealth, 0f, Tunables.I.MaxHealth);
            Stamina = new Stat(Tunables.I.MaxStamina, 0f, Tunables.I.MaxStamina);
            Speed = new Stat(Tunables.I.WalkSpeed, 0f, float.MaxValue);
            MeleeDamage = new Stat(Tunables.I.BaseMeleeMultiplier, 0f, float.MaxValue);
            Defense = new Stat(Tunables.I.BaseDefenseMultiplier, 0f, float.MaxValue);
            // 1.0 is audio pitch's neutral/unmodified value - AudioSource.pitch's own default.
            AudioPitch = new Stat(1.0f, 0f, float.MaxValue);

            _currentAudioPitch = AudioPitch.Value;
        }

        private void OnEnable()
        {
            playerController.OnJumped += HandleJumped;
            playerController.OnSprinting += HandleSprinting;
        }

        private void OnDisable()
        {
            playerController.OnJumped -= HandleJumped;
            playerController.OnSprinting -= HandleSprinting;
        }

        private void Update()
        {
            UpdateStaminaRegen();
            UpdateBreathingThresholds();
            UpdateAudioPitch();
        }

        /// <summary>Deducts <see cref="MoonlightTunables.SwingStaminaCost"/>. Called by the Pickaxe issue on each swing; nothing calls this yet since the Pickaxe isn't built. Owner: MRM-12</summary>
        public void ConsumeSwingStamina()
        {
            if (!Stamina.IsLocked)
            {
                Stamina.Deplete(Tunables.I.SwingStaminaCost);
            }
        }

        private void HandleJumped()
        {
            if (!Stamina.IsLocked)
            {
                Stamina.Deplete(Tunables.I.JumpStaminaCost);
            }
        }

        private void HandleSprinting(float deltaTime)
        {
            _lastSprintTime = Time.time;

            if (Stamina.IsLocked)
            {
                return;
            }

            float staminaFraction = Stamina.MaxValue > 0f ? Stamina.Value / Stamina.MaxValue : 0f;
            float drainRate = Tunables.I.StaminaDrainCurve.Evaluate(staminaFraction);
            Stamina.Deplete(drainRate * deltaTime);
        }

        private void UpdateStaminaRegen()
        {
            if (Stamina.IsLocked)
            {
                return;
            }

            bool regenDelayElapsed = Time.time - _lastSprintTime >= Tunables.I.StaminaRegenDelayAfterSprint;
            if (regenDelayElapsed)
            {
                Stamina.Restore(Tunables.I.StaminaRegenRate * Time.deltaTime);
            }
        }

        private void UpdateBreathingThresholds()
        {
            float staminaPercent = Stamina.MaxValue > 0f ? Stamina.Value / Stamina.MaxValue * 100f : 0f;

            bool isHyperventilating = staminaPercent <= Tunables.I.StaminaHyperventilateThreshold;
            if (isHyperventilating && !_wasHyperventilating)
            {
                OnStaminaHyperventilating?.Invoke();
            }
            _wasHyperventilating = isHyperventilating;

            bool isTired = staminaPercent <= Tunables.I.StaminaTiredThreshold;
            if (isTired && !_wasTired)
            {
                OnStaminaTired?.Invoke();
            }
            _wasTired = isTired;
        }

        private void UpdateAudioPitch()
        {
            _currentAudioPitch = Mathf.MoveTowards(_currentAudioPitch, AudioPitch.Value, Tunables.I.AudioPitchTransitionSpeed * Time.deltaTime);
        }
    }
}
