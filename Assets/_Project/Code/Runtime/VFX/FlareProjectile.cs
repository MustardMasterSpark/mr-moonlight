using MrMoonlight.Data;
using UnityEngine;

namespace MrMoonlight.VFX
{
    /// <summary>
    /// A fired signal flare: arcs out of the gun, burns bright for a while, then dies.
    ///
    /// <b>Built rather than imported.</b> The Asset Store "Flare Gun" pack (Rokay3D) was checked
    /// first, as its flare was the obvious source. Its <c>flarebullet</c> prefab is unusable here:
    /// its particle renderer points at a material GUID that is not in the package at all (it
    /// referenced Unity's long-removed Standard Assets smoke material), and the two materials it
    /// does ship are built-in <c>Standard</c>, which renders magenta under URP. Its gun model is
    /// also redundant — we already have <c>Weapon_FlareGun.prefab</c>. So the effect is ours.
    ///
    /// The composition is deliberately split across four things rather than done in one shader:
    /// an additive billboard core (<c>MrMoonlight/VFX/FlareCore</c>) for the burning chemical
    /// glow, a trail for the arc, particle systems for smoke and sparks, and — per the standing
    /// project rule that glowing objects get a real <c>Light</c>, never an emission map — an actual
    /// point light. The light is the part that matters: a flare that does not change how the forest
    /// is lit is just a sprite.
    ///
    /// Owner: MRM-34. Reused by MRM-57 (Vernon's distraction places flares in the distant forest),
    /// which is why <see cref="Launch"/> takes a direction and nothing about the Spotter.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/VFX/Flare Projectile")]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FlareProjectile : MonoBehaviour
    {
        [Header("Visuals")]
        [Tooltip("The real Light. Standing project rule: a glowing object gets a Light, never an emission map.")]
        [SerializeField] private Light flareLight;

        [Tooltip("Renderer for the additive burning core. Its material colour is faded out at the end of the burn.")]
        [SerializeField] private Renderer coreRenderer;

        [SerializeField] private TrailRenderer trail;

        [Tooltip("Smoke, sparks — anything that should stop emitting when the flare burns out.")]
        [SerializeField] private ParticleSystem[] particles = new ParticleSystem[0];

        [Header("Flight")]
        [Tooltip("Bounciness applied on impact so a flare that lands on rock skitters instead of sticking.")]
        [Range(0f, 1f)]
        [SerializeField] private float bounciness = 0.35f;

        [Tooltip("How much spin the flare leaves the barrel with. Purely cosmetic — it makes the light sweep as it flies.")]
        [SerializeField] private float launchSpin = 240f;

        [Header("Lifetime")]
        [Tooltip("Extra seconds the burnt-out husk lingers before it is destroyed, so smoke has time to disperse.")]
        [SerializeField] private float lingerAfterFade = 3f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Rigidbody _body;
        private Material _coreMaterial;
        private Color _coreColour;
        private float _baseLightRange;
        private float _burnTimer;
        private float _flickerTimer;
        private float _currentFlicker = 1f;
        private bool _fading;

        /// <summary>Seconds since the flare was launched. Read by anything that wants to react to a burning flare.</summary>
        public float BurnTime => _burnTimer;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();

            if (flareLight != null)
            {
                flareLight.range = Tunables.I.FlareLightRange;
                _baseLightRange = flareLight.range;
            }

            if (coreRenderer != null)
            {
                // Instance the material so fading one flare never dims every other flare burning
                // in the same scene — MRM-57 places several at once.
                _coreMaterial = coreRenderer.material;
                _coreColour = _coreMaterial.HasProperty(BaseColorId)
                    ? _coreMaterial.GetColor(BaseColorId)
                    : Color.white;
            }

            ApplyBounciness();
        }

        /// <summary>
        /// Fire the flare along <paramref name="aimDirection"/>. The launch itself is a straight-up
        /// cheat, not a pitched version of the aim: Carlos, 2026-09-02 (MRM-76), twice — the arc
        /// kept reading as "shot sideways" rather than "shot up." A real 90°-from-ground launch plus
        /// a small, separate forward drift (see <see cref="MoonlightTunables.FlareForwardDrift"/>)
        /// reads as vertical at the moment it leaves the gun — which is what a signal flare should
        /// do — while the drift still carries it into a mortar-style curve as gravity bleeds off the
        /// vertical speed near the top. Vertical launch speed is derived from
        /// <see cref="MoonlightTunables.FlareApexHeight"/> and the same reduced gravity
        /// (<see cref="MoonlightTunables.FlareGravityScale"/>) that also keeps the whole flight
        /// slow and floaty rather than falling like a dropped rock.
        ///
        /// <paramref name="aimDirection"/>'s own vertical component is discarded entirely — only
        /// its horizontal facing decides which way the drift carries the flare. The muzzle's actual
        /// tilt is animation-driven and not reliable enough to read as "fired straight up."
        /// </summary>
        public void Launch(Vector3 aimDirection)
        {
            if (_body == null) _body = GetComponent<Rigidbody>();

            Vector3 horizontalAim = new Vector3(aimDirection.x, 0f, aimDirection.z);
            if (horizontalAim.sqrMagnitude < 0.0001f)
            {
                horizontalAim = new Vector3(transform.forward.x, 0f, transform.forward.z);
            }
            horizontalAim.Normalize();

            float effectiveGravity = Mathf.Abs(Physics.gravity.y) * Tunables.I.FlareGravityScale;
            float verticalLaunchSpeed = Mathf.Sqrt(2f * effectiveGravity * Tunables.I.FlareApexHeight);

            _body.useGravity = false; // gravity is applied manually so FlareGravityScale can slow the fall
            _body.linearVelocity = Vector3.up * verticalLaunchSpeed + horizontalAim * Tunables.I.FlareForwardDrift;
            _body.angularVelocity = Random.onUnitSphere * (launchSpin * Mathf.Deg2Rad);
        }

        private void FixedUpdate()
        {
            // Below-1 gravity is what makes it hang in the air long enough to read as a signal
            // rather than dropping like a thrown rock.
            _body.AddForce(Physics.gravity * Tunables.I.FlareGravityScale, ForceMode.Acceleration);
        }

        private void Update()
        {
            _burnTimer += Time.deltaTime;

            UpdateFlicker();

            if (!_fading && _burnTimer >= Tunables.I.FlareBurnDuration)
            {
                BeginFade();
            }

            if (_fading) UpdateFade();
        }

        /// <summary>
        /// Re-randomise the light at a fixed rate rather than every frame. Per-frame randomisation
        /// reads as television static; a slower, held value reads as a chemical flame.
        /// </summary>
        private void UpdateFlicker()
        {
            float period = 1f / Mathf.Max(0.01f, Tunables.I.FlareFlickerFrequency);
            _flickerTimer += Time.deltaTime;

            if (_flickerTimer >= period)
            {
                _flickerTimer -= period;
                _currentFlicker = Random.Range(Tunables.I.FlareLightIntensityMin, Tunables.I.FlareLightIntensityMax);
            }

            if (flareLight == null) return;

            float fade = FadeFraction();
            flareLight.intensity = _currentFlicker * fade;
            flareLight.range = _baseLightRange * Mathf.Lerp(0.35f, 1f, fade);
        }

        private void BeginFade()
        {
            _fading = true;

            foreach (ParticleSystem system in particles)
            {
                if (system == null) continue;
                // Stop emitting but let the already-spawned smoke live out its lifetime.
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (trail != null) trail.emitting = false;

            Destroy(gameObject, Tunables.I.FlareFadeDuration + lingerAfterFade);
        }

        private void UpdateFade()
        {
            float fade = FadeFraction();

            if (_coreMaterial != null && _coreMaterial.HasProperty(BaseColorId))
            {
                Color faded = _coreColour;
                faded.a *= fade;
                // Additive material, so scaling RGB is what actually dims it — alpha alone would
                // leave the core at full brightness right up until it vanished.
                _coreMaterial.SetColor(BaseColorId, new Color(faded.r * fade, faded.g * fade, faded.b * fade, faded.a));
            }

            if (coreRenderer != null && fade <= 0f) coreRenderer.enabled = false;
        }

        /// <summary>1 while burning, ramping to 0 across the fade window.</summary>
        private float FadeFraction()
        {
            if (!_fading) return 1f;

            float elapsed = _burnTimer - Tunables.I.FlareBurnDuration;
            return Mathf.Clamp01(1f - elapsed / Mathf.Max(0.01f, Tunables.I.FlareFadeDuration));
        }

        private void ApplyBounciness()
        {
            var collider = GetComponent<Collider>();
            if (collider == null) return;

            // A material created here rather than an asset, so the flare prefab has no external
            // physics-material dependency to lose.
            collider.material = new PhysicsMaterial("Flare")
            {
                bounciness = bounciness,
                dynamicFriction = 0.4f,
                staticFriction = 0.4f,
                bounceCombine = PhysicsMaterialCombine.Average
            };
        }

        private void OnDestroy()
        {
            if (_coreMaterial != null) Destroy(_coreMaterial);
        }
    }
}
