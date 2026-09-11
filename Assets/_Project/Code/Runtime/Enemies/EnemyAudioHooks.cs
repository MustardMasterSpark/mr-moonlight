using UnityEngine;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// Named, silent audio hooks for an enemy. Every combat and locomotion script calls into this
    /// instead of touching an <see cref="AudioSource"/> directly.
    ///
    /// <c>fire</c> is wired to the player's R870 shotgun take (<c>HQFPS_R870_Shoot1</c>) on
    /// Enemy_Spotter.prefab — the Spotter's own firing sound was still silent and Carlos asked for a
    /// real shot instead (2026-09-10). Plays through the GameObject's own pre-existing
    /// <see cref="AudioSource"/> (already 3D-configured — spatial blend 1, min/max distance 3/40m,
    /// linear rolloff — so it's positional/distance-attenuated with no extra setup).
    ///
    /// <b>Reaction pools</b> (alert/pain/wound/dismemberment/death) were recorded and imported as
    /// part of the island demo wrap-up audio pass (2026-09-10): each is a small pool of clips,
    /// prefixed <c>DEMO_&lt;category&gt;_&lt;n&gt;</c> per Carlos's naming ask, and a call picks one
    /// at random via <see cref="PlayRandom"/> rather than always playing the same take. Empty/short
    /// pools are safe — a missing category just stays silent, same as the single-clip slots below.
    ///
    /// Clip naming for the remaining single-clip slots: <c>ENM_Spotter_*</c> — the prefix drives the
    /// import preset, see Docs/audio-import-workflow.md. Owner: MRM-34 / island-demo-wrapup.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Mr. Moonlight/Enemies/Enemy Audio Hooks")]
    public sealed class EnemyAudioHooks : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Leave empty to use (or add) an AudioSource on this GameObject.")]
        [SerializeField] private AudioSource source;

        [Header("Combat — ENM_Spotter_*")]
        [SerializeField] private AudioClip fire;
        [SerializeField] private AudioClip dryFire;
        [SerializeField] private AudioClip reload;
        [SerializeField] private AudioClip flareFire;

        [Header("Reactions — pooled, one random clip per play (DEMO_ import, 2026-09-10)")]
        [Tooltip("Spotter detects the player, or fires his flare. DEMO_alert_*.")]
        [SerializeField] private AudioClip[] alert = new AudioClip[0];

        [Tooltip("Every non-lethal hit — a groan of pain. Never plays on the killing blow. DEMO_pain_*.")]
        [SerializeField] private AudioClip[] pain = new AudioClip[0];

        [Tooltip("Every non-lethal hit — a wound reaction, separate pool from pain for variety. Never plays alongside a dismemberment. DEMO_wound_*.")]
        [SerializeField] private AudioClip[] wound = new AudioClip[0];

        [Tooltip("A limb is cut on the killing blow. DEMO_dismemberment_*.")]
        [SerializeField] private AudioClip[] dismemberment = new AudioClip[0];

        [Tooltip("The killing blow itself. DEMO_death_*.")]
        [SerializeField] private AudioClip[] death = new AudioClip[0];

        [Header("Locomotion")]
        [SerializeField] private AudioClip footstep;

        private void Awake()
        {
            if (source == null && !TryGetComponent(out source))
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
            }
        }

        public void PlayFire() => Play(fire);
        public void PlayDryFire() => Play(dryFire);
        public void PlayReload() => Play(reload);
        public void PlayFlareFire() => Play(flareFire);
        public void PlayAlert() => PlayRandom(alert);
        public void PlayPain() => PlayRandom(pain);
        public void PlayWound() => PlayRandom(wound);
        public void PlayDismemberment() => PlayRandom(dismemberment);
        public void PlayDeath() => PlayRandom(death);

        /// <summary>Called from an animation event on the walk/run clips once footstep pools exist (MRM-31).</summary>
        public void PlayFootstep() => Play(footstep);

        private void Play(AudioClip clip)
        {
            if (clip == null || source == null) return;
            source.PlayOneShot(clip);
        }

        /// <summary>Picks one random clip from the pool and plays it as a one-shot. Silent on an empty/unassigned pool.</summary>
        private void PlayRandom(AudioClip[] pool)
        {
            if (pool == null || pool.Length == 0 || source == null) return;

            AudioClip clip = pool[Random.Range(0, pool.Length)];
            if (clip != null) source.PlayOneShot(clip);
        }
    }
}
