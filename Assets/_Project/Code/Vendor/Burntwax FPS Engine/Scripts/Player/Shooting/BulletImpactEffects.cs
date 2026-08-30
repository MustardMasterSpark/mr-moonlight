using System.Collections.Generic;
using UnityEngine;

namespace Burntwax
{

    public class BulletImpactEffects : MonoBehaviour
    {

        public static BulletImpactEffects Instance;
        public List<SurfaceEffectSO> surfaceEffects;

        void Awake()
        {
            // Do ntDestroyOnLoad(gameObject);
            if (Instance != null)
            {

                Destroy(gameObject);
            }
            Instance = this;
        }

        public void ImpactVFX(string tag, Vector3 position, Quaternion rotation)
        {
            var effect = surfaceEffects.Find(effect => effect.surfaceTag == tag);
            if (effect != null)
            {
                if (effect.vfxPrefab != null)
                    Instantiate(effect.vfxPrefab, position, rotation);


            }
            else
            {
                Debug.LogWarning($"No surface effect found for tag: {tag}");
            }
        }

        public void ImpactAudio(string tag, Vector3 position, Quaternion rotation)
        {
            var effect = surfaceEffects.Find(effect => effect.surfaceTag == tag);

            if (effect != null)
            {
                if (effect.impactSounds != null && effect.impactSounds.Count > 0)
                {
                    AudioClip clip = effect.impactSounds[Random.Range(0, effect.impactSounds.Count)];
                    GameObject tempGO = new GameObject("ImpactAudio");
                    tempGO.transform.position = position;

                    AudioSource audioSource = tempGO.AddComponent<AudioSource>();

                    audioSource.clip = clip;
                    audioSource.volume = effect.volume;

                    audioSource.pitch = Random.Range(0.9f, 1.1f);

                    audioSource.spatialBlend = 1.0f;

                    audioSource.Play();

                    Destroy(tempGO, clip.length / audioSource.pitch);
                }
            }
            else
            {
                Debug.LogWarning($"No surface effect found for tag: {tag}");
            }
        }
    }

}