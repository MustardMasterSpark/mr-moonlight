using UnityEngine;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// Named, silent audio hooks for an enemy. Every combat and locomotion script calls into this
    /// instead of touching an <see cref="AudioSource"/> directly.
    ///
    /// There are no clips yet — Carlos does not have the enemy pools recorded (2026-09-01), and
    /// wiring a placeholder beep would be worse than silence. So each method is a real call site
    /// with a real serialized clip slot behind it: drop a clip in and it plays, leave it empty and
    /// nothing happens and nothing warns. That means the audio pass later is an inspector job, not
    /// a code job.
    ///
    /// Clip naming when they arrive: <c>ENM_Spotter_*</c> — the prefix drives the import preset,
    /// see Docs/audio-import-workflow.md. Owner: MRM-34.
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

        [Header("Reactions")]
        [SerializeField] private AudioClip pain;
        [SerializeField] private AudioClip death;
        [SerializeField] private AudioClip alerted;

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
        public void PlayPain() => Play(pain);
        public void PlayDeath() => Play(death);
        public void PlayAlerted() => Play(alerted);

        /// <summary>Called from an animation event on the walk/run clips once footstep pools exist (MRM-31).</summary>
        public void PlayFootstep() => Play(footstep);

        private void Play(AudioClip clip)
        {
            if (clip == null || source == null) return;
            source.PlayOneShot(clip);
        }
    }
}
