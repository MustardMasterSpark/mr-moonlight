using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.Audio;

namespace MrMoonlight.Audio
{
    /// <summary>
    /// Converts a UI slider's 0-1 linear value to the logarithmic decibel scale an
    /// <see cref="AudioMixer"/> exposed parameter actually wants, and writes it. The one place
    /// this math happens, so the Settings panel (MRM-18) never hand-rolls a log10 conversion
    /// itself. Owner: MRM-18
    /// </summary>
    public static class AudioMixerVolume
    {
        /// <summary>Applies a 0-1 linear volume to an exposed mixer parameter, in decibels. 0 maps to <see cref="MoonlightTunables.MixerMuteDecibels"/> (true 0 has no finite dB equivalent) rather than -infinity.</summary>
        public static void Apply(AudioMixer mixer, string exposedParameterName, float linear01)
        {
            if (mixer == null)
            {
                Debug.LogError($"[Audio] No mixer assigned when applying \"{exposedParameterName}\". See MRM-18.");
                return;
            }

            float clamped = Mathf.Clamp01(linear01);
            float decibels = clamped <= 0.0001f
                ? Tunables.I.MixerMuteDecibels
                : Mathf.Log10(clamped) * 20f;

            mixer.SetFloat(exposedParameterName, decibels);
        }
    }
}
