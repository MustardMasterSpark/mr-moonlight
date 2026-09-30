using DG.Tweening;
using MrMoonlight.Data;
using MrMoonlight.World.Weather;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// Lights the fire on a Spotter's lamp once it's dropped and comes to rest, and dims it out
    /// again on a timer. Sits dormant on the lamp prop the whole time it's socketed on an enemy's
    /// hand — <see cref="EnemyDeathDrop"/> is what gives it a <c>Rigidbody</c> in the first place,
    /// so this watches for that and for the rigidbody settling, rather than needing any wiring
    /// between the two components.
    ///
    /// <para>The fire VFX (Ian's Fire Pack, "Fire Small URP") already flickers its own light via
    /// its bundled <c>LightFlicker</c> script — that's kept as-is. This only adds the ignite and
    /// fade bookends: settle → spawn, scaled to the lamp → burn → fade the fire out over
    /// <see cref="MoonlightTunables.LampFireVfxFadeDuration"/> while the lamp's own gameplay
    /// <c>Light</c> fades separately over the longer <see cref="MoonlightTunables.LampLightFadeDuration"/>,
    /// so the lamp glows a beat after the flame itself has died down.</para>
    ///
    /// Owner: MRM-76.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Enemies/Lamp Fire Effect")]
    public sealed class LampFireEffect : MonoBehaviour
    {
        [Tooltip("The lamp's own gameplay Light. Leave empty to find it on this object or a child.")]
        [SerializeField] private Light lampLight;

        [Tooltip("Ian's Fire Pack \"Fire Small URP\" prefab (or equivalent). Instantiated once, on settle.")]
        [SerializeField] private GameObject fireVfxPrefab;

        [Tooltip("Where the fire spawns. Leave empty to use this transform.")]
        [SerializeField] private Transform fireSpawnPoint;

        [Tooltip("Plays once on the lamp's first ground impact — deliberately separate from the fire, which waits for it to settle.")]
        [SerializeField] private AudioClip glassBreakClip;

        [Tooltip("Leave empty to add a 3D AudioSource automatically.")]
        [SerializeField] private AudioSource impactAudioSource;

        private Rigidbody _body;
        private Sequence _fireSequence;
        private Sequence _lightSequence;
        private GameObject _fire;
        private Renderer[] _lampRenderers;
        private float _settledTimer;
        private bool _ignited;
        private bool _impactSoundPlayed;

        private static bool _dropCollisionRulesSet;

        private void Awake()
        {
            if (lampLight == null) lampLight = GetComponentInChildren<Light>();
            if (fireSpawnPoint == null) fireSpawnPoint = transform;
            _lampRenderers = GetComponentsInChildren<Renderer>();

            if (impactAudioSource == null) impactAudioSource = GetComponent<AudioSource>();
            if (impactAudioSource == null) impactAudioSource = gameObject.AddComponent<AudioSource>();
            impactAudioSource.playOnAwake = false;
            impactAudioSource.spatialBlend = 1f; // 3D — attenuates with distance from the player's AudioListener

            EnsureDropCollisionRules();
        }

        // The lamp's collider sits on the "DroppedProp" layer whether it's still socketed or
        // already on the ground — MRM-34, Carlos's call 2026-09-03: it should never physically
        // collide with the enemy body it's clipping through or another enemy's hitboxes (clipping
        // into the body is fine, it's cosmetic), and it must never register as a bullet hit either
        // (see ShootConfig HitMask — "DroppedProp" is excluded there too). World geometry (Ground,
        // Default, Destructible, ...) is untouched, so a dropped lamp still lands and rolls
        // normally. A static guard because every Spotter's lamp calls this at Awake and the rule
        // only needs setting once per session.
        private static void EnsureDropCollisionRules()
        {
            if (_dropCollisionRulesSet) return;
            _dropCollisionRulesSet = true;

            int droppedProp = LayerMask.NameToLayer("DroppedProp");
            int enemy = LayerMask.NameToLayer("Enemy");
            if (droppedProp < 0 || enemy < 0) return;

            Physics.IgnoreLayerCollision(droppedProp, enemy, true);
        }

        // EnemyDeathDrop's collider is non-trigger, so this fires on the lamp's first real ground
        // hit — the moment it "falls to the ground", not once it's settled (that's the fire, via
        // Update() below). Plays once regardless of what specifically was hit.
        private void OnCollisionEnter(Collision collision)
        {
            if (_impactSoundPlayed || glassBreakClip == null) return;

            _impactSoundPlayed = true;
            impactAudioSource.PlayOneShot(glassBreakClip);
        }

        private void Update()
        {
            if (_ignited) return;

            if (_body == null)
            {
                // EnemyDeathDrop adds this at drop time — before that, the lamp is socketed on the
                // enemy's hand and has none, so there's nothing to watch yet.
                _body = GetComponent<Rigidbody>();
                if (_body == null) return;
            }

            bool settled = _body.linearVelocity.sqrMagnitude < Tunables.I.LampFireSettleLinearThreshold * Tunables.I.LampFireSettleLinearThreshold
                           && _body.angularVelocity.sqrMagnitude < Tunables.I.LampFireSettleAngularThreshold * Tunables.I.LampFireSettleAngularThreshold;

            if (!settled)
            {
                _settledTimer = 0f;
                return;
            }

            _settledTimer += Time.deltaTime;
            if (_settledTimer >= Tunables.I.LampFireSettleGraceDuration)
            {
                Ignite();
            }
        }

        private void Ignite()
        {
            _ignited = true;

            if (fireVfxPrefab != null)
            {
                GameObject fire = Instantiate(fireVfxPrefab, fireSpawnPoint.position, Quaternion.identity, fireSpawnPoint);
                float scale = LargestLampDimension() * Tunables.I.LampFireVfxScaleFactor;
                FadeFireVfx(fire, scale);
            }

            FadeLampLight();
        }

        private float LargestLampDimension()
        {
            if (_lampRenderers == null || _lampRenderers.Length == 0) return 0.15f;

            Bounds bounds = _lampRenderers[0].bounds;
            for (int i = 1; i < _lampRenderers.Length; i++) bounds.Encapsulate(_lampRenderers[i].bounds);

            return Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        }

        // DOTween's SetDelay does the "burn at full strength, then fade" waiting for us — no
        // manual WaitForSeconds/elapsed-time bookkeeping needed. MRM-76, Carlos's explicit call
        // 2026-09-02 to use DOTween for smooth transitions once it was installed.
        private void FadeFireVfx(GameObject fire, float targetScale)
        {
            var flicker = fire.GetComponentInChildren<LightFlicker>();
            Light fireLight = fire.GetComponentInChildren<Light>();
            AudioSource audio = fire.GetComponentInChildren<AudioSource>();
            var particleSystems = fire.GetComponentsInChildren<ParticleSystem>();

            // Fade in first — Carlos's ask 2026-09-03: the ignite snapped to full size/brightness
            // instantly and "looked weird" next to the already-smooth fade-out. Particle systems
            // have no clean alpha to tween, so scale stands in for it — the flame visibly grows
            // rather than popping in, alongside the light and audio actually fading.
            //
            // Disabling the flicker *before* its own Start() runs (which reads the light's current
            // intensity as its baseline) means it captures the fully-faded-in target as that
            // baseline once re-enabled below, instead of fighting a fade from zero the same way it
            // would fight the fade-out further down if left running through it.
            if (flicker != null) flicker.enabled = false;
            float targetLightIntensity = fireLight != null ? fireLight.intensity : 0f;
            float targetAudioVolume = audio != null ? audio.volume : 0f;
            if (fireLight != null) fireLight.intensity = 0f;
            if (audio != null) audio.volume = 0f;
            fire.transform.localScale = Vector3.zero;

            float fadeInDuration = Tunables.I.LampFireVfxFadeInDuration;
            var sequence = DOTween.Sequence();
            sequence.Join(fire.transform.DOScale(targetScale, fadeInDuration));
            if (fireLight != null) sequence.Join(fireLight.DOIntensity(targetLightIntensity, fadeInDuration));
            if (audio != null) sequence.Join(DOTween.To(() => audio.volume, v => audio.volume = v, targetAudioVolume, fadeInDuration));
            sequence.AppendCallback(() => { if (flicker != null) flicker.enabled = true; });

            sequence.AppendInterval(Tunables.I.LampFireBurnDuration);
            sequence.AppendCallback(() =>
            {
                if (flicker != null) flicker.enabled = false; // stop it fighting the fade below
                for (int i = 0; i < particleSystems.Length; i++)
                {
                    particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            });

            float fadeOutDuration = Tunables.I.LampFireVfxFadeDuration;
            if (fireLight != null) sequence.Join(fireLight.DOIntensity(0f, fadeOutDuration));

            // DOTweenModuleAudio's AudioSource.DOFade shortcut doesn't resolve in this project for
            // reasons not worth chasing further — this is exactly what that shortcut does under
            // the hood, so it's a wash functionally.
            if (audio != null) sequence.Join(DOTween.To(() => audio.volume, v => audio.volume = v, 0f, fadeOutDuration));

            sequence.SetLink(fire).OnComplete(() => Destroy(fire));
            _fire = fire;
            _fireSequence = sequence;
        }

        private void FadeLampLight()
        {
            if (lampLight == null) return;

            _lightSequence = DOTween.Sequence()
                .AppendInterval(Tunables.I.LampFireBurnDuration)
                .Append(lampLight.DOIntensity(0f, Tunables.I.LampLightFadeDuration))
                .SetLink(gameObject)
                .OnComplete(OnLampOut);
        }

        /// <summary>
        /// The lamp's own timeline is unchanged (burn, then fade over LampLightFadeDuration); this is what
        /// happens once it is fully out. The light was already at zero, so removing it is invisible.
        /// With <see cref="MoonlightTunables.CorpseLampCleanupAfterOut"/> the Light (and its weather and URP
        /// companions), the lamp's rigidbody and colliders, and this component are all removed, so a
        /// burnt-out lamp is a plain mesh that costs nothing. MRM-85.
        /// </summary>
        /// <summary>
        /// Cuts the burn timeline short: the lamp light, the fire's light, flame and sound all fade to zero over
        /// <paramref name="duration"/>, then the same clean-up as a natural burn-out runs. Used by the corpse
        /// dissolve (MRM-85, Carlos 2026-09-30): the lamp's dissolve is what dims its light. Safe to call at
        /// any point, before or after ignition.
        /// </summary>
        public void FadeOutAndFinish(float duration)
        {
            _ignited = true; // a lamp that has not ignited yet never will
            _lightSequence?.Kill();
            _fireSequence?.Kill();

            Sequence fade = DOTween.Sequence();
            if (lampLight != null) fade.Join(lampLight.DOIntensity(0f, duration));

            if (_fire != null)
            {
                var flicker = _fire.GetComponentInChildren<LightFlicker>();
                if (flicker != null) flicker.enabled = false; // it would fight the fade
                foreach (ParticleSystem ps in _fire.GetComponentsInChildren<ParticleSystem>())
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

                Light fireLight = _fire.GetComponentInChildren<Light>();
                if (fireLight != null) fade.Join(fireLight.DOIntensity(0f, duration));

                AudioSource audio = _fire.GetComponentInChildren<AudioSource>();
                if (audio != null) fade.Join(DOTween.To(() => audio.volume, v => audio.volume = v, 0f, duration));
            }

            fade.SetLink(gameObject).OnComplete(() =>
            {
                if (_fire != null) Destroy(_fire);
                OnLampOut();
            });
        }

        private void OnLampOut()
        {
            if (lampLight == null) return;

            if (!Tunables.I.CorpseLampCleanupAfterOut)
            {
                lampLight.enabled = false;
                return;
            }

            GameObject lightObject = lampLight.gameObject;
            if (lightObject != gameObject)
            {
                Destroy(lightObject); // a dedicated child: takes Light, URP data and the weather source with it
            }
            else
            {
                if (TryGetComponent(out WeatherLightSource weather)) Destroy(weather);
                if (TryGetComponent(out UniversalAdditionalLightData extra)) Destroy(extra);
                Destroy(lampLight);
            }

            foreach (Collider col in GetComponentsInChildren<Collider>(true)) Destroy(col);
            if (TryGetComponent(out Rigidbody body)) Destroy(body);
            enabled = false;
        }
    }
}
