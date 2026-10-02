using System.Collections;
using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace MrMoonlight.World
{
    /// <summary>
    /// Cross-fades the scene's skybox between two <c>Skybox/Cubemap</c> skies by a 0-1 value that the caller
    /// sets every frame (distance, time of day, a story beat, anything). Uses the blend shader from the
    /// Skybox Blender asset (AST-054, <c>Skybox/SkyboxBlender</c>, tracked copy in
    /// <c>Code/Vendor/AST-054 Skybox Blender/</c>), but NOT its <c>SkyboxBlender</c> component: that one
    /// blends on a timer, and edits its shared material asset in place, so Play Mode changes leak into the
    /// project. This works on a private copy of <see cref="blendTemplate"/> made at startup.
    ///
    /// <para><b>Fading from a sky that is not a cubemap.</b> Unity's default sky is procedural and has no
    /// texture to fade from, so <see cref="CaptureCurrentSky"/> snapshots whatever sky is showing into a
    /// cube render texture once (a throwaway camera with culling mask 0, <c>Camera.RenderToCubemap</c>).</para>
    ///
    /// <para>Nothing is lit by this: ambient light and reflections are not updated when the sky changes.
    /// That is the next step of MRM-86 (lighting and fog).</para>
    ///
    /// Owner: MRM-86.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/World/Sky Blender")]
    public sealed class SkyBlender : MonoBehaviour
    {
        private static readonly int TexId = Shader.PropertyToID("_Tex");
        private static readonly int Tex2Id = Shader.PropertyToID("_Tex2");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int Tint2Id = Shader.PropertyToID("_Tint2");
        private static readonly int BlendId = Shader.PropertyToID("_BlendCubemaps");

        /// <summary>Tint that makes Skybox/Cubemap-style shaders show a texture unchanged (0.5 x colour-space double = 1).</summary>
        private static readonly Color NeutralTint = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Tooltip("Material using the Skybox/SkyboxBlender shader (M_Sky_Blend). Never edited: a copy is made at startup.")]
        [SerializeField] private Material blendTemplate;

        [Header("Read-only, for tuning")]
        [Tooltip("Current blend, 0 = the FROM sky, 1 = the TO sky.")]
        [SerializeField, Range(0f, 1f)] private float blend;

        [Tooltip("Sky being faded from.")]
        [SerializeField] private string fromSky;

        [Tooltip("Sky being faded to.")]
        [SerializeField] private string toSky;

        private Material _blendMaterial;
        private Material _originalSkybox;
        private RenderTexture _capturedSky;

        /// <summary>Current blend, 0 = the FROM sky, 1 = the TO sky.</summary>
        public float Blend => blend;

        /// <summary>The snapshot taken by <see cref="CaptureCurrentSky"/>, or null if none was taken or it failed.</summary>
        public Texture CapturedSky => _capturedSky;

        private void Awake()
        {
            _originalSkybox = RenderSettings.skybox;
            if (blendTemplate == null)
            {
                Debug.LogError("[MRM-86] SkyBlender: no blend material assigned (M_Sky_Blend, shader Skybox/SkyboxBlender).", this);
                enabled = false;
                return;
            }

            _blendMaterial = new Material(blendTemplate) { name = blendTemplate.name + " (runtime)" };
            _blendMaterial.SetFloat(BlendId, 0f);
        }

        private void OnDestroy()
        {
            if (_blendMaterial != null && RenderSettings.skybox == _blendMaterial)
            {
                RenderSettings.skybox = _originalSkybox;
            }

            if (_blendMaterial != null)
            {
                Destroy(_blendMaterial);
            }

            if (_capturedSky != null)
            {
                _capturedSky.Release();
                Destroy(_capturedSky);
            }
        }

        /// <summary>
        /// Snapshots the sky currently showing (e.g. Unity's default procedural sky) into a cube texture, so
        /// it can be used as a FROM sky with <see cref="SetFromCaptured"/>. Run as a coroutine; takes one
        /// frame. On failure <see cref="CapturedSky"/> stays null and a warning is logged.
        /// </summary>
        public IEnumerator CaptureCurrentSky()
        {
            // Wait a frame so the scene's sky and sun are fully set up.
            yield return null;

            // A throwaway camera that sees nothing but the sky. (A realtime reflection probe would be the
            // obvious tool, but realtime probes are switched off in this project's Quality settings.)
            var cameraObject = new GameObject("SkyBlender capture camera") { hideFlags = HideFlags.DontSave };
            cameraObject.transform.position = transform.position;
            Camera captureCamera = cameraObject.AddComponent<Camera>();
            captureCamera.enabled = false;
            captureCamera.cullingMask = 0;
            captureCamera.clearFlags = CameraClearFlags.Skybox;

            int size = Mathf.ClosestPowerOfTwo(Mathf.Max(16, Tunables.I.SkyBlendCaptureResolution));
            var captured = new RenderTexture(size, size, 16, RenderTextureFormat.ARGBHalf)
            {
                name = "SkyBlender captured sky",
                dimension = TextureDimension.Cube,
            };
            captured.Create();

            if (captureCamera.RenderToCubemap(captured, 63))
            {
                _capturedSky = captured;
            }
            else
            {
                captured.Release();
                Destroy(captured);
                Debug.LogWarning("[MRM-86] SkyBlender: could not capture the starting sky; the first blend will "
                                 + "fade from grey instead.", this);
            }

            Destroy(cameraObject);
        }

        /// <summary>Fades FROM the snapshot taken by <see cref="CaptureCurrentSky"/> (grey if there is none).</summary>
        public void SetFromCaptured()
        {
            if (_blendMaterial == null) return;
            _blendMaterial.SetTexture(TexId, _capturedSky);
            _blendMaterial.SetColor(TintId, NeutralTint);
            fromSky = _originalSkybox != null ? _originalSkybox.name + " (captured)" : "captured sky";
        }

        /// <summary>Fades FROM this Skybox/Cubemap material.</summary>
        public void SetFrom(Material sky)
        {
            if (_blendMaterial == null || !IsCubemapSky(sky)) return;
            _blendMaterial.SetTexture(TexId, sky.GetTexture(TexId));
            _blendMaterial.SetColor(TintId, sky.HasProperty(TintId) ? sky.GetColor(TintId) : NeutralTint);
            fromSky = sky.name;
        }

        /// <summary>Fades TO this Skybox/Cubemap material.</summary>
        public void SetTo(Material sky)
        {
            if (_blendMaterial == null || !IsCubemapSky(sky)) return;
            _blendMaterial.SetTexture(Tex2Id, sky.GetTexture(TexId));
            _blendMaterial.SetColor(Tint2Id, sky.HasProperty(TintId) ? sky.GetColor(TintId) : NeutralTint);
            toSky = sky.name;
        }

        /// <summary>
        /// Sets the blend, 0 = the FROM sky, 1 = the TO sky, and makes the blend material the scene's skybox.
        /// Cheap enough to call every frame (one material float).
        /// </summary>
        public void SetBlend(float value)
        {
            if (_blendMaterial == null) return;
            blend = Mathf.Clamp01(value);
            _blendMaterial.SetFloat(BlendId, blend);
            if (RenderSettings.skybox != _blendMaterial)
            {
                RenderSettings.skybox = _blendMaterial;
            }
        }

        private bool IsCubemapSky(Material sky)
        {
            if (sky != null && sky.HasProperty(TexId) && sky.GetTexture(TexId) is Cubemap)
            {
                return true;
            }

            Debug.LogError("[MRM-86] SkyBlender: '" + (sky != null ? sky.name : "null")
                           + "' is not a Skybox/Cubemap sky (needs a cubemap in _Tex).", this);
            return false;
        }
    }
}
