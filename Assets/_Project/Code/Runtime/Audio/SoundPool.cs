using UnityEngine;

namespace MrMoonlight.Audio
{
    /// <summary>
    /// A pool of interchangeable clips sharing a pitch range and volume, so the same sound can
    /// vary each time it plays without every caller hand-picking a clip - the shape
    /// Docs/unity-conventions.md's ScriptableObjects section already describes for sound pools.
    /// First real instance is DeathScreamPool (MRM-17), left empty until Carlos supplies the
    /// scream clips - see that issue's handoff note. An empty pool costs nothing;
    /// <see cref="TryGetRandomClip"/> just reports false, same as every other placeholder pool
    /// in this project. Owner: MRM-17
    /// </summary>
    [CreateAssetMenu(menuName = "MrMoonlight/Sound Pool", fileName = "NewSoundPool")]
    public sealed class SoundPool : ScriptableObject
    {
        public AudioClip[] Clips = System.Array.Empty<AudioClip>();
        public Vector2 PitchRange = new Vector2(0.95f, 1.05f);
        public float Volume = 1f;

        /// <summary>Picks a random clip and a random pitch within <see cref="PitchRange"/>. Returns false (clip null, pitch 1) if the pool is empty - callers should no-op rather than error.</summary>
        public bool TryGetRandomClip(out AudioClip clip, out float pitch)
        {
            if (Clips == null || Clips.Length == 0)
            {
                clip = null;
                pitch = 1f;
                return false;
            }

            clip = Clips[Random.Range(0, Clips.Length)];
            pitch = Random.Range(PitchRange.x, PitchRange.y);
            return true;
        }
    }
}
