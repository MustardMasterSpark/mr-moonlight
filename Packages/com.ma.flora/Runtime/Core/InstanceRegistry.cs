// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Diagnostics;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace MA.Flora
{
    internal struct InstanceInChunk : IComparable<InstanceInChunk>, IEquatable<InstanceInChunk>
    {
        public static InstanceInChunk None => default;

        public ChunkIndex Chunk;
        public int IndexInChunk;

        public int CompareTo(InstanceInChunk other)
        {
            int chunkCmp = Chunk - other.Chunk;
            int indexCmp = IndexInChunk - other.IndexInChunk;
            return (Chunk != other.Chunk) ? chunkCmp : indexCmp;
        }

        public override int GetHashCode() => Chunk ^ IndexInChunk;
        public bool Equals(InstanceInChunk other) => CompareTo(other) == 0;
        public override string ToString() => Equals(None) ? "InstanceInChunk.None" : $"InstanceInChunk({Chunk}:{IndexInChunk})";
    }

    internal struct InstanceInContainer : IComparable<InstanceInContainer>, IEquatable<InstanceInContainer>
    {
        public static InstanceInContainer None => default;

        public EntityId ContainerEntity;
        public int IndexInContainer;
        public EntityObjectRef<FloraInstanceContainer> Container => new EntityObjectRef<FloraInstanceContainer>(ContainerEntity);

        public int CompareTo(InstanceInContainer other)
        {
            int idCmp = ContainerEntity.CompareTo(other.ContainerEntity);
            int indexCmp = IndexInContainer - other.IndexInContainer;
            return (Container != other.Container) ? idCmp : indexCmp;
        }

        public override int GetHashCode() => unchecked((ContainerEntity.GetHashCode() * 397) ^ IndexInContainer);
        public bool Equals(InstanceInContainer other) => CompareTo(other) == 0;
        public override string ToString() => Equals(None) ? "InstanceInContainer.None" : $"InstanceInContainer({Container}:{IndexInContainer})";
    }

    internal struct TreeInTerrain : IComparable<TreeInTerrain>, IEquatable<TreeInTerrain>
    {
        public static TreeInTerrain None => default;

        public EntityId TerrainEntity;
        public int IndexInTreeInstances;

        public int CompareTo(TreeInTerrain other)
        {
            int idCmp = TerrainEntity.CompareTo(other.TerrainEntity);
            int indexCmp = IndexInTreeInstances - other.IndexInTreeInstances;
            return (TerrainEntity != other.TerrainEntity) ? idCmp : indexCmp;
        }

        public override int GetHashCode() => unchecked((TerrainEntity.GetHashCode() * 397) ^ IndexInTreeInstances);
        public bool Equals(TreeInTerrain other) => CompareTo(other) == 0;
        public override string ToString() => Equals(None) ? "TreeInTerrain.None" : $"TreeInTerrain({TerrainEntity}:{IndexInTreeInstances})";
    }

    internal struct DetailInTerrain : IComparable<DetailInTerrain>, IEquatable<DetailInTerrain>
    {
        public static DetailInTerrain None => default;

        public EntityId TerrainEntity;
        public int LayerIndex;
        public EntityObjectRef<Terrain> Terrain => new EntityObjectRef<Terrain>(TerrainEntity);

        public int CompareTo(DetailInTerrain other)
        {
            int idCmp = TerrainEntity.CompareTo(other.TerrainEntity);
            int indexCmp = LayerIndex - other.LayerIndex;
            return (Terrain != other.Terrain) ? idCmp : indexCmp;
        }

        public override int GetHashCode() => unchecked((TerrainEntity.GetHashCode() * 397) ^ LayerIndex);
        public bool Equals(DetailInTerrain other) => CompareTo(other) == 0;
        public override string ToString() => Equals(None) ? "DetailInTerrain.None" : $"DetailInTerrain({Terrain}:{LayerIndex})";
    }

    internal struct InstanceInSourceRecord : IComparable<InstanceInSourceRecord>, IEquatable<InstanceInSourceRecord>
    {
        public static InstanceInSourceRecord None => default;

        public SourceRecordIndex SourceRecord;
        public int IndexInList;

        public int CompareTo(InstanceInSourceRecord other)
        {
            int idCmp = SourceRecord - other.SourceRecord;
            int indexCmp = IndexInList - other.IndexInList;
            return (SourceRecord != other.SourceRecord) ? idCmp : indexCmp;
        }

        public override int GetHashCode() => SourceRecord ^ IndexInList;
        public bool Equals(InstanceInSourceRecord other) => CompareTo(other) == 0;
        public override string ToString() => Equals(None) ? "InstanceInSourceRecord.None" : $"InstanceInSourceRecord({SourceRecord}:{IndexInList})";
    }

    internal unsafe struct InstanceRegistry : IDisposable
    {
        private struct StaticIdentifier
        {
            internal static readonly SharedStatic<InstanceRegistry> Ref = SharedStatic<InstanceRegistry>.GetOrCreate<StaticIdentifier>();
        }

        public static ref InstanceRegistry Data => ref StaticIdentifier.Ref.Data;

        private const int InstancesInBlock = 8192;
        private const int BlockCount = 16384;
        private const int BlockBusy  = -1;
        internal const int MaximumTheoreticalAmountOfInstances = InstancesInBlock * BlockCount;

        private struct DataBlock
        {
            // 8k instances per DataBlock
            public fixed ulong Allocated[InstancesInBlock / 64];
            public fixed ulong InstanceInChunk[InstancesInBlock];
            public fixed ulong InstanceInSource[InstancesInBlock];
#if UNITY_6000_5_OR_NEWER
            public fixed ulong InstanceInContainer[InstancesInBlock * 2];
            public fixed ulong TreeInTerrain[InstancesInBlock * 2];
            public fixed ulong DetailInTerrain[InstancesInBlock * 2];
#else
            public fixed ulong InstanceInContainer[InstancesInBlock];
            public fixed ulong TreeInTerrain[InstancesInBlock];
            public fixed ulong DetailInTerrain[InstancesInBlock];
#endif
#if UNITY_6000_5_OR_NEWER
            public fixed ulong SceneEntityId[InstancesInBlock];
#else
            public fixed int SceneEntityId[InstancesInBlock];
#endif
            public fixed int InstanceRenderer[InstancesInBlock];
            public fixed int Versions[InstancesInBlock];
#if ENABLE_FLORA_DEBUG_NAMES
            public fixed int NameByInstanceIndex[InstancesInBlock];
#endif
        }

        // 16k pointers, each allocation containing data for 8k instances
        private fixed ulong m_DataBlocks[BlockCount];
        private fixed int m_InstanceCount[BlockCount];

        internal int ThreadUnsafeInstanceCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < BlockCount; i++)
                    count += m_InstanceCount[i];

                return count;
            }
        }

        internal void ValidateInstances()
        {
            for (int blockIndex = 0; blockIndex < BlockCount; blockIndex++)
            {
                var block = (DataBlock*)m_DataBlocks[blockIndex];
                if (block == null)
                    continue;

                var allocated = block->Allocated;

                for (int i = 0; i < InstancesInBlock; i++)
                {
                    var mask = 1UL << (i % 64);
                    if ((allocated[i / 64] & mask) != 0)
                    {
                        var instanceInChunk = ((InstanceInChunk*)block->InstanceInChunk)[i];
                        Assert.IsTrue(instanceInChunk.Chunk != ChunkIndex.None);
                        Assert.IsTrue(instanceInChunk.IndexInChunk <= instanceInChunk.Chunk.Count);
                    }
                }
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void DebugOnlyThrowIfInstanceDoesntExist(FloraInstanceHandle instance, DataBlock* block, int indexInBlock)
        {
            bool MissingInBitmask()
            {
                var bitfield = block->Allocated[indexInBlock / 64];
                var mask = 1ul << (indexInBlock % 64);
                return (bitfield & mask) == 0;
            }

            if (block == null || MissingInBitmask())
            {
                throw new ArgumentException(
                    $"All instances passed to InstanceRegistry must exist. " +
                    $"One of the instances has already been destroyed or was never created: ({instance})");
            }
        }

        internal FloraInstanceHandle GetInstanceByIndex(int index)
        {
            var blockIndex = index / InstancesInBlock;
            var indexInBlock = index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];
            if (block == null) return FloraInstanceHandle.Null;

            var version = block->Versions[indexInBlock];
            return ((uint)version & 1) == 0 ? FloraInstanceHandle.Null : new FloraInstanceHandle { Index = index, Version = version };
        }

        internal bool Exists(FloraInstanceHandle instance)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];
            if (block == null) return false;

            if (((uint)instance.Version & 1) == 0 || block->Versions[indexInBlock] != instance.Version) return false;
            return true;
        }

        internal void SetInstanceVersion(FloraInstanceHandle instance, int version)
        {
            // TODO - find a way to remove this function.
            // Changing the version is potentially dangerous but currently required for deserialization.

            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];

            DebugOnlyThrowIfInstanceDoesntExist(instance, block, indexInBlock);

            block->Versions[indexInBlock] = version;
        }

        internal InstanceInChunk GetInstanceInChunk(FloraInstanceHandle instance)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];
            if (block == null) return InstanceInChunk.None;

            var bitfield = block->Allocated[indexInBlock / 64];
            var mask = 1ul << (indexInBlock % 64);

            if ((bitfield & mask) == 0) return InstanceInChunk.None;

            return ((InstanceInChunk*)block->InstanceInChunk)[indexInBlock];
        }

        internal void SetInstanceInChunk(FloraInstanceHandle instance, InstanceInChunk instanceInChunk)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];

            DebugOnlyThrowIfInstanceDoesntExist(instance, block, indexInBlock);

            ((InstanceInChunk*)block->InstanceInChunk)[indexInBlock] = instanceInChunk;
        }

        internal InstanceInSourceRecord GetInstanceInSourceRecord(FloraInstanceHandle instance)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];
            if (block == null) return InstanceInSourceRecord.None;

            var bitfield = block->Allocated[indexInBlock / 64];
            var mask = 1ul << (indexInBlock % 64);

            if ((bitfield & mask) == 0) return InstanceInSourceRecord.None;

            return ((InstanceInSourceRecord*)block->InstanceInSource)[indexInBlock];
        }

        internal void SetInstanceInSourceRecord(FloraInstanceHandle instance, InstanceInSourceRecord instanceInSourceRecord)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];

            DebugOnlyThrowIfInstanceDoesntExist(instance, block, indexInBlock);

            ((InstanceInSourceRecord*)block->InstanceInSource)[indexInBlock] = instanceInSourceRecord;
        }

        internal InstanceInContainer GetInstanceInContainer(FloraInstanceHandle instance)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];
            if (block == null) return InstanceInContainer.None;

            var bitfield = block->Allocated[indexInBlock / 64];
            var mask = 1ul << (indexInBlock % 64);

            if ((bitfield & mask) == 0) return InstanceInContainer.None;

            return ((InstanceInContainer*)block->InstanceInContainer)[indexInBlock];
        }

        internal void SetInstanceInContainer(FloraInstanceHandle instance, InstanceInContainer instanceInContainer)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];

            DebugOnlyThrowIfInstanceDoesntExist(instance, block, indexInBlock);

            ((InstanceInContainer*)block->InstanceInContainer)[indexInBlock] = instanceInContainer;
        }

        internal TreeInTerrain GetTreeInTerrain(FloraInstanceHandle instance)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];
            if (block == null) return TreeInTerrain.None;

            var bitfield = block->Allocated[indexInBlock / 64];
            var mask = 1ul << (indexInBlock % 64);

            if ((bitfield & mask) == 0) return TreeInTerrain.None;

            return ((TreeInTerrain*)block->TreeInTerrain)[indexInBlock];
        }

        internal void SetTreeInTerrain(FloraInstanceHandle instance, TreeInTerrain treeInTerrain)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];

            DebugOnlyThrowIfInstanceDoesntExist(instance, block, indexInBlock);

            ((TreeInTerrain*)block->TreeInTerrain)[indexInBlock] = treeInTerrain;
        }

        internal DetailInTerrain GetDetailInTerrain(FloraInstanceHandle instance)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];
            if (block == null) return DetailInTerrain.None;

            var bitfield = block->Allocated[indexInBlock / 64];
            var mask = 1ul << (indexInBlock % 64);

            if ((bitfield & mask) == 0) return DetailInTerrain.None;

            return ((DetailInTerrain*)block->DetailInTerrain)[indexInBlock];
        }

        internal void SetDetailInTerrain(FloraInstanceHandle instance, DetailInTerrain detailInTerrain)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];

            DebugOnlyThrowIfInstanceDoesntExist(instance, block, indexInBlock);

            ((DetailInTerrain*)block->DetailInTerrain)[indexInBlock] = detailInTerrain;
        }

        internal EntityId GetSceneEntityId(FloraInstanceHandle instance)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];
            if (block == null) return EntityId.None;

            var bitfield = block->Allocated[indexInBlock / 64];
            var mask = 1ul << (indexInBlock % 64);

            if ((bitfield & mask) == 0) return EntityId.None;

#if UNITY_6000_5_OR_NEWER
            return EntityId.FromULong(block->SceneEntityId[indexInBlock]);
#else
            return block->SceneEntityId[indexInBlock];
#endif
        }

        internal void SetSceneEntityId(FloraInstanceHandle instance, EntityId entityId)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];

            DebugOnlyThrowIfInstanceDoesntExist(instance, block, indexInBlock);

#if UNITY_6000_5_OR_NEWER
            block->SceneEntityId[indexInBlock] = EntityId.ToULong(entityId);
#else
            block->SceneEntityId[indexInBlock] = entityId;
#endif
        }

        internal InstanceRendererIndex GetInstanceRendererIndex(FloraInstanceHandle instance)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];
            if (block == null) return InstanceRendererIndex.None;

            var bitfield = block->Allocated[indexInBlock / 64];
            var mask = 1ul << (indexInBlock % 64);

            if ((bitfield & mask) == 0) return InstanceRendererIndex.None;

            return ((InstanceRendererIndex*)block->InstanceRenderer)[indexInBlock];
        }

        internal void SetInstanceRendererIndex(FloraInstanceHandle instance, InstanceRendererIndex instanceRendererIndex)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];

            DebugOnlyThrowIfInstanceDoesntExist(instance, block, indexInBlock);

            ((InstanceRendererIndex*)block->InstanceRenderer)[indexInBlock] = instanceRendererIndex;
        }

        internal void AllocateInstances(NativeArray<FloraInstanceHandle> instances)
        {
            AllocateInstances((FloraInstanceHandle*)instances.GetUnsafePtr(), instances.Length, ChunkIndex.None, 0);
        }

        internal void AllocateInstances(FloraInstanceHandle* instances, int instanceCount)
        {
            AllocateInstances(instances, instanceCount, ChunkIndex.None, 0);
        }

        internal void AllocateInstances(FloraInstanceHandle* instances, int totalCount, ChunkIndex chunkIndex, int firstInstanceInChunkIndex)
        {
            var instanceInChunkIndex = firstInstanceInChunkIndex;

            for (int i = 0; i < BlockCount; i++)
            {
                var blockCount = Interlocked.Add(ref m_InstanceCount[i], 0);

                if (blockCount == BlockBusy || blockCount == InstancesInBlock)
                {
                    continue;
                }

                var blockAvailable = InstancesInBlock - blockCount;
                var count = math.min(blockAvailable, totalCount);

                // Set the count to a flag indicating that this block is busy (-1)
                var before = Interlocked.CompareExchange(ref m_InstanceCount[i], BlockBusy, blockCount);

                if (before != blockCount)
                {
                    // Another thread is messing around with this block, it's either busy or was changed
                    // between the time we read the count and now. In both cases, let's keep looking.
                    continue;
                }

                DataBlock* block = (DataBlock*)m_DataBlocks[i];

                // Be careful that the block might exist even if the count is zero, checking the pointer
                // for null is the only valid way to tell if the block exists or not.
                if (block == null)
                {
                    block = (DataBlock*)AllocatorManager.Allocate(Allocator.Persistent, sizeof(DataBlock), 8);
                    UnsafeUtility.MemClear(block, sizeof(DataBlock));
                    m_DataBlocks[i] = (ulong)block;
                }

                int remainingCount = math.min(blockAvailable, count);
                var allocated = block->Allocated;
                var versions = block->Versions;
                var instanceInChunk = block->InstanceInChunk;
                var baseInstanceIndex = i * InstancesInBlock;

                while (remainingCount > 0)
                {
                    for (int maskIndex = 0; maskIndex < InstancesInBlock / 64; maskIndex++)
                    {
                        if (allocated[maskIndex] != ~0UL)
                        {
                            // There is some space in this one

                            for (int instance = 0; instance < 64; instance++)
                            {
                                var indexInBlock = maskIndex * 64 + instance;
                                var mask = 1UL << (indexInBlock % 64);

                                if ((allocated[maskIndex] & mask) == 0)
                                {
                                    allocated[maskIndex] |= mask;

                                    *instances = new FloraInstanceHandle
                                    {
                                        Index = baseInstanceIndex + indexInBlock,
                                        Version = versions[indexInBlock] += 1
                                    };

                                    if (chunkIndex != ChunkIndex.None)
                                    {
                                        ((InstanceInChunk*)instanceInChunk)[indexInBlock] = new InstanceInChunk
                                        {
                                            Chunk = chunkIndex,
                                            IndexInChunk = instanceInChunkIndex,
                                        };
                                    }
                                    else
                                    {
                                        instanceInChunk[indexInBlock] = 0;
                                    }

                                    instances++;
                                    instanceInChunkIndex++;
                                    remainingCount--;

                                    if (remainingCount == 0)
                                    {
                                        break;
                                    }
                                }
                            }

                            if (remainingCount == 0)
                            {
                                break;
                            }
                        }
                    }
                }

                Assert.AreEqual(0, remainingCount);

                var resultCheck = Interlocked.CompareExchange(ref m_InstanceCount[i], blockCount + count, BlockBusy);

                Assert.AreEqual(resultCheck, BlockBusy);

                totalCount -= count;

                if (totalCount == 0)
                {
                    return;
                }
            }

            throw new InvalidOperationException("Could not find a data block for instance allocation.");
        }

        internal void DeallocateInstances(NativeArray<FloraInstanceHandle> instances)
        {
            DeallocateInstances((FloraInstanceHandle*)instances.GetUnsafePtr(), instances.Length);
        }

        internal void DeallocateInstances(Span<FloraInstanceHandle> instances)
        {
            fixed (FloraInstanceHandle* ptr = instances)
            {
                DeallocateInstances(ptr, instances.Length);
            }
        }

        internal void DeallocateInstances(FloraInstanceHandle* instances, int count)
        {
            for (int i = 0; i < count;)
            {
                int rangeStart = i;
                int startIndex = instances[i].Index;

                int prevIndexInBlock = startIndex % InstancesInBlock;
                int blockIndex = startIndex / InstancesInBlock;

                for (i++; i < count; i++)
                {
                    if (instances[i].Index / InstancesInBlock != blockIndex)
                    {
                        // Different data block
                        break;
                    }

                    int indexInBlock = instances[i].Index % InstancesInBlock;
                    if (indexInBlock != prevIndexInBlock + 1)
                    {
                        // Same data block, but not the next instance in range
                        break;
                    }

                    prevIndexInBlock = indexInBlock;
                }

                int rangeEnd = i;
                int endIndex = startIndex + (rangeEnd - rangeStart);
                int blockCount = Interlocked.Add(ref m_InstanceCount[blockIndex], 0);

                if (blockCount == 0)
                {
                    // Looks like this block has been already deallocated.
                    // We are trying to deallocate instances which are already gone, skip.
                    continue;
                }

                while (true)
                {
                    if (blockCount != BlockBusy)
                    {
                        // Set the count to a flag indicating that this block is busy (-1)
                        var before = Interlocked.CompareExchange(ref m_InstanceCount[blockIndex], BlockBusy, blockCount);

                        if (before == blockCount)
                        {
                            // Exchange succeeded
                            break;
                        }

                        blockCount = before;
                    }
                    else
                    {
                        blockCount = Interlocked.Add(ref m_InstanceCount[blockIndex], 0);
                    }
                }

                if (blockCount == 0)
                {
                    // This is very unlikely, but the block has been deallocated while we were waiting for it.
                    // Same as the test above, skip the block. But don't forget to restore the count.
                    var resultCheck = Interlocked.CompareExchange(ref m_InstanceCount[i], 0, BlockBusy);
                    Assert.AreEqual(resultCheck, BlockBusy);

                    continue;
                }

                var block = (DataBlock*)m_DataBlocks[blockIndex];

                // It would be tempting to check to immediately check if deallocation would bring the instance count
                // for the data block to zero and deallocate the whole block. Unfortunately, in the eventuality that
                // we are trying to deallocate an instance which was already deallocated, this could lead to
                // discarding the data used by allocated instances. So we need to take the slow route and process
                // the data block even if we end up getting rid of it immediately after.

                var allocated = block->Allocated;
                var versions = block->Versions;
                var instanceInChunk = block->InstanceInChunk;
                var instanceInSource = block->InstanceInSource;
                var instanceInContainer = block->InstanceInContainer;
                var treeInTerrain = block->TreeInTerrain;
                var detailInTerrain = block->DetailInTerrain;
                var sceneObjectID = block->SceneEntityId;

                for (int j = startIndex, indexInInstancesArray = rangeStart; j < endIndex; j++, indexInInstancesArray++)
                {
                    var indexInBlock = j % InstancesInBlock;

                    if (versions[indexInBlock] == instances[indexInInstancesArray].Version)
                    {
                        // Matching versions confirm that we are deallocating the intended instance

                        var mask = 1UL << (indexInBlock % 64);

                        versions[indexInBlock]++;
                        allocated[indexInBlock / 64] &= ~0UL ^ mask;
                        instanceInChunk[indexInBlock] = 0;
                        instanceInContainer[indexInBlock] = 0;
                        instanceInSource[indexInBlock] = 0;
                        treeInTerrain[indexInBlock] = 0;
                        detailInTerrain[indexInBlock] = 0;
                        sceneObjectID[indexInBlock] = 0;
#if ENABLE_FLORA_DEBUG_NAMES
                        block->NameByInstanceIndex[indexInBlock] = 0;
#endif

                        blockCount--;
                    }
                }

                // Do not deallocate the block even if it's empty. Versions should be preserved.

                {
                    var resultCheck = Interlocked.CompareExchange(ref m_InstanceCount[blockIndex], blockCount, BlockBusy);
                    Assert.AreEqual(resultCheck, BlockBusy);
                }
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < BlockCount; ++i)
            {
                var block = (void*)m_DataBlocks[i];
                if (block != null)
                {
                    AllocatorManager.Free(Allocator.Persistent, block);
                }
            }

            this = default;
        }

#if ENABLE_FLORA_DEBUG_NAMES
        public InstanceName GetInstanceName(FloraInstance instance)
        {
            return GetInstanceNameByIndex(instance.Index);
        }

        public InstanceName GetInstanceNameByIndex(int index)
        {
            var blockIndex = index / InstancesInBlock;
            var indexInBlock = index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];
            if (block == null) return default;

            var bitfield = block->Allocated[indexInBlock / 64];
            var mask = 1UL << (indexInBlock % 64);

            if ((bitfield & mask) == 0) return default;

            var nameIndex = block->NameByInstanceIndex[indexInBlock];
            return new InstanceName { Index = nameIndex };
        }

        public void SetInstanceName(FloraInstance instance, InstanceName name)
        {
            var blockIndex = instance.Index / InstancesInBlock;
            var indexInBlock = instance.Index % InstancesInBlock;

            var block = (DataBlock*)m_DataBlocks[blockIndex];

            DebugOnlyThrowIfInstanceDoesntExist(instance, block, indexInBlock);

            block->NameByInstanceIndex[indexInBlock] = name.Index;
        }
#endif
    }
}
