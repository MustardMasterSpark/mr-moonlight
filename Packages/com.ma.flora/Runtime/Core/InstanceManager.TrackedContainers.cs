// Copyright © Magnetic Arcade. All Rights Reserved.

using MA.InternalBridge;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace MA.Flora
{
    internal unsafe partial struct InstanceManager
    {
        private const int TrackedContainerInitialCapacity = 8;

        private struct ContainerTransformBatch
        {
            public int Slot;
            public int StartIndex;
            public int Count;
            public FloraLocalToWorld ParentLocalToWorld;
        }

        private int m_NextTrackedContainerSlot;
        private NativeList<int> m_TrackedContainerFreeList;
        private NativeParallelHashMap<EntityId, int> m_TrackedContainerSlotByEntity;
        private NativeBitSet m_TrackedContainerAllocated;
        private NativeBitSet m_TrackedContainerChunkCacheDirty;
        private NativeArray<EntityId> m_TrackedContainerEntities;
        private NativeBufferArray<FloraInstanceHandle> m_TrackedContainerHandles;
        private NativeBufferArray<FloraInstanceTransform> m_TrackedContainerLocalTransforms;
        private NativeBufferArray<ChunkIndex> m_TrackedContainerChunks;

        private void InitializeTrackedContainers()
        {
            m_NextTrackedContainerSlot = 0;
            m_TrackedContainerFreeList = new NativeList<int>(TrackedContainerInitialCapacity, Allocator.Persistent);
            m_TrackedContainerSlotByEntity = new NativeParallelHashMap<EntityId, int>(TrackedContainerInitialCapacity, Allocator.Persistent);
            m_TrackedContainerAllocated = new NativeBitSet(TrackedContainerInitialCapacity, Allocator.Persistent);
            m_TrackedContainerChunkCacheDirty = new NativeBitSet(TrackedContainerInitialCapacity, Allocator.Persistent);
            m_TrackedContainerEntities = new NativeArray<EntityId>(TrackedContainerInitialCapacity, Allocator.Persistent);
            m_TrackedContainerHandles = new NativeBufferArray<FloraInstanceHandle>(TrackedContainerInitialCapacity, 0, Allocator.Persistent);
            m_TrackedContainerLocalTransforms = new NativeBufferArray<FloraInstanceTransform>(TrackedContainerInitialCapacity, 0, Allocator.Persistent);
            m_TrackedContainerChunks = new NativeBufferArray<ChunkIndex>(TrackedContainerInitialCapacity, 0, Allocator.Persistent);
        }

        private void DisposeTrackedContainers()
        {
            m_TrackedContainerFreeList.Dispose();
            m_TrackedContainerSlotByEntity.Dispose();
            m_TrackedContainerAllocated.Dispose();
            m_TrackedContainerChunkCacheDirty.Dispose();
            m_TrackedContainerEntities.Dispose();
            m_TrackedContainerHandles.Dispose();
            m_TrackedContainerLocalTransforms.Dispose();
            m_TrackedContainerChunks.Dispose();
        }

        private void EnsureTrackedContainerCapacity(int minCapacity)
        {
            if (minCapacity <= m_TrackedContainerEntities.Length)
                return;

            int newCapacity = math.max(minCapacity, math.max(TrackedContainerInitialCapacity, m_TrackedContainerEntities.Length * 2));
            m_TrackedContainerAllocated.ReserveCapacity(newCapacity);
            m_TrackedContainerChunkCacheDirty.ReserveCapacity(newCapacity);
            m_TrackedContainerEntities.ResizeArraySafe(newCapacity);
            m_TrackedContainerHandles.Resize(newCapacity);
            m_TrackedContainerLocalTransforms.Resize(newCapacity);
            m_TrackedContainerChunks.Resize(newCapacity);
        }

        private int AllocateTrackedContainerSlot(EntityId containerEntity)
        {
            if (m_TrackedContainerSlotByEntity.TryGetValue(containerEntity, out int existingSlot))
                return existingSlot;

            int slot = m_TrackedContainerFreeList.Length > 0 ? m_TrackedContainerFreeList.Pop() : m_NextTrackedContainerSlot++;
            EnsureTrackedContainerCapacity(slot + 1);

            m_TrackedContainerSlotByEntity.Add(containerEntity, slot);
            m_TrackedContainerAllocated.Add(slot);
            m_TrackedContainerEntities[slot] = containerEntity;
            return slot;
        }

        private bool TryGetTrackedContainerSlot(EntityId containerEntity, out int slot)
        {
            if (m_TrackedContainerSlotByEntity.TryGetValue(containerEntity, out slot) && m_TrackedContainerAllocated.Contains(slot))
                return true;

            slot = -1;
            return false;
        }

        internal void RegisterTrackedContainer(EntityId containerEntity, NativeArray<FloraInstanceHandle> handles, NativeArray<FloraInstanceTransform> localTransforms)
        {
            Assert.AreEqual(handles.Length, localTransforms.Length);

            int slot = AllocateTrackedContainerSlot(containerEntity);
            var trackedHandles = m_TrackedContainerHandles[slot];
            trackedHandles.Resize(handles.Length);
            trackedHandles.CopyFrom(handles);

            var trackedTransforms = m_TrackedContainerLocalTransforms[slot];
            trackedTransforms.Resize(localTransforms.Length);
            trackedTransforms.CopyFrom(localTransforms);

            m_TrackedContainerChunks[slot].Clear();
            m_TrackedContainerChunkCacheDirty.Add(slot);
        }

        internal void UnregisterTrackedContainer(EntityId containerEntity)
        {
            if (!TryGetTrackedContainerSlot(containerEntity, out int slot))
                return;

            m_TrackedContainerSlotByEntity.Remove(containerEntity);
            m_TrackedContainerAllocated.Remove(slot);
            m_TrackedContainerChunkCacheDirty.Remove(slot);
            m_TrackedContainerEntities[slot] = EntityId.None;
            m_TrackedContainerHandles[slot].Clear();
            m_TrackedContainerLocalTransforms[slot].Clear();
            m_TrackedContainerChunks[slot].Clear();
            m_TrackedContainerFreeList.Add(slot);
        }

        internal void AppendTrackedContainerInstances(EntityId containerEntity, NativeArray<FloraInstanceHandle> handles, NativeArray<FloraInstanceTransform> localTransforms)
        {
            Assert.AreEqual(handles.Length, localTransforms.Length);
            if (!TryGetTrackedContainerSlot(containerEntity, out int slot) || handles.Length == 0)
                return;

            m_TrackedContainerHandles[slot].AddRange(handles);
            m_TrackedContainerLocalTransforms[slot].AddRange(localTransforms);
            m_TrackedContainerChunkCacheDirty.Add(slot);
        }

        internal void UpdateTrackedContainerLocalTransforms(EntityId containerEntity, int startIndex, NativeArray<FloraInstanceTransform> localTransforms)
        {
            if (!TryGetTrackedContainerSlot(containerEntity, out int slot) || localTransforms.Length == 0)
                return;

            localTransforms.CopyTo(m_TrackedContainerLocalTransforms[slot].GetSubArray(startIndex, localTransforms.Length));
        }

        internal void UpdateTrackedContainerLocalTransforms(EntityId containerEntity, NativeArray<int> indices, NativeArray<FloraInstanceTransform> localTransforms)
        {
            Assert.AreEqual(indices.Length, localTransforms.Length);
            if (!TryGetTrackedContainerSlot(containerEntity, out int slot))
                return;

            var trackedTransforms = m_TrackedContainerLocalTransforms[slot];
            for (int i = 0; i < indices.Length; i++)
                trackedTransforms[indices[i]] = localTransforms[i];
        }

        internal void RemoveTrackedContainerInstance(EntityId containerEntity, int index)
        {
            if (!TryGetTrackedContainerSlot(containerEntity, out int slot))
                return;

            m_TrackedContainerHandles[slot].RemoveAtSwapBack(index);
            m_TrackedContainerLocalTransforms[slot].RemoveAtSwapBack(index);
            m_TrackedContainerChunkCacheDirty.Add(slot);
        }

        internal void ClearTrackedContainerInstances(EntityId containerEntity)
        {
            if (!TryGetTrackedContainerSlot(containerEntity, out int slot))
                return;

            m_TrackedContainerHandles[slot].Clear();
            m_TrackedContainerLocalTransforms[slot].Clear();
            m_TrackedContainerChunks[slot].Clear();
            m_TrackedContainerChunkCacheDirty.Remove(slot);
        }

        private void MarkTrackedContainerChunkCacheDirty(EntityId containerEntity)
        {
            if (TryGetTrackedContainerSlot(containerEntity, out int slot))
                m_TrackedContainerChunkCacheDirty.Add(slot);
        }

        private void MarkTrackedContainerChunkCacheDirty(FloraInstanceHandle instance)
        {
            var instanceInContainer = InstanceRegistry.Data.GetInstanceInContainer(instance);
            if (!instanceInContainer.Equals(InstanceInContainer.None))
                MarkTrackedContainerChunkCacheDirty(instanceInContainer.ContainerEntity);
        }

        private void RebuildTrackedContainerChunkCache(int slot)
        {
            if (!m_TrackedContainerAllocated.Contains(slot))
                return;

            var trackedHandles = m_TrackedContainerHandles[slot];
            var trackedChunks = m_TrackedContainerChunks[slot];
            trackedChunks.Clear();
            if (trackedHandles.Length == 0)
            {
                m_TrackedContainerChunkCacheDirty.Remove(slot);
                return;
            }

            using var uniqueChunks = new NativeParallelHashMap<ChunkIndex, byte>(trackedHandles.Length, Allocator.Temp);
            for (int i = 0; i < trackedHandles.Length; i++)
            {
                var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(trackedHandles[i]);
                if (instanceInChunk.Chunk == ChunkIndex.None)
                    continue;

                if (uniqueChunks.TryAdd(instanceInChunk.Chunk, 0))
                    trackedChunks.Add(instanceInChunk.Chunk);
            }

            m_TrackedContainerChunkCacheDirty.Remove(slot);
        }

        internal JobHandle ScheduleUpdateTrackedContainerTransforms(in UnityTransformDispatchData containerTransformData, JobHandle inputDeps)
        {
            int changedCount = containerTransformData.transformedID.Length;
            if (changedCount == 0)
                return inputDeps;

            var validSlots = NewFrameArray<int>(changedCount, NativeArrayOptions.UninitializedMemory);
            var parentLocalToWorlds = NewFrameArray<FloraLocalToWorld>(changedCount, NativeArrayOptions.UninitializedMemory);
            int validCount = 0;
            int batchCount = 0;

            for (int i = 0; i < changedCount; i++)
            {
                EntityId containerEntity = containerTransformData.transformedID[i];
                if (!TryGetTrackedContainerSlot(containerEntity, out int slot))
                    continue;

                validSlots[validCount] = slot;
                parentLocalToWorlds[validCount] = containerTransformData.localToWorldMatrices[i];
                validCount++;

                if (m_TrackedContainerChunkCacheDirty.Contains(slot))
                    RebuildTrackedContainerChunkCache(slot);

                var trackedChunks = m_TrackedContainerChunks[slot];
                for (int chunkIndex = 0; chunkIndex < trackedChunks.Length; chunkIndex++)
                    MarkChunkTransformDirty(trackedChunks[chunkIndex]);

                int trackedCount = m_TrackedContainerHandles[slot].Length;
                batchCount += MathUtility.DivideAndRoundUp(trackedCount, UpdateJobBatchSize);
            }

            if (validCount == 0 || batchCount == 0)
                return inputDeps;

            var batches = NewFrameArray<ContainerTransformBatch>(batchCount, NativeArrayOptions.UninitializedMemory);
            int batchWriteIndex = 0;
            for (int i = 0; i < validCount; i++)
            {
                int slot = validSlots[i];
                int trackedCount = m_TrackedContainerHandles[slot].Length;
                for (int startIndex = 0; startIndex < trackedCount; startIndex += UpdateJobBatchSize)
                {
                    batches[batchWriteIndex++] = new ContainerTransformBatch
                    {
                        Slot = slot,
                        StartIndex = startIndex,
                        Count = math.min(UpdateJobBatchSize, trackedCount - startIndex),
                        ParentLocalToWorld = parentLocalToWorlds[i],
                    };
                }
            }

            m_DataDependencies = JobHandle.CombineDependencies(m_DataDependencies, inputDeps);
            m_DataDependencies = new UpdateTrackedContainerTransformsJob
            {
                Batches = batches.GetSubArray(0, batchWriteIndex),
                ContainerHandles = m_TrackedContainerHandles,
                ContainerLocalTransforms = m_TrackedContainerLocalTransforms,
                PrevLocalToWorlds = m_InstancePrevLocalToWorld,
                LocalToWorlds = m_InstanceLocalToWorld,
                InstanceAABBs = m_InstanceAABBs,
                FlippedWinding = m_InstanceFlippedWinding,
                MovedThisFrame = m_InstanceMovedThisFrame,
            }.ScheduleParallel(batchWriteIndex, 1, m_DataDependencies);

            return m_DataDependencies;
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct UpdateTrackedContainerTransformsJob : IJobFor
        {
            [ReadOnly] public NativeArray<ContainerTransformBatch> Batches;
            [ReadOnly] public NativeBufferArray<FloraInstanceHandle> ContainerHandles;
            [ReadOnly] public NativeBufferArray<FloraInstanceTransform> ContainerLocalTransforms;
            [ReadOnly] public NativeArray<GraphicsMatrix> PrevLocalToWorlds;

            [NativeDisableParallelForRestriction] public NativeArray<GraphicsMatrix> LocalToWorlds;
            [NativeDisableParallelForRestriction] public NativeArray<AABB> InstanceAABBs;
            [NativeDisableParallelForRestriction] public NativeArray<byte> MovedThisFrame;
            [NativeDisableParallelForRestriction] public NativeArray<byte> FlippedWinding;

            public void Execute(int index)
            {
                ContainerTransformBatch batch = Batches[index];
                var trackedHandles = ContainerHandles[batch.Slot];
                var trackedTransforms = ContainerLocalTransforms[batch.Slot];

                for (int i = 0; i < batch.Count; i++)
                {
                    int containerIndex = batch.StartIndex + i;
                    var instance = trackedHandles[containerIndex];
                    var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                    if (Hint.Unlikely(instanceInChunk.Chunk == ChunkIndex.None))
                        continue;

                    int instanceIndex = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;
                    var localToWorld = batch.ParentLocalToWorld.Transform(trackedTransforms[containerIndex]);
                    var prevLocalToWorld = PrevLocalToWorlds[instanceIndex];
                    if (Hint.Unlikely(localToWorld.NearlyEquals(prevLocalToWorld)))
                        continue;

                    AABB worldAABB = AABB.TransformAABB(localToWorld, instanceInChunk.Chunk.LocalAABB);
                    LocalToWorlds[instanceIndex] = localToWorld;
                    InstanceAABBs[instanceIndex] = worldAABB;
                    MovedThisFrame[instanceIndex] = 1;
                    FlippedWinding[instanceIndex] = localToWorld.IsFlipped ? (byte)1 : (byte)0;
                }
            }
        }
    }
}
