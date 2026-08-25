// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace MA.Flora
{
    internal unsafe partial class CullingSystem
    {
        internal OccluderHandles PrepareOcclusionForCullingDispatch(
            CommandBuffer cmd,
            EntityId viewId,
            in OcclusionCullingSettings settings,
            in InstanceOcclusionTestSubviewSettings occlusionTestSubviewSettings,
            OccluderHandles occluderHandles = default)
        {
            var hasOcclusionCulling = settings.occlusionTest != OcclusionTest.None && occlusionTestSubviewSettings.testCount > 0;
            var hasOcclusionCullingDebug = false;

            if (hasOcclusionCulling)
            {
                hasOcclusionCulling = m_OcclusionCuller.TryGetContext(viewId, out var occluderCtx);
                hasOcclusionCulling &= ((occlusionTestSubviewSettings.occluderSubviewMask & occluderCtx.SubviewValidMask) == occlusionTestSubviewSettings.occluderSubviewMask);

                if (!occluderHandles.IsValid())
                    // Non-render graph path, we need to create the handles here.
                    occluderHandles = occluderCtx.Import();

                hasOcclusionCulling &= occluderHandles.IsValid();
                if (hasOcclusionCulling)
                {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (DebugDisplayFlora.Active)
                    {
                        hasOcclusionCulling = !DebugDisplayFlora.Properties.OcclusionOverrideTestToAlwaysPass;
                        hasOcclusionCullingDebug = occluderCtx.IsDebugValid;
                    }
#endif
                    PrepareOcclusionForCulling(cmd, in occluderCtx, settings, occlusionTestSubviewSettings, m_IndirectCullingPass.ShadersWithOcclusion);
                }
            }

            if (!hasOcclusionCulling)
                occluderHandles = default;
            else if (!hasOcclusionCullingDebug)
                occluderHandles.DisableDebug();

            return occluderHandles;
        }

        internal void UpdateOcclusionSilhouettePlanes(EntityId viewId, NativeArray<Plane> planes)
        {
            m_OcclusionCuller.UpdateSilhouettePlanes(viewId, planes);
        }

        internal void PrepareOcclusionForCulling(CommandBuffer cmd, in OcclusionContext occlusionContext, in OcclusionCullingSettings settings, in InstanceOcclusionTestSubviewSettings testSubviewSettings, Span<ComputeShader> cs)
        {
            m_OcclusionCuller.PrepareForCulling(cmd, in occlusionContext, in settings, in testSubviewSettings, cs);
        }

        internal bool BuildOcclusionDepth(CommandBuffer cmd, OccluderParameters input, ReadOnlySpan<OccluderSubviewUpdate> subviews)
        {
            return m_OcclusionCuller.BuildOcclusionDepth(cmd, in input, subviews);
        }

        internal void RenderOcclusionDebugTestOverlay(CommandBuffer cmd, EntityId viewId)
        {
            m_OcclusionCuller.RenderDebugTestOverlay(cmd, viewId);
        }

        internal void RenderOcclusionDebugDepthOverlay(CommandBuffer cmd, EntityId viewId, Vector2 positionScreen, float maxHeight)
        {
            m_OcclusionCuller.RenderDebugViewOverlay(cmd, viewId, positionScreen, maxHeight);
        }

        private class UpdateOccludersPassData
        {
            public OcclusionCuller OcclusionCuller;
            public OccluderParameters Parameters;
            public List<OccluderSubviewUpdate> SubviewUpdates;
            public OccluderHandles Handles;
        }

        internal bool BuildOcclusionDepth(RenderGraph renderGraph, OccluderParameters input, ReadOnlySpan<OccluderSubviewUpdate> subviews)
        {
            var occluderHandles = m_OcclusionCuller.PrepareOcclusionHandles(renderGraph, in input);
            if (!occluderHandles.IsValid())
                return false;

            using var builder = renderGraph.AddComputePass<UpdateOccludersPassData>("FloraUpdateOccluders", out var passData, OcclusionSamplers.UpdateOccluders);
            builder.AllowGlobalStateModification(true);

            passData.OcclusionCuller = m_OcclusionCuller;
            passData.Parameters = input;
            if (passData.SubviewUpdates is null)
                passData.SubviewUpdates = new List<OccluderSubviewUpdate>();
            else
                passData.SubviewUpdates.Clear();

            for (int i = 0; i < subviews.Length; ++i)
                passData.SubviewUpdates.Add(subviews[i]);

            passData.Handles = occluderHandles;

            builder.UseTexture(passData.Parameters.DepthTextureHandle);
            passData.Handles.UseForOccluderUpdate(builder);

            builder.SetRenderFunc(
                (UpdateOccludersPassData data, ComputeGraphContext context) =>
                {
                    Span<OccluderSubviewUpdate> subviewUpdates = stackalloc OccluderSubviewUpdate[data.SubviewUpdates.Count];

                    int subviewMask = 0;
                    for (int i = 0; i < data.SubviewUpdates.Count; ++i)
                    {
                        subviewUpdates[i] = data.SubviewUpdates[i];
                        subviewMask |= 1 << data.SubviewUpdates[i].subviewIndex;
                    }

                    data.OcclusionCuller.CreateDepthPyramid(context.cmd.GetWrappedCommandBuffer(), data.Parameters, subviewUpdates, in data.Handles);
                });

            return true;
        }

        private class OcclusionTestOverlaySetupPassData
        {
            public OcclusionCuller OcclusionCuller;
            public OcclusionCullingDebugShaderVariables Constants;
        }

        private class OcclusionTestOverlayPassData
        {
            public OcclusionCuller OcclusionCuller;
            public BufferHandle DebugPyramid;
        }

        internal void RenderOcclusionDebugTestOverlay(RenderGraph renderGraph, EntityId viewId, TextureHandle colorBuffer)
        {
            if (!DebugDisplayFlora.Active || !DebugDisplayFlora.Properties.OcclusionTestOverlayEnabled)
                return;

            var debugOutput = m_OcclusionCuller.GetOcclusionTestDebugOutput(viewId);
            if (debugOutput.OcclusionDepthOverlay == null)
                return;

            using (var builder = renderGraph.AddComputePass<OcclusionTestOverlaySetupPassData>("FloraOcclusionTestOverlay", out var passData, OcclusionSamplers.OcclusionTestOverlay))
            {
                builder.AllowPassCulling(false);

                passData.OcclusionCuller = m_OcclusionCuller;
                passData.Constants = debugOutput.Constants;

                builder.SetRenderFunc(
                    (OcclusionTestOverlaySetupPassData data, ComputeGraphContext ctx) =>
                    {
                        data.OcclusionCuller.DebugConstantBuffer.Value = data.Constants;
                        ctx.cmd.SetBufferData(data.OcclusionCuller.DebugConstantBuffer.Buffer, data.OcclusionCuller.DebugConstantBuffer.Data);

                        OcclusionMaterials.DebugTestMaterial.SetConstantBuffer(
                            OcclusionNameID.OcclusionCullingDebugShaderVariables,
                            data.OcclusionCuller.DebugConstantBuffer,
                            0, data.OcclusionCuller.DebugConstantBuffer.Stride);
                    });
            }

            using (var builder = renderGraph.AddRasterRenderPass<OcclusionTestOverlayPassData>("FloraOcclusionTestOverlay", out var passData, OcclusionSamplers.OcclusionTestOverlay))
            {
                builder.AllowGlobalStateModification(true);
                builder.SetRenderAttachment(colorBuffer, 0);

                passData.OcclusionCuller = m_OcclusionCuller;
                passData.DebugPyramid = renderGraph.ImportBuffer(debugOutput.OcclusionDepthOverlay);
                builder.UseBuffer(passData.DebugPyramid);

                builder.SetRenderFunc(
                    (OcclusionTestOverlayPassData data, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetGlobalBuffer(OcclusionNameID.OcclusionDebugOverlay, data.DebugPyramid);
                        CoreUtils.DrawFullScreen(ctx.cmd, OcclusionMaterials.DebugTestMaterial);
                    });
            }
        }

        private class OcclusionOverlayPassData
        {
            public RTHandle DepthPyramidTexture;
            public Rect Viewport;
            public int PassIndex;
            public Vector2 ValidRange;
        }

        internal void RenderOcclusionDebugDepthOverlay(RenderGraph renderGraph, EntityId viewId, Vector2 positionScreen, float maxHeight, TextureHandle colorBuffer)
        {
            if (!DebugDisplayFlora.Active || !DebugDisplayFlora.Properties.OccluderDepthOverlayEnabled)
                return;

            var debugOutput = m_OcclusionCuller.GetOcclusionTestDebugOutput(viewId);
            if (debugOutput.DepthPyramid == null)
                return;

            var outputSize = (Vector2)debugOutput.DepthPyramid.referenceSize;
            var scaleFactor = maxHeight / outputSize.y;
            outputSize *= scaleFactor;
            var viewport = new Rect(positionScreen.x, positionScreen.y, outputSize.x, outputSize.y);

            using var builder = renderGraph.AddRasterRenderPass<OcclusionOverlayPassData>("FloraOcclusionDepthOverlay", out var passData, OcclusionSamplers.OccluderOverlay);
            builder.AllowGlobalStateModification(true);
            builder.SetRenderAttachment(colorBuffer, 0);

            passData.DepthPyramidTexture = debugOutput.DepthPyramid;
            passData.Viewport = viewport;
            passData.PassIndex = OcclusionMaterials.DebugViewMaterial.FindPass("DebugOccluder");
            passData.ValidRange = DebugDisplayFlora.Properties.OcclusionDepthViewRange;

            builder.SetRenderFunc(
                (OcclusionOverlayPassData data, RasterGraphContext ctx) =>
                {
                    var mpb = ctx.renderGraphPool.GetTempMaterialPropertyBlock();
                    mpb.SetTexture(OcclusionNameID.OccluderTexture, data.DepthPyramidTexture);
                    mpb.SetVector(OcclusionNameID.ValidRange, data.ValidRange);

                    ctx.cmd.SetViewport(data.Viewport);
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, OcclusionMaterials.DebugViewMaterial, data.PassIndex, MeshTopology.Triangles, 3, 1, mpb);
                });
        }
    }
}
