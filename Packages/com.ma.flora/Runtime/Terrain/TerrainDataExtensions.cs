// Copyright © Magnetic Arcade. All Rights Reserved.

using MA.InternalBridge;
using Unity.Collections;
using UnityEngine;

namespace MA.Flora
{
    internal static class TerrainDataExtensions
    {
        public static void RemoveTreePrototype(this TerrainData terrainData, int index)
        {
            TerrainBridge.RemoveTreePrototype(terrainData, index);
        }

        public static void GetTreeInstances(this TerrainData terrainData, NativeList<TreeInstance> treeInstances)
        {
            TerrainBridge.GetTreeInstances(terrainData, treeInstances);
        }

        public static NativeArray<TreeInstance> GetTreeInstances(this TerrainData terrainData, Allocator allocator)
        {
            return TerrainBridge.GetTreeInstances(terrainData, allocator);
        }

        public static void SetTreeInstances(this TerrainData terrainData, NativeArray<TreeInstance> instances, bool snapToHeightmap)
        {
            TerrainBridge.SetTreeInstances(terrainData, instances, snapToHeightmap);
        }

        public static NativeArray<DetailInstanceTransform> ComputeDetailInstanceTransforms(
            this TerrainData terrainData, int patchX, int patchY, int layer, float density, Allocator allocator,
            out Bounds bounds)
        {
            return TerrainBridge.ComputeDetailInstanceTransforms(terrainData, patchX, patchY, layer, density, allocator, out bounds);
        }

        public static  void SetDetailLayer(
            this TerrainData terrainData,
            int xBase, int yBase, int totalWidth, int totalHeight,
            int detailIndex, NativeArray<int> data)
        {
            TerrainBridge.SetDetailLayer(terrainData, xBase, yBase, totalWidth, totalHeight, detailIndex, data);
        }

        public static GameObject RootGameObject(this GameObject gameObject)
        {
            return gameObject == null ? null : gameObject.transform.root.gameObject;
        }

        public static GameObject PrototypeRootGameObject(this DetailPrototype detailPrototype)
        {
            return detailPrototype.prototype.RootGameObject();
        }
    }
}
