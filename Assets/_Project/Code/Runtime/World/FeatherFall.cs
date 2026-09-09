using System;
using UnityEngine;

namespace MrMoonlight.World
{
    /// <summary>
    /// Makes an object fall while swinging side to side like a dropped feather or leaf, instead
    /// of a straight drop. Built for the MainMenu's decorative Eagle/Sparrow feather props (see
    /// Docs/prop-log.md) but works on anything.
    ///
    /// Two independent fall modes, chosen with <see cref="mode"/>:
    /// - <see cref="FallMode.Scripted"/>: a fully deterministic kinematic path. No Rigidbody, no
    ///   gravity. You set the total fall distance, how many left-right swings happen over that
    ///   distance, and how wide each swing is, and the object animates there over
    ///   <see cref="fallDuration"/> seconds.
    /// - <see cref="FallMode.Physics"/>: a real Rigidbody falls under actual gravity; a sideways
    ///   oscillating force gives it the same swinging character, at a frequency derived from the
    ///   same swing-count/distance numbers so both modes feel consistent even though physics
    ///   timing isn't exact.
    ///
    /// Swinging happens along the object's local X axis, in the space of its parent — rotate the
    /// parent (or this object's rest orientation) to point the swing wherever the scene needs it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FeatherFall : MonoBehaviour
    {
        public enum FallMode
        {
            /// <summary>No physics — an authored, repeatable kinematic path.</summary>
            Scripted,

            /// <summary>Real Rigidbody + gravity, with an oscillating sideways force layered on top.</summary>
            Physics
        }

        [Header("Fall Shape")]
        [Tooltip("Total vertical distance the object falls before it's considered landed, in metres.")]
        [SerializeField] private float fallDistance = 2f;

        [Tooltip("How many full left-right swings happen over the fall.")]
        [SerializeField] private int swingCount = 3;

        [Tooltip("Horizontal distance from the centre line to the outer edge of each swing, in metres.")]
        [SerializeField] private float swingRadius = 0.3f;

        [Tooltip("How far the feather banks (tilts, in degrees) into each swing.")]
        [SerializeField] private float maxBankAngle = 35f;

        [Tooltip("Scales swing radius and bank angle over the fall (X = fall progress 0-1, Y = multiplier 0-1). Default ramps from a tight swing at the top to the full radius at the bottom, matching a real feather picking up sideways drift as it falls.")]
        [SerializeField] private AnimationCurve swingGrowth = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("When checked, the final swing eases back to centered and level instead of cutting off abruptly wherever the sine wave happens to be — without this, the object can land mid-tilt, since the swing returns to centre at the end but the bank angle peaks there.")]
        [SerializeField] private bool settleOnLastSwing = false;

        [Header("Mode")]
        [Tooltip("Scripted = deterministic kinematic path, no physics. Physics = real Rigidbody + gravity with an oscillating sideways force.")]
        [SerializeField] private FallMode mode = FallMode.Scripted;

        [Tooltip("Scripted mode only: seconds to fall the full distance.")]
        [SerializeField] private float fallDuration = 3f;

        [Header("Playback")]
        [Tooltip("Start falling automatically when this component becomes active.")]
        [SerializeField] private bool playOnStart = true;

        [Tooltip("When the fall finishes, reset to the start position and fall again.")]
        [SerializeField] private bool loop = false;

        private Vector3 _startLocalPosition;
        private Quaternion _startLocalRotation;
        private Rigidbody _rigidbody;
        private float _elapsed;
        private bool _falling;
        private float _physicsSwingAngularFrequency;
        private float _physicsEstimatedFallTime;

        /// <summary>True while the object is mid-fall (either mode).</summary>
        public bool IsFalling => _falling;

        /// <summary>Raised once, the moment a fall completes (before an optional loop resets it).</summary>
        public event Action OnLanded;

        private void Awake()
        {
            _startLocalPosition = transform.localPosition;
            _startLocalRotation = transform.localRotation;

            if (mode == FallMode.Physics)
            {
                _rigidbody = GetComponent<Rigidbody>();
                if (_rigidbody == null)
                {
                    Debug.LogError($"[FeatherFall] {name} is set to Physics mode but has no Rigidbody. Add one or switch to Scripted mode.");
                }
            }
        }

        private void Start()
        {
            if (playOnStart) StartFalling();
        }

        private void Update()
        {
            if (!_falling || mode != FallMode.Scripted) return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / Mathf.Max(fallDuration, 0.01f));

            // One full swing = one full sine period. swingCount periods spread evenly across
            // the whole fall (t from 0 to 1). swingGrowth widens the swing as the fall
            // progresses, instead of a constant-radius wobble.
            float growth = swingGrowth.Evaluate(t) * GetSettleDamp(t);
            float swingPhase = t * swingCount * Mathf.PI * 2f;
            float xOffset = Mathf.Sin(swingPhase) * swingRadius * growth;
            float yOffset = -fallDistance * t;
            float bankAngle = Mathf.Cos(swingPhase) * maxBankAngle * growth;

            transform.localPosition = _startLocalPosition + new Vector3(xOffset, yOffset, 0f);
            transform.localRotation = _startLocalRotation * Quaternion.Euler(0f, 0f, bankAngle);

            if (t >= 1f) Land();
        }

        private void FixedUpdate()
        {
            if (!_falling || mode != FallMode.Physics || _rigidbody == null) return;

            _elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(_elapsed / Mathf.Max(_physicsEstimatedFallTime, 0.01f));
            float growth = swingGrowth.Evaluate(t) * GetSettleDamp(t);

            // Simple harmonic motion: a horizontal position x(t) = swingRadius * sin(wt) needs
            // acceleration a(t) = -w^2 * x(t) to trace that path (a = -w^2 * x is the definition
            // of SHM). ForceMode.Acceleration ignores mass, so this is the raw acceleration.
            // growth scales the amplitude only (not exact SHM once it's time-varying, but close
            // enough for a decorative fall — same approximation the Scripted path makes).
            float w = _physicsSwingAngularFrequency;
            float swingAcceleration = -swingRadius * growth * w * w * Mathf.Sin(w * _elapsed);
            _rigidbody.AddForce(transform.right * swingAcceleration, ForceMode.Acceleration);

            float bankAngle = Mathf.Cos(w * _elapsed) * maxBankAngle * growth;
            Quaternion targetBank = _startLocalRotation * Quaternion.Euler(0f, 0f, bankAngle);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetBank, Time.fixedDeltaTime * 2f);

            float fallenSoFar = _startLocalPosition.y - transform.localPosition.y;
            if (fallenSoFar >= fallDistance) Land();
        }

        /// <summary>Resets to the start pose and begins falling. Safe to call again mid-fall to restart it.</summary>
        public void StartFalling()
        {
            transform.localPosition = _startLocalPosition;
            transform.localRotation = _startLocalRotation;
            _elapsed = 0f;
            _falling = true;

            if (mode == FallMode.Physics && _rigidbody != null)
            {
                _rigidbody.useGravity = true;
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;

                // Estimate free-fall time under real gravity to pick a swing frequency that
                // lands roughly swingCount swings over fallDistance, same intent as Scripted mode.
                float gravityMagnitude = Mathf.Abs(Physics.gravity.y);
                _physicsEstimatedFallTime = Mathf.Sqrt(2f * fallDistance / Mathf.Max(gravityMagnitude, 0.01f));
                _physicsSwingAngularFrequency = swingCount * Mathf.PI * 2f / Mathf.Max(_physicsEstimatedFallTime, 0.01f);
            }
        }

        private void Land()
        {
            _falling = false;

            if (mode == FallMode.Physics && _rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            OnLanded?.Invoke();

            if (loop) StartFalling();
        }

        /// <summary>
        /// 1 for most of the fall; eases down to 0 across the final swing when
        /// <see cref="settleOnLastSwing"/> is on, so the object comes to rest centered and
        /// level instead of stopping wherever the raw sine/cosine happened to be.
        /// </summary>
        private float GetSettleDamp(float t)
        {
            if (!settleOnLastSwing || swingCount < 1) return 1f;

            float lastSwingStart = (swingCount - 1) / (float)swingCount;
            if (t <= lastSwingStart) return 1f;

            float lastSwingProgress = Mathf.InverseLerp(lastSwingStart, 1f, t);
            return 1f - Mathf.SmoothStep(0f, 1f, lastSwingProgress);
        }

        private void OnDrawGizmosSelected()
        {
            // Preview of the Scripted path (Physics mode is chaotic by nature, but this sine
            // approximates its intended shape too, so it's still a useful preview for both).
            Vector3 origin = Application.isPlaying ? _startLocalPosition : transform.localPosition;
            Vector3 previous = transform.parent != null ? transform.parent.TransformPoint(origin) : origin;

            Gizmos.color = Color.yellow;
            const int steps = 40;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                float growth = swingGrowth.Evaluate(t) * GetSettleDamp(t);
                float swingPhase = t * swingCount * Mathf.PI * 2f;
                Vector3 local = origin + new Vector3(Mathf.Sin(swingPhase) * swingRadius * growth, -fallDistance * t, 0f);
                Vector3 world = transform.parent != null ? transform.parent.TransformPoint(local) : local;
                Gizmos.DrawLine(previous, world);
                previous = world;
            }
        }
    }
}
