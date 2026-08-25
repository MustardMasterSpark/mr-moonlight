// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using MA.InternalBridge;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

#if HAS_PACKAGE_UNITY_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace MA.Flora
{
    internal sealed class FloraRenderPipelineUniversal : FloraRenderPipeline
    {
        public override FloraRenderPipelineType PipelineType => FloraRenderPipelineType.Universal;

#if HAS_PACKAGE_UNITY_URP
        private readonly FloraVisibilityPass m_VisibilityPass;
        private readonly FloraOcclusionDepthPass m_OcclusionDepthPass;
        private readonly FloraDebugOccluderDepthOverlayPass m_DebugOccluderDepthOverlayPass;
        private readonly FloraDebugOcclusionTestOverlayPass m_DebugOcclusionTestOverlayPass;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private DebugDisplayFlora m_DebugDisplayData;
        private UniversalRenderPipelineDebugDisplaySettings m_URPDebugDisplaySettings;
        private HashSet<IDebugDisplaySettingsData> m_URPDebugDisplaySettingsHashSet;
        private bool m_FailedToGetDebugDisplaySettings;
#endif
#endif

        public FloraRenderPipelineUniversal()
        {
#if HAS_PACKAGE_UNITY_URP
            m_VisibilityPass = new FloraVisibilityPass();
            m_OcclusionDepthPass = new FloraOcclusionDepthPass();
            m_DebugOccluderDepthOverlayPass = new FloraDebugOccluderDepthOverlayPass();
            m_DebugOcclusionTestOverlayPass = new FloraDebugOcclusionTestOverlayPass();
#endif
        }

        public override void Dispose()
        {
#if HAS_PACKAGE_UNITY_URP && (UNITY_EDITOR || DEVELOPMENT_BUILD)
            m_URPDebugDisplaySettingsHashSet?.Remove(m_DebugDisplayData);
            m_DebugDisplayData = null;
            m_URPDebugDisplaySettingsHashSet = null;
            m_URPDebugDisplaySettings = null;
            m_FailedToGetDebugDisplaySettings = false;
#endif
        }

        public override void EnqueueCameraPasses(Camera camera, FloraRenderPipelineCameraSettings cameraSettings)
        {
#if HAS_PACKAGE_UNITY_URP
            var renderer = camera.TryGetComponent(out UniversalAdditionalCameraData universalAdditionalCameraData)
                ? universalAdditionalCameraData.scriptableRenderer
                : UniversalRenderPipeline.asset.scriptableRenderer;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ApplyDebugDisplaySettingsHack(renderer);
#endif

            renderer.EnqueuePass(m_VisibilityPass);

            if (cameraSettings.UseGPUOcclusionCulling && renderer.supportsGPUOcclusion)
            {
                renderer.EnqueuePass(m_OcclusionDepthPass);

                if (DebugDisplayFlora.Active)
                {
                    if (DebugDisplayFlora.Properties.OccluderDepthOverlayEnabled)
                        renderer.EnqueuePass(m_DebugOccluderDepthOverlayPass);

                    if (DebugDisplayFlora.Properties.OcclusionTestOverlayEnabled)
                        renderer.EnqueuePass(m_DebugOcclusionTestOverlayPass);
                }
            }
#endif
        }

#if HAS_PACKAGE_UNITY_URP
        private void ApplyDebugDisplaySettingsHack(ScriptableRenderer renderer)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!m_FailedToGetDebugDisplaySettings &&
                m_URPDebugDisplaySettings == null)
            {
                var debugHandler = renderer
                    .GetType()
                    .GetProperty("DebugHandler", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
                    .GetValue(renderer);

                if (debugHandler != null)
                {
                    m_URPDebugDisplaySettings = debugHandler.GetType()
                        .GetField("m_DebugDisplaySettings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
                        .GetValue(debugHandler) as UniversalRenderPipelineDebugDisplaySettings;

                    if (m_URPDebugDisplaySettings != null)
                    {
                        m_URPDebugDisplaySettingsHashSet = m_URPDebugDisplaySettings
                            .GetType()
                            .GetField("m_Settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
                            .GetValue(m_URPDebugDisplaySettings) as HashSet<IDebugDisplaySettingsData>;
                    }
                }

                if (m_URPDebugDisplaySettingsHashSet == null)
                {
                    m_FailedToGetDebugDisplaySettings = true;
                    Debug.LogError("FloraSystem: Failed to retrieve URP DebugHandler. Debug features may not work as expected.");
                }
            }

            if (m_FailedToGetDebugDisplaySettings)
                return;

            if (DebugDisplayFlora.Active && m_URPDebugDisplaySettingsHashSet != null)
            {
                if (m_DebugDisplayData == null ||
                    !m_URPDebugDisplaySettingsHashSet.Contains(m_DebugDisplayData))
                {
                    m_DebugDisplayData = new DebugDisplayFlora();
                    m_URPDebugDisplaySettingsHashSet.Add(m_DebugDisplayData);
                }
            }
#endif
        }

        private abstract class InstancingPass : ScriptableRenderPass
        {
            protected InstancingPass(RenderPassEvent renderPassEvent)
            {
                profilingSampler = new ProfilingSampler($"Flora.{GetType().Name}");
                this.renderPassEvent = renderPassEvent;
            }
        }

        private sealed class FloraVisibilityPass : InstancingPass
        {
            public FloraVisibilityPass() : base(RenderPassEvent.BeforeRendering)
            {
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                cameraData.requiresDepthTexture = true;

                var viewInstanceID = cameraData.camera.GetEntityId();
                var isSinglePassXR = cameraData.xr.enabled && cameraData.xr.singlePassEnabled;
                var subviewCount = isSinglePassXR ? 2 : 1;
                var indirectCullingSettings = new OcclusionCullingSettings(viewInstanceID, OcclusionTest.TestAll)
                {
                    instanceMultiplier = (isSinglePassXR && !SystemInfo.supportsMultiview) ? 2 : 1,
                };

                Span<SubviewOcclusionTest> subviewOcclusionTests = stackalloc SubviewOcclusionTest[subviewCount];
                for (int subviewIndex = 0; subviewIndex < subviewCount; ++subviewIndex)
                {
                    subviewOcclusionTests[subviewIndex] = new SubviewOcclusionTest
                    {
                        cullingSplitIndex = 0,
                        occluderSubviewIndex = subviewIndex,
                    };
                }

                FloraSystem.Instance.CullingSystem.DispatchQueuedCullingRequests(renderGraph,
                    VolumeManager.instance.stack, indirectCullingSettings, subviewOcclusionTests);
            }
        }

        // --- Occlusion Passes ---

        private sealed class FloraOcclusionDepthPass : InstancingPass
        {
            public FloraOcclusionDepthPass() : base(RenderPassEvent.AfterRenderingPrePasses)
            {
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var scaledWidth = cameraData.GetXrCompatibleScreenWidth();
                var scaledHeight = cameraData.GetXrCompatibleScreenHeight();

                bool isSinglePassXR = cameraData.xr.enabled && cameraData.xr.singlePassEnabled;
                bool isDeferred = (renderingData.renderingMode == RenderingMode.Deferred);
                bool useDepthPriming = cameraData.renderer.HasDepthPriming();
                renderPassEvent = useDepthPriming
                    ? RenderPassEvent.AfterRenderingPrePasses
                    : isDeferred ? RenderPassEvent.AfterRenderingGbuffer : RenderPassEvent.AfterRenderingOpaques;

                // If we're in deferred mode, pre-passes always render directly to the depth attachment rather than the camera depth texture.
                // In non-deferred mode, we only render to the depth attachment directly when depth priming is enabled and we're starting with an empty depth buffer.
                bool isDepthPrimingTarget = (useDepthPriming && (cameraData.renderType == CameraRenderType.Base || cameraData.clearDepth));
                bool renderToAttachment = (isDeferred || isDepthPrimingTarget);
                TextureHandle depthTarget = renderToAttachment ? resourceData.activeDepthTexture : resourceData.cameraDepthTexture;

                var occluderParams = new OccluderParameters(cameraData.camera.GetEntityId())
                {
                    SubviewCount = isSinglePassXR ? 2 : 1,
                    DepthTextureHandle = depthTarget,
                    DepthSize = new Vector2Int(scaledWidth, scaledHeight),
                    DepthIsArray = isSinglePassXR,
                };

                Span<OccluderSubviewUpdate> occluderSubviewUpdates = stackalloc OccluderSubviewUpdate[occluderParams.SubviewCount];
                for (int subviewIndex = 0; subviewIndex < occluderParams.SubviewCount; ++subviewIndex)
                {
                    var viewMatrix = cameraData.GetViewMatrix(subviewIndex);
                    var projMatrix = cameraData.GetProjectionMatrix(subviewIndex);
                    occluderSubviewUpdates[subviewIndex] = new OccluderSubviewUpdate(subviewIndex)
                    {
                        depthSliceIndex = subviewIndex,
                        viewMatrix = viewMatrix,
                        invViewMatrix = viewMatrix.inverse,
                        gpuProjMatrix = GL.GetGPUProjectionMatrix(projMatrix, true),
                        viewOffsetWorldSpace = Vector3.zero,
                    };
                }

                FloraSystem.Instance.CullingSystem.BuildOcclusionDepth(renderGraph,
                    occluderParams, occluderSubviewUpdates);
            }
        }

        private sealed class FloraDebugOccluderDepthOverlayPass : InstancingPass
        {
            public FloraDebugOccluderDepthOverlayPass() : base(RenderPassEvent.AfterRenderingPostProcessing)
            {
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var resourceData = frameData.Get<UniversalResourceData>();
                var screenWidth = cameraData.GetXrCompatibleScreenWidth();
                var screenHeight = cameraData.GetXrCompatibleScreenHeight();
                var maxHeight = screenHeight * 50f / 100f;

                FloraSystem.Instance.CullingSystem.RenderOcclusionDebugDepthOverlay(renderGraph,
                    cameraData.camera.GetEntityId(), new Vector2(0.25f * screenWidth, screenHeight - 1.5f * maxHeight), maxHeight, resourceData.activeColorTexture);
            }
        }

        private sealed class FloraDebugOcclusionTestOverlayPass : InstancingPass
        {
            public FloraDebugOcclusionTestOverlayPass() : base(RenderPassEvent.AfterRenderingPostProcessing)
            {
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var resourceData = frameData.Get<UniversalResourceData>();
                FloraSystem.Instance.CullingSystem.RenderOcclusionDebugTestOverlay(renderGraph,
                    cameraData.camera.GetEntityId(), resourceData.activeColorTexture);
            }
        }
#endif // HAS_PACKAGE_UNITY_URP
    }
}
