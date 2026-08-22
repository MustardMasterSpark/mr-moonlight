using UnityEngine;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Sandbox-only debug tool: applies <see cref="PlayerStats.CurrentAudioPitch"/> to an
    /// AudioSource every frame, so Carlos can hear the MRM-12 pitch smoothing (never instant)
    /// live - pair with StatDebugModifierToggle targeting AudioPitch to hear it change. Lives on
    /// its own GameObject anywhere in the scene, not on the Player - drag the Player's
    /// PlayerStats into <see cref="stats"/>, or leave it blank and this finds the scene's one
    /// PlayerStats at Awake. Attach an AudioSource with a looping test clip, Play On Awake
    /// enabled. Not part of the shipped game - same "debug tool, not shipped" category as MRM-8's
    /// InputDebugOverlay. Owner: MRM-12
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class StatDebugAudioPitchTest : MonoBehaviour
    {
        [SerializeField] private PlayerStats stats;
        [SerializeField] private AudioSource audioSource;

        private void Awake()
        {
            if (stats == null)
            {
                stats = FindFirstObjectByType<PlayerStats>();
            }

            if (stats == null)
            {
                Debug.LogError($"[StatDebug] {name} found no PlayerStats in the scene. See MRM-12.");
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        private void Update()
        {
            if (stats != null)
            {
                audioSource.pitch = stats.CurrentAudioPitch;
            }
        }
    }
}
