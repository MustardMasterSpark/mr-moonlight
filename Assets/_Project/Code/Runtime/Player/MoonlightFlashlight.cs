using MrMoonlight.Data;
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
    /// here is tied to the Island scene. Range, angle, intensity, colour, shadows and the cookie
    /// on/off switch are <c>Flashlight*</c> fields in <see cref="MoonlightTunables"/>, applied to the
    /// <see cref="Light"/> at startup. For tuning, tick <c>Live tuning</c> in Play Mode and edit
    /// the fields on this component; they are re-seeded from the tunables on every scene start,
    /// so the sweet spot has to be copied back into the tunables by hand.</para>
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

        [Header("Live tuning (Play Mode)")]
        [Tooltip("Tick this in Play Mode, then edit the values below and watch the beam change. Un-ticked, "
                 + "the beam follows MoonlightTunables. The values below are copied FROM the tunables every "
                 + "time the scene starts, so anything typed here is scratch: Play Mode edits are discarded "
                 + "on stop. Tell Claude the values you like and they get saved as the new defaults.")]
        [SerializeField] private bool liveTuning;

        [Tooltip("Brightness. Tunable: FlashlightIntensity.")]
        [SerializeField, Min(0f)] private float intensity;

        [Tooltip("Reach in metres. Tunable: FlashlightRange.")]
        [SerializeField, Min(0.1f)] private float range;

        [Tooltip("Full cone width in degrees. Tunable: FlashlightOuterSpotAngle.")]
        [SerializeField, Range(1f, 179f)] private float outerSpotAngle;

        [Tooltip("Full-strength core in degrees; fades to nothing at the outer angle. Tunable: "
                 + "FlashlightInnerSpotAngle. Held at or below the outer angle.")]
        [SerializeField, Range(0f, 179f)] private float innerSpotAngle;

        [Tooltip("Beam colour. Tunable: FlashlightColor.")]
        [SerializeField] private Color color = Color.white;

        [Tooltip("None is free. Hard/Soft cost GPU time. Tunable: FlashlightShadows.")]
        [SerializeField] private LightShadows shadows;

        [Tooltip("Shadow darkness, 0-1. Tunable: FlashlightShadowStrength.")]
        [SerializeField, Range(0f, 1f)] private float shadowStrength;

        [Tooltip("Shape the beam with its dark-centred cookie ring, or leave it a plain cone. "
                 + "Tunable: FlashlightUseCookie.")]
        [SerializeField] private bool useCookie;

        private Light _beam;
        private Texture _cookie;
        private InputAction _toggle;

        /// <summary>True while the beam is lit.</summary>
        public bool IsOn => _beam != null && _beam.enabled;

        /// <summary>Raised with the new state every time the beam is switched.</summary>
        public UnityEvent<bool> Toggled => toggled;

        private void Awake()
        {
            _beam = GetComponent<Light>();
            _beam.enabled = startOn;

            // The cookie is authored on the Light; remember it so "use cookie" can be switched off and on.
            _cookie = _beam.cookie;

            LoadFromTunables();
            Apply();
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
            if (liveTuning)
            {
                Apply();
            }

            if (_toggle != null && _toggle.WasPerformedThisFrame())
            {
                SetOn(!IsOn);
            }
        }

        /// <summary>Re-reads the tunables and applies them, e.g. after editing the asset during Play Mode.</summary>
        [ContextMenu("Reload values from MoonlightTunables")]
        private void ReloadFromTunables()
        {
            LoadFromTunables();
            if (_beam != null)
            {
                Apply();
            }
        }

        /// <summary>Copies every beam value from <see cref="Tunables"/> into the inspector fields.</summary>
        private void LoadFromTunables()
        {
            MoonlightTunables t = Tunables.I;
            intensity = t.FlashlightIntensity;
            range = t.FlashlightRange;
            outerSpotAngle = t.FlashlightOuterSpotAngle;
            innerSpotAngle = t.FlashlightInnerSpotAngle;
            color = t.FlashlightColor;
            shadows = t.FlashlightShadows;
            shadowStrength = t.FlashlightShadowStrength;
            useCookie = t.FlashlightUseCookie;
        }

        /// <summary>Pushes the inspector fields onto the <see cref="Light"/>.</summary>
        private void Apply()
        {
            _beam.intensity = intensity;
            _beam.range = range;
            _beam.spotAngle = outerSpotAngle;
            _beam.innerSpotAngle = Mathf.Min(innerSpotAngle, outerSpotAngle);
            _beam.color = color;
            _beam.shadows = shadows;
            _beam.shadowStrength = shadowStrength;

            Texture wantedCookie = useCookie ? _cookie : null;
            if (_beam.cookie != wantedCookie)
            {
                _beam.cookie = wantedCookie;
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
