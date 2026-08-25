// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MA.Flora
{
    public sealed partial class FloraSystem
    {
        #region Domain Lifecycle

        private static bool s_UnloadOrPlayModeChangeShutdownRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void CleanupSystemBeforeSceneLoad()
        {
            DomainUnloadOrPlayModeChangeShutdown();
        }

        private static void RegisterUnloadOrPlayModeChangeShutdown()
        {
            if (s_UnloadOrPlayModeChangeShutdownRegistered)
                return;

            var go = new GameObject { hideFlags = HideFlags.HideInHierarchy };
            if (Application.isPlaying)
                UnityObject.DontDestroyOnLoad(go);
            else
                go.hideFlags = HideFlags.HideAndDontSave;

            go.AddComponent<FloraSystemInitializationProxy>().IsActive = true;

            s_UnloadOrPlayModeChangeShutdownRegistered = true;

            EarlyInitHelpers.FlushEarlyInits();
#if ENABLE_FLORA_DEBUG_NAMES
            InstanceData.NameStorage.Initialize();
#endif

            SetupPlayerLoop();

            RenderPipelineManager.activeRenderPipelineCreated += OnActiveRenderPipelineCreated;
            RenderPipelineManager.activeRenderPipelineDisposed += OnActiveRenderPipelineDisposed;
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
            RenderPipelineManager.endContextRendering += OnEndContextRendering;

            Application.quitting += DomainUnloadOrPlayModeChangeShutdown;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            LightProbes.lightProbesUpdated += OnLightProbesUpdated;
            TerrainCallbacks.heightmapChanged += OnTerrainHeightmapChanged;

#if UNITY_EDITOR
            Lightmapping.bakeStarted += OnEditorBakeStarted;
            Lightmapping.bakeCancelled += OnEditorBakeCompleted;
            Lightmapping.bakeCompleted += OnEditorBakeCompleted;
            Undo.undoRedoPerformed += OnEditorUndoRedo;
            Selection.selectionChanged += OnEditorUndoRedo;
            SceneVisibilityManager.visibilityChanged += OnSceneViewVisibilityChanged;
#endif
        }

        internal static void DomainUnloadOrPlayModeChangeShutdown()
        {
            if (!s_UnloadOrPlayModeChangeShutdownRegistered)
                return;

#if !UNITY_EDITOR
            Debug.Log("[FloraSystem] Shutting down player loop and releasing all resources.");
#endif

            TeardownPlayerLoop();

            RenderPipelineManager.activeRenderPipelineCreated -= OnActiveRenderPipelineCreated;
            RenderPipelineManager.activeRenderPipelineDisposed -= OnActiveRenderPipelineDisposed;
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            RenderPipelineManager.endContextRendering -= OnEndContextRendering;

            Application.quitting -= DomainUnloadOrPlayModeChangeShutdown;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            LightProbes.lightProbesUpdated -= OnLightProbesUpdated;
            TerrainCallbacks.heightmapChanged -= OnTerrainHeightmapChanged;

#if UNITY_EDITOR
            Lightmapping.bakeStarted -= OnEditorBakeStarted;
            Lightmapping.bakeCancelled -= OnEditorBakeCompleted;
            Lightmapping.bakeCompleted -= OnEditorBakeCompleted;
            Undo.undoRedoPerformed -= OnEditorUndoRedo;
            Selection.selectionChanged -= OnEditorUndoRedo;
            SceneVisibilityManager.visibilityChanged -= OnSceneViewVisibilityChanged;
#endif

            Instance?.Dispose();
            Instance = null;

            GraphicsBufferStore.ReleaseAll();
            InstanceRegistry.Data.Dispose();
#if ENABLE_FLORA_DEBUG_NAMES
            InstanceData.NameStorage.Shutdown();
#endif

            s_UnloadOrPlayModeChangeShutdownRegistered = false;
        }

        private static void OnActiveRenderPipelineCreated()
        {
            InitializeIfNeeded();
        }

        private static void OnActiveRenderPipelineDisposed()
        {
            Shutdown();
        }

        private static int s_CurrentScriptableRenderContextID; // Used to prevent double-calling of BeginContext/EndContext

        private static void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (Instance is null)
                return;

            if (s_CurrentScriptableRenderContextID == 0)
            {
                s_CurrentScriptableRenderContextID = context.GetHashCode();
                Instance.OnBeginContextRendering(cameras);
            }
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            Instance?.OnBeginCameraRendering(camera);
        }

        private static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            Instance?.OnEndCameraRendering(camera);
        }

        private static void OnEndContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (Instance is null)
                return;

            if (s_CurrentScriptableRenderContextID == context.GetHashCode())
            {
                Instance.OnEndContextRendering(cameras);
                s_CurrentScriptableRenderContextID = 0;
            }
        }

        #endregion
    }
}
