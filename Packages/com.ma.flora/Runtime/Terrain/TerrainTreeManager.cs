// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;

namespace MA.Flora
{
    internal readonly struct TerrainTreePrototype : IEquatable<TerrainTreePrototype>
    {
        public readonly EntityObjectRef<GameObject> Prefab;
        public readonly float3 Scale;
        public readonly ushort MaxDistance;

        public TerrainTreePrototype(Terrain terrain, TreePrototype prototype)
        {
            Prefab = prototype.prefab;
            Scale = prototype.prefab ? prototype.prefab.transform.localScale : Vector3.one;
            MaxDistance = (ushort)math.clamp(terrain.treeDistance, 0, ushort.MaxValue);
        }

        public bool Equals(TerrainTreePrototype other) => Prefab.Equals(other.Prefab) && Scale.Equals(other.Scale) && MaxDistance == other.MaxDistance;
        public override bool Equals(object obj) => obj is TerrainTreePrototype other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Prefab.GetHashCode();
                hashCode = (hashCode * 397) ^ Scale.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxDistance.GetHashCode();
                return hashCode;
            }
        }
    }

    internal struct TerrainTreeManager : IDisposable
    {
        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct BuildTreeIndicesJob : IJob
        {
            [ReadOnly] public int LayerCount;
            [ReadOnly] public NativeArray<TreeInstance> TreeInstances;

            public NativeBufferArray<int> TreeIndicesByLayer;

            public void Execute()
            {
                for (int treeIndex = 0; treeIndex < TreeInstances.Length; treeIndex++)
                {
                    var treeInstance = TreeInstances[treeIndex];
                    var layerIndex = treeInstance.prototypeIndex;
                    if (layerIndex >= 0 && layerIndex < LayerCount)
                        TreeIndicesByLayer[layerIndex].Add(treeIndex);
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct BuildTreeTransformsJob : IJobParallelFor
        {
            [ReadOnly] public float3 Position;
            [ReadOnly] public float3 Size;
            [ReadOnly] public NativeArray<TerrainTreePrototype> TreePrototypes;
            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<TreeInstance> TreeInstances;
            [ReadOnly] public NativeBufferArray<int> TreeIndicesByLayer;

            [NativeDisableParallelForRestriction] public NativeBufferArray<FloraLocalToWorld> TransformsByLayer;

            public void Execute(int layerIndex)
            {
                var transforms = TransformsByLayer[layerIndex];
                var indices = TreeIndicesByLayer[layerIndex];
                var prototype = TreePrototypes[layerIndex];
                if (!prototype.Prefab.IsValid())
                    return;

                for (int i = 0; i < indices.Length; i++)
                {
                    var treeIndex = indices[i];
                    var treeInstance = TreeInstances[treeIndex];

                    var position = Position + treeInstance.position * Size;
                    var rotation = quaternion.RotateY(treeInstance.rotation);
                    var scale = prototype.Scale * new float3(treeInstance.widthScale, treeInstance.heightScale, treeInstance.widthScale);
                    transforms.Add(FloraLocalToWorld.FromPositionRotationScale(position, rotation, scale));
                }
            }
        }

        private NativeDataReference<InstanceManager> m_InstanceManager;
        private NativeList<FloraInstanceHandle> m_TreeInstances;
        private NativeList<TerrainTreePrototype> m_TreePrototypes;
        private NativeBufferArray<FloraInstanceHandle> m_InstancesByLayer;
        private NativeBufferArray<FloraLocalToWorld> m_LocalToWorldByLayer;
        private NativeBufferArray<int> m_TreeIndicesByLayer;
        private bool m_TreesChanged;
        private bool m_Hidden;

        public TerrainTreeManager(InstanceContext instanceContext)
        {
            m_InstanceManager = instanceContext.InstanceManager;
            m_TreesChanged = true;
            m_TreeInstances = new NativeList<FloraInstanceHandle>(Allocator.Persistent);
            m_TreePrototypes = new NativeList<TerrainTreePrototype>(Allocator.Persistent);
            m_InstancesByLayer = new NativeBufferArray<FloraInstanceHandle>(0, 0, Allocator.Persistent);
            m_LocalToWorldByLayer = new NativeBufferArray<FloraLocalToWorld>(0, 0, Allocator.Persistent);
            m_TreeIndicesByLayer = new NativeBufferArray<int>(0, 0, Allocator.Persistent);
            m_Hidden = false;
        }

        public void Dispose()
        {
            Clear();
            m_TreeInstances.Dispose();
            m_TreePrototypes.Dispose();
            m_InstancesByLayer.Dispose();
            m_LocalToWorldByLayer.Dispose();
            m_TreeIndicesByLayer.Dispose();
            m_InstanceManager = default;
        }

        public FloraInstanceHandle GetTreeInstanceHandle(int treeIndex)
        {
            if (treeIndex < 0 || treeIndex >= m_TreeInstances.Length)
                return FloraInstanceHandle.Null;

            return m_TreeInstances[treeIndex];
        }

        public NativeArray<FloraInstanceHandle> GetTreeInstanceHandles(Allocator allocator)
        {
            return new NativeArray<FloraInstanceHandle>(m_TreeInstances.AsArray(), allocator);
        }

        public void SetDirty()
        {
            m_TreesChanged = true;
        }

        public void SetDirty(TerrainChangedFlags flags)
        {
            if ((flags & TerrainChangedFlags.TreeInstances) != 0 ||
                (flags & TerrainChangedFlags.FlushEverythingImmediately) != 0 ||
                (flags & TerrainChangedFlags.DelayedHeightmapUpdate) != 0 ||
                (flags & TerrainChangedFlags.Heightmap) != 0 ||
                (flags & TerrainChangedFlags.HeightmapResolution) != 0 ||
                (flags & TerrainChangedFlags.Holes) != 0 ||
                (flags & TerrainChangedFlags.DelayedHolesUpdate) != 0)
            {
                m_TreesChanged = true;
            }
        }

        public void Clear()
        {
            for (var layer = 0; layer < m_InstancesByLayer.Length; layer++)
            {
                ClearLayer(layer);
            }

            m_InstancesByLayer.Clear();
            m_TreeInstances.Clear();
            m_TreesChanged = true;
        }

        public void ClearLayer(int layer)
        {
            if (layer < 0 || layer >= m_InstancesByLayer.Length)
                return;

            bool hadMappings = m_TreeIndicesByLayer[layer].Length != 0;
            InvalidateTreeInstanceMappings(m_TreeIndicesByLayer[layer]);
            m_TreeIndicesByLayer[layer].Clear();
            m_LocalToWorldByLayer[layer].Clear();

            var instances = m_InstancesByLayer[layer];
            if (instances.Length != 0)
            {
                m_InstanceManager.ValueRW.Destroy(instances.AsArray());
                instances.Clear();
                m_TreesChanged = true;
            }
            else if (hadMappings)
            {
                m_TreesChanged = true;
            }
        }

        private void SetEmpty()
        {
            Clear();
            m_TreesChanged = false;
        }

        private static readonly ProfilerMarker UpdateMarker = new("Flora.TreeManager.Update");

        public void Update(in TerrainSnapshot terrain)
        {
            if (!m_TreesChanged)
                return;

            using var _ = UpdateMarker.Auto();

            if (!terrain.WithinTreeDistance)
            {
                Clear();
                return;
            }

            var treePrototypes = terrain.TreePrototypes;
            if (treePrototypes.Length == 0)
            {
                SetEmpty();
                return;
            }

            var treeInstances = terrain.GetTreeInstances(Allocator.TempJob);
            if (treeInstances.Length == 0)
            {
                treeInstances.Dispose();
                SetEmpty();
                return;
            }

            var oldLayerCount = m_TreePrototypes.Length;
            var newLayerCount = treePrototypes.Length;
            if (oldLayerCount != newLayerCount)
            {
                if (oldLayerCount > newLayerCount)
                {
                    for (int layer = newLayerCount; layer < oldLayerCount; layer++)
                        ClearLayer(layer);
                }

                m_InstancesByLayer.Resize(newLayerCount);
                m_LocalToWorldByLayer.Resize(newLayerCount);
                m_TreeIndicesByLayer.Resize(newLayerCount);
            }

            int retainedLayerCount = math.min(oldLayerCount, newLayerCount);
            for (int layer = 0; layer < retainedLayerCount; layer++)
                InvalidateTreeInstanceMappings(m_TreeIndicesByLayer[layer]);

            m_TreePrototypes.Resize(newLayerCount, NativeArrayOptions.ClearMemory);
            m_TreeInstances.Resize(treeInstances.Length, NativeArrayOptions.ClearMemory);

            for (int layer = 0; layer < newLayerCount; layer++)
            {
                var oldPrototype = layer < oldLayerCount ? m_TreePrototypes[layer] : default;
                var newPrototype = terrain.TreePrototypes[layer];
                if (!oldPrototype.Equals(newPrototype))
                {
                    ClearLayer(layer);
                    m_TreePrototypes[layer] = newPrototype;
                    m_TreesChanged = true;
                }

                m_TreeIndicesByLayer[layer].Clear();  // Filled in BuildTreeIndicesJob
                m_LocalToWorldByLayer[layer].Clear(); // Filled in BuildTreeTransformsJob
            }

            var buildHandle = new BuildTreeIndicesJob {
                LayerCount = newLayerCount,
                TreeInstances = treeInstances,
                TreeIndicesByLayer = m_TreeIndicesByLayer
            }.Schedule();

            buildHandle = new BuildTreeTransformsJob {
                Position = terrain.Position,
                Size = terrain.Size,
                TreePrototypes = m_TreePrototypes.AsArray(),
                TreeInstances = treeInstances,
                TreeIndicesByLayer = m_TreeIndicesByLayer,
                TransformsByLayer = m_LocalToWorldByLayer
            }.Schedule(newLayerCount, 1, buildHandle);

            buildHandle.Complete();

            for (int layerIndex = 0; layerIndex < newLayerCount; layerIndex++)
                UpdatePrototypeLayer(terrain, treePrototypes[layerIndex], layerIndex);

            m_TreesChanged = false;
        }

        private void UpdatePrototypeLayer(in TerrainSnapshot terrain, in TerrainTreePrototype treePrototype, int layerIndex)
        {
            var instances = m_InstancesByLayer[layerIndex];
            var transforms = m_LocalToWorldByLayer[layerIndex];
            int originalLength = instances.Length;
            int targetLength = transforms.Length;
            int newCount = math.max(0, targetLength - originalLength);
            int removeCount = math.max(0, originalLength - targetLength);
            int updateCount = math.min(originalLength, targetLength);

            // Resize down (remove instances)
            if (removeCount > 0)
            {
                var removeInstances = instances.GetSubArray(targetLength, removeCount);
                m_InstanceManager.ValueRW.Destroy(removeInstances);
                instances.Resize(targetLength);
            }

            // Resize up (add new instances)
            if (newCount > 0)
            {
                instances.Resize(targetLength);
                var newInstances = instances.GetSubArray(originalLength, newCount);
                var newTransforms = transforms.GetSubArray(originalLength, newCount);
                m_InstanceManager.ValueRW.InstantiateTreesWithBurst(terrain.Entity, treePrototype.Prefab, newInstances, newTransforms);
            }

            // Update existing instances
            if (updateCount > 0)
            {
                var updateInstances = instances.GetSubArray(0, updateCount);
                var updateLocalToWorlds = transforms.GetSubArray(0, updateCount);
                m_InstanceManager.ValueRW.UpdateLocalToWorlds(updateInstances, updateLocalToWorlds);
            }

            // Update global tree index to instance handle mapping
            var treeIndices = m_TreeIndicesByLayer[layerIndex];
            Assert.IsTrue(instances.Length == treeIndices.Length, "Tree layer mapping count must match the tree index count.");
            m_InstanceManager.ValueRW.SetTerrainTreeIndices(terrain.Entity, instances.AsArray(), treeIndices.AsArray());
            for (int i = 0; i < instances.Length; i++)
                m_TreeInstances[treeIndices[i]] = instances[i];

#if UNITY_EDITOR
            if (m_Hidden)
                SetHidden();
#endif
        }

        private void InvalidateTreeInstanceMappings(NativeBuffer<int> treeIndices)
        {
            for (int i = 0; i < treeIndices.Length; i++)
            {
                int treeIndex = treeIndices[i];
                if (treeIndex >= 0 && treeIndex < m_TreeInstances.Length)
                    m_TreeInstances[treeIndex] = FloraInstanceHandle.Null;
            }
        }

#if UNITY_EDITOR
        internal void ClearHidden()
        {
            m_Hidden = false;
        }

        internal void SetHidden()
        {
            m_InstanceManager.ValueRW.SetHidden(m_TreeInstances.AsArray());
        }
#endif
    }
}
