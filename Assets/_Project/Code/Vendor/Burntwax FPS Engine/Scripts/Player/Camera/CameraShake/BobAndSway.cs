using UnityEngine;

namespace Burntwax
{
    public class BobAndSway : MonoBehaviour
    {
        [Header("External References")]
        [SerializeField] PlayerStateMachine player;
        [SerializeField] GunStateMachine gun;
        [SerializeField] CamStateMachine cam;
        [SerializeField] Rigidbody rb;

        [Header("Toggles")]
        public bool sway = true;
        public bool swayRotation = true;
        public bool bobOffset = true;
        public bool bobSway = true;

        [Header("Sway")]
        public float step = 0.01f;
        public float maxStepDistance = 0.06f;
        Vector3 swayPos;

        [Header("Sway Rotation")]
        public float rotationStep = 4f;
        public float maxRotationStep = 5f;
        Vector3 swayEulerRot;

        [Header("Bobbing")]
        private float stepTimer;
        public float bobBaseFreq = 5f;
        public Vector3 bobLimit = Vector3.one * 0.01f;
        public AnimationCurve verticalAmplitudeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.25f, 0.8f),
            new Keyframe(0.5f, 1.2f),
            new Keyframe(0.75f, 0.9f),
            new Keyframe(1f, 1f)
        );
        public AnimationCurve rollCurve = new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.25f, 1f),
            new Keyframe(0.5f, 0.5f),
            new Keyframe(0.75f, 0f),
            new Keyframe(1f, 0.5f)
        );
        Vector3 bobPosition;

        [Header("Bob Rotation")]
        public Vector3 multiplier;
        Vector3 bobEulerRotation;

        [Header("Velocity Sway")]
        public float velocitySwayIntensity = 0.002f;
        public float velocitySwaySmooth = 6f;
        private Vector3 velocitySway;
        private Vector3 velocitySwaySpring;
        private Vector3 lastVelocity;

        [Header("Falling Inertia")]
        public float fallUpOffsetAmount = 0.04f;
        public float fallSpringReturnSpeed = 4f;
        public float landingJoltMultiplier = 0.2f;
        public float maxFallOffset = 0.08f;
        public float maxAirTimeForJoltScale = 1.5f;

        [Header("Landing Jolt Curve")]
        public AnimationCurve landingJoltCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.3f, 0.2f),
            new Keyframe(0.7f, 0.6f),
            new Keyframe(1f, 1f)
        );
        [Range(0f, 1f)] public float landingBobStartPhase = 0.375f;

        [Header("ADS Bobbing")]
        public bool adsBob = true; // Toggle for ADS bobbing
        public float adsBobMultiplier = 0.03f; // Intensity of bobbing during ADS (smaller movement)
        public float adsBobFrequencyMultiplier = 0.1f; // Frequency of bobbing during ADS (slower movement)
        public float adsHorizontalMultiplier = 0.5f; // Horizontal movement multiplier during ADS

        private Vector3 fallOffset;
        private Vector3 landingVelocityImpulse;
        private float landingJoltTimer;
        private float landingJoltDuration = 0.3f;
        private bool syncBobWithLandingJolt;

        private bool wasGroundedLastFrame = true;
        private float airTime = 0f;

        Vector2 walkInput;
        Vector2 lookInput;

        float smooth = 10f;
        float smoothRot = 12f;

        bool onSolid;

        void Update()
        {
            GetInput();

            onSolid = player.currentState == player.states.Grounded()
                      || player.currentState == player.states.Slope();

            if (InputManager.Instance.MoveInput != Vector2.zero && onSolid && cam.currentState != cam.states.Aim() && gun.CurrentState != gun.states.Shoot())
            {
                Sway();
            }
            else
            {
                swayPos = Vector3.zero;
            }

            SwayRotation();

            bool landedFromFall = onSolid && !wasGroundedLastFrame && airTime > 0.05f;
            if (landedFromFall)
            {
                syncBobWithLandingJolt = true;
            }

            // Adjusted condition to allow bobbing during ADS if adsBob is enabled
            bool shouldBob = (cam.currentState != cam.states.Aim() || adsBob) && gun.CurrentState != gun.states.Shoot();
            if (shouldBob)
            {
                BobOffset();
                BobRotation();
            }
            else
            {
                bobPosition = Vector3.Lerp(bobPosition, Vector3.zero, Time.deltaTime * smooth);
                bobEulerRotation = Vector3.Lerp(bobEulerRotation, Vector3.zero, Time.deltaTime * smooth);
            }

            Vector3 velocity = rb.linearVelocity;
            Vector3 deltaVelocity = velocity - lastVelocity;
            velocitySway = Vector3.Lerp(velocitySway, deltaVelocity * velocitySwayIntensity, Time.deltaTime * velocitySwaySmooth);
            velocitySwaySpring = Vector3.Lerp(velocitySwaySpring, -velocitySway, Time.deltaTime * velocitySwaySmooth);
            lastVelocity = velocity;

            if (!onSolid)
            {
                airTime += Time.deltaTime;
                float upwardFloat = Mathf.Clamp(airTime * fallUpOffsetAmount, 0, maxFallOffset);
                fallOffset = Vector3.Lerp(fallOffset, new Vector3(0, upwardFloat, 0), Time.deltaTime * fallSpringReturnSpeed);
                landingVelocityImpulse = Vector3.Lerp(landingVelocityImpulse, Vector3.zero, Time.deltaTime * fallSpringReturnSpeed);
            }
            else
            {
                if (landedFromFall)
                {
                    float clampedAirTime = Mathf.Clamp(airTime, 0, maxAirTimeForJoltScale);
                    float normalizedAirTime = clampedAirTime / maxAirTimeForJoltScale;
                    float impactStrength = landingJoltCurve.Evaluate(normalizedAirTime);
                    landingVelocityImpulse = new Vector3(0, -impactStrength * landingJoltMultiplier, 0);
                    landingJoltTimer = landingJoltDuration;
                }

                airTime = 0f;
                fallOffset = Vector3.Lerp(fallOffset, Vector3.zero, Time.deltaTime * fallSpringReturnSpeed);
                landingVelocityImpulse = Vector3.Lerp(landingVelocityImpulse, Vector3.zero, Time.deltaTime * fallSpringReturnSpeed);
            }

            if (landingJoltTimer > 0f)
            {
                landingJoltTimer -= Time.deltaTime;
                if (landingJoltTimer <= 0f)
                {
                    landingJoltTimer = 0f;
                    syncBobWithLandingJolt = false;
                }
            }

            wasGroundedLastFrame = onSolid;

            CompositePositionRotation();
        }

        void GetInput()
        {
            walkInput.x = InputManager.Instance.MoveInput.x;
            walkInput.y = InputManager.Instance.MoveInput.y;
            walkInput = walkInput.normalized;

            lookInput.x = InputManager.Instance.LookInput.x;
            lookInput.y = InputManager.Instance.LookInput.y;
        }

        void Sway()
        {
            if (!sway) return;
            Vector3 invertLook = lookInput * -step;
            invertLook.x = Mathf.Clamp(invertLook.x, -maxStepDistance, maxStepDistance);
            invertLook.y = Mathf.Clamp(invertLook.y, -maxStepDistance, maxStepDistance);
            swayPos = invertLook;
        }

        void SwayRotation()
        {
            if (!swayRotation) return;
            Vector2 invertLook = lookInput * -rotationStep;
            invertLook.x = Mathf.Clamp(invertLook.x, -maxRotationStep, maxRotationStep);
            invertLook.y = Mathf.Clamp(invertLook.y, -maxRotationStep, maxRotationStep);
            swayEulerRot = new Vector3(invertLook.y, invertLook.x, invertLook.x);
        }

        void BobOffset()
        {
            if (!bobOffset || InputManager.Instance.MoveInput == Vector2.zero || !onSolid)
            {
                bobPosition = Vector3.Lerp(bobPosition, Vector3.zero, Time.deltaTime * smooth);
                return;
            }

            // Adjust bobbing frequency for ADS
            float currentBobFreq = (cam.currentState == cam.states.Aim() && adsBob) ? bobBaseFreq * adsBobFrequencyMultiplier : bobBaseFreq;

            stepTimer += Time.deltaTime * rb.linearVelocity.magnitude * currentBobFreq;
            if (stepTimer > 1f) stepTimer -= 1f;

            if (syncBobWithLandingJolt)
            {
                stepTimer = landingBobStartPhase;
                syncBobWithLandingJolt = false;
            }

            float horizontal = Mathf.Sin(stepTimer * 2 * Mathf.PI);
            float verticalBase = Mathf.Sin(stepTimer * 4 * Mathf.PI);
            float amplitude = verticalAmplitudeCurve.Evaluate(stepTimer);
            float vertical = verticalBase > 0 ? verticalBase * 0.5f : verticalBase * amplitude;

            // Adjust bobbing intensity for ADS
            float bobMultiplier = (cam.currentState == cam.states.Aim() && adsBob) ? adsBobMultiplier : 1f;
            float horizontalMultiplier = (cam.currentState == cam.states.Aim() && adsBob) ? adsHorizontalMultiplier : 1f;

            bobPosition = new Vector3(horizontal * bobLimit.x * horizontalMultiplier, vertical * bobLimit.y, 0) * bobMultiplier;
        }

        void BobRotation()
        {
            if (!bobSway || InputManager.Instance.MoveInput == Vector2.zero || !onSolid)
            {
                bobEulerRotation = Vector3.Lerp(bobEulerRotation, Vector3.zero, Time.deltaTime * smooth);
                return;
            }

            float roll = (rollCurve.Evaluate(stepTimer) - 0.5f) * 2f;

            // Adjust bobbing rotation intensity for ADS
            float bobMultiplier = (cam.currentState == cam.states.Aim() && adsBob) ? adsBobMultiplier : 1f;
            bobEulerRotation = new Vector3(0, 0, roll * multiplier.z * bobMultiplier);
        }

        void CompositePositionRotation()
        {
            Vector3 newPos = swayPos + bobPosition + velocitySwaySpring + fallOffset + landingVelocityImpulse;
            transform.localPosition = Vector3.Lerp(transform.localPosition, newPos, Time.deltaTime * smooth);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(swayEulerRot) * Quaternion.Euler(bobEulerRotation), Time.deltaTime * smoothRot);
        }
    }
}
