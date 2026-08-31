using System;
using UnityEngine;
using Random = UnityEngine.Random;


namespace Burntwax
{

    [CreateAssetMenu(fileName = "Audio Config", menuName = "Guns/Audio Config", order = 5)]
    public class AudioConfig : ScriptableObject
    {
        [Range(0, 1f)] public float volume = 1f;
        public AudioClip[] fireClips;
        public AudioClip emptyClip;
        public AudioClip reloadClip;
        public AudioClip lastBulletClip;

        public void PlayShootingClip(AudioSource audioSource, bool isLastBullet = false)
        {
            if (isLastBullet && lastBulletClip != null)
            {
                audioSource.PlayOneShot(lastBulletClip, volume);
            }
            else
            {
                audioSource.PlayOneShot(fireClips[Random.Range(0, fireClips.Length)], volume);
            }
        }

        public void PlayEmptyClip(AudioSource audioSource)
        {
            // Debug.Log("PlayEmptyClip");
            if (emptyClip != null)
            {
                audioSource.PlayOneShot(emptyClip, volume);
            }
        }

        public void PlayReloadClip(AudioSource audioSource)
        {
            if (reloadClip != null)
            {
                audioSource.PlayOneShot(reloadClip, volume);
            }
        }

    }

}
