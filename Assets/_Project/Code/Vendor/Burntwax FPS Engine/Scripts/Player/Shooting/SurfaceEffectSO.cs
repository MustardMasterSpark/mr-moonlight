using System.Collections.Generic;
using UnityEngine;

namespace Burntwax
{

    [CreateAssetMenu(fileName = "SurfaceEffect", menuName = "Effects/SurfaceEffect", order = 1)]
    public class SurfaceEffectSO : ScriptableObject
    {
        public string surfaceTag;
        public GameObject vfxPrefab;
        public List<AudioClip> impactSounds;

        [Header("Audio Settings")]
        [Tooltip("Impact sound volume")]
        [Range(0f, 1f)] public float volume = 1f;
    }


}