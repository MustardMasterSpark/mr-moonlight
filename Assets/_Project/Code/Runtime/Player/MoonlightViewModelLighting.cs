using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Lights the player's hands and weapons with their own light, apart from the world's lights.
    ///
    /// <para><b>The problem (MRM-44, Carlos 2026-09-18).</b> The first-person hands and weapons use a
    /// normal Lit shader, so every nearby light shines on them. The flashlight (a metre from the
    /// hands) and the small personal light blew them out; later, the world sun did too, because
    /// nothing shades the hands from it (a forest canopy darkens the world but not the hands).</para>
    ///
    /// <para><b>The fix is URP Light Layers, not a second camera</b> (URP lights affect every
    /// camera's objects, so a camera cannot exclude them). A light only affects a renderer when
    /// their rendering-layer masks share a bit. This component, put on the player camera:</para>
    /// <list type="number">
    ///   <item>moves every mesh under it to the <c>ViewModel</c> rendering layer and off <c>Default</c>,
    ///   so lights left on <c>Default</c> (the flashlight, the personal light, the sun, enemy lamps)
    ///   never touch them;</item>
    ///   <item>adds <c>ViewModel</c> to the weapon lights under it (muzzle flashes), so a shot still
    ///   lights the gun;</item>
    ///   <item>lets other <b>world lights</b> (Spotter lamps, fires, flares, enemy muzzle flashes) light the
    ///   hands too, by periodically adding <c>ViewModel</c> to every non-sun light within range that is not
    ///   one of the player's own (<see cref="ScanWorldLights"/>);</item>
    ///   <item>owns one directional light on <c>ViewModel</c> only, the hands' own "sun". A directional
    ///   light does not get stronger as the hands get close (a point light does, by distance squared,
    ///   and blew out the M1A's sights).</item>
    /// </list>
    ///
    /// <para><b>The hands' light follows the world sun every frame.</b> Intensity is
    /// <c>max(ViewModelLightFloor, sun.intensity * ViewModelSunFactor)</c> and the colour is the sun's,
    /// so it tracks the current time of day AND any gradual sun change (the full game will move the
    /// sun continuously, not switch presets). The floor is the night look of the hands; without it
    /// they would go black when the sun's intensity drops. Nothing here reads a preset, so a dynamic
    /// day/night system needs no change to this component.</para>
    ///
    /// <para><b>Trap:</b> URP takes a light's layers from <see cref="UniversalAdditionalLightData.renderingLayers"/>,
    /// not from <c>Light.renderingLayerMask</c>. Setting only the latter changes nothing for lighting, and a
    /// mesh with no layer in common with any light renders pure black. Always set the additional data.</para>
    ///
    /// <para>It works by rule at startup, so a weapon added to the player later is covered with no
    /// per-renderer setup. The <c>ViewModel</c> layer is a name in Project Settings > Tags and
    /// Layers > Rendering Layers (index 1, renamed from "Light Layer 1"). Full write-up:
    /// <c>Docs/viewmodel-light-layers.md</c>.</para>
    ///
    /// Owner: MRM-44.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Player/Moonlight View Model Lighting")]
    public sealed class MoonlightViewModelLighting : MonoBehaviour
    {
        /// <summary>Name of the rendering layer, as defined in Tags and Layers.</summary>
        private const string ViewModelLayerName = "ViewModel";

        [Tooltip("The directional light that lights only the hands and weapons. A child of the player camera. "
                 + "Its direction, colour and intensity are driven from the fields below every frame.")]
        [FormerlySerializedAs("fillLight")]
        [SerializeField] private Light viewModelLight;

        [Header("Live tuning (Play Mode)")]
        [Tooltip("Tick this in Play Mode, then edit the values below. Un-ticked, the light follows "
                 + "MoonlightTunables. The values are copied FROM the tunables every time the scene starts, "
                 + "and Play Mode edits are discarded on stop - tell Claude the values you like.")]
        [SerializeField] private bool liveTuning;

        [Tooltip("Fraction of the world sun's intensity the hands get. Tunable: ViewModelSunFactor.")]
        [SerializeField, Min(0f)] private float sunFactor;

        [Tooltip("Minimum brightness, i.e. the night look of the hands. Tunable: ViewModelLightFloor.")]
        [SerializeField, Min(0f)] private float lightFloor;

        [Tooltip("Degrees the light points down from straight ahead of the camera. Tunable: ViewModelLightPitch.")]
        [SerializeField, Range(-90f, 90f)] private float lightPitch;

        [Tooltip("Degrees the light is turned to the player's right (so it comes from the left). Tunable: ViewModelLightYaw.")]
        [SerializeField, Range(-180f, 180f)] private float lightYaw;

        [Tooltip("Take the world sun's colour every frame. Tunable: ViewModelLightFollowsSunColor.")]
        [SerializeField] private bool followSunColor;

        [Tooltip("Colour used when not following the sun's. Tunable: ViewModelLightColor.")]
        [SerializeField] private Color fixedColor = Color.white;

        [Tooltip("Let world lights (Spotter lamps, fires, flares...) also light the hands. The player's own lights "
                 + "never do. Tunable: ViewModelWorldLightsEnabled.")]
        [SerializeField] private bool worldLightsEnabled;

        [Tooltip("A world light only lights the hands within this many metres. Tunable: ViewModelWorldLightMaxDistance.")]
        [SerializeField, Min(0f)] private float worldLightMaxDistance;

        [Tooltip("Seconds between scans for world lights. Tunable: ViewModelWorldLightRescanSeconds.")]
        [SerializeField, Min(0.05f)] private float worldLightRescanSeconds;

        [Header("Read-only, for tuning")]
        [Tooltip("The sun the hands' light is following right now.")]
        [SerializeField] private Light followedSun;

        [Tooltip("What the hands' light is set to this frame.")]
        [SerializeField] private float currentIntensity;

        [Tooltip("How many world lights currently reach the hands.")]
        [SerializeField] private int worldLightsOnHands;

        /// <summary>How many world lights reach the hands right now (read by SessionLog v4).</summary>
        public int WorldLightsOnHands => worldLightsOnHands;

        private uint _viewModelBit;
        private Transform _playerRoot;
        private float _nextWorldLightScan;

        private void Awake()
        {
            string[] names = RenderingLayerMask.GetDefinedRenderingLayerNames();
            int index = System.Array.IndexOf(names, ViewModelLayerName);
            if (index < 0)
            {
                Debug.LogError("[MRM-44] MoonlightViewModelLighting: no rendering layer named '"
                               + ViewModelLayerName + "' (Project Settings > Tags and Layers > Rendering Layers). "
                               + "The hands will be lit by the flashlight again.", this);
                enabled = false;
                return;
            }

            _viewModelBit = 1u << index;

            MoonlightPlayerRig rig = GetComponentInParent<MoonlightPlayerRig>();
            _playerRoot = rig != null ? rig.transform : transform.root;

            MoveMeshesToViewModelLayer();
            AddViewModelToWeaponLights();
            LoadFromTunables();

            if (viewModelLight != null)
            {
                GetLightData(viewModelLight).renderingLayers = (RenderingLayerMask)_viewModelBit;
            }
        }

        private void Start()
        {
            // URP picks the "main light" (shadows, cookies) from RenderSettings.sun, or else the brightest
            // directional light. At night ours can be the brighter one, which would demote the real sun
            // and lose its shadows. Pin the sun as the main light if the scene did not.
            if (RenderSettings.sun == null)
            {
                RenderSettings.sun = FindSun();
            }
        }

        private void Update()
        {
            if (viewModelLight == null)
            {
                return;
            }

            if (!liveTuning && Time.frameCount % 64 == 0)
            {
                // Picks up edits made to the tunables asset in Play Mode; costs nothing.
                LoadFromTunables();
            }

            if (followedSun == null || !followedSun.isActiveAndEnabled)
            {
                followedSun = FindSun();
            }

            float sunIntensity = followedSun != null ? followedSun.intensity : 0f;
            currentIntensity = Mathf.Max(lightFloor, sunIntensity * sunFactor);

            viewModelLight.intensity = currentIntensity;
            viewModelLight.color = followSunColor && followedSun != null ? followedSun.color : fixedColor;
            viewModelLight.transform.localRotation = Quaternion.Euler(lightPitch, lightYaw, 0f);

            if (Time.time >= _nextWorldLightScan)
            {
                _nextWorldLightScan = Time.time + worldLightRescanSeconds;
                ScanWorldLights();
            }
        }

        /// <summary>
        /// Decides which non-sun lights reach the hands: every light that is not directional, not one of the
        /// player's own (flashlight, personal light, the hands' light, all under the player root) and within
        /// <see cref="worldLightMaxDistance"/> gets the ViewModel bit; every other one loses it. A rescan rather
        /// than a one-time setup because lamps, flares and fires spawn at runtime. The bit is only written when
        /// it changes. The player's weapon lights are set once in <see cref="AddViewModelToWeaponLights"/>.
        /// </summary>
        private void ScanWorldLights()
        {
            float maxSqr = worldLightMaxDistance * worldLightMaxDistance;
            Vector3 origin = transform.position;
            int count = 0;

            foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional || light == viewModelLight
                    || light.transform.IsChildOf(_playerRoot))
                {
                    continue;
                }

                bool reaches = worldLightsEnabled && (light.transform.position - origin).sqrMagnitude <= maxSqr;
                UniversalAdditionalLightData data = GetLightData(light);
                uint layers = data.renderingLayers.value;
                bool hasBit = (layers & _viewModelBit) != 0;
                if (reaches != hasBit)
                {
                    data.renderingLayers = (RenderingLayerMask)(reaches ? layers | _viewModelBit : layers & ~_viewModelBit);
                }

                if (reaches)
                {
                    count++;
                }
            }

            worldLightsOnHands = count;
        }

        /// <summary>Re-reads the tunables and applies them, e.g. after editing the asset during Play Mode.</summary>
        [ContextMenu("Reload values from MoonlightTunables")]
        private void ReloadFromTunables() => LoadFromTunables();

        /// <summary>
        /// The world's sun: the scene's assigned sun if there is one, otherwise the brightest
        /// directional light that is not our own.
        /// </summary>
        private Light FindSun()
        {
            Light sun = RenderSettings.sun;
            if (sun != null && sun != viewModelLight)
            {
                return sun;
            }

            Light best = null;
            foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional && light != viewModelLight
                    && (best == null || light.intensity > best.intensity))
                {
                    best = light;
                }
            }

            return best;
        }

        /// <summary>
        /// Puts every mesh under this transform on the ViewModel layer and takes it off Default.
        /// Other bits are kept (some vendor meshes carry an extra one). Particle systems are left
        /// alone: they do not react to lights.
        /// </summary>
        private void MoveMeshesToViewModelLayer()
        {
            foreach (Renderer meshRenderer in GetComponentsInChildren<Renderer>(true))
            {
                if (meshRenderer is MeshRenderer || meshRenderer is SkinnedMeshRenderer)
                {
                    meshRenderer.renderingLayerMask = (meshRenderer.renderingLayerMask & ~1u) | _viewModelBit;
                }
            }
        }

        /// <summary>
        /// Lets the weapons' own lights (muzzle flashes) keep lighting the weapon. Only lights nested
        /// deeper than this transform's direct children count: the direct children are the player's own
        /// lights (flashlight, personal light, the hands' light), which are set explicitly elsewhere.
        /// </summary>
        private void AddViewModelToWeaponLights()
        {
            foreach (Light light in GetComponentsInChildren<Light>(true))
            {
                if (light.transform.parent == transform)
                {
                    continue;
                }

                UniversalAdditionalLightData data = GetLightData(light);
                data.renderingLayers = (RenderingLayerMask)(data.renderingLayers.value | _viewModelBit);
            }
        }

        private static UniversalAdditionalLightData GetLightData(Light light)
        {
            UniversalAdditionalLightData data = light.GetComponent<UniversalAdditionalLightData>();
            return data != null ? data : light.gameObject.AddComponent<UniversalAdditionalLightData>();
        }

        private void LoadFromTunables()
        {
            MoonlightTunables t = Tunables.I;
            sunFactor = t.ViewModelSunFactor;
            lightFloor = t.ViewModelLightFloor;
            lightPitch = t.ViewModelLightPitch;
            lightYaw = t.ViewModelLightYaw;
            followSunColor = t.ViewModelLightFollowsSunColor;
            fixedColor = t.ViewModelLightColor;
            worldLightsEnabled = t.ViewModelWorldLightsEnabled;
            worldLightMaxDistance = t.ViewModelWorldLightMaxDistance;
            worldLightRescanSeconds = t.ViewModelWorldLightRescanSeconds;
        }
    }
}
