using System.Collections;
using MrMoonlight.Data;
using UnityEngine;

namespace MrMoonlight.Audio
{
    /// <summary>
    /// Loops the island's score (MUS_DownfallTheme) with a slow fade-in on scene start — Carlos's
    /// ask for the island demo wrap-up audio pass (2026-09-10): "not very aggressive at the
    /// beginning." Restarting the level reloads the whole scene (see
    /// <c>MrMoonlight.UI.GameOverPanel.OnRestartClicked</c>), so this same Start path covers both
    /// "start" and "restart" with nothing extra to wire.
    ///
    /// Fades <see cref="AudioSource.volume"/> itself, independent of the IslandMusic mixer group's
    /// exposed volume parameter (the debug slider overlay) — they multiply, so the fade-in plays out
    /// under whatever level Carlos has set. Owner: island-demo-wrapup.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Mr. Moonlight/Audio/Island Music Controller")]
    public sealed class IslandMusicController : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip theme;

        private void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
        }

        private void Start()
        {
            if (theme == null || source == null) return;

            source.clip = theme;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            source.Play();

            StartCoroutine(FadeIn());
        }

        private IEnumerator FadeIn()
        {
            float duration = Mathf.Max(0.01f, Tunables.I.IslandMusicFadeInDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            source.volume = 1f;
        }
    }
}
