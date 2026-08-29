using System.Collections;
using MrMoonlight.Audio;
using MrMoonlight.Data;
using MrMoonlight.UI;
using MrMoonlight.VFX;
using UnityEngine;
using UnityEngine.UI;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Orchestrates MRM-17's death sequence once <see cref="PlayerStats.OnDeath"/> fires: remove
    /// control, force-close any open HUD, fall with camera shake while the shared red tint
    /// (<see cref="ScreenTint"/>) rises to its ceiling, scream, cut to black, hold the scream a
    /// moment into the silence, then land on the game over stub. Does not apply to the punji
    /// trap, which has its own sequence per the issue - nothing here subscribes to a
    /// punji-specific event. Owner: MRM-17
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(BurntwaxPlayerBridge))]
    public sealed class DeathSequence : MonoBehaviour
    {
        private const string RedTintSourceName = "Death";

        [Header("References")]
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private BurntwaxPlayerBridge playerController;

        [Header("Placeholder — Carlos fills this in")]

        /// <summary>Death scream pool, separate from the punji trap's own pool per the issue. Empty until Carlos supplies clips - see the issue's handoff note. Owner: MRM-17</summary>
        [SerializeField] private SoundPool deathScreamPool;

        [Header("Scene wiring")]
        [SerializeField] private AudioSource screamAudioSource;
        [SerializeField] private Image blackScreenImage;
        [SerializeField] private GameOverPanel gameOverPanel;

        private void Awake()
        {
            if (playerStats == null)
            {
                playerStats = GetComponent<PlayerStats>();
            }

            if (playerController == null)
            {
                playerController = GetComponent<BurntwaxPlayerBridge>();
            }

            if (screamAudioSource != null)
            {
                // The one source that should still be audible once AudioListener.pause cuts
                // everything else in CutToBlack() below. See the issue's scope §7.
                screamAudioSource.ignoreListenerPause = true;
            }
        }

        private void OnEnable() => playerStats.OnDeath += HandleDeath;

        private void OnDisable() => playerStats.OnDeath -= HandleDeath;

        private void HandleDeath() => StartCoroutine(RunSequence());

        private IEnumerator RunSequence()
        {
            playerController.DisableControl();
            playerController.ResetCameraPitch();
            HudCloseRequest.RaiseForceCloseAll();

            PlayScream();

            yield return FallAndTint();

            ScreenTint.SetRed(RedTintSourceName, 1f);
            yield return new WaitForSeconds(Tunables.I.DeathHoldBeforeBlackDuration);

            CutToBlack();

            yield return new WaitForSeconds(Tunables.I.DeathScreamTailDuration);
            if (screamAudioSource != null)
            {
                screamAudioSource.Stop();
            }

            if (gameOverPanel != null)
            {
                gameOverPanel.Show();
            }
        }

        // Camera-tilt-and-shake placeholder for the "capsule falls in a random direction" beat,
        // not a physically simulated tip-over - the player only has a CharacterController, no
        // Rigidbody/ragdoll, and building one is well beyond this issue. Flagging for Carlos:
        // if the felt result reads as too fake once he's played it, a real physical fall is a
        // follow-up issue, not a rewrite of this sequencing. Owner: MRM-17
        private IEnumerator FallAndTint()
        {
            Transform pivot = playerController.CameraPivot;
            Quaternion startRotation = pivot != null ? pivot.localRotation : Quaternion.identity;

            // Full range on both axes so the camera can genuinely end up anywhere, including
            // straight at the dirt, per the issue's own wording.
            Quaternion fallTarget = Quaternion.Euler(Random.Range(-90f, 90f), 0f, Random.Range(-90f, 90f));

            float noiseSeedX = Random.Range(0f, 100f);
            float noiseSeedY = Random.Range(100f, 200f);

            float duration = Tunables.I.DeathFallDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;

                if (pivot != null)
                {
                    Quaternion tilt = Quaternion.Slerp(startRotation, fallTarget, t);

                    float shakeTime = elapsed * Tunables.I.DeathCameraShakeFrequency;
                    float shakeX = (Mathf.PerlinNoise(noiseSeedX, shakeTime) - 0.5f) * 2f * Tunables.I.DeathCameraShakeAmplitude;
                    float shakeY = (Mathf.PerlinNoise(noiseSeedY, shakeTime) - 0.5f) * 2f * Tunables.I.DeathCameraShakeAmplitude;

                    pivot.localRotation = tilt * Quaternion.Euler(shakeX, shakeY, 0f);
                }

                ScreenTint.SetRed(RedTintSourceName, Tunables.I.DeathRedTintCurve.Evaluate(t));

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void PlayScream()
        {
            if (screamAudioSource == null || deathScreamPool == null)
            {
                return;
            }

            if (deathScreamPool.TryGetRandomClip(out AudioClip clip, out float pitch))
            {
                screamAudioSource.pitch = pitch;
                screamAudioSource.PlayOneShot(clip, deathScreamPool.Volume);
            }
        }

        private void CutToBlack()
        {
            if (blackScreenImage != null)
            {
                Color color = blackScreenImage.color;
                color.a = 1f;
                blackScreenImage.color = color;
                blackScreenImage.raycastTarget = true;
            }

            AudioListener.pause = true;
        }
    }
}
