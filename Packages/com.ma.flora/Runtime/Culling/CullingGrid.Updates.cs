// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine.Assertions;

namespace MA.Flora
{
    internal unsafe partial struct CullingGrid
    {
        #region Updates

        private static readonly ProfilerMarker UpdateInstancesMarker = new ProfilerMarker("CullingGrid.UpdateInstances");

        public void UpdateInstances(NativeArray<ChunkIndex> instanceChunksToUpdate)
        {
            using ProfilerMarker.AutoScope _ = UpdateInstancesMarker.Auto();
            if (instanceChunksToUpdate.Length == 0)
                return;

            NativeArray<AABB> instanceAABBs = m_InstanceManager.ValueRW.InstanceAABBs;
            NativeArray<CellLocation> instanceLocations = m_InstanceManager.ValueRW.InstanceLocations;
            NativeArray<InstanceInCullingChunk> instanceInCullingChunks = m_InstanceManager.ValueRW.InstanceInCullingChunks;

            for (int chunkIndex = 0; chunkIndex < instanceChunksToUpdate.Length; chunkIndex++)
            {
                ChunkIndex instanceChunk = instanceChunksToUpdate[chunkIndex];
                int instanceOffset = instanceChunk.AsInstanceOffset();
                int instanceCount = instanceChunk.Count;
                ArchetypeIndex archetype = instanceChunk.Archetype;
                Assert.IsTrue(archetype.Enabled, "Disabled archetypes should not have their instances in the culling grid.");

                for (int indexInChunk = 0; indexInChunk < instanceCount; indexInChunk++)
                {
                    int instanceIndex = instanceOffset + indexInChunk;
                    InstanceInCullingChunk instanceInCullingChunk = instanceInCullingChunks[instanceIndex];
                    CullingChunkIndex cullingChunk = instanceInCullingChunk.Chunk;
                    if (cullingChunk == CullingChunkIndex.None) continue;

                    CellLocation oldLocation = instanceLocations[instanceIndex];
                    CellLocation newLocation = GetLocationForAABB(instanceAABBs[instanceIndex]);
                    if (newLocation == oldLocation)
                    {
                        SetChunkFlagsDirty(cullingChunk);
                        SetChunkAttributesDirty(cullingChunk);
                    }
                    else
                    {
                        instanceLocations[instanceIndex] = newLocation;
                        m_ChunkDynamic.Add(cullingChunk);

                        CellBucketIndex oldBucket = m_ChunkBucket[cullingChunk];
                        Assert.IsTrue(oldBucket != CellBucketIndex.None, "Culling chunk does not have a valid bucket.");
                        RemoveInstancesFromChunk(oldBucket, cullingChunk, instanceInCullingChunk.IndexInChunk, 1);

                        CellIndex newCell = GetOrCreateCell(newLocation);
                        CellBucketIndex newBucket = GetOrCreateCellBucket(archetype, newCell);
                        CullingChunkIndex newChunk = GetChunkWithFreeSlots(newBucket);
                        AddInstancesToChunk(newBucket, newChunk, instanceIndex, 1);
                    }
                }
            }
        }

        private PackedCullingChunkBatch UpdateChunkBatch(CullingChunkIndex chunk)
        {
            int count = m_ChunkCount[chunk];
            Assert.IsTrue(count > 0, "Cannot update packed chunk data for an empty chunk.");

            int* instanceIndices = GetInstanceIndicesInChunkRO(chunk, 0, count);
            int firstInstanceIndex = instanceIndices[0];
            int firstInstanceOffset = GetBatchOffsetForInstance(firstInstanceIndex);

            bool canCompress = count == ChunkCapacity && !m_ChunkDynamic.Contains(chunk);
            if (canCompress)
            {
                for (int i = 1; i < ChunkCapacity; i++)
                {
                    int instanceIndex = instanceIndices[i];
                    int instanceOffset = GetBatchOffsetForInstance(instanceIndex);
                    if (instanceOffset != firstInstanceOffset + i)
                    {
                        // Offset is not continuous, cannot compress
                        canCompress = false;
                        break;
                    }
                }
            }

            PackedCullingChunkBatch packedChunkBatch;
            if (canCompress)
            {
                FreeIndirectPage(chunk);
                packedChunkBatch = PackedCullingChunkBatch.Compressed((uint)firstInstanceOffset);
            }
            else
            {
                int indirectPageIndex = AllocateIndirectPage(chunk);
                int indirectOffset = indirectPageIndex * IndirectPageSize;

                int scatterOffset = m_PendingIndirectOffsetUpdates.Length;
                uint packedPageIndexAndCount = ((uint)indirectPageIndex << 8) | (uint)count;

                m_QueuedIndirectChunks.Add(chunk);
                m_PendingIndirectPageUpdates.Add(packedPageIndexAndCount);
                m_PendingIndirectOffsetUpdates.Resize(scatterOffset + IndirectPageSize, NativeArrayOptions.ClearMemory);
                packedChunkBatch = PackedCullingChunkBatch.Indirect((uint)indirectOffset, count);
            }

            return packedChunkBatch;
        }

        #endregion
    }
}
