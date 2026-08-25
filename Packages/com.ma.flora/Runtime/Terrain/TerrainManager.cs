// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using MA.InternalBridge;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace MA.Flora
{
    #region Terrain Snapshot

    [Flags]
    internal enum TerrainSnapshotRefresh
    {
        None = 0,
        DynamicData = 1 << 0,
        Prototypes = 1 << 1,
        All = DynamicData | Prototypes,
    }

    internal struct TerrainSnapshot : IDisposable
    {
        public bool IsCreated;
        public EntityId Entity;
        public IntPtr TerrainPtr;
        public IntPtr TerrainDataPtr;

        public float3 Position;
        public float3 Size;
        public Bounds Bounds;

        public NativeArray<TerrainTreePrototype> TreePrototypes;
        public bool WithinTreeDistance;
        public float TreeDistance;

        public NativeArray<TerrainDetailPrototype> DetailPrototypes;
        public bool WithinDetailsRange;
        public float DetailDistance;
        public float DetailDensity;
        public int DetailPatchCount;

        public TerrainSnapshot(Terrain terrain, Allocator allocator)
        {
            if (!terrain || !terrain.terrainData)
            {
                this = default;
                return;
            }

            var terrainData = terrain.terrainData;
            Entity = terrain.GetEntityId();
            TerrainPtr = TerrainBridge.MarshalFromInstanceId<Terrain>(terrain.GetEntityId());
            TerrainDataPtr = TerrainBridge.MarshalFromInstanceId<TerrainData>(terrainData.GetEntityId());
            Position = terrain.transform.position;
            Size = terrainData.size;
            Bounds = terrainData.bounds;
            Bounds.center += (Vector3)Position;

            var treePrototypes = terrainData.treePrototypes;
            TreePrototypes = new NativeArray<TerrainTreePrototype>(treePrototypes.Length, allocator);
            for (int i = 0; i < treePrototypes.Length; i++)
                TreePrototypes[i] = new TerrainTreePrototype(terrain, treePrototypes[i]);

            WithinTreeDistance = false;
            TreeDistance = terrain.treeDistance;

            var detailPrototypes = terrainData.detailPrototypes;
            DetailPrototypes = new NativeArray<TerrainDetailPrototype>(detailPrototypes.Length, allocator);
            for (int i = 0; i < detailPrototypes.Length; i++)
                DetailPrototypes[i] = new TerrainDetailPrototype(terrain, detailPrototypes[i], i);

            WithinDetailsRange = false;
            DetailDistance = terrain.detailObjectDistance;
            DetailDensity = terrain.detailObjectDensity;
            DetailPatchCount = terrainData.detailPatchCount;
            IsCreated = true;
        }

        public void Refresh(Terrain terrain, Allocator allocator, TerrainSnapshotRefresh refreshMask)
        {
            if (!terrain || !terrain.terrainData)
            {
                Dispose();
                this = default;
                return;
            }

            if (!IsCreated)
            {
                this = new TerrainSnapshot(terrain, allocator);
                return;
            }

            if ((refreshMask & TerrainSnapshotRefresh.Prototypes) != 0)
                refreshMask |= TerrainSnapshotRefresh.DynamicData;

            if ((refreshMask & TerrainSnapshotRefresh.DynamicData) != 0)
                RefreshDynamicData(terrain);

            if ((refreshMask & TerrainSnapshotRefresh.Prototypes) != 0)
                RefreshPrototypes(terrain, allocator);
        }

        private void RefreshDynamicData(Terrain terrain)
        {
            var terrainData = terrain.terrainData;

            Entity = terrain.GetEntityId();
            TerrainPtr = TerrainBridge.MarshalFromInstanceId<Terrain>(Entity);
            TerrainDataPtr = TerrainBridge.MarshalFromInstanceId<TerrainData>(terrainData.GetEntityId());
            Position = terrain.transform.position;
            Size = terrainData.size;
            Bounds = terrainData.bounds;
            Bounds.center += (Vector3)Position;

            TreeDistance = terrain.treeDistance;
            DetailDistance = terrain.detailObjectDistance;
            DetailDensity = terrain.detailObjectDensity;
            DetailPatchCount = terrainData.detailPatchCount;
            WithinTreeDistance = false;
            WithinDetailsRange = false;
            IsCreated = true;
        }

        private void RefreshPrototypes(Terrain terrain, Allocator allocator)
        {
            if (TreePrototypes.IsCreated)
                TreePrototypes.Dispose();
            if (DetailPrototypes.IsCreated)
                DetailPrototypes.Dispose();

            var terrainData = terrain.terrainData;

            var treePrototypes = terrainData.treePrototypes;
            TreePrototypes = new NativeArray<TerrainTreePrototype>(treePrototypes.Length, allocator);
            for (int i = 0; i < treePrototypes.Length; i++)
                TreePrototypes[i] = new TerrainTreePrototype(terrain, treePrototypes[i]);

            var detailPrototypes = terrainData.detailPrototypes;
            DetailPrototypes = new NativeArray<TerrainDetailPrototype>(detailPrototypes.Length, allocator);
            for (int i = 0; i < detailPrototypes.Length; i++)
                DetailPrototypes[i] = new TerrainDetailPrototype(terrain, detailPrototypes[i], i);
        }

        public void Dispose()
        {
            if (!IsCreated)
                return;

            TreePrototypes.Dispose();
            DetailPrototypes.Dispose();
            IsCreated = false;
        }

        public NativeArray<TreeInstance> GetTreeInstances(Allocator allocator)
        {
            return TerrainDataBurstInterop.GetTreeInstances(TerrainDataPtr, allocator);
        }

        public void SetTreeInstances(NativeArray<TreeInstance> instances, bool snapToTerrain)
        {
            TerrainDataBurstInterop.SetTreeInstances(TerrainDataPtr, instances, snapToTerrain);
        }

        public NativeArray<DetailInstanceTransform> ComputeDetailInstanceTransforms(int patchX, int patchY, int layer, float density, Allocator allocator)
        {
            return TerrainDataBurstInterop.ComputeDetailInstanceTransforms(TerrainDataPtr, patchX, patchY, layer, density, allocator, out _);
        }

        public float3 GetInterpolatedNormal(float x, float y)
        {
            return TerrainDataBurstInterop.GetInterpolatedNormal(TerrainDataPtr, x, y);
        }
    }

    #endregion

    #region Terrain System

    internal struct TerrainSystemSettings
    {
        public bool AllowPerTreeMotionVectors;
        public bool AllowPerTreeLightProbes;

        public bool AllowPerDetailMotionVectors;
        public bool AllowPerDetailLightProbes;
        public float DetailStreamingDeltaTime;
        public float DetailUnloadHysteresisSeconds;
        public int DetailPatchLayerBudgetPerFrame;
        public int DetailStructuralInstanceBudgetPerFrame;
    }

    [BurstCompile]
    internal unsafe struct TerrainManager : IDisposable
    {
        private InstanceContext m_NativeContext;
        private UnsafeBitSet m_AllocatedIndices;
        private UnsafeBitSet m_DirtyTerrainIndices;
        private UnsafeList<TerrainSnapshotRefresh> m_TerrainRefreshMasks;
        private UnsafeList<int> m_FreeIndices;
        private UnsafeParallelHashMap<EntityId, int> m_TerrainEntityIdToIndexMap;
        private UnsafeList<EntityId> m_TerrainEntityIds;
        private UnsafeList<TerrainSnapshot> m_TerrainSnapshots;
        private UnsafeList<TerrainTreeManager> m_TreeManagers;
        private UnsafeList<TerrainDetailManager> m_DetailManagers;
        private JobHandle m_TerrainUpdateHandle;
        private NativeDataReference<StreamingSphereManager> m_StreamingManager;
        private int m_NextDetailTerrainIndex;

        private const TerrainChangedFlags AllChangedFlags = (TerrainChangedFlags)0xFFFFFFF;

        private const TerrainChangedFlags AllHeightmapFlags =
            TerrainChangedFlags.Heightmap | TerrainChangedFlags.HeightmapResolution | TerrainChangedFlags.DelayedHeightmapUpdate |
            TerrainChangedFlags.Holes | TerrainChangedFlags.DelayedHolesUpdate;

        private TerrainManager* Self => (TerrainManager*)UnsafeUtility.AddressOf(ref this);

        public void Initialize(InstanceContext instanceContext)
        {
            m_NativeContext = instanceContext;
            m_StreamingManager = instanceContext.StreamingManager;
            m_AllocatedIndices = new UnsafeBitSet(16, Allocator.Persistent);
            m_DirtyTerrainIndices = new UnsafeBitSet(16, Allocator.Persistent);
            m_TerrainRefreshMasks = new UnsafeList<TerrainSnapshotRefresh>(16, Allocator.Persistent);
            m_FreeIndices = new UnsafeList<int>(16, Allocator.Persistent);
            m_TerrainEntityIdToIndexMap = new UnsafeParallelHashMap<EntityId, int>(16, Allocator.Persistent);
            m_TerrainEntityIds = new UnsafeList<EntityId>(16, Allocator.Persistent);
            m_TerrainSnapshots = new UnsafeList<TerrainSnapshot>(16, Allocator.Persistent);
            m_TreeManagers = new UnsafeList<TerrainTreeManager>(16, Allocator.Persistent);
            m_DetailManagers = new UnsafeList<TerrainDetailManager>(16, Allocator.Persistent);
            m_TerrainUpdateHandle = default;
            m_NextDetailTerrainIndex = 0;
        }

        public void Dispose()
        {
            m_TerrainUpdateHandle.Complete();

            foreach (int index in m_AllocatedIndices)
            {
                m_TerrainSnapshots[index].Dispose();
                m_TreeManagers[index].Dispose();
                m_DetailManagers[index].Dispose();
            }

            m_AllocatedIndices.Dispose();
            m_DirtyTerrainIndices.Dispose();
            m_TerrainRefreshMasks.Dispose();
            m_FreeIndices.Dispose();
            m_TerrainEntityIdToIndexMap.Dispose();
            m_TerrainEntityIds.Dispose();
            m_TerrainSnapshots.Dispose();
            m_TreeManagers.Dispose();
            m_DetailManagers.Dispose();
        }

        public void Register(Terrain terrain)
        {
            if (terrain == null)
                return;

            EntityId entityId = terrain.GetEntityId();
            if (m_TerrainEntityIdToIndexMap.ContainsKey(entityId))
                return;

            m_TerrainUpdateHandle.Complete();

            int newTerrainIndex;
            if (m_FreeIndices.Length > 0)
            {
                newTerrainIndex = m_FreeIndices[^1];
                m_FreeIndices.RemoveAtSwapBack(m_FreeIndices.Length - 1);
            }
            else
            {
                if (m_TerrainSnapshots.Length == m_TerrainSnapshots.Capacity)
                {
                    int newCapacity = m_TerrainSnapshots.Length + 16;
                    m_TerrainRefreshMasks.SetCapacity(newCapacity);
                    m_TerrainEntityIds.SetCapacity(newCapacity);
                    m_TerrainSnapshots.SetCapacity(newCapacity);
                    m_TreeManagers.SetCapacity(newCapacity);
                    m_DetailManagers.SetCapacity(newCapacity);
                }

                newTerrainIndex = m_TerrainSnapshots.Length;
                int newCount = newTerrainIndex + 1;
                m_TerrainRefreshMasks.Resize(newCount, NativeArrayOptions.ClearMemory);
                m_TerrainEntityIds.Resize(newCount, NativeArrayOptions.ClearMemory);
                m_TerrainSnapshots.Resize(newCount, NativeArrayOptions.ClearMemory);
                m_TreeManagers.Resize(newCount, NativeArrayOptions.ClearMemory);
                m_DetailManagers.Resize(newCount, NativeArrayOptions.ClearMemory);
            }

            m_AllocatedIndices.Add(newTerrainIndex);
            m_TerrainEntityIdToIndexMap.Add(entityId, newTerrainIndex);
            m_TerrainEntityIds[newTerrainIndex] = entityId;
            m_TerrainSnapshots[newTerrainIndex] = new TerrainSnapshot(terrain, Allocator.Persistent);
            m_TreeManagers[newTerrainIndex] = new TerrainTreeManager(m_NativeContext);
            m_DetailManagers[newTerrainIndex] = new TerrainDetailManager(m_NativeContext);
        }

        public void Unregister(EntityId entityId)
        {
            if (!entityId.IsValid())
                return;

            m_TerrainUpdateHandle.Complete();
            if (!m_TerrainEntityIdToIndexMap.TryGetValue(entityId, out var terrainIndex))
                return;

            m_TerrainEntityIdToIndexMap.Remove(entityId);
            m_AllocatedIndices.Remove(terrainIndex);
            m_DirtyTerrainIndices.Remove(terrainIndex);
            m_TerrainRefreshMasks.Ptr[terrainIndex] = TerrainSnapshotRefresh.None;
            m_FreeIndices.Add(terrainIndex);
            m_TerrainSnapshots.Ptr[terrainIndex].Dispose();
            m_TreeManagers.Ptr[terrainIndex].Dispose();
            m_DetailManagers.Ptr[terrainIndex].Dispose();
            m_TerrainEntityIds.Ptr[terrainIndex] = default;
            m_TerrainSnapshots.Ptr[terrainIndex] = default;
            m_TreeManagers.Ptr[terrainIndex] = default;
            m_DetailManagers.Ptr[terrainIndex] = default;
        }

        public void Clear()
        {
            m_TerrainUpdateHandle.Complete();

            foreach (int index in m_AllocatedIndices)
            {
                m_TerrainSnapshots[index].Dispose();
                m_TreeManagers[index].Dispose();
                m_DetailManagers[index].Dispose();
            }

            m_AllocatedIndices.Clear();
            m_DirtyTerrainIndices.Clear();
            m_TerrainRefreshMasks.Clear();
            m_FreeIndices.Clear();
            m_TerrainEntityIdToIndexMap.Clear();
            m_TerrainEntityIds.Clear();
            m_TerrainSnapshots.Clear();
            m_TreeManagers.Clear();
            m_DetailManagers.Clear();
            m_NextDetailTerrainIndex = 0;
        }

        public FloraInstanceHandle GetTreeInstanceHandle(EntityObjectRef<Terrain> terrain, int treeIndex)
        {
            if (m_TerrainEntityIdToIndexMap.TryGetValue(terrain.Value, out var terrainIndex))
            {
                var treeManager = m_TreeManagers.Ptr[terrainIndex];
                return treeManager.GetTreeInstanceHandle(treeIndex);
            }

            return FloraInstanceHandle.Null;
        }

        public NativeArray<FloraInstanceHandle> GetTreeInstanceHandles(EntityObjectRef<Terrain> terrain, Allocator allocator)
        {
            if (m_TerrainEntityIdToIndexMap.TryGetValue(terrain.Value, out var terrainIndex))
            {
                var treeManager = m_TreeManagers.Ptr[terrainIndex];
                return treeManager.GetTreeInstanceHandles(allocator);
            }

            return default;
        }

        public void SetSettingsDirty(NativeArray<EntityId> terrainInstanceIds)
        {
            foreach (var terrainInstanceId in terrainInstanceIds)
            {
                if (m_TerrainEntityIdToIndexMap.TryGetValue(terrainInstanceId, out var terrainIndex))
                {
                    MarkTerrainSnapshotDirty(terrainIndex, TerrainSnapshotRefresh.All);
                    m_TreeManagers.Ptr[terrainIndex].SetDirty();
                    m_DetailManagers.Ptr[terrainIndex].SetDirty();
                }
            }
        }

        public void SetTransformDirty(EntityId terrainInstanceId)
        {
            SetDirty(terrainInstanceId, AllChangedFlags);
        }

        public void SetHeightmapDirty(EntityId terrainInstanceId)
        {
            SetDirty(terrainInstanceId, AllHeightmapFlags);
        }

        public void SetDirty(EntityId terrainInstanceId, TerrainChangedFlags changedFlags)
        {
            if (m_TerrainEntityIdToIndexMap.TryGetValue(terrainInstanceId, out var terrainIndex))
            {
                MarkTerrainSnapshotDirty(terrainIndex, GetSnapshotRefreshMask(changedFlags));
                m_TreeManagers.Ptr[terrainIndex].SetDirty(changedFlags);
                m_DetailManagers.Ptr[terrainIndex].SetDirty(changedFlags);
            }
        }

        private static TerrainSnapshotRefresh GetSnapshotRefreshMask(TerrainChangedFlags changedFlags)
        {
            const TerrainChangedFlags noSnapshotRefreshFlags = TerrainChangedFlags.TreeInstances;
            const TerrainChangedFlags dynamicOnlyFlags =
                TerrainChangedFlags.Heightmap |
                TerrainChangedFlags.HeightmapResolution |
                TerrainChangedFlags.DelayedHeightmapUpdate |
                TerrainChangedFlags.Holes |
                TerrainChangedFlags.DelayedHolesUpdate |
                TerrainChangedFlags.RemoveDirtyDetailsImmediately;

            TerrainChangedFlags unknownFlags = changedFlags & ~(noSnapshotRefreshFlags | dynamicOnlyFlags);
            if (unknownFlags != 0)
                return TerrainSnapshotRefresh.All;

            if ((changedFlags & dynamicOnlyFlags) != 0)
                return TerrainSnapshotRefresh.DynamicData;

            return TerrainSnapshotRefresh.None;
        }

        private void MarkTerrainSnapshotDirty(int terrainIndex, TerrainSnapshotRefresh refreshMask)
        {
            if (refreshMask == TerrainSnapshotRefresh.None)
                return;

            m_DirtyTerrainIndices.Add(terrainIndex);
            m_TerrainRefreshMasks.Ptr[terrainIndex] |= refreshMask;
        }

        private static readonly ProfilerMarker UpdateTerrainManagerMarker = new("Flora.UpdateTerrainManager");

        public void Update(TerrainSystemSettings settings)
        {
            using var _ = UpdateTerrainManagerMarker.Auto();

            if (!m_DirtyTerrainIndices.IsEmpty)
            {
                foreach (int terrainIndex in m_DirtyTerrainIndices)
                {
                    ref var terrainSnapshot = ref m_TerrainSnapshots.Ptr[terrainIndex];
                    var entityId = m_TerrainEntityIds[terrainIndex];
                    var refreshMask = m_TerrainRefreshMasks.Ptr[terrainIndex];
                    if (refreshMask != TerrainSnapshotRefresh.None)
                    {
                        terrainSnapshot.Refresh(entityId.ToObject<Terrain>(), Allocator.Persistent, refreshMask);
                        m_TerrainRefreshMasks.Ptr[terrainIndex] = TerrainSnapshotRefresh.None;
                    }
                }
            }

            ScheduleUpdatesWithBurst(Self, &settings);
            m_DirtyTerrainIndices.Clear();
        }

        [BurstCompile]
        private static void ScheduleUpdatesWithBurst(TerrainManager* terrainSystem, TerrainSystemSettings* settings)
        {
            terrainSystem->UpdateInternal(*settings);
        }

        private void UpdateInternal(in TerrainSystemSettings settings)
        {
            m_TerrainUpdateHandle.Complete();

            // Find terrains within range of streaming spheres, this broadly determines which terrains are active
            var streamingSpheres = m_StreamingManager.ValueRO.StreamingSpheres;
            foreach (int terrainIndex in m_AllocatedIndices)
            {
                ref var terrain = ref m_TerrainSnapshots.Ptr[terrainIndex];

                float treeDistanceSq = terrain.TreeDistance * terrain.TreeDistance;
                float detailDistanceSq = terrain.DetailDistance * terrain.DetailDistance;
                terrain.WithinTreeDistance = false;
                terrain.WithinDetailsRange = false;

                for (int i = 0; i < streamingSpheres.Length; i++)
                {
                    float3 spherePosition = streamingSpheres[i].position;
                    if (!terrain.WithinTreeDistance && terrain.Bounds.IntersectsSphereSq(spherePosition, treeDistanceSq))
                        terrain.WithinTreeDistance = true;
                    if (!terrain.WithinDetailsRange && terrain.Bounds.IntersectsSphereSq(spherePosition, detailDistanceSq))
                        terrain.WithinDetailsRange = true;
                    if (terrain.WithinTreeDistance && terrain.WithinDetailsRange)
                        break;
                }
            }

            // Update tree managers
            foreach (int terrainIndex in m_AllocatedIndices)
                m_TreeManagers.Ptr[terrainIndex].Update(m_TerrainSnapshots.Ptr[terrainIndex]);

            // Schedule detail managers updates
            JobHandle combinedDetailJobs = default;
            int loadedDetailCellCount = 0;
            int unloadedDetailCellCount = 0;
            int structuralDetailInstanceCount = 0;
            TerrainDetailManager.DetailStreamingFrameStats detailFrameStats = default;

            int terrainCount = m_TerrainSnapshots.Length;
            if (terrainCount > 0)
            {
                if (m_NextDetailTerrainIndex >= terrainCount)
                    m_NextDetailTerrainIndex = 0;

                for (int terrainOffset = 0; terrainOffset < terrainCount; terrainOffset++)
                {
                    int terrainIndex = (m_NextDetailTerrainIndex + terrainOffset) % terrainCount;
                    if (!m_AllocatedIndices.Contains(terrainIndex))
                        continue;

                    var detailJobs = m_DetailManagers.Ptr[terrainIndex].ScheduleUpdate(
                        m_TerrainSnapshots.Ptr[terrainIndex],
                        settings,
                        ref loadedDetailCellCount,
                        ref unloadedDetailCellCount,
                        ref structuralDetailInstanceCount,
                        ref detailFrameStats);
                    combinedDetailJobs = JobHandle.CombineDependencies(combinedDetailJobs, detailJobs);
                }

                m_NextDetailTerrainIndex = (m_NextDetailTerrainIndex + 1) % terrainCount;
            }
            TerrainDetailManager.PublishProfilerCounters(detailFrameStats);

            m_TerrainUpdateHandle = JobHandle.CombineDependencies(m_TerrainUpdateHandle, combinedDetailJobs);
            if (!m_TerrainUpdateHandle.Equals(default))
                JobHandle.ScheduleBatchedJobs();
        }

#if UNITY_EDITOR
        internal void ClearHidden()
        {
            foreach (int terrainIndex in m_AllocatedIndices)
            {
                m_TreeManagers.Ptr[terrainIndex].ClearHidden();
                m_DetailManagers.Ptr[terrainIndex].ClearHidden();
            }
        }

        internal void SetHidden(EntityObjectRef<Terrain> terrain)
        {
            if (terrain.IsValid())
            {
                if (m_TerrainEntityIdToIndexMap.TryGetValue(terrain.Value, out var terrainIndex))
                {
                    m_TreeManagers.Ptr[terrainIndex].SetHidden();
                    m_DetailManagers.Ptr[terrainIndex].SetHidden();
                }
            }
        }
#endif
    }

    #endregion
}
