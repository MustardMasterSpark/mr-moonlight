// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using MA.InternalBridge;
using Unity.Collections;
using UnityEngine;

namespace MA.Flora
{
    [GenerateBurstMonoInterop]
    internal static partial class TerrainDataBurstInterop
    {
        [BurstMonoInteropMethod(true)]
        private static NativeArray<TreeInstance> _GetTreeInstances(IntPtr terrainDataPtr, Allocator allocator)
        {
            return TerrainBridge.GetTreeInstances(terrainDataPtr, allocator);
        }

        [BurstMonoInteropMethod(true)]
        private static void _SetTreeInstances(IntPtr terrainDataPtr, NativeArray<TreeInstance> instances, bool snapToHeightmap)
        {
            TerrainBridge.SetTreeInstances(terrainDataPtr, instances, snapToHeightmap);
        }

        [BurstMonoInteropMethod(true)]
        private static NativeArray<DetailInstanceTransform> _ComputeDetailInstanceTransforms(IntPtr terrainDataPtr, int patchX, int patchY, int layer, float density, Allocator allocator, out Bounds bounds)
        {
            return TerrainBridge.ComputeDetailInstanceTransforms(terrainDataPtr, patchX, patchY, layer, density, allocator, out bounds);
        }

        [BurstMonoInteropMethod(true)]
        private static Vector3 _GetInterpolatedNormal(IntPtr terrainDataPtr, float x, float y)
        {
            return TerrainBridge.GetInterpolatedNormal(terrainDataPtr, x, y);
        }
    }
}
