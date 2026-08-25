// Copyright © Magnetic Arcade. All Rights Reserved.
// ReSharper disable InconsistentNaming

using UnityEngine;

#if HAS_PACKAGE_UNITY_HDRP
using MA.InternalBridge;
using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Debug = UnityEngine.Debug;
using DebugOverlay = UnityEngine.Rendering.DebugOverlay;
#endif

namespace MA.Flora
{
    internal sealed class FloraRenderPipelineHighDefinition : FloraRenderPipeline
    {
        public override FloraRenderPipelineType PipelineType => FloraRenderPipelineType.HighDefinition;

#if HAS_PACKAGE_UNITY_HDRP
        readonly FloraVisibilityPass m_VisibilityPass;
        readonly FloraOcclusionDepthPass m_OcclusionDepthPass;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugOverlay m_DebugOverlay = new DebugOverlay();
        readonly FloraDebugOccluderDepthOverlayPass m_DebugOccluderDepthOverlayPass;
        readonly FloraDebugOcclusionTestOverlayPass m_DebugOcclusionTestOverlayPass;
#endif
#endif

        public FloraRenderPipelineHighDefinition()
        {
#if HAS_PACKAGE_UNITY_HDRP
            HDRenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            if (asset == null)
            {
                Debug.Log("HighDefinitionPassManager: HDRP asset is not set in GraphicsSettings.");
                return;
            }

            if (!asset.currentPlatformRenderPipelineSettings.supportCustomPass)
            {
                RenderPipelineSettings currentPlatformRenderPipelineSettings = asset.currentPlatformRenderPipelineSettings;
                currentPlatformRenderPipelineSettings.supportCustomPass = true;
                asset.currentPlatformRenderPipelineSettings = currentPlatformRenderPipelineSettings;
            }

            m_VisibilityPass = new FloraVisibilityPass();
            m_OcclusionDepthPass = new FloraOcclusionDepthPass();
            m_OcclusionDepthPass.enabled = false;

            CustomPassVolume.RegisterUniqueGlobalCustomPass(CustomPassInjectionPoint.BeforeRendering, m_VisibilityPass);
            CustomPassVolume.RegisterUniqueGlobalCustomPass(CustomPassInjectionPoint.AfterOpaqueDepthAndNormal, m_OcclusionDepthPass);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (RenderPipelineManager.currentPipeline is HDRenderPipeline renderPipeline)
            {
                m_DebugOverlay = renderPipeline.GetType()
                    .GetField("m_DebugOverlay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(renderPipeline) as DebugOverlay;
            }

            m_DebugOccluderDepthOverlayPass = new FloraDebugOccluderDepthOverlayPass(m_DebugOverlay);
            m_DebugOcclusionTestOverlayPass = new FloraDebugOcclusionTestOverlayPass();
            m_DebugOccluderDepthOverlayPass.enabled = false;
            m_DebugOcclusionTestOverlayPass.enabled = false;

            CustomPassVolume.RegisterUniqueGlobalCustomPass(CustomPassInjectionPoint.AfterPostProcess, m_DebugOccluderDepthOverlayPass);
            CustomPassVolume.RegisterUniqueGlobalCustomPass(CustomPassInjectionPoint.AfterPostProcess, m_DebugOcclusionTestOverlayPass);
#endif
#endif
        }

        public override void Dispose()
        {
#if HAS_PACKAGE_UNITY_HDRP
            CustomPassVolume.UnregisterGlobalCustomPass(m_VisibilityPass);
            CustomPassVolume.UnregisterGlobalCustomPass(m_OcclusionDepthPass);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CustomPassVolume.UnregisterGlobalCustomPass(m_DebugOccluderDepthOverlayPass);
            CustomPassVolume.UnregisterGlobalCustomPass(m_DebugOcclusionTestOverlayPass);
#endif
#endif
        }

        public override void EnqueueCameraPasses(Camera camera, FloraRenderPipelineCameraSettings cameraSettings)
        {
#if HAS_PACKAGE_UNITY_HDRP
            m_OcclusionDepthPass.enabled = cameraSettings.UseGPUOcclusionCulling;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugDisplayFlora.Active)
            {
                if (m_OcclusionDepthPass.enabled)
                {
                    m_DebugOccluderDepthOverlayPass.enabled = DebugDisplayFlora.Properties.OccluderDepthOverlayEnabled;
                    m_DebugOcclusionTestOverlayPass.enabled = DebugDisplayFlora.Properties.OcclusionTestOverlayEnabled;
                }
            }
#endif
#endif
        }

#if HAS_PACKAGE_UNITY_HDRP
        class FloraVisibilityPass : CustomPass
        {
            public FloraVisibilityPass()
            {
                name = nameof(FloraVisibilityPass);
                targetColorBuffer = TargetBuffer.None;
                targetDepthBuffer = TargetBuffer.None;
            }

            protected override void Execute(CustomPassContext ctx)
            {
                var hdCamera = ctx.hdCamera;

                var isSinglePassXR = hdCamera.IsSinglePassXREnabled();
                var subviewCount = isSinglePassXR ? 2 : 1;
                var indirectCullingSettings = new OcclusionCullingSettings(hdCamera.camera.GetEntityId(), OcclusionTest.TestAll)
                {
                    instanceMultiplier = (isSinglePassXR && !SystemInfo.supportsMultiview) ? 2 : 1,
                };

                Span<SubviewOcclusionTest> occlusionSubviewTests = stackalloc SubviewOcclusionTest[subviewCount];
                for (int subviewIndex = 0; subviewIndex < subviewCount; ++subviewIndex)
                {
                    occlusionSubviewTests[subviewIndex] = new SubviewOcclusionTest
                    {
                        cullingSplitIndex = 0,
                        occluderSubviewIndex = subviewIndex,
                    };
                }

                FloraSystem.Instance.CullingSystem.DispatchQueuedCullingRequests(ctx.cmd,
                    hdCamera.volumeStack, indirectCullingSettings, occlusionSubviewTests);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var debugDisplay = DebugDisplayFlora.Active && DebugDisplayFlora.Properties.InstanceDrawMode != DebugInstanceDrawMode.None;
                if (debugDisplay)
                {
                    ctx.cmd.SetKeyword(DebugGlobalKeywords.DebugDisplay, true);
                }
#endif
            }
        }

        class FloraOcclusionDepthPass : CustomPass
        {
            public FloraOcclusionDepthPass()
            {
                name = nameof(FloraOcclusionDepthPass);
                targetColorBuffer = TargetBuffer.None;
                targetDepthBuffer = TargetBuffer.None;
            }

            protected override void Execute(CustomPassContext ctx)
            {
                var hdCamera = ctx.hdCamera;
                var isSinglePassXR = hdCamera.IsSinglePassXREnabled();
                var xrViewConstants = hdCamera.GetXRViewConstants();
                var occlusionParams = new OccluderParameters(hdCamera.camera.GetEntityId())
                {
                    SubviewCount = isSinglePassXR ? 2 : 1,
                    DepthTextureRT = ctx.cameraDepthBuffer,
                    DepthSize = new Vector2Int(hdCamera.actualWidth, hdCamera.actualHeight),
                    DepthIsArray = TextureXR.useTexArray,
                };

                Span<OccluderSubviewUpdate> occluderSubviewUpdates = stackalloc OccluderSubviewUpdate[occlusionParams.SubviewCount];
                for (int subviewIndex = 0; subviewIndex < occlusionParams.SubviewCount; ++subviewIndex)
                {
                    occluderSubviewUpdates[subviewIndex] = new OccluderSubviewUpdate(subviewIndex)
                    {
                        depthSliceIndex = subviewIndex,
                        viewMatrix = xrViewConstants[subviewIndex].viewMatrix,
                        invViewMatrix = xrViewConstants[subviewIndex].invViewMatrix,
                        gpuProjMatrix = xrViewConstants[subviewIndex].projMatrix,
                        viewOffsetWorldSpace = xrViewConstants[subviewIndex].worldSpaceCameraPos,
                    };
                }

                FloraSystem.Instance.CullingSystem.BuildOcclusionDepth(ctx.cmd,
                    occlusionParams, occluderSubviewUpdates);
            }
        }

        class FloraDebugOccluderDepthOverlayPass : CustomPass
        {
            DebugOverlay m_DebugOverlay;

            public FloraDebugOccluderDepthOverlayPass(DebugOverlay debugOverlay)
            {
                m_DebugOverlay = debugOverlay;
                name = nameof(FloraDebugOccluderDepthOverlayPass);
                targetColorBuffer = TargetBuffer.Camera;
                targetDepthBuffer = TargetBuffer.None;
            }

            protected override void Execute(CustomPassContext ctx)
            {
                Rect rect = m_DebugOverlay.Next();
                FloraSystem.Instance.CullingSystem.RenderOcclusionDebugDepthOverlay(ctx.cmd,
                    ctx.hdCamera.camera.GetEntityId(), new Vector2(rect.x, rect.y), rect.height);
            }
        }

        class FloraDebugOcclusionTestOverlayPass : CustomPass
        {
            public FloraDebugOcclusionTestOverlayPass()
            {
                name = nameof(FloraDebugOcclusionTestOverlayPass);
                targetColorBuffer = TargetBuffer.Camera;
                targetDepthBuffer = TargetBuffer.None;
            }

            protected override void Execute(CustomPassContext ctx)
            {
                FloraSystem.Instance.CullingSystem.RenderOcclusionDebugTestOverlay(ctx.cmd,
                    ctx.hdCamera.camera.GetEntityId());
            }
        }
#endif
    }
}
