using UnityEngine;

namespace MrMoonlight.World
{
    /// <summary>
    /// Gentle rotational sway to sell a feather resting on water (MRM-18 main menu ask). Two
    /// sine waves at slightly different speeds/phases drive pitch and roll so the motion reads
    /// as an idle drift rather than a metronome.
    ///
    /// If a <see cref="FeatherFall"/> is present on this object (the falling Sparrow feather),
    /// wobbling is suppressed while <see cref="FeatherFall.IsFalling"/> is true and only starts
    /// once it lands - the base pose for the wobble is captured at that moment, whatever
    /// orientation the fall happened to settle on. An object with no <see cref="FeatherFall"/>
    /// (the static Eagle feather, already resting on water from the start) wobbles immediately.
    /// </summary>
    public sealed class FeatherWaterWobble : MonoBehaviour
    {
        [Tooltip("Optional. If set (or found on this object), wobbling waits until this feather lands instead of starting immediately.")]
        [SerializeField] private FeatherFall feather;

        [Header("Wobble")]
        [Tooltip("Max tilt around the local X axis (pitch), in degrees.")]
        [Range(0f, 15f)]
        [SerializeField] private float pitchAmplitude = 4f;

        [Tooltip("Max tilt around the local Z axis (roll), in degrees.")]
        [Range(0f, 15f)]
        [SerializeField] private float rollAmplitude = 3f;

        [Tooltip("How fast the wobble cycles. Low values = slow, gentle drift.")]
        [Range(0.05f, 3f)]
        [SerializeField] private float wobbleSpeed = 0.6f;

        [Tooltip("Offset each instance's wobble by a random phase so multiple feathers don't sway in lockstep.")]
        [SerializeField] private bool randomizePhase = true;

        private Quaternion _baseLocalRotation;
        private float _phaseOffset;
        private bool _active;
        private bool _wasFalling;

        private void Awake()
        {
            if (feather == null) feather = GetComponent<FeatherFall>();

            _phaseOffset = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;

            _wasFalling = feather != null && feather.IsFalling;
            _active = !_wasFalling;

            if (_active) _baseLocalRotation = transform.localRotation;
        }

        private void Update()
        {
            if (feather != null)
            {
                bool falling = feather.IsFalling;

                if (_wasFalling && !falling)
                {
                    // Just landed - wobble from wherever the fall settled, not the pre-fall pose.
                    _baseLocalRotation = transform.localRotation;
                    _active = true;
                }
                else if (!_wasFalling && falling)
                {
                    // Fell again (loop) - let FeatherFall own rotation until it lands again.
                    _active = false;
                }

                _wasFalling = falling;
            }

            if (!_active) return;

            float t = Time.time * wobbleSpeed + _phaseOffset;
            float pitch = Mathf.Sin(t) * pitchAmplitude;
            float roll = Mathf.Sin(t * 0.77f + 1.3f) * rollAmplitude;

            transform.localRotation = _baseLocalRotation * Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
