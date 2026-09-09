using UnityEngine;

namespace MrMoonlight.World
{
    /// <summary>
    /// Keeps the InfiniCloud shader's "_midYValue" property matched to this object's live world Y.
    /// The shader computes a vertical falloff band as (_midYValue - worldPositionY) - if the object
    /// is repositioned without also updating this property, the band goes stale and the falloff math
    /// goes negative, making the clouds vanish entirely rather than just fading oddly. The vendor's
    /// own script kept this in sync via its own per-frame draw call, which we replaced with a plain
    /// always-on Renderer for compatibility with Scene View/reflection cameras - this replaces just
    /// that one piece of upkeep.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("Mr. Moonlight/World/Cloud Height Sync")]
    public sealed class CloudHeightSync : MonoBehaviour
    {
        [Tooltip("Vertical thickness of the cloud layer's falloff band, in world units - matches the shader's own 'cloudHeight' concept.")]
        [SerializeField] private float cloudHeight = 6f;

        private Renderer _renderer;

        private static readonly int MidYValueId = Shader.PropertyToID("_midYValue");
        private static readonly int CloudHeightId = Shader.PropertyToID("_cloudHeight");

        private void OnEnable()
        {
            _renderer = GetComponent<Renderer>();
        }

        private void Update()
        {
            if (_renderer == null) return;

            Material material = _renderer.sharedMaterial;
            if (material == null) return;

            material.SetFloat(MidYValueId, transform.position.y);
            material.SetFloat(CloudHeightId, cloudHeight);
        }
    }
}
