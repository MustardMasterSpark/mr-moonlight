// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace MA.Flora
{
    /// <summary>
    /// User-facing presets for terrain detail streaming behavior.
    /// </summary>
    public enum FloraDetailStreamingMode
    {
        /// <summary>
        /// Build visible details as quickly as possible with no per-frame streaming limits.
        /// </summary>
        Immediate = 0,

        /// <summary>
        /// Stream visible terrain details over multiple frames using the responsiveness slider.
        /// </summary>
        Streamed = 1,

        /// <summary>
        /// Advanced mode that exposes Flora's internal streaming budgets directly.
        /// </summary>
        Custom = 3,
    }

    /// <summary>
    /// A global singleton that provides scene-wide settings for the Flora system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This component is intended to be unique per scene (enforced via a singleton pattern). Attempting to create more than one instance
    /// in the same scene will result in the duplicate being disabled.
    /// </para>
    /// <para>
    /// Note that some advanced or contextual settings can be controlled by the volume system (e.g. <see cref="FloraRenderSettings"/>),
    /// which derive from <see cref="VolumeComponent"/>. If present, these volume-based settings may override or supplement the
    /// defaults established here.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent] [ExecuteAlways]
    [AddComponentMenu("Flora/Flora Scene Settings")]
    [Icon("Packages/com.ma.flora/Editor/EditorResources/Icons/SceneSettings Icon.png")] [SceneIcon]
    [HelpURL("https://flora.magneticarcade.com/scripts/scene-settings")]
    public class FloraSceneSettings : MonoBehaviour
    {
        private enum Version : int
        {
            Initial = 0,
            AddedDetailStreamingControls = 1,
        }

        /// <summary>
        /// The active, global <see cref="FloraSceneSettings"/> instance in the current scene, if any.
        /// </summary>
        public static FloraSceneSettings Instance { get; private set; }

        /// <summary>
        /// Retrieves the active <see cref="FloraSceneSettings"/> instance or creates a new one if none exists.
        /// </summary>
        /// <returns>The existing or newly created <see cref="FloraSceneSettings"/>.</returns>
        public static FloraSceneSettings GetOrCreate()
        {
            if (Instance == null)
            {
                Instance = FindAnyObjectByType<FloraSceneSettings>();
                if (Instance == null)
                {
                    var go = new GameObject("Flora Scene Settings");
                    Instance = go.AddComponent<FloraSceneSettings>();
                }
            }
            return Instance;
        }

        #region Rendering

        /// <summary>
        /// Master toggle for Flora rendering in the scene.
        /// </summary>
        public bool EnableRendering = true;

        /// <summary>
        /// Allows Flora to cull instances based on density settings.
        /// </summary>
        public bool AllowDensityCulling = true;

        /// <summary>
        /// Allows the use of GPU occlusion culling for instances.
        /// </summary>
        /// <remarks>
        /// When enabled, instances can be occluded by other objects in the scene using the depth buffer.
        /// </remarks>
        public bool AllowGPUOcclusionCulling = true;

        /// <summary>
        /// If enabled, Flora will use the <see cref="MotionVectorGenerationMode"/> setting from each <see cref="MeshRenderer"/> to determine the motion vector generation mode for instances.
        /// </summary>
        /// <remarks>
        /// When enabled instances will allocate an additional previous local to world matrix for per-object motion vectors.
        /// These instances will also be updated every frame they move.
        /// </remarks>
        public bool AllowPerObjectMotionVectors = false;

        /// <summary>
        /// Allows the legacy light probe system to be used for instanced rendering.
        /// </summary>
        /// <remarks>
        /// When enabled, instances can receive indirect lighting from legacy light probes.
        /// If you plan to use the newer APV system or only lightmaps, this can be disabled.
        /// </remarks>
        public bool AllowLegacyLightProbes;

        #endregion

        #region Terrain

        /// <summary>
        /// Determines whether the <see cref="FloraSystem"/> renders terrain-based foliage (e.g. trees or details).
        /// </summary>
        public bool EnableTerrainFoliage = true;

        /// <summary>
        /// When enabled, any <see cref="Terrain"/> in the scene will automatically register with the <see cref="FloraSystem"/> to render terrain foliage.
        /// </summary>
        public bool AutoRegisterTerrains = true;

        /// <summary>
        /// Allows the use of per-tree motion vectors for terrain tree instances.
        /// <see cref="AllowPerObjectMotionVectors"/> must also be enabled for this to take effect.
        /// </summary>
        /// <seealso cref="AllowPerObjectMotionVectors"/>
        public bool AllowPerTreeMotionVectors = false;

        /// <summary>
        /// Allows the use of per-tree light probes for terrain tree instances.
        /// When enabled, each terrain tree instance will sample the light probes at its position.
        /// This provides more accurate lighting for individual trees, but increases memory usage.
        /// </summary>
        public bool AllowPerTreeLightProbes = false;

        /// <summary>
        /// Allows the use of per-tree motion vectors for terrain detail instances.
        /// <see cref="AllowPerObjectMotionVectors"/> must also be enabled for this to take effect.
        /// </summary>
        /// <seealso cref="AllowPerObjectMotionVectors"/>
        public bool AllowPerDetailMotionVectors = false;

        /// <summary>
        /// Allows the use of per-detail light probes for terrain detail instances.
        /// When enabled, each terrain detail instance will sample the light probes at its position at an additional memory cost.
        /// </summary>
        /// <remarks>
        /// Can dramatically increase memory usage if you have a lot of terrain details, use with caution.
        /// </remarks>
        public bool AllowPerDetailLightProbes = false;

        /// <summary>
        /// Controls the overall terrain detail streaming behavior.
        /// </summary>
        public FloraDetailStreamingMode DetailStreamingMode = FloraDetailStreamingMode.Streamed;

        /// <summary>
        /// Controls how quickly terrain details should catch up once they become visible.
        /// </summary>
        [Range(0f, 1f)] public float DetailStreamingResponsiveness = 0.5f;

        /// <summary>
        /// Advanced override for how many patch-layer rebuilds Flora may schedule per frame in custom streaming mode.
        /// A value of 0 means unbounded.
        /// </summary>
        [Min(0)] public int CustomDetailPatchLayerBudgetPerFrame = 12;

        /// <summary>
        /// Advanced override for how many structural detail instance operations Flora may apply per frame in custom streaming mode.
        /// A value of 0 means unbounded.
        /// </summary>
        [Min(0)] public int CustomDetailStructuralInstanceBudgetPerFrame = 6000;

        /// <summary>
        /// How long to keep terrain details alive after they leave the active range of a camera.
        /// This acts as a grace period to prevent rapid load/unload thrash near the detail distance boundary.
        /// </summary>
        /// <see cref="Terrain.detailObjectDistance"/>
        [Min(0f)] public float DetailUnloadHysteresisSeconds = 0.5f;

        /// <summary>
        /// How long to keep a detail patch alive after it leaves the active range of a camera (in frames).
        /// </summary>
        /// <see cref="Terrain.detailObjectDistance"/>
        [HideInInspector]
        [Obsolete("Use DetailStreamingMode, DetailStreamingResponsiveness, and DetailUnloadHysteresisSeconds instead.")]
        [Min(0)] public int DetailUnloadDelayFrames = 30;

        /// <summary>
        /// The maximum number of detail patches to unload per frame after they have exceeded the unload delay.
        /// </summary>
        /// <see cref="Terrain.detailObjectDistance"/>
        /// <see cref="TerrainData.detailPatchCount"/>
        [HideInInspector]
        [Obsolete("Use DetailStreamingMode, DetailStreamingResponsiveness, and DetailUnloadHysteresisSeconds instead.")]
        [Min(0)] public int DetailUnloadBudgetPerFrame = 0;

        /// <summary>
        /// The maximum number of detail patches to stream in per frame within range of a camera.
        /// </summary>
        /// <see cref="Terrain.detailObjectDistance"/>
        /// <see cref="TerrainData.detailPatchCount"/>
        [HideInInspector]
        [Obsolete("Use DetailStreamingMode, DetailStreamingResponsiveness, and DetailUnloadHysteresisSeconds instead.")]
        [Min(0)] public int DetailLoadBudgetPerFrame = 0;

        #endregion

        /// <summary>
        /// Utility function to retrieve the global <see cref="FloraRenderSettings"/> from the current SRP pipeline.
        /// </summary>
        /// <returns>An instance of<see cref="FloraRenderSettings"/>, applied before any global volumes in the scene.</returns>
        public FloraRenderSettings GetGlobalRenderSettings() => GetGlobalVolumeSettings<FloraRenderSettings>();

        /// <summary>
        /// Retrieves the global <see cref="FloraDensitySettings"/> from the current SRP pipeline.
        /// </summary>
        /// <returns>An instance of <see cref="FloraDensitySettings"/>, applied before any global volumes in the scene.</returns>
        public FloraDensitySettings GetGlobalDensitySettings() => GetGlobalVolumeSettings<FloraDensitySettings>();

        /// <summary>
        /// Helper function to retrieve a global volume component from the current SRP pipeline.
        /// </summary>
        /// <typeparam name="T">The type of volume component to retrieve.</typeparam>
        /// <returns>An instance of the specified volume component type.</returns>
        public T GetGlobalVolumeSettings<T>() where T : VolumeComponent
        {
            if (Instance == null)
                return null;

            VolumeProfile globalProfile = VolumeManager.instance.qualityDefaultProfile;
            if (globalProfile == null)
            {
                globalProfile = m_VolumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                VolumeManager.instance.SetQualityDefaultProfile(m_VolumeProfile);
            }

            if (!globalProfile.TryGet(out T volumeSettings))
                volumeSettings = globalProfile.Add<T>(false);

            return volumeSettings;
        }

        #region Obsolete

        [Obsolete("Flora now always uses the BatchRendererGroup culling pipeline. This setting has no effect.")]
        public bool OverrideCullingPipeline;

        [Obsolete("Flora now always uses the BatchRendererGroup culling pipeline. This setting has no effect.")]
        public FloraCullingPipeline CullingPipelineOverride = FloraCullingPipeline.BatchRendererGroup;

        [Obsolete("Flora now always uses BatchRendererGroup and MainLight has no effect.")]
        public Light MainLightOverride;

        [Obsolete("Use the 'Tree Distance' setting on the Terrain component instead.")]
        public bool OverrideTerrainTreeDistance = false;

        [Obsolete("Use the 'Tree Distance' setting on the Terrain component instead.")]
        public float TerrainTreeDistance = 1000f;

        [Obsolete("Use the 'Detail Density' setting on the Terrain component instead.")]
        public bool OverrideTerrainDetailDistance = false;

        [Obsolete("Use the 'Detail Distance' setting on the Terrain component instead.")]
        public float TerrainDetailDistance = 150f;

        #endregion

        [NonSerialized] private VolumeProfile m_VolumeProfile;
        [SerializeField, HideInInspector] private bool m_NewlyCreated = true;
        [SerializeField, HideInInspector] private Version m_Version = 0;

        private static float DecodeResponsiveness(float normalizedBudget)
        {
            if (normalizedBudget <= 0f)
                return 0f;

            return Mathf.Clamp01(Mathf.Pow(normalizedBudget, 1f / 0.75f));
        }

        private static void DeriveDetailStreamingControlsFromLegacy(
            int legacyLoadBudgetPerFrame,
            int legacyUnloadBudgetPerFrame,
            int legacyUnloadDelayFrames,
            out FloraDetailStreamingMode mode,
            out float responsiveness,
            out float hysteresisSeconds)
        {
            hysteresisSeconds = Mathf.Max(0f, legacyUnloadDelayFrames / 60f);

            int effectiveBudget = legacyLoadBudgetPerFrame > 0 ? legacyLoadBudgetPerFrame : legacyUnloadBudgetPerFrame;
            if (legacyLoadBudgetPerFrame == 0 && legacyUnloadBudgetPerFrame == 0)
            {
                mode = FloraDetailStreamingMode.Immediate;
                responsiveness = 1f;
            }
            else
            {
                mode = FloraDetailStreamingMode.Streamed;
                float normalizedBudget = Mathf.InverseLerp(1f, 24f, Mathf.Clamp(effectiveBudget, 1, 24));
                responsiveness = DecodeResponsiveness(normalizedBudget);
            }
        }

        private bool MigrateSettingsIfNeeded()
        {
            if (m_NewlyCreated)
            {
                m_Version = Version.AddedDetailStreamingControls;
                return false;
            }

            bool migrated = false;
            if (m_Version < Version.AddedDetailStreamingControls)
            {
#pragma warning disable CS0618
                DeriveDetailStreamingControlsFromLegacy(
                    DetailLoadBudgetPerFrame,
                    DetailUnloadBudgetPerFrame,
                    DetailUnloadDelayFrames,
                    out FloraDetailStreamingMode derivedMode,
                    out float derivedResponsiveness,
                    out float derivedHysteresisSeconds);
#pragma warning restore CS0618

                DetailStreamingMode = derivedMode;
                DetailStreamingResponsiveness = derivedResponsiveness;
                DetailUnloadHysteresisSeconds = derivedHysteresisSeconds;
                migrated = true;
            }

            m_Version = Version.AddedDetailStreamingControls;
            return migrated;
        }

        private void OnEnable()
        {
            if (!gameObject.scene.IsValid())
                return; // Prefab stage

#if UNITY_EDITOR
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            var isInPrefabStage = prefabStage != null && gameObject.scene == prefabStage.scene;
            if (isInPrefabStage)
            {
                // Don't register in prefab edit mode.
                return;
            }
#endif

            if (Instance == null)
            {
                Instance = this;

                if (m_NewlyCreated)
                {
                    m_NewlyCreated = false;
                    m_Version = Version.AddedDetailStreamingControls;

                    if (GraphicsSettings.TryGetRenderPipelineSettings<FloraRuntimeSettings>(out var runtimeSettings))
                    {
                        AllowGPUOcclusionCulling = !runtimeSettings.DisableGPUOcclusionCulling;
                    }
                }

                if (MigrateSettingsIfNeeded())
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
                }
            }
            else if (Instance != this)
            {
                Debug.LogError($"Multiple {nameof(FloraSceneSettings)} components detected. Duplicate instance will be disabled.", this);
                enabled = false;
            }

            FloraSystem.InitializeIfNeeded();
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
