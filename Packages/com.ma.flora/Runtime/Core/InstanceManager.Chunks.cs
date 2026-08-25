// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using Random = Unity.Mathematics.Random;

namespace MA.Flora
{
    internal unsafe partial struct InstanceManager
    {
        public const int MaxPossibleChunkCount = 524288;
        public const int ChunkCapacity = 64;
        public const int ChunkShift    = 6; // log2(ChunkCapacity)
        public const int ChunkMask     = ChunkCapacity - 1;

        public const int ChunkInitialCapacity = 8;
        public const int ChunkInitialInstanceCapacity = ChunkInitialCapacity * ChunkCapacity;
        private const int ChunkGrowPageSize = 4096;
        private const int InstanceGrowPageSize = ChunkGrowPageSize * ChunkCapacity;

        private const int BatchingMinSize = 16;
        private const int MemCpyThreshold = 8;

        #region Chunk Storage

        internal struct ChunkStore
        {
            private struct StaticIdentifier
            {
                internal static readonly SharedStatic<ChunkStore> Ref = SharedStatic<ChunkStore>.GetOrCreate<StaticIdentifier>();
            }

            public static PerChunkData* Data => StaticIdentifier.Ref.Data.m_PerChunkData;

            public struct PerChunkData
            {
                public static PerChunkData Default =>
                    new PerChunkData
                {
                    Type = ArchetypeIndex.None,
                    InstanceCount = 0,
                    IndexInArchetype = -1,
                    IndexInArchetypeFreeSlotList = -1,
                    IndexInTemplateList = -1,
                };

                public ArchetypeIndex Type;
                public int InstanceCount;
                public int IndexInArchetype;
                public int IndexInArchetypeFreeSlotList;
                public int IndexInTemplateList;
                public BatchAllocation BatchAllocation;
                public BatchCullingAddresses BatchDomainAddresses;
            }

            private PerChunkData* m_PerChunkData;

            [BurstDiscard]
            internal static void Initialize()
            {
                if (StaticIdentifier.Ref.Data.m_PerChunkData == null)
                {
                    var data = AllocatorManager.Allocate<PerChunkData>(Allocator.Persistent, MaxPossibleChunkCount);
                    StaticIdentifier.Ref.Data.m_PerChunkData = data;

                    void Shutdown()
                    {
                        AllocatorManager.Free(Allocator.Persistent, StaticIdentifier.Ref.Data.m_PerChunkData);
                        StaticIdentifier.Ref.Data.m_PerChunkData = null;
                    }

                    AppDomain.CurrentDomain.DomainUnload += (_, _) => Shutdown();
                    AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
                }

                UnsafeUtility.MemClear(StaticIdentifier.Ref.Data.m_PerChunkData, sizeof(PerChunkData) * MaxPossibleChunkCount);
            }

            internal static void Init(ChunkIndex chunk)
            {
                if (StaticIdentifier.Ref.Data.m_PerChunkData != null)
                {
                    StaticIdentifier.Ref.Data.m_PerChunkData[chunk] = PerChunkData.Default;
                }
            }
        }

        #endregion

        #region Chunk State

        internal void MarkChunkTransformDirty(ChunkIndex chunk)
        {
            if (m_ChunkEnabled.Contains(chunk))
            {
                m_DirtyChunkTransforms.Add(chunk);
                m_PendingSpatialUpdates.Add(chunk);
                m_PendingTransformUpload.Add(chunk);
                UpdateContentVersion();
            }
        }

        private void RecomputeInstanceWorldBounds(ChunkIndex chunk, int indexInChunk, int count)
        {
            if (chunk == ChunkIndex.None || count <= 0)
                return;

            GraphicsMatrix* localToWorldPtr = GetInstanceLocalToWorldsRW(chunk, indexInChunk, count);
            AABB* worldAABBPtr = GetInstanceAABBsRW(chunk, indexInChunk, count);
            AABB localAABB = chunk.LocalAABB;

            for (int i = 0; i < count; i++)
                worldAABBPtr[i] = AABB.TransformAABB(localToWorldPtr[i], localAABB);
        }

        private void MarkChunkTemplateDataDirty(ChunkIndex chunk)
        {
            if (!m_ChunkEnabled.Contains(chunk))
                return;

            m_PendingSpatialUpdates.Add(chunk);
            if (chunk.Archetype.Key.Template.HasLightProbes)
                m_PendingTransformUpload.Add(chunk);

            UpdateContentVersion();
        }

        internal void RefreshChunkTemplateData(ChunkIndex chunk)
        {
            if (chunk == ChunkIndex.None || chunk.Count == 0)
                return;

            RecomputeInstanceWorldBounds(chunk, 0, chunk.Count);
            MarkChunkTemplateDataDirty(chunk);
        }

        #endregion

        #region Chunk Allocation

        private ChunkIndex AllocateChunk()
        {
            ChunkIndex chunk;
            if (m_ChunkFreeList.Length > 0)
            {
                chunk = m_ChunkFreeList.Pop();
            }
            else
            {
                chunk = new ChunkIndex(m_NextChunkIndex++);

                int requiredChunkCapacity = chunk + 1;
                int requiredInstanceCapacity = requiredChunkCapacity * ChunkCapacity;
                if (m_InstanceHandles.Length < requiredInstanceCapacity)
                {
                    int newInstanceCapacity = MathUtility.NextMultipleOf(requiredInstanceCapacity, InstanceGrowPageSize);
                    m_InstanceHandles.ResizeArraySafe(newInstanceCapacity);
                    m_InstanceLocalToWorld.ResizeArraySafe(newInstanceCapacity);
                    m_InstancePrevLocalToWorld.ResizeArraySafe(newInstanceCapacity);
                    m_InstanceAABBs.ResizeArraySafe(newInstanceCapacity);
                    m_InstanceLocations.ResizeArraySafe(newInstanceCapacity);
                    m_InstanceInCullingChunks.ResizeArraySafe(newInstanceCapacity);
                    m_InstanceRandomIDs.ResizeArraySafe(newInstanceCapacity);
                    m_InstanceVariationColors.ResizeArraySafe(newInstanceCapacity);
                    m_InstanceLightmapSTs.ResizeArraySafe(newInstanceCapacity);
                    m_InstanceMovedThisFrame.ResizeArraySafe(newInstanceCapacity);
                    m_InstanceMovedLastFrame.ResizeArraySafe(newInstanceCapacity);
                    m_InstanceFlippedWinding.ResizeArraySafe(newInstanceCapacity);
#if UNITY_EDITOR
                    m_InstanceEditorHidden.Resize(newInstanceCapacity);
                    m_InstanceEditorSelected.Resize(newInstanceCapacity);
#endif
                }
            }

            Assert.IsTrue(chunk > 0 && chunk < MaxPossibleChunkCount);
            ChunkStore.Init(chunk);
            m_ChunkAllocated.Add(chunk);

            return chunk;
        }

        private ChunkIndex AllocateChunkForArchetype(ArchetypeIndex archetype)
        {
            var newChunk = AllocateChunk();
            var template = archetype.Key.Template;

            AddChunkToArchetype(archetype, newChunk);
            AddChunkToArchetypeFreeSlotList(archetype, newChunk);
            m_TemplateManager.ValueRW.AddChunk(template, newChunk);

            if (archetype.Enabled)
            {
                m_ChunkEnabled.Add(newChunk);
                newChunk.BatchAllocation = m_InstanceBuffer.ValueRW.Allocate(template.BatchDomainIndex, ChunkCapacity);

                if (template.HasLightProbes)
                    m_ChunkHasProbes.Add(newChunk);
                if (template.HasRandomID)
                    m_ChunkHasRandomValue.Add(newChunk);
                if (template.HasVariationColor)
                    m_ChunkHasColorVariation.Add(newChunk);
                if (template.HasLightmaps)
                    m_ChunkHasLightmapST.Add(newChunk);

                if (template.HasMotionVectors)
                    m_ChunkDynamic.Add(newChunk);
                else
                    m_ChunkStatic.Add(newChunk);
            }

            return newChunk;
        }

        private ChunkIndex GetChunkWithFreeSpace(ArchetypeIndex archetype)
        {
            if (!TryGetArchetypeChunkWithFreeSlots(archetype, out ChunkIndex chunk))
                chunk = AllocateChunkForArchetype(archetype);

            Assert.IsTrue(chunk != ChunkIndex.None);

            return chunk;
        }

        private void ReleaseChunk(ArchetypeIndex archetype, ChunkIndex chunk)
        {
            m_InstanceBuffer.ValueRW.Free(chunk.BatchAllocation);
            m_TemplateManager.ValueRW.RemoveChunk(archetype.Key.Template, chunk);

            m_ChunkEnabled.Remove(chunk);
            m_ChunkStatic.Remove(chunk);
            m_ChunkDynamic.Remove(chunk);

            m_DirtyChunkTransforms.Remove(chunk);
            m_PendingSpatialUpdates.Remove(chunk);
            m_PendingInstanceUpload.Remove(chunk);
            m_PendingTransformUpload.Remove(chunk);
            m_PendingVariationColorUpload.Remove(chunk);
            m_PendingLightmapSTUpload.Remove(chunk);

            m_ChunkHasProbes.Remove(chunk);
            m_ChunkHasRandomValue.Remove(chunk);
            m_ChunkHasLightmapST.Remove(chunk);

            // Remove from archetype lists
            if (chunk.Count < ChunkCapacity)
                RemoveChunkFromArchetypeFreeSlotList(archetype, chunk);
            RemoveChunkFromArchetype(archetype, chunk);

            // Return chunk to the free list
            chunk.Count = 0;
            m_ChunkAllocated.Remove(chunk);
            m_ChunkFreeList.Add(chunk);
        }

        #endregion

        #region Chunk GPU Management

        internal void TemplateBufferTypeChanged(ChunkIndex chunk, TemplateIndex template, bool updateBatchAllocation = false)
        {
            if (chunk == ChunkIndex.None || template == TemplateIndex.None)
                return;

            if (updateBatchAllocation && m_ChunkEnabled.Contains(chunk))
            {
                BatchAllocation oldBatchAllocation = chunk.BatchAllocation;
                if (oldBatchAllocation.IsValid() && oldBatchAllocation.Domain != template.BatchDomainIndex)
                {
                    BatchAllocation newBatchAllocation = m_InstanceBuffer.ValueRW.Allocate(template.BatchDomainIndex, oldBatchAllocation.Length);
                    chunk.BatchAllocation = newBatchAllocation;
                    m_InstanceBuffer.ValueRW.Free(oldBatchAllocation);
                    m_PendingInstanceUpload.Add(chunk);
                    m_PendingTransformUpload.Add(chunk);
                }
            }

            m_ChunkHasProbes.Remove(chunk);
            m_ChunkHasRandomValue.Remove(chunk);
            m_ChunkHasColorVariation.Remove(chunk);
            m_ChunkHasLightmapST.Remove(chunk);
            m_ChunkStatic.Remove(chunk);
            m_ChunkDynamic.Remove(chunk);

            if (template.HasLightProbes)
            {
                m_ChunkHasProbes.Add(chunk);
                // Probe uploads are driven by transform dirtiness and probe-capability enablement.
                // A chunk that newly gains probe support needs an upload even if its batch allocation did not change.
                m_PendingTransformUpload.Add(chunk);
            }
            else
            {
                m_PendingTransformUpload.Remove(chunk);
            }

            if (template.HasRandomID)
            {
                m_ChunkHasRandomValue.Add(chunk);
                m_PendingInstanceUpload.Add(chunk);
            }

            if (template.HasVariationColor)
            {
                m_ChunkHasColorVariation.Add(chunk);
                m_PendingVariationColorUpload.Add(chunk);
            }

            if (template.HasLightmaps)
            {
                m_ChunkHasLightmapST.Add(chunk);
                m_PendingLightmapSTUpload.Add(chunk);
            }

            if (template.HasMotionVectors)
                m_ChunkDynamic.Add(chunk);
            else
                m_ChunkStatic.Add(chunk);

            UpdateContentVersion();
        }

        #endregion

        #region Chunk Instance Access

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckChunkIndexCount(ChunkIndex chunk, int indexInChunk, int count)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Hint.Unlikely(chunk == ChunkIndex.None))
                throw new ArgumentException("Chunk is ChunkIndex.None.");
            if (Hint.Unlikely(indexInChunk < 0 || indexInChunk + count > chunk.Count))
                throw new ArgumentException("Index out of range in chunk.");
            if (Hint.Unlikely(chunk.AsInstanceOffset() + indexInChunk + count > m_InstanceHandles.Length))
                throw new ArgumentException("Index out of range in instances array.");
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NoAlias] internal FloraInstanceHandle* GetInstanceHandlesRW(ChunkIndex chunk, int indexInChunk, int count)
        {
            CheckChunkIndexCount(chunk, indexInChunk, count);
            return m_InstanceHandles.GetUnsafePtrT() + chunk.AsInstanceOffset() + indexInChunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NoAlias] internal GraphicsMatrix* GetInstanceLocalToWorldsRW(ChunkIndex chunk, int indexInChunk, int count)
        {
            CheckChunkIndexCount(chunk, indexInChunk, count);
            return m_InstanceLocalToWorld.GetUnsafePtrT() + chunk.AsInstanceOffset() + indexInChunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NoAlias] internal GraphicsMatrix* GetInstancePrevLocalToWorldsRW(ChunkIndex chunk, int indexInChunk, int count)
        {
            CheckChunkIndexCount(chunk, indexInChunk, count);
            return m_InstancePrevLocalToWorld.GetUnsafePtrT() + chunk.AsInstanceOffset() + indexInChunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NoAlias] internal AABB* GetInstanceAABBsRW(ChunkIndex chunk, int indexInChunk, int count)
        {
            CheckChunkIndexCount(chunk, indexInChunk, count);
            return m_InstanceAABBs.GetUnsafePtrT() + chunk.AsInstanceOffset() + indexInChunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NoAlias] internal CellLocation* GetInstanceCellLocationsRW(ChunkIndex chunk, int indexInChunk, int count)
        {
            CheckChunkIndexCount(chunk, indexInChunk, count);
            return m_InstanceLocations.GetUnsafePtrT() + chunk.AsInstanceOffset() + indexInChunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NoAlias] internal InstanceInCullingChunk* GetInstanceInCullingChunksRW(ChunkIndex chunk, int indexInChunk, int count)
        {
            CheckChunkIndexCount(chunk, indexInChunk, count);
            return m_InstanceInCullingChunks.GetUnsafePtrT() + chunk.AsInstanceOffset() + indexInChunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NoAlias] internal float* GetInstanceRandomIDsRW(ChunkIndex chunk, int indexInChunk, int count)
        {
            CheckChunkIndexCount(chunk, indexInChunk, count);
            return m_InstanceRandomIDs.GetUnsafePtrT() + chunk.AsInstanceOffset() + indexInChunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NoAlias] internal float4* GetInstanceVariationColorsRW(ChunkIndex chunk, int indexInChunk, int count)
        {
            CheckChunkIndexCount(chunk, indexInChunk, count);
            return m_InstanceVariationColors.GetUnsafePtrT() + chunk.AsInstanceOffset() + indexInChunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NoAlias] internal float4* GetInstanceLightmapSTsRW(ChunkIndex chunk, int indexInChunk, int count)
        {
            CheckChunkIndexCount(chunk, indexInChunk, count);
            return m_InstanceLightmapSTs.GetUnsafePtrT() + chunk.AsInstanceOffset() + indexInChunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NoAlias] internal byte* GetInstanceMovedThisFrame(ChunkIndex chunk, int indexInChunk, int count)
        {
            CheckChunkIndexCount(chunk, indexInChunk, count);
            return m_InstanceMovedThisFrame.GetUnsafePtrT() + chunk.AsInstanceOffset() + indexInChunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NoAlias] internal byte* GetInstanceMovedLastFrame(ChunkIndex chunk, int indexInChunk, int count)
        {
            CheckChunkIndexCount(chunk, indexInChunk, count);
            return m_InstanceMovedLastFrame.GetUnsafePtrT() + chunk.AsInstanceOffset() + indexInChunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NoAlias] internal byte* GetInstanceFlippedWinding(ChunkIndex chunk, int indexInChunk, int count)
        {
            CheckChunkIndexCount(chunk, indexInChunk, count);
            return m_InstanceFlippedWinding.GetUnsafePtrT() + chunk.AsInstanceOffset() + indexInChunk;
        }

#if UNITY_EDITOR
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private UnsafeBitArray GetInstanceSelected(ChunkIndex chunk) =>
            new UnsafeBitArray(m_InstanceEditorSelected.GetUnsafePtrUnchecked() + chunk, sizeof(ulong));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private UnsafeBitArray GetInstanceSceneViewHidden(ChunkIndex chunk) =>
            new UnsafeBitArray(m_InstanceEditorHidden.GetUnsafePtrUnchecked() + chunk, sizeof(ulong));
#endif

        #endregion

        #region Chunk Management

        private struct InstanceBatchInChunk
        {
            public ChunkIndex Chunk;
            public int StartIndex;
            public int Count;
        }

        private void SetChunkCount(ArchetypeIndex archetype, ChunkIndex chunk, int newCount)
        {
            Assert.AreNotEqual(newCount, chunk.Count);
            Assert.IsTrue(newCount is >= 0 and <= ChunkCapacity);

            if (newCount == 0)
            {
                // Release to empty chunk pool
                ReleaseChunk(archetype, chunk);
            }
            else
            {
                if (newCount == ChunkCapacity)
                {
                    // No longer has empty slots, it shouldn't be in the empty slot list
                    RemoveChunkFromArchetypeFreeSlotList(archetype, chunk);
                }
                else if (chunk.Count == ChunkCapacity)
                {
                    Assert.IsTrue(newCount < chunk.Count);
                    AddChunkToArchetypeFreeSlotList(archetype, chunk);
                }

                if (m_ChunkEnabled.Contains(chunk))
                {
                    UpdateContentVersion();
                    m_PendingInstanceUpload.Add(chunk);
                    m_PendingTransformUpload.Add(chunk);
                    if (m_ChunkHasColorVariation.Contains(chunk))
                        m_PendingVariationColorUpload.Add(chunk);
                    if (m_ChunkHasLightmapST.Contains(chunk))
                        m_PendingLightmapSTUpload.Add(chunk);
                }

                chunk.Count = newCount;
            }
        }

        private int AllocateSpaceIntoChunk(ArchetypeIndex archetype, ChunkIndex chunk, int count, out int outIndex)
        {
            Assert.IsTrue(chunk != ChunkIndex.None);
            outIndex = chunk.Count;
            var allocatedCount = math.min(ChunkCapacity - outIndex, count);
            SetChunkCount(archetype, chunk, outIndex + allocatedCount);
            archetype.InstanceCount += allocatedCount;
            return allocatedCount;
        }

        #endregion

        #region Copying

        private void CopyInstances(ChunkIndex srcChunk, int srcIndexInChunk, ChunkIndex dstChunk, int dstIndexInChunk, [AssumeRange(0, 64)] int count)
        {
            UpdateContentVersion();

            var srcInstanceHandles = GetInstanceHandlesRW(srcChunk, srcIndexInChunk, count);
            var dstInstanceHandles = GetInstanceHandlesRW(dstChunk, dstIndexInChunk, count);
            UnsafeUtility.MemCpy(dstInstanceHandles, srcInstanceHandles, count * sizeof(FloraInstanceHandle));

            var srcLocalToWorld = GetInstanceLocalToWorldsRW(srcChunk, srcIndexInChunk, count);
            var dstLocalToWorld = GetInstanceLocalToWorldsRW(dstChunk, dstIndexInChunk, count);
            UnsafeUtility.MemCpy(dstLocalToWorld, srcLocalToWorld, count * sizeof(GraphicsMatrix));

            var srcPrevLocalToWorld = GetInstancePrevLocalToWorldsRW(srcChunk, srcIndexInChunk, count);
            var dstPrevLocalToWorld = GetInstancePrevLocalToWorldsRW(dstChunk, dstIndexInChunk, count);
            UnsafeUtility.MemCpy(dstPrevLocalToWorld, srcPrevLocalToWorld, count * sizeof(GraphicsMatrix));

            var srcAABBs = GetInstanceAABBsRW(srcChunk, srcIndexInChunk, count);
            var dstAABBs = GetInstanceAABBsRW(dstChunk, dstIndexInChunk, count);
            UnsafeUtility.MemCpy(dstAABBs, srcAABBs, count * sizeof(AABB));

            var srcCellLocations = GetInstanceCellLocationsRW(srcChunk, srcIndexInChunk, count);
            var dstCellLocations = GetInstanceCellLocationsRW(dstChunk, dstIndexInChunk, count);
            UnsafeUtility.MemCpy(dstCellLocations, srcCellLocations, count * sizeof(CellLocation));

            if (m_ChunkHasRandomValue.Contains(dstChunk) && m_ChunkHasRandomValue.Contains(srcChunk))
            {
                var srcRandomIDs = GetInstanceRandomIDsRW(srcChunk, srcIndexInChunk, count);
                var dstRandomIDs = GetInstanceRandomIDsRW(dstChunk, dstIndexInChunk, count);
                UnsafeUtility.MemCpy(dstRandomIDs, srcRandomIDs, count * sizeof(float));
            }

            if (m_ChunkHasColorVariation.Contains(dstChunk) && m_ChunkHasColorVariation.Contains(srcChunk))
            {
                var srcVariationColors = GetInstanceVariationColorsRW(srcChunk, srcIndexInChunk, count);
                var dstVariationColors = GetInstanceVariationColorsRW(dstChunk, dstIndexInChunk, count);
                UnsafeUtility.MemCpy(dstVariationColors, srcVariationColors, count * sizeof(float4));
            }

            if (m_ChunkHasLightmapST.Contains(dstChunk) && m_ChunkHasLightmapST.Contains(srcChunk))
            {
                var srcLightmapSTs = GetInstanceLightmapSTsRW(srcChunk, srcIndexInChunk, count);
                var dstLightmapSTs = GetInstanceLightmapSTsRW(dstChunk, dstIndexInChunk, count);
                UnsafeUtility.MemCpy(dstLightmapSTs, srcLightmapSTs, count * sizeof(float4));
            }

            CopyInstanceFlags(srcChunk, srcIndexInChunk, dstChunk, dstIndexInChunk, count);
        }

        private void CopyInstanceFlags(ChunkIndex srcChunk, int srcIndexInChunk, ChunkIndex dstChunk, int dstIndexInChunk, int count)
        {
            var srcMovedThisFrame = GetInstanceMovedThisFrame(srcChunk, srcIndexInChunk, count);
            var dstMovedThisFrame = GetInstanceMovedThisFrame(dstChunk, dstIndexInChunk, count);
            UnsafeUtility.MemCpy(dstMovedThisFrame, srcMovedThisFrame, count);

            var srcMovedLastFrame = GetInstanceMovedLastFrame(srcChunk, srcIndexInChunk, count);
            var dstMovedLastFrame = GetInstanceMovedLastFrame(dstChunk, dstIndexInChunk, count);
            UnsafeUtility.MemCpy(dstMovedLastFrame, srcMovedLastFrame, count);

            var srcFlippedWinding = GetInstanceFlippedWinding(srcChunk, srcIndexInChunk, count);
            var dstFlippedWinding = GetInstanceFlippedWinding(dstChunk, dstIndexInChunk, count);
            UnsafeUtility.MemCpy(dstFlippedWinding, srcFlippedWinding, count);

#if UNITY_EDITOR
            var srcSelected = GetInstanceSelected(srcChunk);
            var dstSelected = GetInstanceSelected(dstChunk);
            dstSelected.Copy(dstIndexInChunk, ref srcSelected, srcIndexInChunk, count);

            var srcHidden = GetInstanceSceneViewHidden(srcChunk);
            var dstHidden = GetInstanceSceneViewHidden(dstChunk);
            dstHidden.Copy(dstIndexInChunk, ref srcHidden, srcIndexInChunk, count);
#endif
        }

        private void Clone(in InstanceBatchInChunk srcBatch, ArchetypeIndex dstArchetype, ChunkIndex dstChunk)
        {
            var srcChunk = srcBatch.Chunk;
            var srcChunkIndex = srcBatch.StartIndex;
            var srcCount = srcBatch.Count;
            var dstCount = AllocateSpaceIntoChunk(dstArchetype, dstChunk, srcCount, out var dstChunkIndex);
            Assert.IsTrue(dstCount == srcCount);

            CopyInstances(srcChunk, srcChunkIndex, dstChunk, dstChunkIndex, dstCount);
            if (dstArchetype.Enabled)
                m_CullingGrid.ValueRW.AddInstances(dstArchetype, dstChunk.AsInstanceOffset() + dstChunkIndex, dstCount);

            // Propagate change states
            m_DirtyChunkTransforms.UnionAt(srcChunk, dstChunk);
            m_PendingSpatialUpdates.UnionAt(srcChunk, dstChunk);

            var dstInstances = GetInstanceHandlesRW(dstChunk, dstChunkIndex, dstCount);
            for (int i = 0; i < dstCount; i++)
            {
                var instance = dstInstances[i];
                InstanceRegistry.Data.SetInstanceInChunk(instance, new InstanceInChunk { Chunk = dstChunk, IndexInChunk = dstChunkIndex + i });
            }
        }

        #endregion

        #region Moving

        private void Move(InstanceBatchInChunk srcBatch, ArchetypeIndex dstArchetype)
        {
            var srcChunk = srcBatch.Chunk;
            var srcRemainingCount = srcBatch.Count;
            var startIndex = srcBatch.StartIndex;

            while (srcRemainingCount > 0)
            {
                var dstChunk = GetChunkWithFreeSpace(dstArchetype);
                var dstCount = Move(new InstanceBatchInChunk { Chunk = srcChunk, Count = srcRemainingCount, StartIndex = startIndex }, dstChunk);
                srcRemainingCount -= dstCount;
            }
        }

        private int Move(InstanceBatchInChunk srcBatch, ChunkIndex dstChunk)
        {
            var srcChunk = srcBatch.Chunk;
            var srcArchetype = srcChunk.Archetype;
            var dstArchetype = dstChunk.Archetype;
            var dstUnusedCount = ChunkCapacity - dstChunk.Count;
            var srcCount = math.min(dstUnusedCount, srcBatch.Count);
            var srcStartIndex = srcBatch.StartIndex + srcBatch.Count - srcCount;
            var srcInstances = GetInstanceHandlesRW(srcChunk, srcStartIndex, srcCount);

            var partialSrcBatch = new InstanceBatchInChunk
            {
                Chunk = srcChunk,
                StartIndex = srcStartIndex,
                Count = srcCount
            };

            for (int i = 0; i < srcCount; i++)
                MarkTrackedContainerChunkCacheDirty(srcInstances[i]);

            Clone(partialSrcBatch, dstArchetype, dstChunk);
            RemoveInstancesInChunk(srcArchetype, partialSrcBatch);

            return srcCount;
        }

        private void Move(FloraInstanceHandle instance, ChunkIndex dstChunk)
        {
            var srcInstanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
            var srcChunk = srcInstanceInChunk.Chunk;
            var srcChunkIndex = srcInstanceInChunk.IndexInChunk;
            var instanceBatch = new InstanceBatchInChunk { Chunk = srcChunk, Count = 1, StartIndex = srcChunkIndex };
            Move(instanceBatch, dstChunk);
        }

        private void Move(FloraInstanceHandle instance, ArchetypeIndex dstArchetype)
        {
            var dstChunk = GetChunkWithFreeSpace(dstArchetype);
            Move(instance, dstChunk);
        }

        #endregion

        #region Instance Management

        private static readonly ProfilerMarker AllocateInstancesMarker = new ProfilerMarker("Flora.AllocateInstances");
        private static readonly ProfilerMarker DestroyInstancesMarker = new ProfilerMarker("Flora.DestroyInstances");

        private void RemoveInstancesInChunk(ArchetypeIndex archetype, in InstanceBatchInChunk batchInChunk)
        {
            var chunk = batchInChunk.Chunk;
            var batchCount = batchInChunk.Count;
            var indexInChunk = batchInChunk.StartIndex;
            var instanceOffset = chunk.AsInstanceOffset();

            // Remove from culling grid
            m_CullingGrid.ValueRW.RemoveInstances(instanceOffset + indexInChunk, batchCount);

            // Fill in moved data from the end
            int patchCount = math.min(batchCount, chunk.Count - indexInChunk - batchCount);
            if (patchCount > 0)
            {
                var copyFromIndex = chunk.Count - patchCount;
                var handlesToMove = GetInstanceHandlesRW(chunk, copyFromIndex, patchCount);
                for (int i = 0; i < patchCount; i++)
                {
                    var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(handlesToMove[i]);
                    instanceInChunk.IndexInChunk = indexInChunk + i;
                    InstanceRegistry.Data.SetInstanceInChunk(handlesToMove[i], instanceInChunk);
                }

                CopyInstances(chunk, copyFromIndex, chunk, indexInChunk, patchCount);

                // Update culling grid with remapped indices
                m_CullingGrid.ValueRW.RemapInstanceIndices(instanceOffset, copyFromIndex, indexInChunk, patchCount);
            }

            archetype.InstanceCount -= batchCount;
            int newChunkInstanceCount = chunk.Count - batchCount;
            SetChunkCount(archetype, chunk, newChunkInstanceCount);
        }

        private void AllocateInstances(ChunkIndex chunk, int baseIndex, int count, FloraInstanceHandle* outputEntities)
        {
            var instancesStart = GetInstanceHandlesRW(chunk, baseIndex, count);

            InstanceRegistry.Data.AllocateInstances(instancesStart, count, chunk, baseIndex);

            if (outputEntities != null)
            {
                UnsafeUtility.MemCpy(outputEntities, instancesStart, count * sizeof(FloraInstanceHandle));
            }
        }

        private void AllocateInstances(
            FloraInstanceHandle* instances,
            FloraLocalToWorld* localToWorlds,
            int remaining,
            in InstantiateParams parameters)
        {
            using var _ = AllocateInstancesMarker.Auto();

            SyncJobsForMainThread();

            var template = parameters.Template;
            var archetype = FindOrCreateArchetype(parameters);

            Assert.IsTrue(template != TemplateIndex.None, "Failed to find template during instance allocation.");
            Assert.IsTrue(archetype != ArchetypeIndex.None, "Failed to find or create archetype during instance allocation.");

            var initialVariationColor = template.InitialVariationColor;
            var prefabAABB = parameters.Template.LocalAABB;

            ref var cullingGrid = ref m_CullingGrid.ValueRW;
            ref var templateManager = ref m_TemplateManager.ValueRW;

            while (remaining != 0)
            {
                var chunk = GetChunkWithFreeSpace(archetype);
                var count = AllocateSpaceIntoChunk(archetype, chunk, remaining, out var indexInChunk);
                AllocateInstances(chunk, indexInChunk, count, instances);

                var instanceOffset = chunk * ChunkCapacity + indexInChunk;
                for (int i = 0; i < count; i++)
                {
                    var localToWorld = localToWorlds[i];
                    var instanceAABB = AABB.TransformAABB(localToWorld, prefabAABB);
                    var instanceIndex = instanceOffset + i;
                    var instance = instances[i];

                    m_InstanceLocalToWorld[instanceIndex] = localToWorld;
                    m_InstancePrevLocalToWorld[instanceIndex] = localToWorld;
                    m_InstanceAABBs[instanceIndex] = instanceAABB;
                    m_InstanceLocations[instanceIndex] = CellLocation.None;
                    m_InstanceInCullingChunks[instanceIndex] = InstanceInCullingChunk.None;
                    m_InstanceRandomIDs[instanceIndex] = Random.CreateFromIndex((uint)instance.Index).NextFloat();
                    m_InstanceVariationColors[instanceIndex] = initialVariationColor;
                    m_InstanceLightmapSTs[instanceIndex] = parameters.LightmapST;
                    m_InstanceMovedThisFrame[instanceIndex] = 0;
                    m_InstanceMovedLastFrame[instanceIndex] = 0;
                    m_InstanceFlippedWinding[instanceIndex] = localToWorld.IsFlipped ? (byte)1 : (byte)0;

                    InstanceRegistry.Data.SetSceneEntityId(instance, parameters.SceneEntityId);
                    if ((parameters.AdditionalTags & InstanceTag.TerrainDetail) != 0)
                    {
                        InstanceRegistry.Data.SetDetailInTerrain(instance,
                            new DetailInTerrain { TerrainEntity = parameters.SceneEntityId, LayerIndex = parameters.TerrainDetailLayerIndex });
                    }
                }

#if UNITY_EDITOR
                m_InstanceEditorHidden.SetRange(instanceOffset, false, count);
                m_InstanceEditorSelected.SetRange(instanceOffset, false, count);
#endif

                cullingGrid.AddInstances(archetype, instanceOffset, count);
                templateManager.AddInstancesToSourceRecord(parameters.SourceRecord, instances, count);

                remaining -= count;
                instances += count;
                localToWorlds += count;
            }
        }

        private void DeallocateInstances(ArchetypeIndex archetype, ChunkIndex chunk, int indexInChunk, int batchCount)
        {
            var instancesToDeallocate = GetInstanceHandlesRW(chunk, indexInChunk, batchCount);
            m_TemplateManager.ValueRW.RemoveInstancesFromSourceRecords(instancesToDeallocate, batchCount);
            InstanceRegistry.Data.DeallocateInstances(instancesToDeallocate, batchCount);
        }

        private void DeallocateInstancesInChunk(ArchetypeIndex archetype, in InstanceBatchInChunk batch)
        {
            var chunk = batch.Chunk;
            var startIndex = batch.StartIndex;
            var count = batch.Count;

            DeallocateInstances(archetype, chunk, startIndex, count);
            RemoveInstancesInChunk(archetype, batch);
        }

        private void DestroyBatch(in InstanceBatchInChunk batch)
        {
            DeallocateInstancesInChunk(batch.Chunk.Archetype, batch);
        }

        private void DestroyInstances(FloraInstanceHandle* instances, int count)
        {
            using var _ = DestroyInstancesMarker.Auto();
            SyncJobsForMainThread();

            var instanceIndex = 0;

            while (instanceIndex != count)
            {
                var instanceBatchInChunk = GetFirstInstanceBatchInChunk(instances + instanceIndex, count - instanceIndex);
                var chunk = instanceBatchInChunk.Chunk;
                var batchCount = instanceBatchInChunk.Count;
                var indexInChunk = instanceBatchInChunk.StartIndex;

                if (chunk == ChunkIndex.None)
                {
                    instanceIndex += batchCount;
                    continue;
                }

                DestroyBatch(new InstanceBatchInChunk {Chunk = chunk, StartIndex = indexInChunk, Count = batchCount});

                instanceIndex += batchCount;
            }
        }

        public void DestroyAllInstancesInScene(Scene scene)
        {
            DestroyAllInstancesInSceneWithBurst(Self, ref scene);
        }

        [BurstCompile]
        private static void DestroyAllInstancesInSceneWithBurst(InstanceManager* im, ref Scene scene)
        {
            im->DestroyAllInstancesInSceneInternal(scene);
        }

        private void DestroyAllInstancesInSceneInternal(Scene scene)
        {
            SyncJobsForMainThread();

            var batchesToDestroy = new UnsafeList<InstanceBatchInChunk>(m_ChunkAllocated.Count(), Allocator.TempJob);
            foreach (var archetype in m_SceneHandleArchetypes.GetValuesForKey(GetSceneHandleRaw(scene)))
            {
                var chunks = m_ArchetypeChunks[archetype];
                for (int j = chunks.Length - 1; j >= 0; j--)
                {
                    var chunk = chunks[j];
                    batchesToDestroy.Add(new InstanceBatchInChunk { Chunk = chunk, StartIndex = 0, Count = chunk.Count });
                }
            }

            for (int i = batchesToDestroy.Length - 1; i >= 0; i--)
                DestroyBatch(batchesToDestroy[i]);

            batchesToDestroy.Dispose();
        }

        #endregion

        #region Chunk Batching

        /// <summary>
        /// Returns the first batch of instances that are contiguous in the same chunk
        /// Works best if the instances are already sorted by chunk and index
        /// </summary>
        private InstanceBatchInChunk GetFirstInstanceBatchInChunk(FloraInstanceHandle* instances, int count)
        {
            var instanceInChunk = Exists(instances[0]) ? InstanceRegistry.Data.GetInstanceInChunk(instances[0]) : default;
            var chunk = instanceInChunk.Chunk;
            var indexInChunk = instanceInChunk.IndexInChunk;

            int batchCount = 0;

            for (; batchCount < count; batchCount++)
            {
                var instance = instances[batchCount];
                instanceInChunk = Exists(instance) ? InstanceRegistry.Data.GetInstanceInChunk(instance) : default;
                if (instanceInChunk.Chunk != chunk || instanceInChunk.IndexInChunk != indexInChunk + batchCount)
                {
                    break;
                }
            }

            Assert.IsTrue(chunk == ChunkIndex.None || indexInChunk < chunk.Count);
            Assert.IsTrue(chunk == ChunkIndex.None || indexInChunk + batchCount <= chunk.Count);

            return new InstanceBatchInChunk
            {
                Chunk = chunk,
                Count = batchCount,
                StartIndex = indexInChunk
            };
        }

        [BurstCompile]
        private static void SortInstanceInChunk(InstanceInChunk* instanceInChunks, int count)
        {
            NativeSortExtension.Sort(instanceInChunks, count);
        }

        [BurstCompile]
        private static void GatherInstanceInChunkForInstances(
            FloraInstanceHandle* instances,
            InstanceInChunk* instanceChunkData,
            int instanceCount)
        {
            for (int index = 0; index < instanceCount; ++index)
                instanceChunkData[index] = InstanceRegistry.Data.GetInstanceInChunk(instances[index]);
        }

        /// <summary>
        /// Creates a list of batches of instances that are contiguous in the same chunk
        /// </summary>
        [BurstCompile]
        private static void InstanceBatchFromInstanceChunkData(
            in InstanceInChunk* chunkData, int chunkCount,
            InstanceBatchInChunk* instanceBatchList, int* currentBatchIndex,
            int* foundError)
        {
            *foundError = 0;

            var instanceIndex = 0;
            var instanceBatch = new InstanceBatchInChunk
            {
                Chunk = chunkData[instanceIndex].Chunk,
                StartIndex = chunkData[instanceIndex].IndexInChunk,
                Count = 1
            };
            instanceIndex++;

            while (instanceIndex < chunkCount)
            {
                // Skip this instance if it's a duplicate. Checking previous instanceIndex is sufficient since arrays are sorted.
                if (chunkData[instanceIndex].Equals(chunkData[instanceIndex - 1]))
                {
                    instanceIndex++;
                    continue;
                }

                var chunk = chunkData[instanceIndex].Chunk;
                if (chunk == ChunkIndex.None)
                {
                    instanceIndex++;
                    continue;
                }

                var indexInChunk = chunkData[instanceIndex].IndexInChunk;
                var chunkBreak = (chunk != instanceBatch.Chunk);
                var indexBreak = (indexInChunk != (instanceBatch.StartIndex + instanceBatch.Count));
                var runBreak = chunkBreak || indexBreak;
                if (runBreak && instanceBatch.Chunk != ChunkIndex.None)
                {
                    instanceBatchList[*currentBatchIndex] = instanceBatch;
                    (*currentBatchIndex) += 1;

                    //just to make sure we do not overflow our instanceBatchList buffer
                    if (*currentBatchIndex > chunkCount)
                    {
                        *foundError = 1;
                        return;
                    }

                    instanceBatch = new InstanceBatchInChunk
                    {
                        Chunk = chunk,
                        StartIndex = indexInChunk,
                        Count = 1
                    };
                }
                else
                {
                    instanceBatch = new InstanceBatchInChunk
                    {
                        Chunk = instanceBatch.Chunk,
                        StartIndex = instanceBatch.StartIndex,
                        Count = instanceBatch.Count + 1
                    };
                }

                instanceIndex++;
            }

            if (instanceBatch.Chunk == ChunkIndex.None)
                return;

            instanceBatchList[*currentBatchIndex] = instanceBatch;
            (*currentBatchIndex) += 1;

            // Just to make sure we do not overflow our instanceBatchList buffer
            if (*currentBatchIndex > chunkCount)
            {
                *foundError = 1;
            }
        }

        private bool CreateInstanceBatchList(FloraInstanceHandle* instances, int count, AllocatorManager.AllocatorHandle allocator, out NativeList<InstanceBatchInChunk> instanceBatchList)
        {
            if (count == 0)
            {
                instanceBatchList = default;
                return false;
            }

            var instanceChunkData = new NativeArray<InstanceInChunk>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            GatherInstanceInChunkForInstances(instances, (InstanceInChunk*)instanceChunkData.GetUnsafePtr(), count);
            SortInstanceInChunk((InstanceInChunk*)instanceChunkData.GetUnsafePtr(), instanceChunkData.Length);

            instanceBatchList = new NativeList<InstanceBatchInChunk>(instanceChunkData.Length, allocator);
            instanceBatchList.Length = instanceChunkData.Length;

            int foundError = 0;
            int finalBatchSize = 0;

            InstanceBatchFromInstanceChunkData((InstanceInChunk*)instanceChunkData.GetUnsafePtr(), instanceChunkData.Length, instanceBatchList.GetUnsafePtr(), &finalBatchSize, &foundError);
            instanceBatchList.Length = finalBatchSize;

            instanceChunkData.Dispose();
            if (foundError != 0)
            {
                instanceBatchList.Dispose();
                instanceBatchList = default;
                return false;
            }

            return true;
        }

        #endregion

        #region Archetype Change

        internal void MoveInstancesToNewArchetype(FloraInstanceHandle* instances, int count, ArchetypeKey dstArchetypeKey)
        {
            SyncJobsForMainThread();
            ArchetypeIndex dstArchetype = FindOrCreateArchetype(dstArchetypeKey);
            MoveInstancesToNewArchetype(instances, count, dstArchetype);
        }

        internal void MoveInstancesToNewArchetype(FloraInstanceHandle* instances, int count, ArchetypeIndex dstArchetype)
        {
            SyncJobsForMainThread();

            if (count >= BatchingMinSize && CreateInstanceBatchList(instances, count, Allocator.Temp, out var instanceBatchList))
            {
                MoveInstancesToNewArchetypeBatchWithBurst(Self, (UnsafeList<InstanceBatchInChunk>*)NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref instanceBatchList), dstArchetype);
                instanceBatchList.Dispose();
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var instance = instances[i];
                    if (!Exists(instance)) continue;
                    Move(instance, dstArchetype);
                }
            }
        }

        internal void MoveInstancesToNewTemplate(NativeArray<FloraInstanceHandle> instances, TemplateIndex template, int lightmapIndex, float4 lightmapST)
        {
            SyncJobsForMainThread();

            int resolvedLightmapIndex = template.HasLightmaps ? lightmapIndex : -1;
            float4 resolvedLightmapST = template.HasLightmaps ? lightmapST : new float4(1f, 1f, 0f, 0f);
            var archetypeLookup = new Dictionary<ArchetypeKey, ArchetypeIndex>();
            for (int i = 0; i < instances.Length; i++)
            {
                FloraInstanceHandle instance = instances[i];
                if (!Exists(instance))
                    continue;

                InstanceInChunk instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                if (instanceInChunk.Equals(InstanceInChunk.None))
                    continue;

                ArchetypeKey srcArchetypeKey = instanceInChunk.Chunk.Archetype.Key;
                ArchetypeKey dstArchetypeKey = srcArchetypeKey;
                dstArchetypeKey.Template = template;
                dstArchetypeKey.LightmapIndex = resolvedLightmapIndex;

                if (dstArchetypeKey.Equals(srcArchetypeKey))
                    continue;

                if (!archetypeLookup.TryGetValue(dstArchetypeKey, out ArchetypeIndex dstArchetype))
                {
                    dstArchetype = FindOrCreateArchetype(dstArchetypeKey);
                    archetypeLookup.Add(dstArchetypeKey, dstArchetype);
                }

                Move(instance, dstArchetype);

                InstanceInChunk dstInstanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                if (dstInstanceInChunk.Equals(InstanceInChunk.None))
                    continue;

                RecomputeInstanceWorldBounds(dstInstanceInChunk.Chunk, dstInstanceInChunk.IndexInChunk, 1);
                MarkChunkTemplateDataDirty(dstInstanceInChunk.Chunk);

                if (!template.HasLightmaps)
                    continue;

                GetInstanceLightmapSTsRW(dstInstanceInChunk.Chunk, dstInstanceInChunk.IndexInChunk, 1)[0] = resolvedLightmapST;
                m_PendingLightmapSTUpload.Add(dstInstanceInChunk.Chunk);
            }
        }

        internal void UpdateInstancesLightmapData(NativeArray<FloraInstanceHandle> instances, int lightmapIndex, float4 lightmapST)
        {
            SyncJobsForMainThread();

            var archetypeLookup = new Dictionary<ArchetypeKey, ArchetypeIndex>();
            for (int i = 0; i < instances.Length; i++)
            {
                FloraInstanceHandle instance = instances[i];
                if (!Exists(instance))
                    continue;

                InstanceInChunk instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                if (instanceInChunk.Equals(InstanceInChunk.None))
                    continue;

                ArchetypeKey srcArchetypeKey = instanceInChunk.Chunk.Archetype.Key;
                int resolvedLightmapIndex = srcArchetypeKey.Template.HasLightmaps ? lightmapIndex : -1;
                if (srcArchetypeKey.LightmapIndex != resolvedLightmapIndex)
                {
                    ArchetypeKey dstArchetypeKey = srcArchetypeKey;
                    dstArchetypeKey.LightmapIndex = resolvedLightmapIndex;

                    if (!archetypeLookup.TryGetValue(dstArchetypeKey, out ArchetypeIndex dstArchetype))
                    {
                        dstArchetype = FindOrCreateArchetype(dstArchetypeKey);
                        archetypeLookup.Add(dstArchetypeKey, dstArchetype);
                    }

                    Move(instance, dstArchetype);
                    instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                    if (instanceInChunk.Equals(InstanceInChunk.None))
                        continue;
                }

                if (!instanceInChunk.Chunk.Archetype.Key.Template.HasLightmaps)
                    continue;

                GetInstanceLightmapSTsRW(instanceInChunk.Chunk, instanceInChunk.IndexInChunk, 1)[0] = lightmapST;
                m_PendingLightmapSTUpload.Add(instanceInChunk.Chunk);
            }
        }

        [BurstCompile]
        private static void MoveInstancesToNewArchetypeBatchWithBurst(InstanceManager* data, UnsafeList<InstanceBatchInChunk>* sortedInstanceBatchList, in ArchetypeIndex dstArchetype)
        {
            data->MoveInstancesToNewArchetypeBatch(sortedInstanceBatchList, dstArchetype);
        }

        private void MoveInstancesToNewArchetypeBatch(UnsafeList<InstanceBatchInChunk>* sortedInstanceBatchList, ArchetypeIndex dstArchetype)
        {
            for (int i = sortedInstanceBatchList->Length - 1; i >= 0; i--)
            {
                var srcBatch = sortedInstanceBatchList->Ptr[i];
                var srcType = srcBatch.Chunk.Archetype;
                if (srcType != dstArchetype)
                    Move(sortedInstanceBatchList->Ptr[i], dstArchetype);
            }
        }

        #endregion

        #region Tag Management

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static InstanceTag NormalizeMutableTags(InstanceTag tags)
        {
            // Template-driven capabilities must not participate in archetype key churn.
            return tags & ~(InstanceTag.RandomID | InstanceTag.VariationColor);
        }

        internal NativeArray<ChunkIndex> GetChunksWithTags(InstanceTag tags, AllocatorManager.AllocatorHandle allocator, out int instanceCount)
        {
            tags = NormalizeMutableTags(tags);
            var chunksWithTag = new NativeBitSet(m_ChunkEnabled.MaxLength, Allocator.Temp);
            instanceCount = 0;

            foreach (var chunk in m_ChunkEnabled.AsType<ChunkIndex>())
            {
                if ((chunk.Archetype.Key.Tags & tags) != 0)
                {
                    chunksWithTag.Add(chunk);
                    instanceCount += chunk.Count;
                }
            }

            return chunksWithTag.ToArray<ChunkIndex>(allocator);
        }

        internal void AddTagsToInstances(NativeArray<FloraInstanceHandle> instances, InstanceTag tags)
        {
            tags = NormalizeMutableTags(tags);
            if (tags == InstanceTag.None)
                return;

            SyncJobsForMainThread();

            if (instances.Length >= BatchingMinSize && CreateInstanceBatchList(instances.GetUnsafePtrT(), instances.Length, Allocator.Temp, out var instanceBatchList))
            {
                AddTagsToInstancesBatchWithBurst(Self,
                    (UnsafeList<InstanceBatchInChunk>*)NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref instanceBatchList), tags);
            }
            else
            {
                AddTagsToInstancesWithBurst(Self,
                    (FloraInstanceHandle*)instances.GetUnsafeReadOnlyPtr(), instances.Length, tags);
            }
        }

        [BurstCompile]
        private static void AddTagsToInstancesBatchWithBurst(InstanceManager* data, UnsafeList<InstanceBatchInChunk>* sortedInstanceBatchList, InstanceTag tags)
        {
            data->AddTagsToInstancesBatch(sortedInstanceBatchList, tags);
        }

        private void AddTagsToInstancesBatch(UnsafeList<InstanceBatchInChunk>* sortedInstanceBatchList, InstanceTag tags)
        {
            for (int i = sortedInstanceBatchList->Length - 1; i >= 0; i--)
                AddTagsToBatch(sortedInstanceBatchList->Ptr[i], tags);
        }

        private void AddTagsToBatch(in InstanceBatchInChunk batch, InstanceTag tags)
        {
            if (batch.Chunk == ChunkIndex.None)
                return;

            var srcArchetypeKey = batch.Chunk.Archetype.Key;
            var tagsToAdd = (uint)(tags & ~srcArchetypeKey.Tags);
            if (tagsToAdd == 0)
                return; // All tags already present

            var dstArchetypeKey = srcArchetypeKey;
            dstArchetypeKey.Tags |= tags;
            var dstArchetype = FindOrCreateArchetype(dstArchetypeKey);
            Move(batch, dstArchetype);
        }

        [BurstCompile]
        private static void AddTagsToInstanceWithBurst(InstanceManager* data, FloraInstanceHandle* instance, InstanceTag tags)
        {
            data->AddTagsToInstance(*instance, tags);
        }

        [BurstCompile]
        private static void AddTagsToInstancesWithBurst(InstanceManager* data, FloraInstanceHandle* instances, int count, InstanceTag tags)
        {
            for (int i = 0; i < count; i++)
                data->AddTagsToInstance(instances[i], tags);
        }

        private void AddTagsToInstance(FloraInstanceHandle instance, InstanceTag tags)
        {
            tags = NormalizeMutableTags(tags);
            if (tags == InstanceTag.None)
                return;

            var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
            if (instanceInChunk.Chunk == ChunkIndex.None)
                return;

            var srcArchetypeKey = instanceInChunk.Chunk.Archetype.Key;
            var tagsToAdd = (uint)(tags & ~srcArchetypeKey.Tags);
            if (tagsToAdd == 0)
                return; // All tags already present

            var dstArchetypeKey = srcArchetypeKey;
            dstArchetypeKey.Tags |= tags;
            var dstArchetype = FindOrCreateArchetype(dstArchetypeKey);
            Move(instance, dstArchetype);
        }

        private void RemoveTagsFromInstances(NativeArray<FloraInstanceHandle> instances, InstanceTag tags)
        {
            tags = NormalizeMutableTags(tags);
            if (tags == InstanceTag.None)
                return;

            SyncJobsForMainThread();

            if (instances.Length >= BatchingMinSize && CreateInstanceBatchList(instances.GetUnsafePtrT(), instances.Length, Allocator.Temp, out var instanceBatchList))
            {
                RemoveTagsFromInstancesBatchWithBurst(Self,
                    (UnsafeList<InstanceBatchInChunk>*)NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref instanceBatchList), tags);
            }
            else
            {
                RemoveTagsFromInstancesWithBurst(Self,
                    (FloraInstanceHandle*)instances.GetUnsafeReadOnlyPtr(), instances.Length, tags);
            }
        }

        [BurstCompile]
        private static void RemoveTagsFromInstancesBatchWithBurst(InstanceManager* data, UnsafeList<InstanceBatchInChunk>* sortedInstanceBatchList, InstanceTag tags)
        {
            data->RemoveTagsFromInstancesBatch(sortedInstanceBatchList, tags);
        }

        private void RemoveTagsFromInstancesBatch(UnsafeList<InstanceBatchInChunk>* sortedInstanceBatchList, InstanceTag tags)
        {
            for (int i = sortedInstanceBatchList->Length - 1; i >= 0; i--)
                RemoveTagsFromBatch(sortedInstanceBatchList->Ptr[i], tags);
        }

        private void RemoveTagsFromBatch(in InstanceBatchInChunk batch, InstanceTag tags)
        {
            if (batch.Chunk == ChunkIndex.None)
                return;

            var srcArchetypeKey = batch.Chunk.Archetype.Key;
            var tagsToRemove = (uint)(tags & srcArchetypeKey.Tags);
            if (tagsToRemove == 0)
                return; // No tags to remove

            var dstArchetypeKey = srcArchetypeKey;
            dstArchetypeKey.Tags &= ~tags;
            var dstArchetype = FindOrCreateArchetype(dstArchetypeKey);
            Move(batch, dstArchetype);
        }

        [BurstCompile]
        private static void RemoveTagsFromInstanceWithBurst(InstanceManager* data, FloraInstanceHandle* instance, InstanceTag tags)
        {
            data->RemoveTagsFromInstance(*instance, tags);
        }

        [BurstCompile]
        private static void RemoveTagsFromInstancesWithBurst(InstanceManager* data, FloraInstanceHandle* instances, int count, InstanceTag tags)
        {
            for (int i = 0; i < count; i++)
                data->RemoveTagsFromInstance(instances[i], tags);
        }

        private void RemoveTagsFromInstance(FloraInstanceHandle instance, InstanceTag tags)
        {
            tags = NormalizeMutableTags(tags);
            if (tags == InstanceTag.None)
                return;

            var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
            if (instanceInChunk.Chunk == ChunkIndex.None)
                return;

            var srcArchetypeKey = instanceInChunk.Chunk.Archetype.Key;
            var tagsToRemove = (uint)(tags & srcArchetypeKey.Tags);
            if (tagsToRemove == 0)
                return; // No tags to remove

            var dstArchetypeKey = srcArchetypeKey;
            dstArchetypeKey.Tags &= ~tags;
            var dstArchetype = FindOrCreateArchetype(dstArchetypeKey);
            Move(instance, dstArchetype);
        }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SyncJobsForMainThread()
        {
            m_DataDependencies.Complete();
            m_DataDependencies = default;
        }

        internal void FlushPendingSpatialUpdates()
        {
            SyncJobsForMainThread();
            FlushPendingSpatialUpdatesInternal();
        }

        private void FlushPendingSpatialUpdatesInternal()
        {
            if (m_PendingSpatialUpdates.IsEmpty)
                return;

            var pendingSpatialUpdates = m_PendingSpatialUpdates.ToArray<ChunkIndex>(Allocator.Temp);
            m_PendingSpatialUpdates.Clear();
            m_CullingGrid.ValueRW.UpdateInstances(pendingSpatialUpdates);
        }

        #region Initialize Frame

        private static readonly ProfilerMarker InitializeFrameMarker = new ProfilerMarker("Flora.InstanceManager.InitializeFrame");

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct CopyLocalToWorldPreviousJob : IJobParallelFor
        {
            public const int BatchSize = 16;

            [ReadOnly] public NativeArray<ChunkIndex> Chunks;
            [ReadOnly] public NativeArray<GraphicsMatrix> LocalToWorlds;

            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<GraphicsMatrix> PrevLocalToWorlds;

            public void Execute(int index)
            {
                var chunk = Chunks[index];
                var instanceCount = chunk.Count;
                var baseInstanceIndex = chunk.AsInstanceOffset();
                var localToWorlds = LocalToWorlds.GetSubArray(baseInstanceIndex, instanceCount).GetUnsafeReadOnlyPtrT();
                var prevLocalToWorlds = PrevLocalToWorlds.GetSubArray(baseInstanceIndex, instanceCount).GetUnsafePtrT();
                UnsafeUtility.MemCpy(prevLocalToWorlds, localToWorlds, instanceCount * sizeof(GraphicsMatrix));
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private static void InitializeFrameWithBurst(InstanceManager* data)
        {
            data->InitializeFrameInternal();
        }

        public void InitializeFrame()
        {
            using var _ = InitializeFrameMarker.Auto();

            // Ensure all previous jobs are complete
            m_DataDependencies.Complete();

            // Now initialize the frame
            InitializeFrameWithBurst(Self);
        }

        private void InitializeFrameInternal()
        {
            m_FrameAllocators.Update();
            IncrementFrameVersion();

            // Copy instance moved flags to last frame and clear this frame
            (m_InstanceMovedThisFrame, m_InstanceMovedLastFrame) = (m_InstanceMovedLastFrame, m_InstanceMovedThisFrame);
            m_InstanceMovedThisFrame.MemClear();

            if (!m_DirtyChunkTransforms.IsEmpty)
            {
                // Prev-frame data changed
                UpdateContentVersion();

                // Upload the transforms again
                m_PendingTransformUpload.UnionWith(m_DirtyChunkTransforms);

                // Copy LocalToWorld to PrevLocalToWorld
                var changedTransforms = m_DirtyChunkTransforms.ToArray<ChunkIndex>(Allocator.TempJob);
                m_DataDependencies = new CopyLocalToWorldPreviousJob {
                    Chunks = changedTransforms,
                    LocalToWorlds = m_InstanceLocalToWorld,
                    PrevLocalToWorlds = m_InstancePrevLocalToWorld,
                }.Schedule(changedTransforms.Length, CopyLocalToWorldPreviousJob.BatchSize, m_DataDependencies);
                changedTransforms.Dispose(m_DataDependencies);
            }

            m_DirtyChunkTransforms.Clear();
        }

        #endregion

        #region Post Late Update

        private static readonly ProfilerMarker PostLateUpdateMarker = new ProfilerMarker("Flora.InstanceManager.PostLateUpdate");

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private static void OnPostLateUpdateWithBurst(InstanceManager* data)
        {
            data->OnPostLateUpdateInternal();
        }

        public void OnPostLateUpdate()
        {
            OnPostLateUpdateWithBurst(Self);
        }

        private void OnPostLateUpdateInternal()
        {
            using var _ = PostLateUpdateMarker.Auto();

            m_DataDependencies.Complete();
            m_DataDependencies = default;
            FlushPendingSpatialUpdatesInternal();
        }

        #endregion
    }
}
