// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal unsafe partial struct CullingGrid
    {
        #region Uploads

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct BuildChunkFlags : IJobParallelFor
        {
            public const int BatchSize = 1;

            [ReadOnly] public NativeArray<CullingChunkIndex> Chunks;
            [ReadOnly] public NativeArray<int> ChunkCounts;
            [ReadOnly] public NativeArray<int> ChunkInstanceIndices;

            // InstanceManager arrays
            [ReadOnly] public NativeArray<byte> InstanceFlippedWinding;
            [ReadOnly] public NativeArray<byte> InstanceHasMovedThisFrame;
            [ReadOnly] public NativeArray<byte> InstanceHasMovedLastFrame;
#if UNITY_EDITOR
            [ReadOnly] public NativeArray<ulong> InstanceEditorSelected;
            [ReadOnly] public NativeArray<ulong> InstanceEditorHidden;
#endif

            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<ulong> ChunkCPUFlags; // Indexed by [chunk * CullingFlagChannel.Count + channel]
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<ulong> ChunkGPUFlags; // Indexed by [index * CullingFlagChannel.Count + channel]

            public void Execute(int index)
            {
                CullingChunkIndex chunk = Chunks[index];
                int count = ChunkCounts[chunk];

                ulong* chunkFlags = stackalloc ulong[(int)CullingFlagChannel.Count];
                for (int i = 0; i < (int)CullingFlagChannel.Count; i++)
                    chunkFlags[i] = 0ul;

                int* instanceIndices = ChunkInstanceIndices.GetSubArray(chunk.AsInstanceOffset(), count).GetUnsafeReadOnlyPtrT();

                for (int i = 0; i < count; i++)
                {
                    int instanceIndex = instanceIndices[i];
                    if (InstanceFlippedWinding[instanceIndex] != 0)
                        chunkFlags[(int)CullingFlagChannel.FlippedWinding] |= 1ul << i;
                    if (InstanceHasMovedThisFrame[instanceIndex] != 0 || InstanceHasMovedLastFrame[instanceIndex] != 0)
                        chunkFlags[(int)CullingFlagChannel.HasMotion] |= 1ul << i;
#if UNITY_EDITOR
                    int chunkIndex = instanceIndex >> 6;
                    int bitIndex = instanceIndex & 0x3f;
                    if ((InstanceEditorSelected[chunkIndex] & (1ul << bitIndex)) != 0)
                        chunkFlags[(int)CullingFlagChannel.EditorSelected] |= 1ul << i;
                    if ((InstanceEditorHidden[chunkIndex] & (1ul << bitIndex)) != 0)
                        chunkFlags[(int)CullingFlagChannel.EditorHidden] |= 1ul << i;
#endif
                }

                ulong* cpuFlags = ChunkCPUFlags.GetSubArray(chunk * (int)CullingFlagChannel.Count, (int)CullingFlagChannel.Count).GetUnsafePtrT();
                ulong* gpuFlags = ChunkGPUFlags.GetSubArray(index * (int)CullingFlagChannel.Count, (int)CullingFlagChannel.Count).GetUnsafePtrT();
                for (int channel = 0; channel < (int)CullingFlagChannel.Count; channel++)
                {
                    cpuFlags[channel] = chunkFlags[channel];
                    gpuFlags[channel] = chunkFlags[channel];
                }
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct BuildChunkUpdatePackets : IJobParallelFor
        {
            public const int BatchSize = 64;

            [ReadOnly] public NativeArray<CullingChunkIndex> Chunks;
            [ReadOnly] public NativeArray<CellIndex> ChunkCells;
            [ReadOnly] public NativeArray<ArchetypeIndex> ChunkArchetypes;
            [ReadOnly] public NativeArray<BatchDomainIndex> ChunkBatchDomains;
            [ReadOnly] public NativeArray<PackedCullingChunkBatch> ChunkBatches;

            [WriteOnly] public NativeArray<CullingChunkUpdatePacket> ChunkUpdatePackets;

            public void Execute(int index)
            {
                CullingChunkIndex chunk = Chunks[index];
                ChunkUpdatePackets[index] = CullingChunkUpdatePacket.Create(
                    chunk,
                    ChunkCells[chunk],
                    ChunkBatches[chunk],
                    PackedCullingChunkInfo.Create(ChunkArchetypes[chunk], ChunkBatchDomains[chunk])
                );
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct BuildIndirectOffsets : IJobParallelFor
        {
            public const int BatchSize = 1;

            [ReadOnly] public NativeArray<CullingChunkIndex> IndirectChunks;
            [ReadOnly] public NativeArray<int> ChunkCounts;
            [ReadOnly] public NativeArray<int> ChunkIndirectPageIndex;
            [ReadOnly] public NativeArray<int> InstanceIndices;

            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<int> PersistentIndirectOffsets;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<int> ScatterIndirectOffsets;

            public void Execute(int index)
            {
                CullingChunkIndex chunk = IndirectChunks[index];
                int count = ChunkCounts[chunk];

                int* instanceIndices = InstanceIndices.GetSubArray(chunk.AsInstanceOffset(), count).GetUnsafeReadOnlyPtrT();
                int indirectPageOffset = ChunkIndirectPageIndex[chunk] * IndirectPageSize;
                int scatterStart = index * IndirectPageSize;

                int* persistentOffsets = PersistentIndirectOffsets.GetSubArray(indirectPageOffset, count).GetUnsafePtrT();
                int* scatterOffsets = ScatterIndirectOffsets.GetSubArray(scatterStart, count).GetUnsafePtrT();

                for (int i = 0; i < count; i++)
                {
                    int instanceOffset = GetBatchOffsetForInstance(instanceIndices[i]);
                    persistentOffsets[i] = instanceOffset;
                    scatterOffsets[i] = instanceOffset;
                }
            }
        }

        private static readonly ProfilerMarker ScheduleUploadsMarker = new ProfilerMarker("CullingGrid.ScheduleUploads");

        public void ScheduleUploads()
        {
            ScheduleUploadsWithBurst(Self);
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private static void ScheduleUploadsWithBurst(CullingGrid* cullingGrid)
        {
            cullingGrid->ScheduleUploadsInternal();
        }

        private void ScheduleUploadsInternal()
        {
            using ProfilerMarker.AutoScope _ = ScheduleUploadsMarker.Auto();

            if (m_ContentVersionScheduled == m_ContentVersion)
                return; // Already scheduled
            if (!ChangeVersionUtility.DidChange(m_ContentVersion, m_ContentVersionApplied))
                return; // No changes

            if (!m_BlockDataDirty.IsEmpty)
            {
                NativeArray<BlockIndex> blockIndices = m_BlockDataDirty.ToArray<BlockIndex>(Allocator.Temp);
                m_PendingBlockIndexUpdates.ResizeUninitialized(blockIndices.Length);
                m_PendingBlockDataUpdates.ResizeUninitialized(blockIndices.Length);

                for (int i = 0; i < blockIndices.Length; i++)
                {
                    BlockIndex blockIndex = blockIndices[i];
                    m_PendingBlockIndexUpdates[i] = blockIndex;
                    m_PendingBlockDataUpdates[i] = m_BlockData[blockIndex];
                }

                m_BlockDataDirty.Clear();
            }

            if (!m_ChunkFlagsDirty.IsEmpty)
            {
                m_ChunkFlagsDirty.CopyToList(m_PendingChunkFlagIndices);
                NativeArray<int> chunkIndices = m_PendingChunkFlagIndices.AsArray();
                m_PendingChunkFlagUpdates.ResizeUninitialized(chunkIndices.Length * (int)CullingFlagChannel.Count);

                JobHandle flagUpdateHandle = new BuildChunkFlags {
                    Chunks = chunkIndices.Reinterpret<CullingChunkIndex>(),
                    ChunkCounts = m_ChunkCount,
                    ChunkInstanceIndices = m_ChunkInstanceIndices,
                    InstanceFlippedWinding = m_InstanceManager.ValueRO.InstanceFlippedWinding,
                    InstanceHasMovedThisFrame = m_InstanceManager.ValueRO.InstanceMovedThisFrame,
                    InstanceHasMovedLastFrame = m_InstanceManager.ValueRO.InstanceMovedLastFrame,
#if UNITY_EDITOR
                    InstanceEditorSelected = m_InstanceManager.ValueRO.InstanceEditorSelected.AsArray(),
                    InstanceEditorHidden = m_InstanceManager.ValueRO.InstanceEditorHidden.AsArray(),
#endif
                    ChunkCPUFlags = m_ChunkFlags,
                    ChunkGPUFlags = m_PendingChunkFlagUpdates.AsArray(),
                }.Schedule(chunkIndices.Length, BuildChunkFlags.BatchSize);

                m_PreDispatchHandle = JobHandle.CombineDependencies(m_PreDispatchHandle, flagUpdateHandle);
                m_ChunkFlagsDirty.Clear();
            }

            if (!m_ChunkInfoDirty.IsEmpty)
            {
                NativeArray<CullingChunkIndex> chunkIndices = m_ChunkInfoDirty.ToArray<CullingChunkIndex>(Allocator.TempJob);
                m_PendingChunkUpdatePackets.ResizeUninitialized(chunkIndices.Length);

                for (int i = 0; i < chunkIndices.Length; i++)
                {
                    CullingChunkIndex chunkIndex = chunkIndices[i];
                    m_ChunkBatch[chunkIndex] = UpdateChunkBatch(chunkIndex);
                }

                JobHandle chunkUpdateHandle = new BuildChunkUpdatePackets {
                    Chunks = chunkIndices,
                    ChunkCells = m_ChunkCell,
                    ChunkArchetypes = m_ChunkArchetype,
                    ChunkBatchDomains = m_ChunkBatchDomain,
                    ChunkBatches = m_ChunkBatch,
                    ChunkUpdatePackets = m_PendingChunkUpdatePackets.AsArray(),
                }.Schedule(chunkIndices.Length, BuildChunkUpdatePackets.BatchSize);
                chunkIndices.Dispose(chunkUpdateHandle);

                m_PreDispatchHandle = JobHandle.CombineDependencies(m_PreDispatchHandle, chunkUpdateHandle);
                m_ChunkInfoDirty.Clear();
            }

            if (!m_QueuedIndirectChunks.IsEmpty)
            {
                NativeArray<CullingChunkIndex> chunkIndices = m_QueuedIndirectChunks.AsArray();
                JobHandle indirectHandle = new BuildIndirectOffsets {
                    IndirectChunks = chunkIndices,
                    ChunkCounts = m_ChunkCount,
                    ChunkIndirectPageIndex = m_ChunkIndirectPageIndex,
                    InstanceIndices = m_ChunkInstanceIndices,
                    PersistentIndirectOffsets = m_IndirectInstanceOffsets.AsArray(),
                    ScatterIndirectOffsets = m_PendingIndirectOffsetUpdates.AsArray(),
                }.Schedule(chunkIndices.Length, BuildIndirectOffsets.BatchSize);

                m_PreDispatchHandle = JobHandle.CombineDependencies(m_PreDispatchHandle, indirectHandle);
            }

            if (!m_ChunkAttributesDirty.IsEmpty)
            {
                NativeArray<CullingChunkIndex> chunkIndices = m_ChunkAttributesDirty.ToArray<CullingChunkIndex>(Allocator.Temp);
                m_PendingChunkAttributesUpdates.ResizeUninitialized(chunkIndices.Length);
                for (int i = 0; i < chunkIndices.Length; i++)
                {
                    CullingChunkIndex chunkIndex = chunkIndices[i];
                    CellIndex cellIndex = m_ChunkCell[chunkIndex];
                    m_PendingChunkAttributesUpdates[i] = new int2(cellIndex.Index, chunkIndex.Index);
                }

                m_ChunkAttributesDirty.Clear();
            }

            m_ContentVersionScheduled = m_ContentVersion;
        }

        private static readonly ProfilerMarker DispatchUploadsMarker = new ProfilerMarker("CullingGrid.DispatchUploads");

        private void GrowBuffersIfNeeded()
        {
            int maxBlockCount = m_BlockData.Length;
            m_BlockDataBuffer.GrowIfNeeded(maxBlockCount);

            int maxChunkCount = m_ChunkCount.Length;
            m_ChunkCellBuffer.ResizeIfNeeded(maxChunkCount);
            m_ChunkInfoBuffer.ResizeIfNeeded(maxChunkCount);
            m_ChunkFlagBuffer.ResizeIfNeeded(maxChunkCount * (int)CullingFlagChannel.Count * 2);
            m_ChunkBatchBuffer.ResizeIfNeeded(maxChunkCount);
            m_ChunkAttributeBuffer.ResizeIfNeeded(maxChunkCount);

            int maxIndirectOffsetCount = m_IndirectInstanceOffsets.Length;
            m_IndirectOffsetBuffer.ResizeIfNeeded(maxIndirectOffsetCount);
        }

        public void DispatchUploads(CommandBuffer cmd)
        {
            if (m_ContentVersionScheduled == 0)
                return;

            m_PreDispatchHandle.Complete();
            m_ContentVersionApplied = m_ContentVersionScheduled;
            m_ContentVersionScheduled = 0;

            GrowBuffersIfNeeded();

            cmd.BeginSample(DispatchUploadsMarker);

            if (!m_PendingBlockDataUpdates.IsEmpty)
            {
                NativeArray<BlockData> blockData = m_PendingBlockDataUpdates.AsArray();
                NativeArray<uint> blockIndices = m_PendingBlockIndexUpdates.AsArray().Reinterpret<uint>();
                GraphicsBufferUtility.Scatter(cmd, m_BlockDataBuffer, blockData, blockIndices);

                m_PendingBlockDataUpdates.Clear();
                m_PendingBlockIndexUpdates.Clear();
            }

            if (!m_PendingChunkFlagIndices.IsEmpty)
            {
                NativeArray<int> chunkIndices = m_PendingChunkFlagIndices.AsArray();
                NativeArray<uint> chunkFlags = m_PendingChunkFlagUpdates.AsArray().Reinterpret<uint>(8);

                CullingGridCompute.UpdateChunkFlagsParams updateFlagsParams = new CullingGridCompute.UpdateChunkFlagsParams
                {
                    UpdateCount = chunkIndices.Length,
                    ChannelCount = (int)CullingFlagChannel.Count,
                    ChunkFlagUpdateBuffer = GraphicsBufferStore.RequestStructured(cmd, chunkFlags),
                    ChunkFlagIndexBuffer = GraphicsBufferStore.RequestStructured(cmd, chunkIndices),
                    ChunkFlagBuffer = m_ChunkFlagBuffer,
                };

                CullingGridCompute.DispatchUpdateChunkFlags(cmd, updateFlagsParams);

                m_PendingChunkFlagIndices.Clear();
                m_PendingChunkFlagUpdates.Clear();
            }

            if (!m_PendingChunkUpdatePackets.IsEmpty)
            {
                NativeArray<CullingChunkUpdatePacket> chunkUpdatePackets = m_PendingChunkUpdatePackets.AsArray();
                GraphicsBuffer updatePacketBuffer = GraphicsBufferStore.RequestStructured(cmd, chunkUpdatePackets);

                CullingGridCompute.UpdateChunkInfoParams updateChunkInfoParams = new CullingGridCompute.UpdateChunkInfoParams
                {
                    PacketCount = chunkUpdatePackets.Length,
                    ChunkPacketBuffer = updatePacketBuffer,
                    ChunkCellBuffer = m_ChunkCellBuffer,
                    ChunkInfoBuffer = m_ChunkInfoBuffer,
                    ChunkBatchBuffer = m_ChunkBatchBuffer,
                };

                CullingGridCompute.DispatchUpdateChunkInfo(cmd, updateChunkInfoParams);

                m_PendingChunkUpdatePackets.Clear();
            }

            if (!m_PendingIndirectPageUpdates.IsEmpty)
            {
                NativeArray<uint> indirectPageUpdates = m_PendingIndirectPageUpdates.AsArray();
                NativeArray<int> indirectOffsetUpdates = m_PendingIndirectOffsetUpdates.AsArray();

                CullingGridCompute.UpdateIndirectPagesParams updateIndirectParams = new CullingGridCompute.UpdateIndirectPagesParams
                {
                    IndirectPageUpdateCount = indirectPageUpdates.Length,
                    IndirectPageUpdateBuffer = GraphicsBufferStore.RequestStructured(cmd, indirectPageUpdates),
                    IndirectOffsetUpdateBuffer = GraphicsBufferStore.RequestStructured(cmd, indirectOffsetUpdates),
                    IndirectOffsetBuffer = m_IndirectOffsetBuffer,
                };

                CullingGridCompute.DispatchScatterIndirectPages(cmd, updateIndirectParams);

                m_QueuedIndirectChunks.Clear();
                m_PendingIndirectPageUpdates.Clear();
                m_PendingIndirectOffsetUpdates.Clear();
            }

            if (!m_PendingChunkAttributesUpdates.IsEmpty)
            {
                CullingGridCompute.UpdateChunkAttributesParams updateAttributeParams = new CullingGridCompute.UpdateChunkAttributesParams
                {
                    AttributeUpdateCount = m_PendingChunkAttributesUpdates.Length,
                    CellChunkIndices = m_PendingChunkAttributesUpdates.AsArray(),
                    InstanceBuffer = m_InstanceBuffer.ValueRO.DataBuffer,
                    BatchDomainAddressBuffer = m_InstanceBuffer.ValueRO.DomainCullingAddresses,
                    ArchetypeDataBuffer = m_InstanceManager.ValueRO.ArchetypeDataBuffer,
                    TemplateDataBuffer = m_TemplateManager.ValueRO.TemplateDataBuffer,
                    BlockDataBuffer = m_BlockDataBuffer,
                    ChunkBatchBuffer = m_ChunkBatchBuffer,
                    ChunkInfoBuffer = m_ChunkInfoBuffer,
                    ChunkAttributeBuffer = m_ChunkAttributeBuffer,
                    IndirectOffsetBuffer = m_IndirectOffsetBuffer,
                };

                CullingGridCompute.DispatchUpdateChunkAttributes(cmd, updateAttributeParams);

                m_PendingChunkAttributesUpdates.Clear();
            }

            cmd.EndSample(DispatchUploadsMarker);
        }

        #endregion
    }
}
