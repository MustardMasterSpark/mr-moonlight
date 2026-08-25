// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using MA.InternalBridge;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal struct DisposableBatchCullingContext : IDisposable
    {
        public BatchCullingContext Value;
        public DisposableBatchCullingContext(BatchCullingContext value) => Value = value;
        public void Dispose() => Value.Dispose();
        public JobHandle Dispose(JobHandle dependency) => Value.Dispose(dependency);
        public static implicit operator BatchCullingContext(DisposableBatchCullingContext disposable) => disposable.Value;
        public static implicit operator DisposableBatchCullingContext(BatchCullingContext context) => new DisposableBatchCullingContext(context);
    }

    internal static class BatchCullingExtensions
    {
        public static EntityId GetEntityIdCompat(this BatchPackedCullingViewID viewId)
        {
#if UNITY_6000_5_OR_NEWER
            return viewId.GetEntityId();
#else
            return viewId.GetInstanceID();
#endif
        }

        public static bool IsCreated(this in BatchCullingContext context)
        {
            return context.cullingPlanes.IsCreated && context.cullingSplits.IsCreated;
        }

        public static void Dispose(this ref BatchCullingContext context)
        {
            if (context.cullingPlanes.IsCreated)
                context.cullingPlanes.Dispose();

            if (context.cullingSplits.IsCreated)
                context.cullingSplits.Dispose();
        }

        public static JobHandle Dispose(this ref BatchCullingContext context, JobHandle dependency)
        {
            if (context.cullingPlanes.IsCreated)
                dependency = context.cullingPlanes.Dispose(dependency);

            if (context.cullingSplits.IsCreated)
                dependency = context.cullingSplits.Dispose(dependency);

            return dependency;
        }

        public static BatchCullingContext Clone(this BatchCullingContext cc, Allocator allocator)
        {
            var cullingPlanes = cc.cullingPlanes.IsCreated ? new NativeArray<Plane>(cc.cullingPlanes, allocator) : default;
            var cullingSplits = cc.cullingSplits.IsCreated ? new NativeArray<CullingSplit>(cc.cullingSplits, allocator) : default;
            var viewID = cc.viewID;

            return BatchRendererGroupBridge.CreateCustomBatchCullingContext(
                cullingPlanes,
                cullingSplits,
                cc.lodParameters,
                cc.localToWorldMatrix,
                cc.viewType,
                cc.projectionType,
                cc.cullingFlags,
                UnsafeUtility.As<BatchPackedCullingViewID, ulong>(ref viewID),
                cc.cullingLayerMask,
                cc.sceneCullingMask,
                inReceiverPlaneOffset: cc.receiverPlaneOffset,
                inReceiverPlaneCount: cc.receiverPlaneCount);
        }

        public static DisposableBatchCullingContext CreateCameraCullingContext(Camera camera, Allocator allocator, BatchCullingViewType viewType)
        {
            if (!camera.TryGetCullingParameters(out var cullingParameters))
                return default;

            return CreateCameraCullingContext(camera, cullingParameters, allocator, viewType);
        }

        public static DisposableBatchCullingContext CreateCameraCullingContext(Camera camera, in ScriptableCullingParameters cullingParameters, Allocator allocator, BatchCullingViewType viewType)
        {
            var cullingPlanes = new NativeArray<Plane>(6, allocator);
            var cullingSplits = new NativeArray<CullingSplit>(1, allocator);
            return CreateCameraCullingContext(camera, cullingParameters, cullingPlanes, cullingSplits, viewType);
        }

        public static DisposableBatchCullingContext CreateCameraCullingContext(
            Camera camera, in ScriptableCullingParameters cullingParameters,
            NativeArray<Plane> cullingPlanes, NativeArray<CullingSplit> cullingSplits,
            BatchCullingViewType viewType)
        {
#if UNITY_6000_5_OR_NEWER
            ulong packedCullingViewID = EntityId.ToULong(camera.GetEntityId());
#else
            var viewID = new BatchPackedCullingViewID(camera.GetEntityId(), 0);
            var packedCullingViewID = UnsafeUtility.As<BatchPackedCullingViewID, ulong>(ref viewID);
#endif

            var frustumPlanes = cullingPlanes.GetSubArray(0, 6);
            for (int i = 0; i < 6; i++)
                frustumPlanes[i] = cullingParameters.GetCullingPlane(i);

            cullingSplits[0] = new CullingSplit
            {
                cullingPlaneOffset = 0,
                cullingPlaneCount = 6,
                nearPlane = camera.nearClipPlane,
                cullingMatrix = cullingParameters.cullingMatrix
            };

            var localToWorldMatrix = camera.transform.localToWorldMatrix;
            var projectionType = cullingParameters.isOrthographic ? BatchCullingProjectionType.Orthographic : BatchCullingProjectionType.Perspective;
            var cullingLayerMask = (uint)camera.cullingMask;
            var sceneCullingMask = CullingUtility.GetSceneCullingMaskFromCamera(camera);

            return BatchRendererGroupBridge.CreateCustomBatchCullingContext(
                cullingPlanes,
                cullingSplits,
                cullingParameters.lodParameters,
                localToWorldMatrix,
                viewType,
                projectionType,
                BatchCullingFlags.None,
                packedCullingViewID,
                cullingLayerMask,
                sceneCullingMask);
        }

        private static readonly Plane[] TempPlanes = new Plane[6];

        public static DisposableBatchCullingContext CreatePickingCullingContext(Camera camera, Matrix4x4 projectionMatrix, Allocator allocator)
        {
            var cullingPlanes = new NativeArray<Plane>(6, allocator);
            var cullingSplits = new NativeArray<CullingSplit>(1, allocator);
#if UNITY_6000_5_OR_NEWER
            ulong packedCullingViewID = EntityId.ToULong(camera.GetEntityId());
#else
            var viewID = new BatchPackedCullingViewID(camera.GetEntityId(), 0);
            var packedCullingViewID = UnsafeUtility.As<BatchPackedCullingViewID, ulong>(ref viewID);
#endif

            var cullingMatrix = projectionMatrix * camera.worldToCameraMatrix;
            GeometryUtility.CalculateFrustumPlanes(camera, TempPlanes);

            var frustumPlanes = cullingPlanes.GetSubArray(0, 6);
            for (int i = 0; i < 6; i++)
                frustumPlanes[i] = TempPlanes[i];

            cullingSplits[0] = new CullingSplit
            {
                cullingPlaneOffset = 0,
                cullingPlaneCount = 6,
                nearPlane = camera.nearClipPlane,
                cullingMatrix = cullingMatrix
            };

            var isOrthographic = cullingMatrix[15] == 1.0f && cullingMatrix[11] == 0.0f && cullingMatrix[7] == 0.0f && cullingMatrix[3] == 0.0f;
            var localToWorldMatrix = camera.transform.localToWorldMatrix;
            var projectionType = isOrthographic ? BatchCullingProjectionType.Orthographic : BatchCullingProjectionType.Perspective;
            var cullingLayerMask = (uint)camera.cullingMask;
            var sceneCullingMask = CullingUtility.GetSceneCullingMaskFromCamera(camera);
            var lodParameters = new LODParameters
            {
                isOrthographic = camera.orthographic,
                cameraPosition = camera.transform.position,
                fieldOfView = Mathf.Atan(1.0f / projectionMatrix.m11) * 2.0f * Mathf.Rad2Deg,
                orthoSize = 1.0f / projectionMatrix.m11,
                cameraPixelHeight = camera.pixelHeight,
            };

            return BatchRendererGroupBridge.CreateCustomBatchCullingContext(
                cullingPlanes,
                cullingSplits,
                lodParameters,
                localToWorldMatrix,
                BatchCullingViewType.Light,
                projectionType,
                BatchCullingFlags.None,
                packedCullingViewID,
                cullingLayerMask,
                sceneCullingMask);
        }

        public static DisposableBatchCullingContext CreatePickingCullingContext(Camera camera, NativeArray<Plane> planes, Allocator allocator)
        {
            var cullingPlanes = new NativeArray<Plane>(6, allocator);
            var cullingSplits = new NativeArray<CullingSplit>(1, allocator);
#if UNITY_6000_5_OR_NEWER
            ulong packedCullingViewID = EntityId.ToULong(camera.GetEntityId());
#else
            var viewID = new BatchPackedCullingViewID(camera.GetEntityId(), 0);
            var packedCullingViewID = UnsafeUtility.As<BatchPackedCullingViewID, ulong>(ref viewID);
#endif

            var frustumPlanes = cullingPlanes.GetSubArray(0, 6);
            for (int i = 0; i < 6; i++)
                frustumPlanes[i] = planes[i];

            cullingSplits[0] = new CullingSplit
            {
                cullingPlaneOffset = 0,
                cullingPlaneCount = 6,
                nearPlane = camera.nearClipPlane,
                cullingMatrix = camera.projectionMatrix * camera.worldToCameraMatrix
            };

            var isOrthographic = camera.orthographic;
            var localToWorldMatrix = camera.transform.localToWorldMatrix;
            var projectionType = isOrthographic ? BatchCullingProjectionType.Orthographic : BatchCullingProjectionType.Perspective;
            var cullingLayerMask = (uint)camera.cullingMask;
            var sceneCullingMask = CullingUtility.GetSceneCullingMaskFromCamera(camera);
            var lodParameters = new LODParameters
            {
                isOrthographic = camera.orthographic,
                cameraPosition = camera.transform.position,
                fieldOfView = camera.fieldOfView,
                orthoSize = camera.orthographicSize,
                cameraPixelHeight = camera.pixelHeight,
            };

            return BatchRendererGroupBridge.CreateCustomBatchCullingContext(
                cullingPlanes,
                cullingSplits,
                lodParameters,
                localToWorldMatrix,
                BatchCullingViewType.Light,
                projectionType,
                BatchCullingFlags.None,
                packedCullingViewID,
                cullingLayerMask,
                sceneCullingMask);
        }
    }
}
