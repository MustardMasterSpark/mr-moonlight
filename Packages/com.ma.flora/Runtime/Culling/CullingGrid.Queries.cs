// Copyright © Magnetic Arcade. All Rights Reserved.

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MA.Flora
{
    internal unsafe partial struct CullingGrid
    {
        private enum SourceFilterMode : byte
        {
            None,
            AuthoringOnly,
            IdentityOnly,
            RenderOnly,
            AuthoringAndIdentity,
            AuthoringAndRender,
            General,
        }

        private static SourceFilterMode GetSourceFilterMode(FloraInstanceFilter filter)
        {
            bool hasOwner = filter.OwnerGameObjectID != EntityId.None;
            bool hasIdentity = filter.IdentitySourceGameObjectID != EntityId.None;
            bool hasRender = filter.RenderSourceGameObjectID != EntityId.None;

            if (!hasOwner && !hasIdentity && !hasRender)
                return SourceFilterMode.None;
            if (hasOwner && !hasIdentity && !hasRender)
                return SourceFilterMode.AuthoringOnly;
            if (!hasOwner && hasIdentity && !hasRender)
                return SourceFilterMode.IdentityOnly;
            if (!hasOwner && !hasIdentity && hasRender)
                return SourceFilterMode.RenderOnly;
            if (hasOwner && hasIdentity && !hasRender)
                return SourceFilterMode.AuthoringAndIdentity;
            if (hasOwner && !hasIdentity && hasRender)
                return SourceFilterMode.AuthoringAndRender;

            return SourceFilterMode.General;
        }

        private readonly bool TryGetInstanceSourceIds(FloraInstanceHandle instance, out EntityId identitySourceId, out EntityId renderSourceId)
        {
            InstanceInSourceRecord instanceInSourceRecord = InstanceRegistry.Data.GetInstanceInSourceRecord(instance);
            if (instanceInSourceRecord.Equals(InstanceInSourceRecord.None))
            {
                identitySourceId = EntityId.None;
                renderSourceId = EntityId.None;
                return false;
            }

            identitySourceId = m_TemplateManager.ValueRO.GetIdentitySourceId(instanceInSourceRecord.SourceRecord);
            renderSourceId = m_TemplateManager.ValueRO.GetRenderSourceId(instanceInSourceRecord.SourceRecord);
            return true;
        }

        private readonly bool MatchesIdentitySource(FloraInstanceHandle instance, EntityId identitySourceId)
        {
            InstanceInSourceRecord instanceInSourceRecord = InstanceRegistry.Data.GetInstanceInSourceRecord(instance);
            return !instanceInSourceRecord.Equals(InstanceInSourceRecord.None) &&
                   m_TemplateManager.ValueRO.GetIdentitySourceId(instanceInSourceRecord.SourceRecord) == identitySourceId;
        }

        private readonly bool MatchesRenderSource(FloraInstanceHandle instance, EntityId renderSourceId)
        {
            InstanceInSourceRecord instanceInSourceRecord = InstanceRegistry.Data.GetInstanceInSourceRecord(instance);
            return !instanceInSourceRecord.Equals(InstanceInSourceRecord.None) &&
                   m_TemplateManager.ValueRO.GetRenderSourceId(instanceInSourceRecord.SourceRecord) == renderSourceId;
        }

        private readonly bool MatchesSourceFilter(FloraInstanceHandle instance, FloraInstanceFilter filter, SourceFilterMode mode)
        {
            switch (mode)
            {
                case SourceFilterMode.None:
                    return true;
                case SourceFilterMode.AuthoringOnly:
                    return InstanceRegistry.Data.GetSceneEntityId(instance) == filter.OwnerGameObjectID;
                case SourceFilterMode.IdentityOnly:
                    return MatchesIdentitySource(instance, filter.IdentitySourceGameObjectID);
                case SourceFilterMode.RenderOnly:
                    return MatchesRenderSource(instance, filter.RenderSourceGameObjectID);
                case SourceFilterMode.AuthoringAndIdentity:
                    return InstanceRegistry.Data.GetSceneEntityId(instance) == filter.OwnerGameObjectID &&
                           MatchesIdentitySource(instance, filter.IdentitySourceGameObjectID);
                case SourceFilterMode.AuthoringAndRender:
                    return InstanceRegistry.Data.GetSceneEntityId(instance) == filter.OwnerGameObjectID &&
                           MatchesRenderSource(instance, filter.RenderSourceGameObjectID);
            }

            if (!TryGetInstanceSourceIds(instance, out EntityId identitySourceId, out EntityId renderSourceId))
                return filter.IdentitySourceGameObjectID == EntityId.None && filter.RenderSourceGameObjectID == EntityId.None;

            if (filter.OwnerGameObjectID != EntityId.None && InstanceRegistry.Data.GetSceneEntityId(instance) != filter.OwnerGameObjectID)
                return false;

            if (filter.IdentitySourceGameObjectID != EntityId.None && identitySourceId != filter.IdentitySourceGameObjectID)
                return false;

            if (filter.RenderSourceGameObjectID != EntityId.None && renderSourceId != filter.RenderSourceGameObjectID)
                return false;

            return true;
        }

        private readonly bool MatchesIdentitySources(FloraInstanceHandle instance, NativeParallelHashSet<EntityId> identitySourceIds)
        {
            if (!TryGetInstanceSourceIds(instance, out EntityId identitySourceId, out _))
                return false;

            return identitySourceIds.Contains(identitySourceId);
        }

        private readonly bool MatchesChunkFilter(CullingChunkIndex chunk, FloraInstanceFilter filter)
        {
            ArchetypeKey archetypeKey = m_ChunkArchetype[chunk].Key;
            return MatchesTypeMask(archetypeKey.Tags, filter.TypeMask) &&
                   (filter.LayerMask & (1 << archetypeKey.Layer)) != 0;
        }

        private readonly NativeArray<CullingChunkIndex> FindCandidateChunksIntersectingSphere(FloraInstanceFilter filter, BoundingSphere sphere, AllocatorManager.AllocatorHandle allocator)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingSphere = FindChunksIntersectingSphere(sphere, Allocator.TempJob);
            NativeList<CullingChunkIndex> filteredChunks = new NativeList<CullingChunkIndex>(chunksIntersectingSphere.Length, allocator);
            for (int i = 0; i < chunksIntersectingSphere.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingSphere[i];
                if (MatchesChunkFilter(chunk, filter))
                    filteredChunks.Add(chunk);
            }

            return filteredChunks.TransferOwnershipToNativeArray();
        }

        private readonly NativeArray<CullingChunkIndex> FindCandidateChunksIntersectingBox(FloraInstanceFilter filter, AABB testAABB, AllocatorManager.AllocatorHandle allocator)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingBox = FindChunksIntersectingBox(testAABB, Allocator.TempJob);
            NativeList<CullingChunkIndex> filteredChunks = new NativeList<CullingChunkIndex>(chunksIntersectingBox.Length, allocator);
            for (int i = 0; i < chunksIntersectingBox.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingBox[i];
                if (MatchesChunkFilter(chunk, filter))
                    filteredChunks.Add(chunk);
            }

            return filteredChunks.TransferOwnershipToNativeArray();
        }

        private readonly int CountInstancesInChunks(NativeArray<CullingChunkIndex> chunks)
        {
            return chunks.Length * InstanceManager.ChunkCapacity;
        }

        private readonly void PrepareResultList(NativeArray<CullingChunkIndex> chunks, NativeList<FloraInstanceHandle> result)
        {
            result.Clear();

            int candidateCount = CountInstancesInChunks(chunks);
            if (result.Capacity < candidateCount)
                result.Capacity = candidateCount;
        }

        private static bool OriginWithinSphere(float3 origin, BoundingSphere sphere)
        {
            float radiusSq = sphere.radius * sphere.radius;
            return math.distancesq(origin, sphere.position) <= radiusSq;
        }

        private static bool OriginWithinAABB(float3 origin, AABB testAABB)
        {
            return testAABB.Contains(new float4(origin, 0f));
        }

        private readonly bool ChunkHasMatchingSphereInstance(CullingChunkIndex chunk, BoundingSphere sphere, FloraInstanceFilter filter, SourceFilterMode filterMode)
        {
            int chunkCount = m_ChunkCount[chunk];
            int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
            NativeArray<AABB> instanceAABBs = m_InstanceManager.ValueRO.InstanceAABBs;
            NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

            for (int indexInChunk = 0; indexInChunk < chunkCount; indexInChunk++)
            {
                int instanceIndex = instanceIndices[indexInChunk];
                FloraInstanceHandle instance = instanceHandles[instanceIndex];
                if (!MatchesSourceFilter(instance, filter, filterMode))
                    continue;

                if (AABB.IntersectsSphere(instanceAABBs[instanceIndex], sphere.position, sphere.radius))
                    return true;
            }

            return false;
        }

        private readonly bool ChunkHasMatchingSphereInstance(CullingChunkIndex chunk, BoundingSphere sphere, NativeParallelHashSet<EntityId> identitySourceIds)
        {
            int chunkCount = m_ChunkCount[chunk];
            int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
            NativeArray<AABB> instanceAABBs = m_InstanceManager.ValueRO.InstanceAABBs;
            NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

            for (int indexInChunk = 0; indexInChunk < chunkCount; indexInChunk++)
            {
                int instanceIndex = instanceIndices[indexInChunk];
                FloraInstanceHandle instance = instanceHandles[instanceIndex];
                if (!MatchesIdentitySources(instance, identitySourceIds))
                    continue;

                if (AABB.IntersectsSphere(instanceAABBs[instanceIndex], sphere.position, sphere.radius))
                    return true;
            }

            return false;
        }

        private readonly bool ChunkHasMatchingBoxInstance(CullingChunkIndex chunk, AABB testAABB, FloraInstanceFilter filter, SourceFilterMode filterMode)
        {
            int chunkCount = m_ChunkCount[chunk];
            int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
            NativeArray<AABB> instanceAABBs = m_InstanceManager.ValueRO.InstanceAABBs;
            NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

            for (int indexInChunk = 0; indexInChunk < chunkCount; indexInChunk++)
            {
                int instanceIndex = instanceIndices[indexInChunk];
                FloraInstanceHandle instance = instanceHandles[instanceIndex];
                if (!MatchesSourceFilter(instance, filter, filterMode))
                    continue;

                if (AABB.IntersectsAABB(instanceAABBs[instanceIndex], testAABB))
                    return true;
            }

            return false;
        }

        private readonly bool ChunkHasMatchingBoxInstance(CullingChunkIndex chunk, AABB testAABB, NativeParallelHashSet<EntityId> identitySourceIds)
        {
            int chunkCount = m_ChunkCount[chunk];
            int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
            NativeArray<AABB> instanceAABBs = m_InstanceManager.ValueRO.InstanceAABBs;
            NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

            for (int indexInChunk = 0; indexInChunk < chunkCount; indexInChunk++)
            {
                int instanceIndex = instanceIndices[indexInChunk];
                FloraInstanceHandle instance = instanceHandles[instanceIndex];
                if (!MatchesIdentitySources(instance, identitySourceIds))
                    continue;

                if (AABB.IntersectsAABB(instanceAABBs[instanceIndex], testAABB))
                    return true;
            }

            return false;
        }

        private static bool MatchesTypeMask(InstanceTag archetypeTags, FloraInstanceTypeMask typeMask)
        {
            if (typeMask == FloraInstanceTypeMask.Any)
                return true;

            bool isTerrainTree = (archetypeTags & InstanceTag.TerrainTree) != 0;
            bool isTerrainDetail = (archetypeTags & InstanceTag.TerrainDetail) != 0;
            if (!isTerrainTree && !isTerrainDetail)
                return typeMask == FloraInstanceTypeMask.Default;

            FloraInstanceTypeMask archetypeType = isTerrainTree
                ? FloraInstanceTypeMask.TerrainTree
                : FloraInstanceTypeMask.TerrainDetail;

            return (typeMask & archetypeType) != 0;
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct TestSelectionPlanesJob : IJob
        {
            [ReadOnly] public InstanceTag IncludeTags;
            [ReadOnly] public InstanceTag ExcludeTags;
            [ReadOnly] public NativeArray<FrustumSIMDPacket> FrustumPackets;

            [ReadOnly] public NativeBitSet ActiveBlocks;
            [ReadOnly] public NativeArray<BlockLocation> BlockLocations;
            [ReadOnly] public NativeBitSet ActiveCells;
            [ReadOnly] public NativeBufferArray<CullingChunkIndex> CullingChunks;
            [ReadOnly] public NativeArray<ArchetypeIndex> ChunkArchetypes;
            [ReadOnly] public NativeArray<int> ChunkCounts;
            [ReadOnly] public NativeArray<AABB> InstanceAABBs;
            [ReadOnly] public NativeArray<FloraInstanceHandle> InstanceHandles;

            [WriteOnly] public NativeList<FloraInstanceHandle> Result;

            public void Execute()
            {
                foreach (int blockIndex in ActiveBlocks)
                {
                    BlockLocation blockLocation = BlockLocations[blockIndex];
                    AABBMinMax blockAABB = blockLocation.PaddedAABB;

                    FrustumIntersectResult blockIntersection = FrustumUtility.IntersectBoundsSIMD(FrustumPackets, blockAABB);
                    if (blockIntersection == FrustumIntersectResult.Outside)
                        continue;

                    int baseCellIndex = blockIndex * CellsPerBlock;
                    foreach (int cellIndex in ActiveCells.IndicesInRange(baseCellIndex, CellsPerBlock))
                    {
                        int indexInBlock = cellIndex & CellIndex.LocalIndexMask;
                        CellLocation cellLocation = CellLocation.FromBlock(blockLocation, indexInBlock);
                        AABBMinMax cellAABB = cellLocation.PaddedAABB;

                        FrustumIntersectResult cellIntersection = blockIntersection == FrustumIntersectResult.Inside
                            ? FrustumIntersectResult.Inside
                            : FrustumUtility.IntersectBoundsSIMD(FrustumPackets, cellAABB);

                        if (cellIntersection == FrustumIntersectResult.Outside)
                            continue;

                        NativeBuffer<CullingChunkIndex> chunks = CullingChunks[cellIndex];
                        for (int i = 0; i < chunks.Length; i++)
                        {
                            CullingChunkIndex chunk = chunks[i];
                            ArchetypeIndex archetype = ChunkArchetypes[chunk];
                            ArchetypeKey archetypeKey = archetype.Key;
                            if (IncludeTags != 0 && (archetypeKey.Tags & IncludeTags) == 0)
                                continue;
                            if (ExcludeTags != 0 && (archetypeKey.Tags & ExcludeTags) != 0)
                                continue;

                            int chunkCount = ChunkCounts[chunk];
                            int baseInstanceIndex = chunk * InstanceManager.ChunkCapacity;
                            for (int j = 0; j < chunkCount; j++)
                            {
                                AABB instanceBounds = InstanceAABBs[baseInstanceIndex + j];
                                FrustumIntersectResult instanceIntersection = FrustumUtility.IntersectBoundsSIMD(FrustumPackets, instanceBounds);
                                if (instanceIntersection == FrustumIntersectResult.Inside)
                                {
                                    Result.Add(InstanceHandles[baseInstanceIndex + j]);
                                }
                            }
                        }
                    }
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct TestCellsSphereJob : IJob
        {
            [ReadOnly] public BoundingSphere Sphere;
            [ReadOnly] public NativeBitSet ActiveBlocks;
            [ReadOnly] public NativeArray<BlockLocation> BlockLocations;
            [ReadOnly] public NativeBitSet ActiveCells;
            [ReadOnly] public NativeBufferArray<CullingChunkIndex> CullingChunks;

            [WriteOnly] public NativeList<CullingChunkIndex> IntersectingChunks;

            public void Execute()
            {
                foreach (int blockIndex in ActiveBlocks)
                {
                    BlockLocation blockLocation = BlockLocations[blockIndex];
                    AABBMinMax blockAABB = blockLocation.PaddedAABB;

                    if (AABB.IntersectsSphere(blockAABB, Sphere.position, Sphere.radius))
                    {
                        int baseCellIndex = blockIndex * CellsPerBlock;
                        foreach (int cellIndex in ActiveCells.IndicesInRange(baseCellIndex, CellsPerBlock))
                        {
                            int indexInBlock = cellIndex & CellIndex.LocalIndexMask;
                            CellLocation cellLocation = CellLocation.FromBlock(blockLocation, indexInBlock);
                            AABBMinMax cellAABB = cellLocation.PaddedAABB;

                            if (AABB.IntersectsSphere(cellAABB, Sphere.position, Sphere.radius))
                            {
                                NativeBuffer<CullingChunkIndex> chunks = CullingChunks[cellIndex];
                                IntersectingChunks.AddRangeNoResize(chunks.GetUnsafeReadOnlyPtr(), chunks.Length);
                            }
                        }
                    }
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct TestCellsBoxJob : IJob
        {
            [ReadOnly] public AABB TestAABB;
            [ReadOnly] public NativeBitSet ActiveBlocks;
            [ReadOnly] public NativeArray<BlockLocation> BlockLocations;
            [ReadOnly] public NativeBitSet ActiveCells;
            [ReadOnly] public NativeBufferArray<CullingChunkIndex> CullingChunks;

            [WriteOnly] public NativeList<CullingChunkIndex> OverlappingChunks;

            public void Execute()
            {
                foreach (int blockIndex in ActiveBlocks)
                {
                    BlockLocation blockLocation = BlockLocations[blockIndex];
                    AABBMinMax blockAABB = blockLocation.PaddedAABB;

                    if (AABB.IntersectsAABB(blockAABB, TestAABB))
                    {
                        int baseCellIndex = blockIndex * CellsPerBlock;
                        foreach (int cellIndex in ActiveCells.IndicesInRange(baseCellIndex, CellsPerBlock))
                        {
                            int indexInBlock = cellIndex & CellIndex.LocalIndexMask;
                            CellLocation cellLocation = CellLocation.FromBlock(blockLocation, indexInBlock);
                            AABBMinMax cellAABB = cellLocation.PaddedAABB;

                            if (AABB.IntersectsAABB(cellAABB, TestAABB))
                            {
                                NativeBuffer<CullingChunkIndex> chunks = CullingChunks[cellIndex];
                                OverlappingChunks.AddRangeNoResize(chunks.GetUnsafeReadOnlyPtr(), chunks.Length);
                            }
                        }
                    }
                }
            }
        }

        #region Rect Selection Queries

        public readonly NativeArray<FloraInstanceHandle> CullInstancesInSelectionPlanes(
            InstanceTag includeTags, InstanceTag excludeTags,
            NativeArray<Plane> planes, AllocatorManager.AllocatorHandle allocator)
        {
            m_InstanceManager.ValueRW.SyncJobsForMainThread();

            using NativeArray<FrustumSIMDPacket> planePackets = CollectionHelper.CreateNativeArray<FrustumSIMDPacket>(planes.Length, Allocator.TempJob);
            FrustumUtility.InitializeSIMDPackets(planes, planePackets);

            using NativeList<FloraInstanceHandle> instances = new NativeList<FloraInstanceHandle>(m_ChunkAllocated.MaxLength * InstanceManager.ChunkCapacity, Allocator.TempJob);

            new TestSelectionPlanesJob {
                IncludeTags = includeTags,
                ExcludeTags = excludeTags | InstanceTag.TerrainDetail,
                FrustumPackets = planePackets,
                ActiveBlocks = m_BlockAllocated,
                BlockLocations = m_BlockLocations,
                ActiveCells = m_CellAllocated,
                CullingChunks = m_CellChunks,
                ChunkArchetypes = m_ChunkArchetype,
                ChunkCounts = m_ChunkCount,
                InstanceAABBs = m_InstanceManager.ValueRO.InstanceAABBs,
                InstanceHandles = m_InstanceManager.ValueRO.InstanceHandles,
                Result = instances
            }.Run();

            NativeArray<FloraInstanceHandle> result = CollectionHelper.CreateNativeArray<FloraInstanceHandle>(instances.Length, allocator, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < instances.Length; i++)
                result[i] = instances[i];

            return result;
        }

        #endregion

        #region Sphere Queries

        public readonly NativeArray<CullingChunkIndex> FindChunksIntersectingSphere(BoundingSphere sphere, AllocatorManager.AllocatorHandle allocator)
        {
            m_InstanceManager.ValueRW.SyncJobsForMainThread();

            NativeList<CullingChunkIndex> intersectingChunks = new NativeList<CullingChunkIndex>(m_ChunkAllocated.MaxLength, allocator);
            new TestCellsSphereJob {
                Sphere = sphere,
                ActiveBlocks = m_BlockAllocated,
                BlockLocations = m_BlockLocations,
                ActiveCells = m_CellAllocated,
                CullingChunks = m_CellChunks,
                IntersectingChunks = intersectingChunks
            }.Run();

            return intersectingChunks.TransferOwnershipToNativeArray();
        }

        public readonly NativeArray<CullingChunkIndex> FindChunksIntersectingSphere(FloraInstanceFilter filter, BoundingSphere sphere, AllocatorManager.AllocatorHandle allocator)
        {
            using NativeArray<CullingChunkIndex> candidateChunks = FindCandidateChunksIntersectingSphere(filter, sphere, Allocator.TempJob);
            SourceFilterMode filterMode = GetSourceFilterMode(filter);
            NativeList<CullingChunkIndex> filteredChunks = new NativeList<CullingChunkIndex>(candidateChunks.Length, allocator);
            for (int i = 0; i < candidateChunks.Length; i++)
            {
                CullingChunkIndex chunk = candidateChunks[i];
                if (ChunkHasMatchingSphereInstance(chunk, sphere, filter, filterMode))
                    filteredChunks.Add(chunk);
            }

            return filteredChunks.TransferOwnershipToNativeArray();
        }

        public readonly NativeArray<CullingChunkIndex> FindChunksIntersectingSphere(NativeArray<EntityId> sourceEntityIds, BoundingSphere sphere, AllocatorManager.AllocatorHandle allocator)
        {
            if (sourceEntityIds.Length == 0)
                return CollectionHelper.CreateNativeArray<CullingChunkIndex>(0, allocator);

            using var identitySourceIds = new NativeParallelHashSet<EntityId>(sourceEntityIds.Length, Allocator.Temp);
            for (int i = 0; i < sourceEntityIds.Length; i++)
                identitySourceIds.Add(sourceEntityIds[i]);

            using NativeArray<CullingChunkIndex> chunksIntersectingSphere = FindChunksIntersectingSphere(sphere, Allocator.TempJob);

            NativeList<CullingChunkIndex> result = new NativeList<CullingChunkIndex>(chunksIntersectingSphere.Length, allocator);
            for (int i = 0; i < chunksIntersectingSphere.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingSphere[i];
                if (ChunkHasMatchingSphereInstance(chunk, sphere, identitySourceIds))
                    result.Add(chunk);
            }

            return result.TransferOwnershipToNativeArray();
        }

        public readonly NativeList<FloraInstanceHandle> FindInstancesIntersectingSphere(BoundingSphere sphere, AllocatorManager.AllocatorHandle allocator)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingSphere = FindChunksIntersectingSphere(sphere, Allocator.TempJob);
            NativeList<FloraInstanceHandle> result = new NativeList<FloraInstanceHandle>(CountInstancesInChunks(chunksIntersectingSphere), allocator);
            AddInstancesIntersectingSphere(chunksIntersectingSphere, sphere, result);
            return result;
        }

        public readonly void FindInstancesIntersectingSphere(BoundingSphere sphere, NativeList<FloraInstanceHandle> result)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingSphere = FindChunksIntersectingSphere(sphere, Allocator.TempJob);
            AddInstancesIntersectingSphere(chunksIntersectingSphere, sphere, result);
        }

        private readonly void AddInstancesIntersectingSphere(NativeArray<CullingChunkIndex> chunksIntersectingSphere, BoundingSphere sphere, NativeList<FloraInstanceHandle> result)
        {
            PrepareResultList(chunksIntersectingSphere, result);

            for (int i = 0; i < chunksIntersectingSphere.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingSphere[i];
                int chunkCount = m_ChunkCount[chunk];
                int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
                NativeArray<AABB> instanceAABBs = m_InstanceManager.ValueRO.InstanceAABBs;
                NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

                for (int indexInChunk = 0; indexInChunk < chunkCount; indexInChunk++)
                {
                    int instanceIndex = instanceIndices[indexInChunk];
                    FloraInstanceHandle instance = instanceHandles[instanceIndex];

                    AABB instanceAABB = instanceAABBs[instanceIndex];
                    if (AABB.IntersectsSphere(instanceAABB, sphere.position, sphere.radius))
                        result.Add(instance);
                }
            }
        }

        public readonly NativeList<FloraInstanceHandle> FindInstancesIntersectingSphereMatching(FloraInstanceFilter filter, BoundingSphere sphere, AllocatorManager.AllocatorHandle allocator)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingSphere = FindCandidateChunksIntersectingSphere(filter, sphere, Allocator.TempJob);
            SourceFilterMode filterMode = GetSourceFilterMode(filter);
            NativeList<FloraInstanceHandle> result = new NativeList<FloraInstanceHandle>(CountInstancesInChunks(chunksIntersectingSphere), allocator);
            AddInstancesIntersectingSphereMatching(chunksIntersectingSphere, filter, filterMode, sphere, result);
            return result;
        }

        public readonly void FindInstancesIntersectingSphereMatching(FloraInstanceFilter filter, BoundingSphere sphere, NativeList<FloraInstanceHandle> result)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingSphere = FindCandidateChunksIntersectingSphere(filter, sphere, Allocator.TempJob);
            SourceFilterMode filterMode = GetSourceFilterMode(filter);
            AddInstancesIntersectingSphereMatching(chunksIntersectingSphere, filter, filterMode, sphere, result);
        }

        private readonly void AddInstancesIntersectingSphereMatching(
            NativeArray<CullingChunkIndex> chunksIntersectingSphere,
            FloraInstanceFilter filter,
            SourceFilterMode filterMode,
            BoundingSphere sphere,
            NativeList<FloraInstanceHandle> result)
        {
            PrepareResultList(chunksIntersectingSphere, result);

            for (int i = 0; i < chunksIntersectingSphere.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingSphere[i];
                int chunkCount = m_ChunkCount[chunk];
                int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
                NativeArray<AABB> instanceAABBs = m_InstanceManager.ValueRO.InstanceAABBs;
                NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

                for (int indexInChunk = 0; indexInChunk < chunkCount; indexInChunk++)
                {
                    int instanceIndex = instanceIndices[indexInChunk];
                    FloraInstanceHandle instance = instanceHandles[instanceIndex];

                    if (!MatchesSourceFilter(instance, filter, filterMode))
                        continue;

                    AABB instanceAABB = instanceAABBs[instanceIndex];
                    if (AABB.IntersectsSphere(instanceAABB, sphere.position, sphere.radius))
                        result.Add(instance);
                }
            }
        }

        public readonly NativeList<FloraInstanceHandle> FindInstancesIntersectingSphereMatching(NativeArray<EntityId> prefabGameObjectIDs, BoundingSphere sphere, AllocatorManager.AllocatorHandle allocator)
        {
            if (prefabGameObjectIDs.Length == 0)
                return new NativeList<FloraInstanceHandle>(0, allocator);

            using var identitySourceIds = new NativeParallelHashSet<EntityId>(prefabGameObjectIDs.Length, Allocator.Temp);
            for (int i = 0; i < prefabGameObjectIDs.Length; i++)
                identitySourceIds.Add(prefabGameObjectIDs[i]);

            using NativeArray<CullingChunkIndex> chunksIntersectingSphere = FindChunksIntersectingSphere(sphere, Allocator.TempJob);
            NativeList<FloraInstanceHandle> result = new NativeList<FloraInstanceHandle>(CountInstancesInChunks(chunksIntersectingSphere), allocator);
            AddInstancesIntersectingSphereMatching(chunksIntersectingSphere, identitySourceIds, sphere, result);
            return result;
        }

        public readonly void FindInstancesIntersectingSphereMatching(NativeArray<EntityId> prefabGameObjectIDs, BoundingSphere sphere, NativeList<FloraInstanceHandle> result)
        {
            result.Clear();
            if (prefabGameObjectIDs.Length == 0)
                return;

            using var identitySourceIds = new NativeParallelHashSet<EntityId>(prefabGameObjectIDs.Length, Allocator.Temp);
            for (int i = 0; i < prefabGameObjectIDs.Length; i++)
                identitySourceIds.Add(prefabGameObjectIDs[i]);

            using NativeArray<CullingChunkIndex> chunksIntersectingSphere = FindChunksIntersectingSphere(sphere, Allocator.TempJob);
            AddInstancesIntersectingSphereMatching(chunksIntersectingSphere, identitySourceIds, sphere, result);
        }

        private readonly void AddInstancesIntersectingSphereMatching(
            NativeArray<CullingChunkIndex> chunksIntersectingSphere,
            NativeParallelHashSet<EntityId> identitySourceIds,
            BoundingSphere sphere,
            NativeList<FloraInstanceHandle> result)
        {
            PrepareResultList(chunksIntersectingSphere, result);

            for (int i = 0; i < chunksIntersectingSphere.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingSphere[i];
                int chunkCount = m_ChunkCount[chunk];
                int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
                NativeArray<AABB> instanceAABBs = m_InstanceManager.ValueRO.InstanceAABBs;
                NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

                for (int indexInChunk = 0; indexInChunk < chunkCount; indexInChunk++)
                {
                    int instanceIndex = instanceIndices[indexInChunk];
                    FloraInstanceHandle instance = instanceHandles[instanceIndex];
                    if (!MatchesIdentitySources(instance, identitySourceIds))
                        continue;

                    AABB instanceAABB = instanceAABBs[instanceIndex];
                    if (AABB.IntersectsSphere(instanceAABB, sphere.position, sphere.radius))
                        result.Add(instance);
                }
            }
        }

        public readonly NativeList<FloraInstanceHandle> FindInstanceOriginsWithinSphere(BoundingSphere sphere, AllocatorManager.AllocatorHandle allocator)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingSphere = FindChunksIntersectingSphere(sphere, Allocator.TempJob);
            NativeList<FloraInstanceHandle> result = new NativeList<FloraInstanceHandle>(CountInstancesInChunks(chunksIntersectingSphere), allocator);
            AddInstanceOriginsWithinSphere(chunksIntersectingSphere, sphere, result);
            return result;
        }

        public readonly void FindInstanceOriginsWithinSphere(BoundingSphere sphere, NativeList<FloraInstanceHandle> result)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingSphere = FindChunksIntersectingSphere(sphere, Allocator.TempJob);
            AddInstanceOriginsWithinSphere(chunksIntersectingSphere, sphere, result);
        }

        private readonly void AddInstanceOriginsWithinSphere(NativeArray<CullingChunkIndex> chunksIntersectingSphere, BoundingSphere sphere, NativeList<FloraInstanceHandle> result)
        {
            PrepareResultList(chunksIntersectingSphere, result);

            for (int i = 0; i < chunksIntersectingSphere.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingSphere[i];
                int chunkCount = m_ChunkCount[chunk];
                int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
                NativeArray<GraphicsMatrix> localToWorlds = m_InstanceManager.ValueRO.InstanceLocalToWorld;
                NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

                for (int indexInChunk = 0; indexInChunk < chunkCount; indexInChunk++)
                {
                    int instanceIndex = instanceIndices[indexInChunk];
                    if (OriginWithinSphere(localToWorlds[instanceIndex].Position, sphere))
                        result.Add(instanceHandles[instanceIndex]);
                }
            }
        }

        public readonly NativeList<FloraInstanceHandle> FindInstanceOriginsWithinSphereMatching(FloraInstanceFilter filter, BoundingSphere sphere, AllocatorManager.AllocatorHandle allocator)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingSphere = FindCandidateChunksIntersectingSphere(filter, sphere, Allocator.TempJob);
            SourceFilterMode filterMode = GetSourceFilterMode(filter);
            NativeList<FloraInstanceHandle> result = new NativeList<FloraInstanceHandle>(CountInstancesInChunks(chunksIntersectingSphere), allocator);
            AddInstanceOriginsWithinSphereMatching(chunksIntersectingSphere, filter, filterMode, sphere, result);
            return result;
        }

        public readonly void FindInstanceOriginsWithinSphereMatching(FloraInstanceFilter filter, BoundingSphere sphere, NativeList<FloraInstanceHandle> result)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingSphere = FindCandidateChunksIntersectingSphere(filter, sphere, Allocator.TempJob);
            SourceFilterMode filterMode = GetSourceFilterMode(filter);
            AddInstanceOriginsWithinSphereMatching(chunksIntersectingSphere, filter, filterMode, sphere, result);
        }

        private readonly void AddInstanceOriginsWithinSphereMatching(
            NativeArray<CullingChunkIndex> chunksIntersectingSphere,
            FloraInstanceFilter filter,
            SourceFilterMode filterMode,
            BoundingSphere sphere,
            NativeList<FloraInstanceHandle> result)
        {
            PrepareResultList(chunksIntersectingSphere, result);

            for (int i = 0; i < chunksIntersectingSphere.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingSphere[i];
                int chunkCount = m_ChunkCount[chunk];
                int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
                NativeArray<GraphicsMatrix> localToWorlds = m_InstanceManager.ValueRO.InstanceLocalToWorld;
                NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

                for (int indexInChunk = 0; indexInChunk < chunkCount; indexInChunk++)
                {
                    int instanceIndex = instanceIndices[indexInChunk];
                    FloraInstanceHandle instance = instanceHandles[instanceIndex];

                    if (!MatchesSourceFilter(instance, filter, filterMode))
                        continue;

                    if (OriginWithinSphere(localToWorlds[instanceIndex].Position, sphere))
                        result.Add(instance);
                }
            }
        }

        public readonly NativeList<FloraInstanceHandle> FindInstanceOriginsWithinSphereMatching(NativeArray<EntityId> prefabGameObjectIDs, BoundingSphere sphere, AllocatorManager.AllocatorHandle allocator)
        {
            if (prefabGameObjectIDs.Length == 0)
                return new NativeList<FloraInstanceHandle>(0, allocator);

            using var identitySourceIds = new NativeParallelHashSet<EntityId>(prefabGameObjectIDs.Length, Allocator.Temp);
            for (int i = 0; i < prefabGameObjectIDs.Length; i++)
                identitySourceIds.Add(prefabGameObjectIDs[i]);

            using NativeArray<CullingChunkIndex> chunksIntersectingSphere = FindChunksIntersectingSphere(sphere, Allocator.TempJob);
            NativeList<FloraInstanceHandle> result = new NativeList<FloraInstanceHandle>(CountInstancesInChunks(chunksIntersectingSphere), allocator);
            AddInstanceOriginsWithinSphereMatching(chunksIntersectingSphere, identitySourceIds, sphere, result);
            return result;
        }

        public readonly void FindInstanceOriginsWithinSphereMatching(NativeArray<EntityId> prefabGameObjectIDs, BoundingSphere sphere, NativeList<FloraInstanceHandle> result)
        {
            result.Clear();
            if (prefabGameObjectIDs.Length == 0)
                return;

            using var identitySourceIds = new NativeParallelHashSet<EntityId>(prefabGameObjectIDs.Length, Allocator.Temp);
            for (int i = 0; i < prefabGameObjectIDs.Length; i++)
                identitySourceIds.Add(prefabGameObjectIDs[i]);

            using NativeArray<CullingChunkIndex> chunksIntersectingSphere = FindChunksIntersectingSphere(sphere, Allocator.TempJob);
            AddInstanceOriginsWithinSphereMatching(chunksIntersectingSphere, identitySourceIds, sphere, result);
        }

        private readonly void AddInstanceOriginsWithinSphereMatching(
            NativeArray<CullingChunkIndex> chunksIntersectingSphere,
            NativeParallelHashSet<EntityId> identitySourceIds,
            BoundingSphere sphere,
            NativeList<FloraInstanceHandle> result)
        {
            PrepareResultList(chunksIntersectingSphere, result);

            for (int i = 0; i < chunksIntersectingSphere.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingSphere[i];
                int chunkCount = m_ChunkCount[chunk];
                int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
                NativeArray<GraphicsMatrix> localToWorlds = m_InstanceManager.ValueRO.InstanceLocalToWorld;
                NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

                for (int indexInChunk = 0; indexInChunk < chunkCount; indexInChunk++)
                {
                    int instanceIndex = instanceIndices[indexInChunk];
                    FloraInstanceHandle instance = instanceHandles[instanceIndex];
                    if (!MatchesIdentitySources(instance, identitySourceIds))
                        continue;

                    if (OriginWithinSphere(localToWorlds[instanceIndex].Position, sphere))
                        result.Add(instance);
                }
            }
        }

        #endregion

        #region Bounds Queries

        public readonly NativeArray<CullingChunkIndex> FindChunksIntersectingBox(AABB testAABB, AllocatorManager.AllocatorHandle allocator)
        {
            m_InstanceManager.ValueRW.SyncJobsForMainThread();

            NativeList<CullingChunkIndex> overlappingChunks = new NativeList<CullingChunkIndex>(m_ChunkAllocated.MaxLength, allocator);
            new TestCellsBoxJob {
                TestAABB = testAABB,
                ActiveBlocks = m_BlockAllocated,
                BlockLocations = m_BlockLocations,
                ActiveCells = m_CellAllocated,
                CullingChunks = m_CellChunks,
                OverlappingChunks = overlappingChunks
            }.Run();

            return overlappingChunks.TransferOwnershipToNativeArray();
        }

        public readonly NativeArray<CullingChunkIndex> FindChunksIntersectingBox(FloraInstanceFilter filter, AABB testAABB, AllocatorManager.AllocatorHandle allocator)
        {
            using NativeArray<CullingChunkIndex> candidateChunks = FindCandidateChunksIntersectingBox(filter, testAABB, Allocator.TempJob);
            SourceFilterMode filterMode = GetSourceFilterMode(filter);
            NativeList<CullingChunkIndex> result = new NativeList<CullingChunkIndex>(candidateChunks.Length, allocator);
            for (int i = 0; i < candidateChunks.Length; i++)
            {
                CullingChunkIndex chunk = candidateChunks[i];
                if (ChunkHasMatchingBoxInstance(chunk, testAABB, filter, filterMode))
                    result.Add(chunk);
            }

            return result.TransferOwnershipToNativeArray();
        }

        public readonly NativeArray<CullingChunkIndex> FindChunksIntersectingBox(NativeArray<EntityId> sourceEntityIds, AABB testAABB, AllocatorManager.AllocatorHandle allocator)
        {
            if (sourceEntityIds.Length == 0)
                return CollectionHelper.CreateNativeArray<CullingChunkIndex>(0, allocator);

            using var identitySourceIds = new NativeParallelHashSet<EntityId>(sourceEntityIds.Length, Allocator.Temp);
            for (int i = 0; i < sourceEntityIds.Length; i++)
                identitySourceIds.Add(sourceEntityIds[i]);

            using NativeArray<CullingChunkIndex> chunksIntersectingBox = FindChunksIntersectingBox(testAABB, Allocator.TempJob);

            NativeList<CullingChunkIndex> result = new NativeList<CullingChunkIndex>(chunksIntersectingBox.Length, allocator);
            for (int i = 0; i < chunksIntersectingBox.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingBox[i];
                if (ChunkHasMatchingBoxInstance(chunk, testAABB, identitySourceIds))
                    result.Add(chunk);
            }

            return result.TransferOwnershipToNativeArray();
        }

        public readonly NativeList<FloraInstanceHandle> FindInstancesIntersectingBox(AABB testAABB, AllocatorManager.AllocatorHandle allocator)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingBox = FindChunksIntersectingBox(testAABB, Allocator.TempJob);
            NativeList<FloraInstanceHandle> result = new NativeList<FloraInstanceHandle>(CountInstancesInChunks(chunksIntersectingBox), allocator);
            AddInstancesIntersectingBox(chunksIntersectingBox, testAABB, result);
            return result;
        }

        public readonly void FindInstancesIntersectingBox(AABB testAABB, NativeList<FloraInstanceHandle> result)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingBox = FindChunksIntersectingBox(testAABB, Allocator.TempJob);
            AddInstancesIntersectingBox(chunksIntersectingBox, testAABB, result);
        }

        private readonly void AddInstancesIntersectingBox(NativeArray<CullingChunkIndex> chunksIntersectingBox, AABB testAABB, NativeList<FloraInstanceHandle> result)
        {
            PrepareResultList(chunksIntersectingBox, result);

            for (int i = 0; i < chunksIntersectingBox.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingBox[i];
                int chunkCount = m_ChunkCount[chunk];
                int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
                NativeArray<AABB> instanceAABBs = m_InstanceManager.ValueRO.InstanceAABBs;
                NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

                for (int indexInChunk = 0; indexInChunk < chunkCount; indexInChunk++)
                {
                    int instanceIndex = instanceIndices[indexInChunk];
                    AABB instanceAABB = instanceAABBs[instanceIndex];
                    if (AABB.IntersectsAABB(instanceAABB, testAABB))
                        result.Add(instanceHandles[instanceIndex]);
                }
            }
        }

        public readonly NativeList<FloraInstanceHandle> FindInstancesIntersectingBoxMatching(FloraInstanceFilter filter, AABB testAABB, AllocatorManager.AllocatorHandle allocator)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingBox = FindCandidateChunksIntersectingBox(filter, testAABB, Allocator.TempJob);
            SourceFilterMode filterMode = GetSourceFilterMode(filter);
            NativeList<FloraInstanceHandle> result = new NativeList<FloraInstanceHandle>(CountInstancesInChunks(chunksIntersectingBox), allocator);
            AddInstancesIntersectingBoxMatching(chunksIntersectingBox, filter, filterMode, testAABB, result);
            return result;
        }

        public readonly void FindInstancesIntersectingBoxMatching(FloraInstanceFilter filter, AABB testAABB, NativeList<FloraInstanceHandle> result)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingBox = FindCandidateChunksIntersectingBox(filter, testAABB, Allocator.TempJob);
            SourceFilterMode filterMode = GetSourceFilterMode(filter);
            AddInstancesIntersectingBoxMatching(chunksIntersectingBox, filter, filterMode, testAABB, result);
        }

        private readonly void AddInstancesIntersectingBoxMatching(
            NativeArray<CullingChunkIndex> chunksIntersectingBox,
            FloraInstanceFilter filter,
            SourceFilterMode filterMode,
            AABB testAABB,
            NativeList<FloraInstanceHandle> result)
        {
            PrepareResultList(chunksIntersectingBox, result);

            for (int i = 0; i < chunksIntersectingBox.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingBox[i];
                int chunkCount = m_ChunkCount[chunk];
                int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
                NativeArray<AABB> instanceAABBs = m_InstanceManager.ValueRO.InstanceAABBs;
                NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

                for (int indexInChunk = 0; indexInChunk < chunkCount; indexInChunk++)
                {
                    int instanceIndex = instanceIndices[indexInChunk];
                    FloraInstanceHandle instance = instanceHandles[instanceIndex];

                    if (!MatchesSourceFilter(instance, filter, filterMode))
                        continue;

                    AABB instanceAABB = instanceAABBs[instanceIndex];
                    if (AABB.IntersectsAABB(instanceAABB, testAABB))
                        result.Add(instance);
                }
            }
        }

        public readonly NativeList<FloraInstanceHandle> FindInstancesIntersectingBoxMatching(NativeArray<EntityId> prefabGameObjectIDs, AABB testAABB, AllocatorManager.AllocatorHandle allocator)
        {
            if (prefabGameObjectIDs.Length == 0)
                return new NativeList<FloraInstanceHandle>(0, allocator);

            using var identitySourceIds = new NativeParallelHashSet<EntityId>(prefabGameObjectIDs.Length, Allocator.Temp);
            for (int i = 0; i < prefabGameObjectIDs.Length; i++)
                identitySourceIds.Add(prefabGameObjectIDs[i]);

            using NativeArray<CullingChunkIndex> chunksIntersectingBox = FindChunksIntersectingBox(testAABB, Allocator.TempJob);
            NativeList<FloraInstanceHandle> result = new NativeList<FloraInstanceHandle>(CountInstancesInChunks(chunksIntersectingBox), allocator);
            AddInstancesIntersectingBoxMatching(chunksIntersectingBox, identitySourceIds, testAABB, result);
            return result;
        }

        public readonly void FindInstancesIntersectingBoxMatching(NativeArray<EntityId> prefabGameObjectIDs, AABB testAABB, NativeList<FloraInstanceHandle> result)
        {
            result.Clear();
            if (prefabGameObjectIDs.Length == 0)
                return;

            using var identitySourceIds = new NativeParallelHashSet<EntityId>(prefabGameObjectIDs.Length, Allocator.Temp);
            for (int i = 0; i < prefabGameObjectIDs.Length; i++)
                identitySourceIds.Add(prefabGameObjectIDs[i]);

            using NativeArray<CullingChunkIndex> chunksIntersectingBox = FindChunksIntersectingBox(testAABB, Allocator.TempJob);
            AddInstancesIntersectingBoxMatching(chunksIntersectingBox, identitySourceIds, testAABB, result);
        }

        private readonly void AddInstancesIntersectingBoxMatching(
            NativeArray<CullingChunkIndex> chunksIntersectingBox,
            NativeParallelHashSet<EntityId> identitySourceIds,
            AABB testAABB,
            NativeList<FloraInstanceHandle> result)
        {
            PrepareResultList(chunksIntersectingBox, result);

            for (int i = 0; i < chunksIntersectingBox.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingBox[i];
                int chunkCount = m_ChunkCount[chunk];
                int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
                NativeArray<AABB> instanceAABBs = m_InstanceManager.ValueRO.InstanceAABBs;
                NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

                for (int j = 0; j < chunkCount; j++)
                {
                    int instanceIndex = instanceIndices[j];
                    FloraInstanceHandle instance = instanceHandles[instanceIndex];
                    if (!MatchesIdentitySources(instance, identitySourceIds))
                        continue;

                    AABB instanceAABB = instanceAABBs[instanceIndex];
                    if (AABB.IntersectsAABB(instanceAABB, testAABB))
                        result.Add(instance);
                }
            }
        }

        public readonly NativeList<FloraInstanceHandle> FindInstanceOriginsWithinBox(AABB testAABB, AllocatorManager.AllocatorHandle allocator)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingBox = FindChunksIntersectingBox(testAABB, Allocator.TempJob);
            NativeList<FloraInstanceHandle> result = new NativeList<FloraInstanceHandle>(CountInstancesInChunks(chunksIntersectingBox), allocator);
            AddInstanceOriginsWithinBox(chunksIntersectingBox, testAABB, result);
            return result;
        }

        public readonly void FindInstanceOriginsWithinBox(AABB testAABB, NativeList<FloraInstanceHandle> result)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingBox = FindChunksIntersectingBox(testAABB, Allocator.TempJob);
            AddInstanceOriginsWithinBox(chunksIntersectingBox, testAABB, result);
        }

        private readonly void AddInstanceOriginsWithinBox(NativeArray<CullingChunkIndex> chunksIntersectingBox, AABB testAABB, NativeList<FloraInstanceHandle> result)
        {
            PrepareResultList(chunksIntersectingBox, result);

            for (int i = 0; i < chunksIntersectingBox.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingBox[i];
                int chunkCount = m_ChunkCount[chunk];
                int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
                NativeArray<GraphicsMatrix> localToWorlds = m_InstanceManager.ValueRO.InstanceLocalToWorld;
                NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

                for (int indexInChunk = 0; indexInChunk < chunkCount; indexInChunk++)
                {
                    int instanceIndex = instanceIndices[indexInChunk];
                    if (OriginWithinAABB(localToWorlds[instanceIndex].Position, testAABB))
                        result.Add(instanceHandles[instanceIndex]);
                }
            }
        }

        public readonly NativeList<FloraInstanceHandle> FindInstanceOriginsWithinBoxMatching(FloraInstanceFilter filter, AABB testAABB, AllocatorManager.AllocatorHandle allocator)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingBox = FindCandidateChunksIntersectingBox(filter, testAABB, Allocator.TempJob);
            SourceFilterMode filterMode = GetSourceFilterMode(filter);
            NativeList<FloraInstanceHandle> result = new NativeList<FloraInstanceHandle>(CountInstancesInChunks(chunksIntersectingBox), allocator);
            AddInstanceOriginsWithinBoxMatching(chunksIntersectingBox, filter, filterMode, testAABB, result);
            return result;
        }

        public readonly void FindInstanceOriginsWithinBoxMatching(FloraInstanceFilter filter, AABB testAABB, NativeList<FloraInstanceHandle> result)
        {
            using NativeArray<CullingChunkIndex> chunksIntersectingBox = FindCandidateChunksIntersectingBox(filter, testAABB, Allocator.TempJob);
            SourceFilterMode filterMode = GetSourceFilterMode(filter);
            AddInstanceOriginsWithinBoxMatching(chunksIntersectingBox, filter, filterMode, testAABB, result);
        }

        private readonly void AddInstanceOriginsWithinBoxMatching(
            NativeArray<CullingChunkIndex> chunksIntersectingBox,
            FloraInstanceFilter filter,
            SourceFilterMode filterMode,
            AABB testAABB,
            NativeList<FloraInstanceHandle> result)
        {
            PrepareResultList(chunksIntersectingBox, result);

            for (int i = 0; i < chunksIntersectingBox.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingBox[i];
                int chunkCount = m_ChunkCount[chunk];
                int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
                NativeArray<GraphicsMatrix> localToWorlds = m_InstanceManager.ValueRO.InstanceLocalToWorld;
                NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

                for (int indexInChunk = 0; indexInChunk < chunkCount; indexInChunk++)
                {
                    int instanceIndex = instanceIndices[indexInChunk];
                    FloraInstanceHandle instance = instanceHandles[instanceIndex];

                    if (!MatchesSourceFilter(instance, filter, filterMode))
                        continue;

                    if (OriginWithinAABB(localToWorlds[instanceIndex].Position, testAABB))
                        result.Add(instance);
                }
            }
        }

        public readonly NativeList<FloraInstanceHandle> FindInstanceOriginsWithinBoxMatching(NativeArray<EntityId> prefabGameObjectIDs, AABB testAABB, AllocatorManager.AllocatorHandle allocator)
        {
            if (prefabGameObjectIDs.Length == 0)
                return new NativeList<FloraInstanceHandle>(0, allocator);

            using var identitySourceIds = new NativeParallelHashSet<EntityId>(prefabGameObjectIDs.Length, Allocator.Temp);
            for (int i = 0; i < prefabGameObjectIDs.Length; i++)
                identitySourceIds.Add(prefabGameObjectIDs[i]);

            using NativeArray<CullingChunkIndex> chunksIntersectingBox = FindChunksIntersectingBox(testAABB, Allocator.TempJob);
            NativeList<FloraInstanceHandle> result = new NativeList<FloraInstanceHandle>(CountInstancesInChunks(chunksIntersectingBox), allocator);
            AddInstanceOriginsWithinBoxMatching(chunksIntersectingBox, identitySourceIds, testAABB, result);
            return result;
        }

        public readonly void FindInstanceOriginsWithinBoxMatching(NativeArray<EntityId> prefabGameObjectIDs, AABB testAABB, NativeList<FloraInstanceHandle> result)
        {
            result.Clear();
            if (prefabGameObjectIDs.Length == 0)
                return;

            using var identitySourceIds = new NativeParallelHashSet<EntityId>(prefabGameObjectIDs.Length, Allocator.Temp);
            for (int i = 0; i < prefabGameObjectIDs.Length; i++)
                identitySourceIds.Add(prefabGameObjectIDs[i]);

            using NativeArray<CullingChunkIndex> chunksIntersectingBox = FindChunksIntersectingBox(testAABB, Allocator.TempJob);
            AddInstanceOriginsWithinBoxMatching(chunksIntersectingBox, identitySourceIds, testAABB, result);
        }

        private readonly void AddInstanceOriginsWithinBoxMatching(
            NativeArray<CullingChunkIndex> chunksIntersectingBox,
            NativeParallelHashSet<EntityId> identitySourceIds,
            AABB testAABB,
            NativeList<FloraInstanceHandle> result)
        {
            PrepareResultList(chunksIntersectingBox, result);

            for (int i = 0; i < chunksIntersectingBox.Length; i++)
            {
                CullingChunkIndex chunk = chunksIntersectingBox[i];
                int chunkCount = m_ChunkCount[chunk];
                int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, chunkCount);
                NativeArray<GraphicsMatrix> localToWorlds = m_InstanceManager.ValueRO.InstanceLocalToWorld;
                NativeArray<FloraInstanceHandle> instanceHandles = m_InstanceManager.ValueRO.InstanceHandles;

                for (int indexInChunk = 0; indexInChunk < chunkCount; indexInChunk++)
                {
                    int instanceIndex = instanceIndices[indexInChunk];
                    FloraInstanceHandle instance = instanceHandles[instanceIndex];
                    if (!MatchesIdentitySources(instance, identitySourceIds))
                        continue;

                    if (OriginWithinAABB(localToWorlds[instanceIndex].Position, testAABB))
                        result.Add(instance);
                }
            }
        }

        #endregion
    }
}
