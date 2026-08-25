// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

#if UNITY_EDITOR
#endif

namespace MA.Flora
{
    public sealed partial class FloraSystem
    {
        #region Scene Events

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Instance?.RebuildAmbientLighting();
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            if (Instance != null)
            {
                var instanceManager = Instance.m_NativeContext.InstanceManager.ValueRW;
                instanceManager.DestroyAllInstancesInScene(scene);
            }
        }

        private static void OnLightProbesUpdated()
        {
            Instance?.RebuildAmbientLighting();
        }

        private static void OnTerrainHeightmapChanged(Terrain terrain, RectInt region, bool didSync)
        {
            Instance?.SetTerrainHeightmapChanged(terrain);
        }

        private void RebuildAmbientLighting()
        {
            m_CullingSystem?.UpdateAmbientLighting(forceUpdate: true);
        }

        private void SetTerrainHeightmapChanged(Terrain terrain)
        {
            m_NativeContext.TerrainManager.ValueRW.SetHeightmapDirty(terrain.GetEntityId());
        }

        #endregion

        #region Player Loop

        private struct FloraBeginFrame { }

        private struct FloraPostLateUpdate { }

        private struct FloraEndFrame { }

        private static void SetupPlayerLoop()
        {
            PlayerLoopUtility.TryAddToPlayerLoop(OnInitialization, typeof(FloraBeginFrame), typeof(Initialization.UpdateCameraMotionVectors), PlayerLoopUtility.AddMode.End);
            PlayerLoopUtility.TryAddToPlayerLoop(OnPostLateUpdate, typeof(FloraPostLateUpdate), typeof(PostLateUpdate.UpdateAllRenderers), PlayerLoopUtility.AddMode.End);
            PlayerLoopUtility.TryAddToPlayerLoop(OnPostPostLateUpdate, typeof(FloraEndFrame), typeof(PostLateUpdate.FinishFrameRendering), PlayerLoopUtility.AddMode.End);
        }

        private static void TeardownPlayerLoop()
        {
            PlayerLoopUtility.TryRemoveLoopSystem(typeof(FloraBeginFrame));
            PlayerLoopUtility.TryRemoveLoopSystem(typeof(FloraPostLateUpdate));
            PlayerLoopUtility.TryRemoveLoopSystem(typeof(FloraEndFrame));
        }

        private static void OnInitialization()
        {
            Instance?.FrameInitialization();
        }

        private static void OnPostLateUpdate()
        {
            Instance?.FramePostLateUpdate();
        }

        private static void OnPostPostLateUpdate()
        {
            Instance?.FramePostPostLateUpdate();
        }

        private bool UpdateSettings()
        {
            var newSettings = SystemSettingsResolver.ResolveSettings(m_Settings);

            var requiresSystemReinit = newSettings.AllowPerObjectMotionVectors != m_ResolvedSettings.AllowPerObjectMotionVectors ||
                                       newSettings.IsLegacyLightProbesEnabled != m_ResolvedSettings.IsLegacyLightProbesEnabled;
            if (requiresSystemReinit)
                return false;

            bool terrainRequiresReinit = newSettings.IsTerrainFoliageEnabled != m_ResolvedSettings.IsTerrainFoliageEnabled ||
                                         newSettings.Terrain.AllowPerTreeMotionVectors != m_ResolvedSettings.Terrain.AllowPerTreeMotionVectors ||
                                         newSettings.Terrain.AllowPerDetailMotionVectors != m_ResolvedSettings.Terrain.AllowPerDetailMotionVectors ||
                                         newSettings.Terrain.AllowPerTreeLightProbes != m_ResolvedSettings.Terrain.AllowPerTreeLightProbes ||
                                         newSettings.Terrain.AllowPerDetailLightProbes != m_ResolvedSettings.Terrain.AllowPerDetailLightProbes;

            if (terrainRequiresReinit)
            {
                m_NativeContext.TerrainManager.ValueRW.Clear();

                if (newSettings.IsTerrainFoliageEnabled)
                {
                    foreach (var terrain in m_Terrains.Values)
                    {
                        if (terrain != null)
                        {
                            m_NativeContext.TerrainManager.ValueRW.Register(terrain);
                            ApplyTerrainFoliageOwnership(terrain, floraOwnsTerrainFoliage: true);
                        }
                    }
                }
                else
                {
                    foreach (var entity in m_Terrains.Keys)
                    {
                        m_NativeContext.TerrainManager.ValueRW.Unregister(entity);

                        var terrain = entity.ToObject<Terrain>();
                        if (terrain != null)
                            ApplyTerrainFoliageOwnership(terrain, floraOwnsTerrainFoliage: false);
                    }
                }
            }

            if (newSettings.IsAutoRegisterTerrainsEnabled)
                RegisterTerrains();

            m_ResolvedSettings = newSettings;
            return true;
        }

        private void FrameInitialization()
        {
            using var _ = InitializeFrameMarker.Auto();

            TemplateUtility.NextFrame();
            BatchAssetManager.NextFrame();
            GraphicsBufferStore.NextFrame();

            if (UpdateSettings())
            {
                if (m_ResolvedSettings.IsRenderingEnabled != RenderingEnabled)
                {
                    if (m_ResolvedSettings.IsRenderingEnabled)
                        SetupRendering();
                    else
                        TeardownRendering();
                }

                m_NativeContext.InstanceManager.ValueRW.InitializeFrame();

                BeginFrame?.Invoke();
                DelayCall?.Invoke();
            }
            else
            {
                Reinitialize();
            }
        }

        private void FramePostLateUpdate()
        {
            PostLateUpdate?.Invoke();

            using var _ = PostLateUpdateMarker.Auto();

#if UNITY_EDITOR
            ProcessSelection();
            ProcessSceneVisibility();
#endif

            try
            {
                UpdateTracking();

                m_NativeContext.StreamingManager.ValueRW.Update();
                m_NativeContext.TerrainManager.ValueRW.Update(m_ResolvedSettings.Terrain);

                m_NativeContext.InstanceManager.ValueRW.OnPostLateUpdate();
                m_NativeContext.TemplateManager.ValueRW.RebuildDrawBatches();

                m_CullingSystem?.UpdateAmbientLighting(forceUpdate: false);
                m_NativeContext.InstanceManager.ValueRW.SubmitToGpu(m_BatchRendererGroup);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FloraSystem] Encountered an error during the PostLateUpdate phase. [Error: {e.Message}]");
                Debug.LogError($"[FloraSystem] PostLateUpdate stack trace: {e.StackTrace}");
                Shutdown();
                throw;
            }

#if UNITY_EDITOR
            m_FrameUpdateNeeded = false;
#endif
        }

        private void FramePostPostLateUpdate()
        {
        }

        #endregion

        #region Rendering Setup

        private FloraRenderPipelineType GetCurrentRenderPipelineType()
        {
            if (GraphicsSettings.currentRenderPipeline)
            {
                switch (GraphicsSettings.currentRenderPipeline)
                {
#if HAS_PACKAGE_UNITY_URP
                    case UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset:
                        return FloraRenderPipelineType.Universal;
#endif
#if HAS_PACKAGE_UNITY_HDRP
                    case UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset:
                        return FloraRenderPipelineType.HighDefinition;
#endif
                    default:
                        return FloraRenderPipelineType.Custom;
                }
            }

            return FloraRenderPipelineType.Builtin;
        }

        private void SetupRendering()
        {
            if (RenderingEnabled)
                return;

            RenderPipelineType = GetCurrentRenderPipelineType();
            m_RenderPipeline = RenderPipelineType switch
            {
                FloraRenderPipelineType.Universal => new FloraRenderPipelineUniversal(),
                FloraRenderPipelineType.HighDefinition => new FloraRenderPipelineHighDefinition(),
                _ => m_RenderPipeline
            };

            if (m_RenderPipeline == null)
            {
                Debug.LogError($"Flora: The current render pipeline ({RenderPipelineType}) is not supported.");
                RenderPipelineType = FloraRenderPipelineType.Unknown;
                return;
            }
            else
            {
                var cullingSystemSetup = new CullingSystemSetup
                {
                    RenderPipeline = m_RenderPipeline,
                    RuntimeResources = m_Resources,
                };

                m_CullingSystem = new CullingSystem(cullingSystemSetup, m_BatchRendererGroup, m_NativeContext);
            }

            DisableUnityTerrainRendering();
            DisableUnityRenderers();

            DidStartRendering?.Invoke(this);
        }

        private void TeardownRenderingIfEmpty()
        {
            if (!HasInstancesOrObjects)
                TeardownRendering();
        }

        private void TeardownRendering()
        {
            if (!RenderingEnabled)
                return;

            WillStopRendering?.Invoke(this);

            EnableUnityRenderers();
            EnableUnityTerrainRendering();

            RenderPipelineType = FloraRenderPipelineType.Unknown;
            s_CurrentScriptableRenderContextID = 0;

            m_CullingSystem?.Dispose();
            m_CullingSystem = null;

            m_RenderPipeline?.Dispose();
            m_RenderPipeline = null;
        }

        private void ReinitializeCullingSystem()
        {
            bool wasRenderingEnabled = RenderingEnabled;
            m_CullingSystem?.Dispose();
            m_CullingSystem = null;

            if (wasRenderingEnabled)
            {
                var cullingSystemSetup = new CullingSystemSetup
                {
                    RenderPipeline = m_RenderPipeline,
                    RuntimeResources = m_Resources,
                };

                m_CullingSystem = new CullingSystem(cullingSystemSetup, m_BatchRendererGroup, m_NativeContext);
            }
        }

        #endregion

        #region Rendering Events

        private void UpdateDebugDisplay()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Shader.SetGlobalInteger(DebugShaderPropertyId.flora_DebugViewMode, 0);
            Shader.SetGlobalFloat(DebugShaderPropertyId.flora_DebugOpacity, 0);

            if (DebugManager.instance.isAnyDebugUIActive)
                FloraDebugDisplaySettings.Instance.UpdateDisplay();

            m_DebugCullingGrid?.NextFrame();
#endif
        }

        private void OnBeginContextRendering(List<Camera> cameras)
        {
#if UNITY_EDITOR
            EditorFrameUpdate(cameras);
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpdateDebugDisplay();
#endif
            if (!RenderingEnabled)
                return;

            using var _ = BeginRenderingContextMarker.Auto();
            m_CullingSystem.BeginContextRendering();
        }

        private void OnBeginCameraRendering(Camera camera)
        {
            if (!RenderingEnabled)
                return;

            using var _ = BeginRenderingCameraMarker.Auto();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_DebugCullingGrid?.UpdateDisplay(camera);
#endif
            m_CullingSystem.BeginCameraRendering(camera);
        }

        private void OnEndCameraRendering(Camera camera)
        {
            if (!RenderingEnabled)
                return;

            m_CullingSystem.EndCameraRendering(camera);
        }

        private void OnEndContextRendering(List<Camera> cameras)
        {
            if (!RenderingEnabled)
                return;

            m_CullingSystem.EndContextRendering();
        }

        #endregion

        #region BatchRendererGroup

        private JobHandle OnPerformBatchCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            if (!RenderingEnabled)
                return default;

            using var _ = PerformBatchCullingMarker.Auto();

            return m_CullingSystem.OnPerformBatchCulling(rendererGroup, cullingContext, cullingOutput, userContext);
        }

        private void OnBatchCullingComplete(IntPtr customCullingResult)
        {
            if (!RenderingEnabled)
                return;

            m_CullingSystem.OnBatchCullingComplete((int)customCullingResult);
        }

        #endregion
    }
}
