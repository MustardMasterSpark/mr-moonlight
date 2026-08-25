// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.InternalBridge
{
    internal static class BatchRendererGroupBridge
    {
        public static bool OcclusionTestAABB(IntPtr occlusionBuffer, Bounds aabb)
        {
            return BatchRendererGroup.OcclusionTestAABB(occlusionBuffer, aabb);
        }

        public static IntPtr GetOcclusionBuffer(this in BatchCullingContext context)
        {
            return context.occlusionBuffer;
        }

        public static BatchCullingContext CreateCustomBatchCullingContext(
            NativeArray<Plane> inCullingPlanes,
            NativeArray<CullingSplit> inCullingSplits,
            LODParameters inLodParameters,
            Matrix4x4 inLocalToWorldMatrix,
            BatchCullingViewType inViewType,
            BatchCullingProjectionType inProjectionType,
            BatchCullingFlags inBatchCullingFlags,
            ulong inViewID,
            uint inCullingLayerMask,
            ulong inSceneCullingMask,
            int inReceiverPlaneOffset = 0,
            int inReceiverPlaneCount = 0)
        {
            return new BatchCullingContext(
                inCullingPlanes,
                inCullingSplits,
                inLodParameters,
                inLocalToWorldMatrix,
                inViewType,
                inProjectionType,
                inBatchCullingFlags,
                inViewID,
                inCullingLayerMask,
                inSceneCullingMask,
                0,
                inReceiverPlaneOffset,
                inReceiverPlaneCount,
                IntPtr.Zero
                );
        }
    }
}
