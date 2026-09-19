using MrMoonlight.Input;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace MrMoonlight.Player
{
    /// <summary>
    /// The flashlight: press F (D-Pad Up on the gamepad) and a cone of light turns on or off.
    ///
    /// <para><b>No wieldable, no animation, no arm.</b> Carlos, 2026-09-18: <i>"When we activate the
    /// flashlight we don't have any animation or anything. We just have the beam of light... There
    /// will be a model for the lamp in my game and it's going to be fixed on the chest of Tracey's
    /// model."</i> So this deliberately does not use PolymindGames' Flashlight wieldable (which
    /// equips a hand-held tool with a Switch animation). It is just a <see cref="Light"/> that gets
    /// switched.</para>
    ///
    /// <para>Drop it on a Spot Light that is a child of the player camera (or, later, the chest lamp
    /// model). It finds the player through <see cref="MoonlightPlayerRig"/> in a parent, so nothing
    /// here is tied to the Island scene. Range, angle, intensity, cookie and shadows all live on the
    /// <see cref="Light"/> itself while this is a prototype.</para>
    ///
    /// <para><b>Shadows are a GPU cost.</b> The frame is already GPU-bound with shadow-atlas
    /// warnings (MRM-85), so the beam ships with shadows off. Switch them on the Light and re-check
    /// with a build if the beam needs to be occluded by geometry.</para>
    ///
    /// <para>Hooks for later (MRM-44): <see cref="Toggled"/> and <see cref="IsOn"/> are what the
    /// enemy visual-detection change will read, and what a volumetric beam or sway component would
    /// listen to. Nothing in the existing systems calls into this.</para>
    ///
    /// Owner: MRM-44.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Player/Moonlight Flashlight")]
    [RequireComponent(typeof(Light))]
    public sealed class MoonlightFlashlight : MonoBehaviour
    {
        [Tooltip("Whether the beam is lit when the scene starts. The story finds the flashlight already "
                 + "switched on (MRM-44), but the default player starts in the dark.")]
        [SerializeField] private bool startOn;

        [Tooltip("Raised with the new state every time the beam is switched.")]
        [SerializeField] private UnityEvent<bool> toggled = new UnityEvent<bool>();

        private Light _beam;
        private InputAction _toggle;

        /// <summary>True while the beam is lit.</summary>
        public bool IsOn => _beam != null && _beam.enabled;

        /// <summary>Raised with the new state every time the beam is switched.</summary>
        public UnityEvent<bool> Toggled => toggled;

        private void Awake()
        {
            _beam = GetComponent<Light>();
            _beam.enabled = startOn;
        }

        private void Start()
        {
            MoonlightPlayerRig rig = GetComponentInParent<MoonlightPlayerRig>();
            InputMapController input = rig != null ? rig.Input : null;
            if (input == null)
            {
                Debug.LogError("[MRM-44] MoonlightFlashlight found no MoonlightPlayerRig/InputMapController "
                               + "in a parent - the F key does nothing. Put this under the player.", this);
                enabled = false;
                return;
            }

            _toggle = input.Actions.Gameplay.FlashlightToggle;
        }

        private void Update()
        {
            if (_toggle != null && _toggle.WasPerformedThisFrame())
            {
                SetOn(!IsOn);
            }
        }

        /// <summary>Switches the beam. Safe to call from the event director or a story beat.</summary>
        public void SetOn(bool on)
        {
            if (_beam == null || _beam.enabled == on)
            {
                return;
            }

            _beam.enabled = on;
            toggled.Invoke(on);
        }
    }
}
