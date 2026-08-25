// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using MA.InternalBridge;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using UnityObject = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MA.Flora
{
    /// <summary>
    /// The type of render pipeline that Flora is currently using.
    /// </summary>
    /// <remarks>
    /// Flora attempts to detect the active pipeline at runtime, switching between <see cref="Builtin"/>, <see cref="Universal"/>, and <see cref="HighDefinition"/>.
    /// If none of these match or if a custom SRP is in use, <see cref="Custom"/> is assigned.
    /// </remarks>
    public enum FloraRenderPipelineType
    {
        /// <summary>No known pipeline (not yet detected).</summary>
        Unknown,
        /// <summary>The built-in pipeline.</summary>
        Builtin,
        /// <summary>The Universal Render Pipeline (URP).</summary>
        Universal,
        /// <summary>The High Definition Render Pipeline (HDRP).</summary>
        HighDefinition,
        /// <summary>A custom or unsupported render pipeline.</summary>
        Custom,
    }

    /// <summary>
    /// A bitmask representing the type of instances to be filtered or queried.
    /// </summary>
    [Flags]
    public enum FloraInstanceTypeMask : uint
    {
        /// <summary>
        /// All non-terrain instances.
        /// </summary>
        Default = 0,
        /// <summary>
        /// Terrain tree instances.
        /// </summary>
        TerrainTree = InstanceTag.TerrainTree,
        /// <summary>
        /// Terrain detail instances.
        /// </summary>
        TerrainDetail = InstanceTag.TerrainDetail,
        /// <summary>
        /// All instance types, including terrain trees and details.
        /// </summary>
        Any = Default | TerrainTree | TerrainDetail
    }

    /// <summary>
    /// A filter structure used to specify which instances to include in queries.
    /// </summary>
    public partial struct FloraInstanceFilter
    {
        /// <summary>
        /// Default filter that matches all instance types and layers, with no owner, identity-source, or render-source filters.
        /// </summary>
        public static FloraInstanceFilter Any => new()
        {
            TypeMask                 = FloraInstanceTypeMask.Any,
            LayerMask                = -1, // All layers
            PrefabGameObjectID       = EntityId.None, // Compatibility alias for IdentitySourceGameObjectID
            RenderSourceGameObjectID = EntityId.None,
            OwnerGameObjectID        = EntityId.None, // No owner filter
        };

        /// <summary>
        /// Which types of instances to include in the filter.
        /// </summary>
        public FloraInstanceTypeMask TypeMask;

        /// <summary>
        /// Which layers to include in the filter.
        /// </summary>
        public LayerMask LayerMask;

        /// <summary>
        /// Compatibility alias for the identity source <see cref="GameObject"/> ID to filter instances by, or <see cref="EntityId.None"/> to ignore.
        /// </summary>
        public EntityId PrefabGameObjectID;

        /// <summary>
        /// The identity source <see cref="GameObject"/> ID to filter instances by, or <see cref="EntityId.None"/> to ignore.
        /// </summary>
        public EntityId IdentitySourceGameObjectID
        {
            readonly get => PrefabGameObjectID;
            set => PrefabGameObjectID = value;
        }

        /// <summary>
        /// The render source <see cref="GameObject"/> ID to filter instances by, or <see cref="EntityId.None"/> to ignore.
        /// </summary>
        public EntityId RenderSourceGameObjectID;

        /// <summary>
        /// The GameObject ID of the owner scene object (terrain, container, or renderer host) to filter instances by.
        /// </summary>
        /// <remarks>
        /// Use the <see cref="UnityObject.GetEntityId()"/> of the scene object that owns the instance registration path.
        /// </remarks>
        public EntityId OwnerGameObjectID;

        /// <summary>
        /// Creates a new instance of the <see cref="FloraInstanceFilter"/> struct with the specified parameters.
        /// </summary>
        public FloraInstanceFilter(
            FloraInstanceTypeMask typeMask,
            LayerMask layerMask,
            EntityId prefabGameObjectID = default,
            EntityId authoringGameObjectID = default,
            EntityId renderSourceGameObjectID = default)
        {
            TypeMask = typeMask;
            LayerMask = layerMask;
            PrefabGameObjectID = prefabGameObjectID;
            OwnerGameObjectID = authoringGameObjectID;
            RenderSourceGameObjectID = renderSourceGameObjectID;
        }

        /// <summary>
        /// Creates a filter that matches all tree instances.
        /// </summary>
        public static FloraInstanceFilter ByTrees(Terrain terrain = null)
        {
            return new FloraInstanceFilter
            {
                TypeMask = FloraInstanceTypeMask.TerrainTree,
                LayerMask = -1, // All layers
                OwnerGameObjectID = terrain ? terrain.gameObject.GetEntityId() : EntityId.None,
                PrefabGameObjectID = EntityId.None, // No prefab filter
                RenderSourceGameObjectID = EntityId.None,
            };
        }

        /// <summary>
        /// Creates a filter that matches all detail instances.
        /// </summary>
        public static FloraInstanceFilter ByDetails(Terrain terrain = null)
        {
            return new FloraInstanceFilter
            {
                TypeMask = FloraInstanceTypeMask.TerrainDetail,
                LayerMask = -1, // All layers
                OwnerGameObjectID = terrain ? terrain.gameObject.GetEntityId() : EntityId.None,
                PrefabGameObjectID = EntityId.None, // No prefab filter
                RenderSourceGameObjectID = EntityId.None,
            };
        }

        /// <summary>
        /// Creates a filter that matches all instances a set of layers.
        /// </summary>
        public static FloraInstanceFilter ByLayerMask(LayerMask layerMask)
        {
            return new FloraInstanceFilter
            {
                TypeMask = FloraInstanceTypeMask.Any,
                LayerMask = layerMask,
                RenderSourceGameObjectID = EntityId.None,
            };
        }

        /// <summary>
        /// Creates a filter that matches all instances owned by a specific scene object.
        /// </summary>
        public static FloraInstanceFilter ByOwner(GameObject owner)
        {
            return new FloraInstanceFilter
            {
                TypeMask = FloraInstanceTypeMask.Any,
                LayerMask = -1,
                OwnerGameObjectID = owner ? owner.GetEntityId() : EntityId.None,
                RenderSourceGameObjectID = EntityId.None,
            };
        }

        /// <summary>
        /// Creates a filter that matches all instances owned by a specific parent transform.
        /// </summary>
        public static FloraInstanceFilter ByOwner(Transform owner)
            => ByOwner(owner ? owner.gameObject : null);

        /// <summary>
        /// Creates a filter that matches all instances with a specific identity source.
        /// </summary>
        public static FloraInstanceFilter ByIdentitySource(GameObject identitySource)
        {
            return new FloraInstanceFilter
            {
                TypeMask = FloraInstanceTypeMask.Any,
                LayerMask = -1,
                IdentitySourceGameObjectID = identitySource ? identitySource.GetEntityId() : EntityId.None,
                RenderSourceGameObjectID = EntityId.None,
            };
        }

        /// <summary>
        /// Creates a filter that matches all instances with a specific render source.
        /// </summary>
        public static FloraInstanceFilter ByRenderSource(GameObject renderSource)
        {
            return new FloraInstanceFilter
            {
                TypeMask = FloraInstanceTypeMask.Any,
                LayerMask = -1,
                RenderSourceGameObjectID = renderSource ? renderSource.GetEntityId() : EntityId.None,
            };
        }

    }

    /// <summary>
    /// A global system responsible for managing all Flora instance management and rendering in Unity.
    /// </summary>
    /// <remarks>
    /// This class orchestrates culling, rendering, and data management for all instances.
    /// </remarks>
    [HelpURL("https://flora.magneticarcade.com/scripts/system")]
    public sealed partial class FloraSystem
    {
        /// <summary>
        /// The singleton instance of the <see cref="FloraSystem"/>, or null if it has not been initialized.
        /// </summary>
        public static FloraSystem Instance { get; private set; }

        /// <summary>
        /// Indicates whether the <see cref="FloraSystem"/> has been created and is valid.
        /// </summary>
        public static bool Exists => Instance is not null;

        /// <summary>
        /// Indicates whether the <see cref="FloraSystem"/> exists and is rendering.
        /// </summary>
        public static bool Active => Instance is not null && Instance.RenderingEnabled;

        /// <summary>
        /// Gets or creates the global <see cref="FloraSystem"/> instance.
        /// If none exists, a new <see cref="FloraSystem"/> is constructed, initializing all necessary rendering paths.
        /// </summary>
        /// <returns>The active <see cref="FloraSystem"/> instance.</returns>
        public static FloraSystem GetOrCreate()
        {
            InitializeIfNeeded();
            return Instance;
        }

        /// <summary>
        /// Initializes the global <see cref="FloraSystem"/> instance, creating a new one if none exists.
        /// </summary>
        public static void InitializeIfNeeded()
        {
            if (Instance is null)
            {
                if (IsSupportedOnSystem(out string failReason))
                {
                    RegisterUnloadOrPlayModeChangeShutdown();

                    try
                    {
                        Instance = new FloraSystem();
                        if (Instance != null)
                            WasCreated?.Invoke(Instance);

#if !UNITY_EDITOR
                        Debug.Log($"[FloraSystem] Initialized.");
                        if (!Unity.Burst.BurstCompiler.IsEnabled)
                            Debug.Log("[FloraSystem] Burst Compiler is disabled. Performance will be significantly degraded.");
#endif
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[FloraSystem] Encountered an error during initialization. [Error: {e.Message}]");
                        Debug.LogError($"[FloraSystem] Initialization stack trace: {e.StackTrace}");
                    }
                }
                else
                {
                    Debug.LogError($"[FloraSystem] Cannot initialize, unsupported error. [Error: {failReason}]");
                }
            }
        }

        /// <summary>
        /// Shuts down the global <see cref="FloraSystem"/> instance, releasing all resources and unregistering from global events.
        /// </summary>
        public static void Shutdown()
        {
            if (Instance is null && !s_UnloadOrPlayModeChangeShutdownRegistered)
                return;

            DomainUnloadOrPlayModeChangeShutdown();

#if !UNITY_EDITOR
            Debug.Log("[FloraSystem] Shutdown.");
#endif
        }

        /// <summary>
        /// Re-initializes the global <see cref="FloraSystem"/>, disposing of the current instance and creating a new one if one exists.
        /// </summary>
        public static void Reinitialize()
        {
            Shutdown();
            InitializeIfNeeded();
        }

        #region Events

        /// <summary>
        /// Invoked after the <see cref="FloraSystem"/> has been successfully created and initialized.
        /// </summary>
        public static event Action<FloraSystem> WasCreated;

        /// <summary>
        /// Invoked just before the <see cref="FloraSystem"/> is destroyed or disposed.
        /// </summary>
        public static event Action<FloraSystem> WillBeDestroyed;

        /// <summary>
        /// Invoked immediately before the first rendering pass begins, after the system is set up.
        /// </summary>
        public static event Action<FloraSystem> DidStartRendering;

        /// <summary>
        /// Invoked just before the system stops rendering, typically when rendering is disabled or shut down.
        /// </summary>
        public static event Action<FloraSystem> WillStopRendering;

        #endregion

        #region Properties

        /// <summary>
        /// The currently detected render pipeline type (Builtin, URP, HDRP, or Custom).
        /// </summary>
        public FloraRenderPipelineType RenderPipelineType { get; private set; }

        /// <summary>
        /// Returns true if the system is currently rendering.
        /// </summary>
        public bool RenderingEnabled => m_CullingSystem != null;

        /// <summary>
        /// Returns true if the system is currently rendering terrain foliage.
        /// </summary>
        public bool RenderTerrainFoliage => m_ResolvedSettings.IsTerrainFoliageEnabled;

        /// <summary>
        /// Returns true if GPU occlusion culling is allowed.
        /// </summary>
        public bool AllowGPUOcclusionCulling => m_ResolvedSettings.IsGPUOcclusionCullingEnabled;

        /// <summary>
        /// Determines if density-based culling is active.
        /// </summary>
        public bool AllowDensityCulling => AllowDensityCullingOverride && m_ResolvedSettings.IsDensityCullingEnabled;

        /// <summary>
        /// Returns true if legacy light probes are allowed.
        /// </summary>
        public bool AllowLegacyLightProbes => m_ResolvedSettings.IsLegacyLightProbesEnabled;

        /// <summary>
        /// Returns true if per-object light probes are allowed.
        /// </summary>
        public bool AllowPerTreeLightProbes => m_ResolvedSettings.Terrain.AllowPerTreeLightProbes;

        /// <summary>
        /// Returns true if per-detail light probes are allowed.
        /// </summary>
        public bool AllowPerDetailLightProbes => m_ResolvedSettings.Terrain.AllowPerDetailLightProbes;

        /// <summary>
        /// Returns true if per-object motion vectors are allowed.
        /// </summary>
        public bool AllowPerObjectMotionVectors => m_ResolvedSettings.AllowPerObjectMotionVectors;

        /// <summary>
        /// Returns true if per-tree motion vectors are allowed.
        /// </summary>
        public bool AllowPerTreeMotionVectors => m_ResolvedSettings.Terrain.AllowPerTreeMotionVectors;

        /// <summary>
        /// Returns true if per-detail motion vectors are allowed.
        /// </summary>
        public bool AllowPerDetailMotionVectors => m_ResolvedSettings.Terrain.AllowPerDetailMotionVectors;

        /// <summary>
        /// The <see cref="BatchRendererGroup"/> used for rendering.
        /// </summary>
        public BatchRendererGroup BatchRendererGroup => m_BatchRendererGroup;

        /// <summary>
        /// Returns the current runtime shaders resources.
        /// </summary>
        public FloraRuntimeResources Resources => m_Resources;

        /// <summary>
        /// Returns the number of instances currently registered in the system.
        /// </summary>
        public int RegisteredInstanceCount => m_NativeContext.InstanceManager.ValueRO.InstancesAllocated;

        /// <summary>
        /// The current frame scratch allocator used during rendering.
        /// </summary>
        public ref RewindableAllocator FrameAllocator => ref m_NativeContext.InstanceManager.ValueRW.FrameAllocator;

        #endregion

        #region Fields

        internal const string InitializeFrameMarkerName = "Flora.InitializeFrame";
        internal const string PostLateUpdateMarkerName = "Flora.PostLateUpdate";
        internal const string BeginRenderingContextMarkerName = "Flora.BeginRenderingContext";
        internal const string BeginRenderingCameraMarkerName = "Flora.BeginRenderingCamera";
        internal const string PerformBatchCullingMarkerName = "Flora.PerformBatchCulling";

        private static readonly ProfilerMarker InitializeFrameMarker = new ProfilerMarker(InitializeFrameMarkerName);
        private static readonly ProfilerMarker PostLateUpdateMarker = new ProfilerMarker(PostLateUpdateMarkerName);
        private static readonly ProfilerMarker BeginRenderingContextMarker = new ProfilerMarker(BeginRenderingContextMarkerName);
        private static readonly ProfilerMarker BeginRenderingCameraMarker = new ProfilerMarker(BeginRenderingCameraMarkerName);
        private static readonly ProfilerMarker PerformBatchCullingMarker = new ProfilerMarker(PerformBatchCullingMarkerName);

        private FloraRuntimeResources m_Resources;
        private FloraRuntimeSettings m_Settings;
        private ResolvedSystemSettings m_ResolvedSettings;
        private BatchRendererGroup m_BatchRendererGroup;
        private Material m_PickingMaterial;
        private Material m_LoadingMaterial;
        private Material m_ErrorMaterial;

        private InstanceRendererManager m_InstanceRendererManager;
        private CullingSystem m_CullingSystem;

        private HashSet<EntityId> m_DisabledTerrains = new HashSet<EntityId>();
        private Dictionary<EntityId, Terrain> m_Terrains = new Dictionary<EntityId, Terrain>();
        private Dictionary<EntityId, FloraInstanceContainer> m_Containers = new Dictionary<EntityId, FloraInstanceContainer>();
        private Dictionary<EntityId, FloraInstanceRenderer> m_InstanceRenderers = new Dictionary<EntityId, FloraInstanceRenderer>();
        private Dictionary<EntityId, HashSet<EntityId>> m_InstanceRendererChildren = new Dictionary<EntityId, HashSet<EntityId>>();
        private Dictionary<EntityId, Renderer> m_Renderers = new Dictionary<EntityId, Renderer>();

        private InstanceContext m_NativeContext;
        private FloraRenderPipeline m_RenderPipeline;
        private UnityObjectDispatcher m_ObjectTracker;

#if UNITY_EDITOR
        private NativeList<EntityId> m_FrameCameraIDs;
        private bool m_FrameUpdateNeeded;
        private bool m_SelectionChanged;
        private bool m_SceneVisibilityChanged;
        private bool m_HasSelection;
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly DebugDisplaySettingsUI m_DebugDisplaySettingsUI = new DebugDisplaySettingsUI();
        private DebugCullingGrid m_DebugCullingGrid;
#endif

        internal NativeDataReference<InstanceManager> InstanceManager => m_NativeContext.InstanceManager;
        internal NativeDataReference<CullingGrid> CullingGrid => m_NativeContext.CullingGrid;
        internal NativeDataReference<DrawManager> DrawManager => m_NativeContext.DrawManager;
        internal NativeDataReference<TemplateManager> TemplateManager => m_NativeContext.TemplateManager;
        internal NativeDataReference<InstanceBuffer> InstanceBuffer => m_NativeContext.InstanceBuffer;
        internal CullingSystem CullingSystem => m_CullingSystem;
        internal bool AllowDensityCullingOverride { get; set; } = true;

        private bool HasInstancesOrObjects => m_Terrains.Count > 0 || m_Containers.Count > 0  ||
                                              m_InstanceRenderers.Count > 0 || m_Renderers.Count > 0 ||
                                              InstanceRegistry.Data.ThreadUnsafeInstanceCount > 0;

        #endregion

        #region Lifecycle

        private FloraSystem()
        {
            m_Resources = GraphicsSettings.GetRenderPipelineSettings<FloraRuntimeResources>();
            if (m_Resources == null)
                throw new InvalidOperationException("[FloraSystem] No FloraRuntimeShaders found in Graphics Settings.");

            m_Settings = GraphicsSettings.GetRenderPipelineSettings<FloraRuntimeSettings>();
            if (m_Resources == null)
                throw new InvalidOperationException("[FloraSystem] No FloraRuntimeSettings found in Graphics Settings.");

            m_ResolvedSettings = SystemSettingsResolver.ResolveSettings(m_Settings);
#if UNITY_EDITOR
            m_Resources.EnsureShadersCompiled();
#endif
            GraphicsBufferUtility.Initialize(m_Resources);
            switch (GetCurrentRenderPipelineType())
            {
                case FloraRenderPipelineType.Builtin:
                    throw new NotSupportedException("[FloraSystem] The built-in render pipeline is no longer supported. Please upgrade to URP or HDRP.");
                case FloraRenderPipelineType.Universal:
#if UNITY_EDITOR
                    m_PickingMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Universal Render Pipeline/BRGPicking"));
                    m_LoadingMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Universal Render Pipeline/FallbackLoading"));
                    m_ErrorMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Universal Render Pipeline/FallbackError"));
#endif
                    break;
                case FloraRenderPipelineType.HighDefinition:
#if UNITY_EDITOR
                    m_PickingMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/HDRP/BRGPicking"));
                    m_LoadingMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/HDRP/MaterialLoading"));
                    m_ErrorMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/HDRP/MaterialError"));
#endif
                    break;
            }

            m_BatchRendererGroup = new BatchRendererGroup(new BatchRendererGroupCreateInfo
            {
                cullingCallback = OnPerformBatchCulling,
                finishedCullingCallback = OnBatchCullingComplete,
                userContext = default
            });

#if UNITY_EDITOR
            if (m_PickingMaterial)
                m_BatchRendererGroup.SetPickingMaterial(m_PickingMaterial);
            if (m_LoadingMaterial)
                m_BatchRendererGroup.SetLoadingMaterial(m_LoadingMaterial);
            if (m_ErrorMaterial)
                m_BatchRendererGroup.SetErrorMaterial(m_ErrorMaterial);
#endif

            BatchAssetManager.Initialize(m_BatchRendererGroup, m_Resources);

            m_NativeContext = new InstanceContext(m_Resources);
            m_InstanceRendererManager = new InstanceRendererManager(m_NativeContext);

            SetupTracking();
            SetupRendering();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugGlobalKeywords.Initialize();
            m_DebugCullingGrid = new DebugCullingGrid(m_NativeContext, m_Resources);
            m_DebugDisplaySettingsUI.RegisterDebug(FloraDebugDisplaySettings.Instance);
#if UNITY_6000_5_OR_NEWER
            DebugManager.instance.RecreateDebugUI();
#else
            DebugManager.instance.RefreshEditor();
#endif
#endif

#if UNITY_EDITOR
            m_FrameUpdateNeeded = true;
            m_SelectionChanged = true;
            m_SceneVisibilityChanged = true;
            m_FrameCameraIDs = new NativeList<EntityId>(Allocator.Persistent);
#endif
        }

        private void Dispose()
        {
            Assert.IsNotNull(Instance);

            WillBeDestroyed?.Invoke(this);

            Instance = null;
            s_CurrentScriptableRenderContextID = 0;

            TeardownRendering();
            TeardownTracking();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_DebugCullingGrid?.Dispose();
            m_DebugDisplaySettingsUI.UnregisterDebug();
#endif
#if UNITY_EDITOR
            if (m_FrameCameraIDs.IsCreated)
                m_FrameCameraIDs.Dispose();
#endif

            m_NativeContext.InstanceManager.ValueRW.SyncJobsForMainThread();
            m_InstanceRendererManager.Dispose();
            m_NativeContext.Dispose();
            m_BatchRendererGroup.Dispose();

            CoreUtils.Destroy(m_PickingMaterial);
            CoreUtils.Destroy(m_LoadingMaterial);
            CoreUtils.Destroy(m_ErrorMaterial);

            CoreUtils.emptyBuffer?.Dispose();
            BatchAssetManager.Shutdown();
            GraphicsBufferStore.ReleaseAll();
        }

        #endregion

        #region Compatibility

        /// <summary>
        /// Verifies system support for Flora’s GPU-driven instancing, compute shaders, etc.
        /// </summary>
        /// <param name="failReason">A reason string if not supported.</param>
        /// <returns>True if supported on this device, otherwise false.</returns>
        public static bool IsSupportedOnSystem(out string failReason)
        {
            failReason = string.Empty;

#if UNITY_EDITOR
            if (AssetDatabase.IsAssetImportWorkerProcess())
                return false;
#endif

            if (BatchRendererGroup.BufferTarget != BatchBufferTarget.RawBuffer)
            {
                failReason = $"The current platform does not support {BatchBufferTarget.RawBuffer.GetType()}";
                return false;
            }

            if (!GraphicsSettings.currentRenderPipeline)
            {
                failReason = "A render pipeline is required for Flora.";
                return false;
            }

            if (!SystemInfo.supportsComputeShaders)
            {
                failReason = "Compute Shaders are not supported.";
                return false;
            }

            if (!SystemInfo.supportsInstancing)
            {
                failReason = "Instancing is not supported.";
                return false;
            }

            if (SystemInfo.graphicsShaderLevel < 45)
            {
                failReason = "Shader 4.5 support is required.";
                return false;
            }

            if (!SystemInfo.supportsIndirectArgumentsBuffer)
            {
                failReason = "Indirect Arguments Buffers are not supported.";
                return false;
            }

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore && !CheckGLVersion())
            {
                failReason = "The current graphics device is not supported.";
                return false;
            }

            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                failReason = "Async GPU Readback is not supported.";
                return false;
            }

            return true;
        }

        private static bool CheckGLVersion()
        {
            char[] delimiterChars = { ' ', '.'};
            string[] arr = SystemInfo.graphicsDeviceVersion.Split(delimiterChars);
            if (arr.Length >= 3)
            {
                var tokens = SystemInfo.graphicsDeviceVersion.Split(new[]{' ', '.'}, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 3 &&
                    int.TryParse(tokens[1], out var major) &&
                    int.TryParse(tokens[2], out var minor))
                {
                    return major >= 4 && minor >= 3;
                }
            }

            return false;
        }

        #endregion
    }

    internal struct ResolvedSystemSettings
    {
        public static readonly ResolvedSystemSettings Default = new()
        {
            IsRenderingEnabled = true,
            IsGPUOcclusionCullingEnabled = true,
            IsDensityCullingEnabled = true,
            IsTerrainFoliageEnabled = true,
        };

        public bool IsRenderingEnabled;
        public bool IsGPUOcclusionCullingEnabled;
        public bool IsDensityCullingEnabled;
        public bool IsLegacyLightProbesEnabled;
        public bool AllowPerObjectMotionVectors;

        public bool IsTerrainFoliageEnabled;
        public bool IsAutoRegisterTerrainsEnabled;
        public TerrainSystemSettings Terrain;
    }

    internal static class SystemSettingsResolver
    {
        private static float EvaluateDetailStreamingResponsiveness(float responsiveness)
        {
            return Mathf.Pow(Mathf.Clamp01(responsiveness), 0.75f);
        }

        private static int ResolveDetailPatchLayerBudget(float responsiveness)
        {
            float t = EvaluateDetailStreamingResponsiveness(responsiveness);
            return Mathf.RoundToInt(Mathf.Lerp(1f, 24f, t));
        }

        private static int ResolveDetailStructuralBudget(float responsiveness)
        {
            float t = EvaluateDetailStreamingResponsiveness(responsiveness);
            return Mathf.RoundToInt(Mathf.Lerp(500f, 12000f, t));
        }

        public static ResolvedSystemSettings ResolveSettings(FloraRuntimeSettings runtimeSettings)
        {
            var resolvedSettings = ResolvedSystemSettings.Default;
            var sceneSettings = FloraSceneSettings.Instance;
            if (sceneSettings)
            {
                resolvedSettings.IsRenderingEnabled = sceneSettings.EnableRendering;
                resolvedSettings.AllowPerObjectMotionVectors = sceneSettings.AllowPerObjectMotionVectors;
                resolvedSettings.IsGPUOcclusionCullingEnabled = sceneSettings.AllowGPUOcclusionCulling;
                resolvedSettings.IsLegacyLightProbesEnabled = sceneSettings.AllowLegacyLightProbes;
                resolvedSettings.IsDensityCullingEnabled = sceneSettings.AllowDensityCulling;

                resolvedSettings.IsTerrainFoliageEnabled = resolvedSettings.IsRenderingEnabled && sceneSettings.EnableTerrainFoliage;
                if (resolvedSettings.IsTerrainFoliageEnabled)
                {
                    resolvedSettings.IsAutoRegisterTerrainsEnabled = sceneSettings.AutoRegisterTerrains;
                    resolvedSettings.Terrain.AllowPerTreeMotionVectors = resolvedSettings.AllowPerObjectMotionVectors && sceneSettings.AllowPerTreeMotionVectors;
                    resolvedSettings.Terrain.AllowPerTreeLightProbes = resolvedSettings.IsLegacyLightProbesEnabled && sceneSettings.AllowPerTreeLightProbes;

                    resolvedSettings.Terrain.AllowPerDetailMotionVectors = resolvedSettings.AllowPerObjectMotionVectors && sceneSettings.AllowPerDetailMotionVectors;
                    resolvedSettings.Terrain.AllowPerDetailLightProbes = resolvedSettings.IsLegacyLightProbesEnabled && sceneSettings.AllowPerDetailLightProbes;
                    resolvedSettings.Terrain.DetailStreamingDeltaTime = Time.unscaledDeltaTime;
                    resolvedSettings.Terrain.DetailUnloadHysteresisSeconds = Mathf.Max(0f, sceneSettings.DetailUnloadHysteresisSeconds);
                    switch (sceneSettings.DetailStreamingMode)
                    {
                        case FloraDetailStreamingMode.Immediate:
                            resolvedSettings.Terrain.DetailPatchLayerBudgetPerFrame = 0;
                            resolvedSettings.Terrain.DetailStructuralInstanceBudgetPerFrame = 0;
                            break;
                        case FloraDetailStreamingMode.Custom:
                            resolvedSettings.Terrain.DetailPatchLayerBudgetPerFrame = Mathf.Max(0, sceneSettings.CustomDetailPatchLayerBudgetPerFrame);
                            resolvedSettings.Terrain.DetailStructuralInstanceBudgetPerFrame = Mathf.Max(0, sceneSettings.CustomDetailStructuralInstanceBudgetPerFrame);
                            break;
                        default:
                            resolvedSettings.Terrain.DetailPatchLayerBudgetPerFrame =
                                ResolveDetailPatchLayerBudget(sceneSettings.DetailStreamingResponsiveness);
                            resolvedSettings.Terrain.DetailStructuralInstanceBudgetPerFrame =
                                ResolveDetailStructuralBudget(sceneSettings.DetailStreamingResponsiveness);
                            break;
                    }
                }
            }

            if (runtimeSettings.DisableGPUOcclusionCulling)
                resolvedSettings.IsGPUOcclusionCullingEnabled = false;

            if (runtimeSettings.DisableLegacyLightProbes)
            {
                resolvedSettings.IsLegacyLightProbesEnabled = false;
                resolvedSettings.Terrain.AllowPerTreeLightProbes = false;
                resolvedSettings.Terrain.AllowPerDetailLightProbes = false;
            }

            if (runtimeSettings.DisablePerObjectMotionVectors)
            {
                resolvedSettings.AllowPerObjectMotionVectors = false;
                resolvedSettings.Terrain.AllowPerTreeMotionVectors = false;
                resolvedSettings.Terrain.AllowPerDetailMotionVectors = false;
            }

            return resolvedSettings;
        }

        public static bool DisableInstanceRenderersInEditMode
        {
            get
            {
                bool disableInstanceRenderersInEditMode = true;
#if UNITY_EDITOR
                var editorSettings = GraphicsSettings.GetRenderPipelineSettings<FloraEditorSettings>();
                disableInstanceRenderersInEditMode = editorSettings.DisableInstanceRenderersInEditMode;
#endif
                return disableInstanceRenderersInEditMode;
            }
        }
    }
}
