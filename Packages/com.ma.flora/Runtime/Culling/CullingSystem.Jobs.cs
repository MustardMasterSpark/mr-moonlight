// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MA.InternalBridge;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal static class DrawStateUtility
    {
        public const int StateBitCount = 3;
        public const int StateKeyCount = 1 << StateBitCount;
        public const int StateLodStride = CullingConstants.MaxLodCount * StateKeyCount;

        public static byte CreateStateMask(IndirectStateFlags supported)
        {
            uint mask = 1u; // Always support the "no flags" state

            for (int key = 1; key < StateKeyCount; key++)
            {
                var combo = (IndirectStateFlags)key;
                if ((combo & ~supported) == 0)
                    mask |= (uint)(1 << key); // All flags in this combination are supported
            }

            return (byte)mask;
        }

        public static int ComputePartitionStateOffset(int lightmapInfoOffset, int partitionIndex)
        {
            return (lightmapInfoOffset + partitionIndex) * StateKeyCount;
        }

        public static bool StateMaskContainsKey(uint mask, int key)
        {
            return ((mask >> key) & 1) != 0;
        }

        public static uint CreateStateIndices(uint mask)
        {
            uint indices = 0;
            int binCount = 0;

            for (int key = 0; key < StateKeyCount; key++)
            {
                int slot = StateMaskContainsKey(mask, key) ? binCount++ : 0xf;
                indices |= (uint)(slot & 0xf) << (key * 4);
            }

            return indices;
        }

        public static int StateSlotFromKey(uint indices, int key)
        {
            uint slot = (indices >> (key * 4)) & 0xf;
            return slot == 0xf ? -1 : (int)slot;
        }

        public static int ComputeBinIndex(int baseOffset, int splitIndex, int slotsPerLod, int lodCount, int stateSlot, int lodIndex)
        {
            // GPU Bin layout
            return baseOffset + splitIndex * (slotsPerLod * lodCount) + stateSlot * lodCount + lodIndex;
        }

        public static int ComputePartitionBinStride(int splitCount, int slotsPerLod, int lodCount)
        {
            return splitCount * slotsPerLod * lodCount;
        }

        public static int ComputePartitionedBinIndex(int baseOffset, int partitionIndex, int splitIndex, int splitCount, int slotsPerLod, int lodCount, int stateSlot, int lodIndex)
        {
            int partitionStride = ComputePartitionBinStride(splitCount, slotsPerLod, lodCount);
            return baseOffset + partitionIndex * partitionStride + splitIndex * (slotsPerLod * lodCount) + stateSlot * lodCount + lodIndex;
        }

        public static int ComputeTemplateLodStateIndex(int templateIndex, int lodIndex, int stateKey)
        {
            return templateIndex * StateLodStride + lodIndex * StateKeyCount + stateKey;
        }

        public static byte CreateChunkStateMask(ulong validInstanceMask, ulong motionBits, ulong flippedBits, bool supportsFadeKeyword)
        {
            if (validInstanceMask == 0)
                return 0;

            byte stateMask = 0;

            ulong noneBits = validInstanceMask & ~motionBits & ~flippedBits;
            ulong motionOnlyBits = motionBits & ~flippedBits;
            ulong flippedOnlyBits = flippedBits & ~motionBits;
            ulong motionAndFlippedBits = motionBits & flippedBits;

            AddChunkStateVariants(ref stateMask, 0, noneBits != 0, supportsFadeKeyword);
            AddChunkStateVariants(ref stateMask, (int)IndirectStateFlags.HasMotion, motionOnlyBits != 0, supportsFadeKeyword);
            AddChunkStateVariants(ref stateMask, (int)IndirectStateFlags.HasFlippedWinding, flippedOnlyBits != 0, supportsFadeKeyword);
            AddChunkStateVariants(ref stateMask, (int)(IndirectStateFlags.HasMotion | IndirectStateFlags.HasFlippedWinding), motionAndFlippedBits != 0, supportsFadeKeyword);

            return stateMask;
        }

        public static void AddChunkStateVariants(ref byte stateMask, int baseStateKey, bool occupied, bool supportsFadeKeyword)
        {
            if (!occupied)
                return;

            stateMask |= (byte)(1 << baseStateKey);
            if (supportsFadeKeyword)
                stateMask |= (byte)(1 << (baseStateKey | (int)IndirectStateFlags.HasFadeKeyword));
        }

        public static void InsertSortedUniqueLightmapIndex(NativeList<int> sortedLightmapIndices, int lightmapIndex)
        {
            int insertIndex = 0;
            while (insertIndex < sortedLightmapIndices.Length)
            {
                int currentValue = sortedLightmapIndices[insertIndex];
                if (currentValue == lightmapIndex)
                    return;
                if (currentValue > lightmapIndex)
                    break;

                insertIndex++;
            }

            sortedLightmapIndices.Add(0);
            for (int i = sortedLightmapIndices.Length - 1; i > insertIndex; i--)
                sortedLightmapIndices[i] = sortedLightmapIndices[i - 1];
            sortedLightmapIndices[insertIndex] = lightmapIndex;
        }

        public static int FindSortedLightmapPartitionIndex(NativeList<int> sortedLightmapIndices, int lightmapIndex)
        {
            int low = 0;
            int high = sortedLightmapIndices.Length - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                int currentValue = sortedLightmapIndices[mid];
                if (currentValue == lightmapIndex)
                    return mid;
                if (currentValue < lightmapIndex)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            return -1;
        }

    }

    internal struct GridCullCounts
    {
        public int VisibleChunkCount;
        public int VisibleInstanceCount;
        public bool IsEmpty => VisibleChunkCount == 0 || VisibleInstanceCount == 0;
    }

    internal struct CullingLayoutCounts
    {
        public int VisibleChunkCount;
        public int VisibleInstanceCount;
        public int VisibilityBufferCapacity;
        public int DrawPartitionCount;
        public int DrawCommandCount;
        public int DrawBinCount;
        public int UsedDrawRangeCount;
    }

    internal struct DrawBinConfig
    {
        public int SplitCount;
        public bool SupportsCrossFade;
        public bool SupportsMotionCheck;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DrawVisibilityMask : IEquatable<DrawVisibilityMask>
    {
        public static DrawVisibilityMask None => default;

        public const byte SplitBits  = 0b01111000;
        public const byte FlippedBit = 0b00000100;
        public const byte MotionBit  = 0b00000010;
        public const byte FadeBit    = 0b00000001;

        public byte packed;

        public static DrawVisibilityMask Create(byte splitMask, bool hasFlippedWinding, bool hasMotion, bool hasFadeKeyword)
        {
            byte packed = (byte)((splitMask << 3) & SplitBits);
            if (hasFlippedWinding) packed |= FlippedBit;
            if (hasMotion)         packed |= MotionBit;
            if (hasFadeKeyword)    packed |= FadeBit;
            return new DrawVisibilityMask { packed = packed };
        }

        public bool IsVisible => (packed & SplitBits) != 0;

        public byte SplitMask
        {
            get => (byte)((packed & SplitBits) >> 3);
            set => packed = (byte)((packed & ~SplitBits & 0xff) | ((value << 3) & SplitBits));
        }

        public bool Equals(DrawVisibilityMask other) => packed == other.packed;
        public override bool Equals(object obj) => obj is DrawVisibilityMask other && Equals(other);
        public override int GetHashCode() => packed;

        public static DrawVisibilityMask operator |(DrawVisibilityMask a, DrawVisibilityMask b) => new DrawVisibilityMask { packed = (byte)(a.packed | b.packed) };
        public static DrawVisibilityMask operator &(DrawVisibilityMask a, DrawVisibilityMask b) => new DrawVisibilityMask { packed = (byte)(a.packed & b.packed) };

        public static bool operator ==(DrawVisibilityMask a, DrawVisibilityMask b) => a.packed == b.packed;
        public static bool operator !=(DrawVisibilityMask a, DrawVisibilityMask b) => a.packed != b.packed;
    }

    internal unsafe partial class CullingSystem
    {
        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DebugThrowIf(bool condition, string message)
        {
#if DEBUG
            if (!condition)
                throw new InvalidOperationException(message);
#endif
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct SetupFrustumCullingInputs : IJob
        {
            public float LODBias;
            public float MeshLodThreshold;
            [NativeDisableUnsafePtrRestriction] public BatchCullingContext* Context;
            [NativeDisableUnsafePtrRestriction] public ReceiverPlanes* ReceiverPlanes;
            [NativeDisableUnsafePtrRestriction] public ReceiverSphereCuller* ReceiverSphereCuller;
            [NativeDisableUnsafePtrRestriction] public FrustumPlaneCuller* FrustumPlaneCuller;
            [NativeDisableUnsafePtrRestriction] public float* ScreenRelativeMetric;
            [NativeDisableUnsafePtrRestriction] public float* MeshLodSelectionConstant;

            public void Execute()
            {
                *ReceiverPlanes = MA.Flora.ReceiverPlanes.Create(*Context, Allocator.TempJob);
                *ReceiverSphereCuller = MA.Flora.ReceiverSphereCuller.Create(*Context, Allocator.TempJob);
                *FrustumPlaneCuller = MA.Flora.FrustumPlaneCuller.Create(*Context, ReceiverPlanes->Planes.AsArray(), *ReceiverSphereCuller, Allocator.TempJob);
                *ScreenRelativeMetric = CullingUtility.CalculateScreenRelativeMetricNoBias(Context->lodParameters);
                *MeshLodSelectionConstant = CullingUtility.CalculateMeshLodConstant(Context->lodParameters, *ScreenRelativeMetric, MeshLodThreshold);
                *ScreenRelativeMetric /= LODBias;
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct CullGrid : IJob
        {
            [ReadOnly] public NativeArray<FrustumSIMDPacket> FrustumPlanePackets;
            [ReadOnly] public NativeArray<FrustumPlaneCuller.SplitInfo> FrustumSplitInfos;
            [ReadOnly] public NativeArray<Plane> LightFacingFrustumPlanes;
            [ReadOnly] public NativeArray<ReceiverSphereCuller.SplitInfo> ReceiverSplitInfos;
            [ReadOnly] public float3x3 WorldToLightSpaceRotation;
            [ReadOnly, NativeDisableUnsafePtrRestriction] public IntPtr OcclusionBuffer;

            [ReadOnly] public NativeBitSet Blocks;
            [ReadOnly] public NativeArray<BlockLocation> BlockLocations;

            [ReadOnly] public NativeBitSet Cells;
            [ReadOnly] public NativeArray<int> CellInstanceCount;
            [ReadOnly] public NativeBufferArray<CullingChunkIndex> CellChunks;

            [WriteOnly] public NativeArray<byte> OutCellVisibility;
            [WriteOnly] public NativeList<CullingChunkIndex> OutCullingChunks;
            [NativeDisableUnsafePtrRestriction] public GridCullCounts* OutCullingCounts;

            public void Execute()
            {
                GridCullCounts counts = default;

                foreach (int blockIndex in Blocks)
                {
                    BlockLocation blockLocation = BlockLocations[blockIndex];
                    AABBMinMax blockAABB = blockLocation.PaddedAABB;

                    uint blockVisibilityMask = FrustumPlaneCuller.ComputeSplitVisibilityMask(FrustumPlanePackets, FrustumSplitInfos, blockAABB);
                    if (blockVisibilityMask != 0 && ReceiverSplitInfos.Length > 0)
                        blockVisibilityMask &= ReceiverSphereCuller.ComputeSplitVisibilityMask(LightFacingFrustumPlanes, ReceiverSplitInfos, WorldToLightSpaceRotation, blockAABB);
                    if (blockVisibilityMask != 0 && OcclusionBuffer != IntPtr.Zero)
                        blockVisibilityMask = BatchRendererGroupBridge.OcclusionTestAABB(OcclusionBuffer, blockAABB.ToBounds()) ? blockVisibilityMask : 0;

                    if (blockVisibilityMask != 0)
                    {
                        int baseCellIndex = blockIndex * CullingGrid.CellsPerBlock;
                        foreach (int cellIndex in Cells.IndicesInRange(baseCellIndex, CullingGrid.CellsPerBlock))
                        {
                            int indexInBlock = cellIndex & CellIndex.LocalIndexMask;
                            CellLocation cellLocation = CellLocation.FromBlock(blockLocation, indexInBlock);
                            AABBMinMax cellAABB = cellLocation.PaddedAABB;

                            uint cellVisibilityMask = blockVisibilityMask & FrustumPlaneCuller.ComputeSplitVisibilityMask(FrustumPlanePackets, FrustumSplitInfos, cellAABB);
                            if (cellVisibilityMask != 0 && ReceiverSplitInfos.Length > 0)
                                cellVisibilityMask &= ReceiverSphereCuller.ComputeSplitVisibilityMask(LightFacingFrustumPlanes, ReceiverSplitInfos, WorldToLightSpaceRotation, cellAABB);
                            if (cellVisibilityMask != 0 && OcclusionBuffer != IntPtr.Zero)
                                cellVisibilityMask = BatchRendererGroupBridge.OcclusionTestAABB(OcclusionBuffer, cellAABB.ToBounds()) ? cellVisibilityMask : 0;

                            if (cellVisibilityMask != 0)
                            {
                                NativeArray<CullingChunkIndex> cellChunks = CellChunks[cellIndex].AsArray();
                                OutCellVisibility[cellIndex] = (byte)cellVisibilityMask;
                                OutCullingChunks.AddRange(cellChunks.GetUnsafeReadOnlyPtr(), cellChunks.Length);

                                counts.VisibleChunkCount += cellChunks.Length;
                                counts.VisibleInstanceCount += CellInstanceCount[cellIndex];
                            }
                        }
                    }
                }

                OutCullingCounts[0] = counts;
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct CullChunks : IJobParallelFor
        {
            public const int BatchSize = 64;

            [ReadOnly] public BatchCullingViewType ViewType;
            [ReadOnly] public DrawBinConfig BinConfig;
            [ReadOnly] public uint CullingLayerMask;

            [ReadOnly] public NativeArray<byte> CellVisibility;
            [ReadOnly] public NativeArray<CullingChunkIndex> Chunks;
            [ReadOnly] public NativeArray<int> ChunkCounts;
            [ReadOnly] public NativeArray<CellIndex> ChunkCells;
            [ReadOnly] public NativeArray<ulong> ChunkFlags;
            [ReadOnly] public NativeArray<ArchetypeIndex> ChunkArchetypes;

            [NativeDisableContainerSafetyRestriction, NoAlias] [WriteOnly] public NativeArray<DrawVisibilityMask> ChunkVisibility;

#if UNITY_EDITOR
            [ReadOnly] public bool CullHiddenChunks;
            [ReadOnly] public ulong SceneCullingMask;
#endif

            public void Execute(int chunkDrawIndex)
            {
                CullingChunkIndex chunk = Chunks[chunkDrawIndex];
                ArchetypeIndex archetype = ChunkArchetypes[chunk];
                ArchetypeKey archetypeKey = archetype.Key;

                if (ViewType == BatchCullingViewType.Light && !archetypeKey.Template.HasShadowCasters)
                    return; // No shadow casters
                if (CullingLayerMask == 0u || (CullingLayerMask & 1u << archetypeKey.Layer) == 0u)
                    return; // Layer is not in the culling mask
#if UNITY_EDITOR
                if ((SceneCullingMask & archetypeKey.SceneCullingMask) == 0ul)
                    return; // Scene is not in the culling mask

                if (ViewType == BatchCullingViewType.SelectionOutline)
                {
                    ulong chunkSelectedBits = ChunkFlags[chunk * (int)CullingFlagChannel.Count + (int)CullingFlagChannel.EditorSelected];
                    if (chunkSelectedBits == 0)
                        return; // No instances are selected in this chunk
                }

                if (CullHiddenChunks)
                {
                    ulong chunkHiddenBits = ChunkFlags[chunk * (int)CullingFlagChannel.Count + (int)CullingFlagChannel.EditorHidden];
                    ulong validInstanceMask = ChunkCounts[chunk] < 64 ? ((1ul << ChunkCounts[chunk]) - 1ul) : ulong.MaxValue;
                    if ((chunkHiddenBits & validInstanceMask) == validInstanceMask)
                        return; // All instances in this chunk are hidden in the scene view
                }
#endif

                CellIndex cell = ChunkCells[chunk];
                uint chunkVisibilityMask = CellVisibility[cell];
                if (chunkVisibilityMask != 0)
                {
                    ulong chunkFlippedWindingBits = ChunkFlags[chunk * (int)CullingFlagChannel.Count + (int)CullingFlagChannel.FlippedWinding];
                    ulong chunkMotionBits = ChunkFlags[chunk * (int)CullingFlagChannel.Count + (int)CullingFlagChannel.HasMotion];

                    TemplateIndex template = archetypeKey.Template;
                    bool canFade = BinConfig.SupportsCrossFade && template.SupportsFadeKeyword;
                    bool canMove = BinConfig.SupportsMotionCheck && template.HasMotionVectors && chunkMotionBits != 0;
                    bool canFlip = chunkFlippedWindingBits != 0;

                    DrawVisibilityMask visibility = DrawVisibilityMask.Create(
                        (byte)chunkVisibilityMask,
                        canFlip, canMove, canFade);

                    ChunkVisibility[chunk] = visibility;
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct ReduceVisibleChunksByTemplate : IJob
        {
            [ReadOnly] public NativeArray<CullingChunkIndex> VisibleChunks;
            [ReadOnly] public NativeArray<int> ChunkCounts;
            [ReadOnly] public NativeArray<ArchetypeIndex> ChunkArchetypes;
            [ReadOnly] public NativeArray<DrawVisibilityMask> ChunkVisibility;

            [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<TemplateVisibilitySummary> TemplateSummaries;
            [NativeDisableContainerSafetyRestriction, NoAlias] [WriteOnly] public NativeArray<CullingLayoutCounts> OutputCounts;

            public void Execute()
            {
                CullingLayoutCounts counts = default;

                for (int visibleIndex = 0; visibleIndex < VisibleChunks.Length; visibleIndex++)
                {
                    CullingChunkIndex chunk = VisibleChunks[visibleIndex];
                    DebugThrowIf(IsValidChunk(chunk), "Visible chunk index is out of range for culling layout.");

                    DrawVisibilityMask chunkVisibility = ChunkVisibility[chunk];
                    if (!chunkVisibility.IsVisible)
                        continue;

                    TemplateIndex template = ChunkArchetypes[chunk].Key.Template;
                    DebugThrowIf(IsValidTemplate(template), "Visible chunk references an invalid template.");

                    TemplateVisibilitySummary summary = TemplateSummaries[template];
                    summary.VisibleChunkCount += 1;
                    summary.VisibleInstanceCount += ChunkCounts[chunk];
                    TemplateSummaries[template] = summary;

                    counts.VisibleChunkCount += 1;
                    counts.VisibleInstanceCount += ChunkCounts[chunk];
                }

                OutputCounts[0] = counts;
            }

            private bool IsValidChunk(CullingChunkIndex chunk)
            {
                return chunk.Index >= 0 &&
                       chunk.Index < ChunkVisibility.Length &&
                       chunk.Index < ChunkArchetypes.Length &&
                       chunk.Index < ChunkCounts.Length;
            }

            private bool IsValidTemplate(TemplateIndex template)
            {
                return template.Index > 0 && template.Index < TemplateSummaries.Length;
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct ComputeTemplateChunkOffsets : IJob
        {
            [ReadOnly] public NativeBitSet Templates;
            [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<TemplateVisibilitySummary> TemplateSummaries;

            public void Execute()
            {
                int drawChunkOffset = 0;
                foreach (TemplateIndex template in Templates.AsType<TemplateIndex>())
                {
                    DebugThrowIf(template.Index > 0 && template.Index < TemplateSummaries.Length, "Allocated template index is out of range for template summaries.");

                    TemplateVisibilitySummary summary = TemplateSummaries[template];
                    summary.OrderedChunkOffset = drawChunkOffset;
                    summary.PartitionSummaryOffset = drawChunkOffset;
                    TemplateSummaries[template] = summary;
                    drawChunkOffset += summary.VisibleChunkCount;
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct OrderVisibleChunksByTemplate : IJob
        {
            [ReadOnly] public NativeBitSet Templates;
            [ReadOnly] public NativeArray<TemplateVisibilitySummary> TemplateSummaries;
            [ReadOnly] public NativeArray<CullingChunkIndex> VisibleChunks;
            [ReadOnly] public NativeArray<DrawVisibilityMask> ChunkVisibility;
            [ReadOnly] public NativeArray<ArchetypeIndex> ChunkArchetypes;

            [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<int> TemplateChunkWriteCursors;
            [NativeDisableContainerSafetyRestriction, NoAlias] [WriteOnly] public NativeArray<CullingChunkIndex> OrderedVisibleChunks;
            [NativeDisableContainerSafetyRestriction, NoAlias] [WriteOnly] public NativeArray<int> OrderedVisibleChunkSourceIndices;

            public void Execute()
            {
                foreach (TemplateIndex template in Templates.AsType<TemplateIndex>())
                {
                    DebugThrowIf(
                        template.Index > 0 &&
                        template.Index < TemplateSummaries.Length &&
                        template.Index < TemplateChunkWriteCursors.Length,
                        "Allocated template index is out of range while ordering visible chunks.");

                    TemplateChunkWriteCursors[template] = TemplateSummaries[template].OrderedChunkOffset;
                }

                for (int visibleIndex = 0; visibleIndex < VisibleChunks.Length; visibleIndex++)
                {
                    CullingChunkIndex chunk = VisibleChunks[visibleIndex];
                    DebugThrowIf(IsValidChunk(chunk), "Visible chunk index is out of range while ordering visible chunks.");

                    if (!ChunkVisibility[chunk].IsVisible)
                        continue;

                    TemplateIndex template = ChunkArchetypes[chunk].Key.Template;
                    DebugThrowIf(template.Index > 0 && template.Index < TemplateChunkWriteCursors.Length, "Visible chunk references an invalid template while ordering chunks.");

                    int orderedIndex = TemplateChunkWriteCursors[template]++;
                    DebugThrowIf(
                        orderedIndex >= 0 &&
                        orderedIndex < OrderedVisibleChunks.Length &&
                        orderedIndex < OrderedVisibleChunkSourceIndices.Length,
                        "Ordered visible chunk index exceeded reserved layout capacity.");

                    OrderedVisibleChunks[orderedIndex] = chunk;
                    OrderedVisibleChunkSourceIndices[orderedIndex] = visibleIndex;
                }
            }

            private bool IsValidChunk(CullingChunkIndex chunk)
            {
                return chunk.Index >= 0 &&
                       chunk.Index < ChunkVisibility.Length &&
                       chunk.Index < ChunkArchetypes.Length;
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct ComputeChunkStateMasks : IJobParallelFor
        {
            [ReadOnly] public DrawBinConfig BinConfig;
            [ReadOnly] public NativeArray<CullingLayoutCounts> Counts;
            [ReadOnly] public NativeArray<CullingChunkIndex> OrderedVisibleChunks;
            [ReadOnly] public NativeArray<int> ChunkCounts;
            [ReadOnly] public NativeArray<ulong> ChunkFlags;
            [ReadOnly] public NativeArray<ArchetypeIndex> ChunkArchetypes;

            [WriteOnly] public NativeArray<byte> OrderedVisibleChunkStateMasks;

            public void Execute(int index)
            {
                if (index >= Counts[0].VisibleChunkCount)
                    return;

                CullingChunkIndex chunk = OrderedVisibleChunks[index];
                DebugThrowIf(
                    chunk.Index >= 0 &&
                    chunk.Index < ChunkArchetypes.Length &&
                    chunk.Index < ChunkCounts.Length,
                    "Ordered visible chunk index is out of range while computing state masks.");

                TemplateIndex template = ChunkArchetypes[chunk].Key.Template;
                int chunkCount = ChunkCounts[chunk];
                ulong validInstanceMask = chunkCount < 64 ? ((1ul << chunkCount) - 1ul) : ulong.MaxValue;
                ulong chunkMotionBits = ChunkFlags[chunk * (int)CullingFlagChannel.Count + (int)CullingFlagChannel.HasMotion] & validInstanceMask;
                ulong chunkFlippedBits = ChunkFlags[chunk * (int)CullingFlagChannel.Count + (int)CullingFlagChannel.FlippedWinding] & validInstanceMask;

                OrderedVisibleChunkStateMasks[index] = DrawStateUtility.CreateChunkStateMask(
                    validInstanceMask,
                    BinConfig.SupportsMotionCheck && template.HasMotionVectors ? chunkMotionBits : 0ul,
                    chunkFlippedBits,
                    BinConfig.SupportsCrossFade && template.SupportsFadeKeyword);
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct BuildDrawPartitions : IJob
        {
            [ReadOnly] public NativeBitSet Templates;
            [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<TemplateVisibilitySummary> TemplateSummaries;
            [ReadOnly] public NativeArray<CullingChunkIndex> OrderedVisibleChunks;
            [ReadOnly] public NativeArray<byte> OrderedVisibleChunkStateMasks;
            [ReadOnly] public NativeArray<ArchetypeIndex> ChunkArchetypes;
            [ReadOnly] public NativeArray<int> ChunkCounts;
            [ReadOnly] public NativeArray<DrawVisibilityMask> ChunkVisibility;

            [NativeDisableContainerSafetyRestriction, NoAlias] [WriteOnly] public NativeArray<int> OrderedVisibleChunkDrawPartitions;
            [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<PartitionSummary> Partitions;
            [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<byte> PartitionStateSplitMasks;

            public void Execute()
            {
                using var sortedLightmapIndices = new NativeList<int>(math.max(1, OrderedVisibleChunks.Length), Allocator.Temp);

                foreach (TemplateIndex template in Templates.AsType<TemplateIndex>())
                {
                    DebugThrowIf(template.Index > 0 && template.Index < TemplateSummaries.Length, "Allocated template index is out of range while building draw partitions.");

                    TemplateVisibilitySummary templateSummary = TemplateSummaries[template];
                    int orderedChunkOffset = templateSummary.OrderedChunkOffset;
                    int partitionSummaryOffset = templateSummary.PartitionSummaryOffset;
                    int orderedChunkCount = templateSummary.VisibleChunkCount;
                    if (orderedChunkCount == 0)
                        continue;
                    DebugThrowIf(
                        orderedChunkOffset >= 0 &&
                        orderedChunkOffset + orderedChunkCount <= OrderedVisibleChunks.Length,
                        "Template visible chunk range exceeds ordered chunk buffer.");

                    sortedLightmapIndices.Clear();

                    for (int i = 0; i < orderedChunkCount; i++)
                    {
                        CullingChunkIndex chunk = OrderedVisibleChunks[orderedChunkOffset + i];
                        DebugThrowIf(chunk.Index >= 0 && chunk.Index < ChunkArchetypes.Length, "Ordered visible chunk index is out of range while collecting lightmap partitions.");

                        DrawStateUtility.InsertSortedUniqueLightmapIndex(sortedLightmapIndices, ChunkArchetypes[chunk].Key.LightmapIndex);
                    }

                    int partitionCount = sortedLightmapIndices.Length;
                    templateSummary.DrawPartitionCount = partitionCount;
                    TemplateSummaries[template] = templateSummary;
                    DebugThrowIf(
                        partitionSummaryOffset >= 0 &&
                        partitionSummaryOffset + partitionCount <= Partitions.Length &&
                        DrawStateUtility.ComputePartitionStateOffset(partitionSummaryOffset, partitionCount) <= PartitionStateSplitMasks.Length,
                        "Template draw partition range exceeds partition layout buffers.");

                    for (int partitionIndex = 0; partitionIndex < partitionCount; partitionIndex++)
                    {
                        int partitionStorageIndex = partitionSummaryOffset + partitionIndex;
                        Partitions[partitionStorageIndex] = new PartitionSummary
                        {
                            LightmapIndex = sortedLightmapIndices[partitionIndex],
                            VisibleInstanceCount = 0,
                            CandidateStateMask = 0,
                            StateMask = 0,
                            StateIndices = 0,
                        };

                        int partitionStateOffset = DrawStateUtility.ComputePartitionStateOffset(partitionSummaryOffset, partitionIndex);
                        for (int stateKey = 0; stateKey < DrawStateUtility.StateKeyCount; stateKey++)
                            PartitionStateSplitMasks[partitionStateOffset + stateKey] = 0;
                    }

                    for (int i = 0; i < orderedChunkCount; i++)
                    {
                        int orderedChunkIndex = orderedChunkOffset + i;
                        CullingChunkIndex chunk = OrderedVisibleChunks[orderedChunkIndex];
                        DebugThrowIf(
                            chunk.Index >= 0 &&
                            chunk.Index < ChunkArchetypes.Length &&
                            chunk.Index < ChunkCounts.Length &&
                            chunk.Index < ChunkVisibility.Length,
                            "Ordered visible chunk index is out of range while assigning draw partitions.");

                        int partitionIndex = DrawStateUtility.FindSortedLightmapPartitionIndex(sortedLightmapIndices, ChunkArchetypes[chunk].Key.LightmapIndex);
                        if (partitionIndex < 0)
                            continue;

                        OrderedVisibleChunkDrawPartitions[orderedChunkIndex] = partitionIndex;
                        int partitionStorageIndex = partitionSummaryOffset + partitionIndex;
                        PartitionSummary partitionSummary = Partitions[partitionStorageIndex];
                        partitionSummary.VisibleInstanceCount += ChunkCounts[chunk];
                        partitionSummary.CandidateStateMask |= OrderedVisibleChunkStateMasks[orderedChunkIndex];
                        partitionSummary.StateMask = 0;
                        partitionSummary.StateIndices = 0;
                        Partitions[partitionStorageIndex] = partitionSummary;

                        DrawVisibilityMask chunkVisibility = ChunkVisibility[chunk];
                        int partitionStateOffset = DrawStateUtility.ComputePartitionStateOffset(partitionSummaryOffset, partitionIndex);
                        uint chunkStateMask = OrderedVisibleChunkStateMasks[orderedChunkIndex];
                        while (chunkStateMask != 0)
                        {
                            int stateKey = math.tzcnt(chunkStateMask);
                            chunkStateMask &= chunkStateMask - 1;
                            PartitionStateSplitMasks[partitionStateOffset + stateKey] |= chunkVisibility.SplitMask;
                        }
                    }
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        internal struct PlanDrawLayout : IJob
        {
            [ReadOnly] public DrawBinConfig BinConfig;
            [ReadOnly] public NativeBitSet Templates;
            [ReadOnly] public NativeArray<DrawBatch> DrawBatches;
            [ReadOnly] public NativeArray<BatchID> BatchIDs;
            [ReadOnly] public NativeArray<DrawRangeIndex> DrawBatchRangeIndices;
            [ReadOnly] public NativeBufferArray<DrawBatchIndex> TemplateDrawIndicesPerLod;
            [ReadOnly] public NativeArray<byte> PartitionStateSplitMasks;

            [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<TemplateVisibilitySummary> TemplateSummaries;
            [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<PartitionSummary> Partitions;
            [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<int> TemplateCommandCounts;
            [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<int> RangeCommandCounts;
            [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<int> RangeCommandOffsets;
            [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<CullingLayoutCounts> LayoutCounts;

            public void Execute()
            {
                CullingLayoutCounts counts = LayoutCounts[0];
                int drawPartitionOffset = 0;
                int visibleInstanceOffset = 0;
                int drawCommandOffset = 0;
                int drawBinOffset = 0;

                foreach (TemplateIndex template in Templates.AsType<TemplateIndex>())
                {
                    DebugThrowIf(template.Index > 0 && template.Index < TemplateSummaries.Length, "Allocated template index is out of range while planning draw layout.");

                    TemplateVisibilitySummary templateSummary = TemplateSummaries[template];
                    if (templateSummary.VisibleChunkCount == 0 || templateSummary.VisibleInstanceCount == 0)
                        continue;

                    int lodCount = template.LodCount;
                    int partitionSummaryOffset = templateSummary.PartitionSummaryOffset;
                    int drawPartitionCount = templateSummary.DrawPartitionCount;
                    if (drawPartitionCount == 0)
                        continue;
                    DebugThrowIf(
                        partitionSummaryOffset >= 0 &&
                        partitionSummaryOffset + drawPartitionCount <= Partitions.Length,
                        "Template draw partition range exceeds partition summary buffer.");

                    templateSummary.DrawPartitionOffset = drawPartitionOffset;

                    for (int lod = 0; lod < lodCount; lod++)
                    {
                        int templateLodIndex = template * CullingConstants.MaxLodCount + lod;
                        DebugThrowIf(templateLodIndex >= 0 && templateLodIndex < TemplateDrawIndicesPerLod.Length, "Template LOD draw index is out of range.");

                        NativeBuffer<DrawBatchIndex> drawIndices = TemplateDrawIndicesPerLod[templateLodIndex];
                        for (int i = 0; i < drawIndices.Length; i++)
                        {
                            DrawBatchIndex drawBatchIndex = drawIndices[i];
                            int drawBatchValue = drawBatchIndex;
                            DebugThrowIf(
                                drawBatchValue >= 0 &&
                                drawBatchValue < DrawBatches.Length &&
                                drawBatchValue < DrawBatchRangeIndices.Length,
                                "Template draw batch index is out of range.");

                            DrawBatch drawBatch = DrawBatches[drawBatchIndex];
                            DebugThrowIf(
                                drawBatch.Key.BatchDomainIndex.Index >= 0 &&
                                drawBatch.Key.BatchDomainIndex.Index < BatchIDs.Length,
                                "Draw batch references an invalid batch domain.");

                            BatchID batchID = BatchIDs[drawBatch.Key.BatchDomainIndex];
                            if (batchID == BatchID.Null)
                                continue;

                            DrawRangeIndex rangeIndex = DrawBatchRangeIndices[drawBatchIndex];
                            uint drawStateMask = drawBatch.Key.SupportedStateMask;
                            while (drawStateMask != 0)
                            {
                                int stateKey = math.tzcnt(drawStateMask);
                                drawStateMask &= drawStateMask - 1;

                                int templateCommandIndex = DrawStateUtility.ComputeTemplateLodStateIndex(template, lod, stateKey);
                                DebugThrowIf(templateCommandIndex >= 0 && templateCommandIndex < TemplateCommandCounts.Length, "Template command count index is out of range.");

                                TemplateCommandCounts[templateCommandIndex] += 1;

                                for (int partition = 0; partition < drawPartitionCount; partition++)
                                {
                                    int partitionStorageIndex = partitionSummaryOffset + partition;
                                    PartitionSummary partitionSummary = Partitions[partitionStorageIndex];
                                    if (((partitionSummary.CandidateStateMask >> stateKey) & 1) == 0)
                                        continue;

                                    int partitionStateOffset = DrawStateUtility.ComputePartitionStateOffset(partitionSummaryOffset, partition);
                                    DebugThrowIf(partitionStateOffset + stateKey < PartitionStateSplitMasks.Length, "Partition state split mask index is out of range.");

                                    int visibleSplitCount = math.countbits((uint)PartitionStateSplitMasks[partitionStateOffset + stateKey]);
                                    if (visibleSplitCount == 0)
                                        continue;

                                    int range = rangeIndex;
                                    DebugThrowIf(range >= 0 && range < RangeCommandCounts.Length, "Draw batch range index is out of range.");
                                    RangeCommandCounts[rangeIndex] += visibleSplitCount;

                                    partitionSummary.StateMask |= (byte)(1 << stateKey);
                                    Partitions[partitionStorageIndex] = partitionSummary;
                                }
                            }
                        }
                    }

                    for (int partition = 0; partition < drawPartitionCount; partition++)
                    {
                        int partitionStorageIndex = partitionSummaryOffset + partition;
                        PartitionSummary partitionSummary = Partitions[partitionStorageIndex];
                        uint partitionStateMask = partitionSummary.StateMask;
                        int slotsPerLod = math.countbits(partitionStateMask);

                        partitionSummary.BinOffset = drawBinOffset;
                        partitionSummary.CommandOffset = drawCommandOffset;
                        partitionSummary.VisibleInstanceOffset = visibleInstanceOffset;
                        partitionSummary.StateIndices = DrawStateUtility.CreateStateIndices(partitionStateMask);

                        if (partitionSummary.VisibleInstanceCount > 0 && partitionStateMask != 0)
                        {
                            int partitionStateOffset = DrawStateUtility.ComputePartitionStateOffset(partitionSummaryOffset, partition);
                            uint iterationMask = partitionStateMask;
                            while (iterationMask != 0)
                            {
                                int stateKey = math.tzcnt(iterationMask);
                                iterationMask &= iterationMask - 1;

                                DebugThrowIf(partitionStateOffset + stateKey < PartitionStateSplitMasks.Length, "Partition state split mask index is out of range while reserving output.");

                                int visibleSplitCount = math.countbits((uint)PartitionStateSplitMasks[partitionStateOffset + stateKey]);
                                if (visibleSplitCount == 0)
                                    continue;

                                for (int lod = 0; lod < lodCount; lod++)
                                {
                                    int templateCommandIndex = DrawStateUtility.ComputeTemplateLodStateIndex(template, lod, stateKey);
                                    DebugThrowIf(templateCommandIndex >= 0 && templateCommandIndex < TemplateCommandCounts.Length, "Template command count index is out of range while reserving output.");

                                    int commandCount = TemplateCommandCounts[templateCommandIndex];
                                    if (commandCount == 0)
                                        continue;

                                    visibleInstanceOffset += partitionSummary.VisibleInstanceCount * visibleSplitCount;
                                    drawCommandOffset += commandCount * visibleSplitCount;
                                }
                            }

                            drawBinOffset += DrawStateUtility.ComputePartitionBinStride(BinConfig.SplitCount, slotsPerLod, lodCount);
                        }

                        Partitions[partitionStorageIndex] = partitionSummary;
                        drawPartitionOffset += 1;
                    }

                    TemplateSummaries[template] = templateSummary;
                }

                int usedRangeCount = 0;
                int rangeCommandOffset = 0;
                for (int rangeIndex = 0; rangeIndex < RangeCommandCounts.Length; rangeIndex++)
                {
                    RangeCommandOffsets[rangeIndex] = rangeCommandOffset;
                    int rangeCommandCount = RangeCommandCounts[rangeIndex];
                    if (rangeCommandCount > 0)
                    {
                        usedRangeCount += 1;
                        rangeCommandOffset += rangeCommandCount;
                    }
                }

                counts.VisibilityBufferCapacity = visibleInstanceOffset;
                counts.DrawPartitionCount = drawPartitionOffset;
                counts.DrawCommandCount = drawCommandOffset;
                counts.DrawBinCount = drawBinOffset;
                counts.UsedDrawRangeCount = usedRangeCount;
                LayoutCounts[0] = counts;
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct ReorderIncludedInstanceBits : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> OrderedVisibleChunkSourceIndices;
            [ReadOnly] public NativeArray<ulong> SourceIncludedInstances;
            [WriteOnly] public NativeArray<ulong> OutputIncludedInstances;

            public void Execute(int index)
            {
                OutputIncludedInstances[index] = SourceIncludedInstances[OrderedVisibleChunkSourceIndices[index]];
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        internal struct WriteCullingOutputPerTemplate : IJobParallelFor
        {
            [ReadOnly] public DrawBinConfig BinConfig;
            [ReadOnly] public GraphicsBufferHandle VisibilityBufferHandle;
            [ReadOnly] public GraphicsBufferHandle DrawArgsBufferHandle;
            [ReadOnly] public NativeBitSet Templates;
            [ReadOnly] public NativeArray<DrawBatch> DrawBatches;
            [ReadOnly] public NativeArray<BatchID> BatchIDs;
            [ReadOnly] public NativeArray<ArchetypeIndex> ChunkArchetypes;
            [ReadOnly] public NativeArray<CullingChunkIndex> OrderedVisibleChunks;
            [ReadOnly] public NativeArray<byte> OrderedVisibleChunkStateMasks;
            [ReadOnly] public NativeArray<byte> OrderedVisibleChunkSplitMasks;
            [ReadOnly] public NativeArray<int> OrderedVisibleChunkDrawPartitions;
            [ReadOnly] public NativeBufferArray<DrawBatchIndex> TemplateDrawIndicesPerLod;
            [ReadOnly] public NativeArray<TemplateVisibilitySummary> TemplateSummaries;
            [ReadOnly] public NativeArray<PartitionSummary> Partitions;
            [ReadOnly] public NativeArray<byte> PartitionStateSplitMasks;
            [ReadOnly] public NativeArray<int> TemplateCommandCounts;

            [NativeDisableContainerSafetyRestriction] public NativeArray<IndirectCullingOutput> CullingViewOutput;

            public void Execute(int templateIndex)
            {
                if (!Templates.Contains(templateIndex))
                    return;

                TemplateVisibilitySummary templateSummary = TemplateSummaries[templateIndex];
                int orderedChunkCount = templateSummary.VisibleChunkCount;
                if (orderedChunkCount == 0)
                    return;

                IndirectCullingOutput indirectCullingOutput = CullingViewOutput[0];
                if (indirectCullingOutput.DrawChunkCount == 0)
                    return;

                int lodCount = new TemplateIndex(templateIndex).LodCount;

                int drawPartitionCount = templateSummary.DrawPartitionCount;
                if (drawPartitionCount == 0)
                    return;

                int partitionSummaryOffset = templateSummary.PartitionSummaryOffset;
                int drawPartitionOutputOffset = templateSummary.DrawPartitionOffset;
                for (int partition = 0; partition < drawPartitionCount; partition++)
                {
                    int partitionStorageIndex = partitionSummaryOffset + partition;
                    PartitionSummary partitionSummary = Partitions[partitionStorageIndex];
                    int partitionInstanceCount = partitionSummary.VisibleInstanceCount;
                    uint partitionStateMask = partitionSummary.StateMask;
                    uint stateIndices = partitionSummary.StateIndices;
                    int slotsPerLod = math.countbits(partitionStateMask);
                    int binOffset = partitionSummary.BinOffset;
                    int reservedCommandOffset = partitionSummary.CommandOffset;
                    int reservedVisibleInstanceOffset = partitionSummary.VisibleInstanceOffset;
                    int outputPartitionIndex = drawPartitionOutputOffset + partition;

                    indirectCullingOutput.DrawPartitions[outputPartitionIndex] = new IndirectDrawPartition
                    {
                        binOffset = (uint)binOffset,
                        slotsPerLod = (uint)slotsPerLod,
                        stateMask = partitionStateMask,
                        stateIndices = stateIndices,
                    };

                    if (partitionInstanceCount == 0 || partitionStateMask == 0)
                        continue;

                    int partitionStateOffset = DrawStateUtility.ComputePartitionStateOffset(partitionSummaryOffset, partition);
                    for (int split = 0; split < BinConfig.SplitCount; split++)
                    {
                        uint iterationMask = partitionStateMask;
                        while (iterationMask != 0)
                        {
                            int stateKey = math.tzcnt(iterationMask);
                            iterationMask &= iterationMask - 1;

                            byte splitMask = PartitionStateSplitMasks[partitionStateOffset + stateKey];
                            if (((splitMask >> split) & 1) == 0)
                                continue;

                            for (int lod = 0; lod < lodCount; lod++)
                            {
                                int drawCount = TemplateCommandCounts[DrawStateUtility.ComputeTemplateLodStateIndex(templateIndex, lod, stateKey)];
                                if (drawCount == 0)
                                    continue;

                                int stateSlot = DrawStateUtility.StateSlotFromKey(stateIndices, stateKey);
                                if (stateSlot < 0)
                                    continue;

                                int binIndex = DrawStateUtility.ComputeBinIndex(binOffset, split, slotsPerLod, lodCount, stateSlot, lod);
                                indirectCullingOutput.DrawBins[binIndex] = new IndirectDrawBin
                                {
                                    visibleStart = (uint)reservedVisibleInstanceOffset,
                                    visibleCount = 0,
                                    commandStart = (uint)reservedCommandOffset,
                                    commandCount = (uint)drawCount,
                                };

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                                if (indirectCullingOutput.DrawBinCapacities != null)
                                    indirectCullingOutput.DrawBinCapacities[binIndex] = partitionInstanceCount;
#endif

                                reservedVisibleInstanceOffset += partitionInstanceCount;
                                reservedCommandOffset += drawCount;
                            }
                        }
                    }
                }

                for (int partition = 0; partition < drawPartitionCount; partition++)
                {
                    int partitionStorageIndex = partitionSummaryOffset + partition;
                    PartitionSummary partitionSummary = Partitions[partitionStorageIndex];
                    int partitionLightmapIndex = partitionSummary.LightmapIndex;
                    uint partitionStateMask = partitionSummary.StateMask;
                    if (partitionStateMask == 0)
                        continue;

                    int binOffset = partitionSummary.BinOffset;
                    int partitionStateOffset = DrawStateUtility.ComputePartitionStateOffset(partitionSummaryOffset, partition);
                    int slotsPerLod = math.countbits(partitionStateMask);
                    uint stateIndices = partitionSummary.StateIndices;
                    uint stateIterationMask = partitionStateMask;
                    while (stateIterationMask != 0)
                    {
                        int stateKey = math.tzcnt(stateIterationMask);
                        stateIterationMask &= stateIterationMask - 1;

                        byte splitMask = PartitionStateSplitMasks[partitionStateOffset + stateKey];
                        if (splitMask == 0)
                            continue;

                        for (int split = 0; split < BinConfig.SplitCount; split++)
                        {
                            if (((splitMask >> split) & 1) == 0)
                                continue;

                            for (int lod = 0; lod < lodCount; lod++)
                            {
                                NativeBuffer<DrawBatchIndex> drawIndices = TemplateDrawIndicesPerLod[templateIndex * CullingConstants.MaxLodCount + lod];

                                int stateSlot = DrawStateUtility.StateSlotFromKey(stateIndices, stateKey);
                                if (stateSlot < 0)
                                    continue;

                                int drawBinIndex = DrawStateUtility.ComputeBinIndex(binOffset, split, slotsPerLod, lodCount, stateSlot, lod);
                                IndirectDrawBin drawBin = indirectCullingOutput.DrawBins[drawBinIndex];
                                if (drawBin.commandCount == 0)
                                    continue;

                                int commandIndex = (int)drawBin.commandStart;
                                for (int i = 0; i < drawIndices.Length; i++)
                                {
                                    DrawBatchIndex drawBatchIndex = drawIndices[i];
                                    DrawBatch drawBatch = DrawBatches[drawBatchIndex];
                                    BatchID batchID = BatchIDs[drawBatch.Key.BatchDomainIndex];
                                    if (batchID == BatchID.Null || ((drawBatch.Key.SupportedStateMask >> stateKey) & 1) == 0)
                                        continue;

                                    BatchDrawCommandFlags batchFlags = drawBatch.Key.Flags;
                                    if (((IndirectStateFlags)stateKey & IndirectStateFlags.HasFlippedWinding) != 0)
                                        batchFlags |= BatchDrawCommandFlags.FlipWinding;
                                    if (((IndirectStateFlags)stateKey & IndirectStateFlags.HasMotion) != 0)
                                        batchFlags |= BatchDrawCommandFlags.HasMotion;
                                    if (((IndirectStateFlags)stateKey & IndirectStateFlags.HasFadeKeyword) != 0)
                                        batchFlags |= BatchDrawCommandFlags.LODCrossFadeKeyword;

                                    indirectCullingOutput.DrawInfos[commandIndex] = new IndirectDrawInfo
                                    {
                                        indexCountPerInstance = drawBatch.MeshInfo.IndexCount,
                                        startIndex = drawBatch.MeshInfo.FirstIndex,
                                        baseVertexIndex = drawBatch.MeshInfo.BaseVertex,
                                        startInstance = drawBin.visibleStart,
                                    };

                                    indirectCullingOutput.DrawCommandInfos[commandIndex] = new IndirectDrawCommandInfo
                                    {
                                        BatchIndex = drawBatchIndex,
                                        Command = new BatchDrawCommandIndirect
                                        {
                                            flags = batchFlags,
                                            batchID = batchID,
                                            materialID = drawBatch.Key.MaterialID,
                                            splitVisibilityMask = (ushort)(1 << split),
                                            lightmapIndex = partitionLightmapIndex >= 0 ? (ushort)partitionLightmapIndex : unchecked((ushort)-1),
                                            sortingPosition = 0,
                                            visibleOffset = drawBin.visibleStart,
                                            meshID = drawBatch.Key.MeshID,
                                            topology = drawBatch.MeshInfo.Topology,
                                            visibleInstancesBufferHandle = VisibilityBufferHandle,
                                            indirectArgsBufferHandle = DrawArgsBufferHandle,
                                            indirectArgsBufferOffset = (uint)(commandIndex * (5 * sizeof(uint))),
                                        }
                                    };

                                    commandIndex++;
                                }
                            }
                        }
                    }
                }

                int orderedChunkOffset = templateSummary.OrderedChunkOffset;
                for (int i = 0; i < orderedChunkCount; i++)
                {
                    int orderedChunkIndex = orderedChunkOffset + i;
                    CullingChunkIndex chunk = OrderedVisibleChunks[orderedChunkIndex];
                    int localPartitionIndex = OrderedVisibleChunkDrawPartitions[orderedChunkIndex];
                    uint drawPartitionIndex = (uint)(drawPartitionOutputOffset + localPartitionIndex);
                    indirectCullingOutput.DrawChunks[orderedChunkOffset + i] = new IndirectDrawChunk(
                        ChunkArchetypes[chunk],
                        chunk,
                        OrderedVisibleChunkSplitMasks[orderedChunkIndex],
                        OrderedVisibleChunkStateMasks[orderedChunkIndex],
                        drawPartitionIndex);
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        internal struct BuildBatchCommands : IJob
        {
            [ReadOnly] public int UsedDrawRangeCount;
            [ReadOnly] public NativeArray<IndirectCullingOutput> CullingViewOutput;
            [ReadOnly] public NativeArray<DrawRangeIndex> DrawBatchRangeIndices;
            [ReadOnly] public NativeArray<DrawRangeKey> DrawRangeKeys;
            [ReadOnly] public NativeArray<int> RangeCommandCounts;
            [ReadOnly] public NativeArray<int> RangeCommandOffsets;
            public NativeArray<int> RangeCommandWriteCursors;

            [NativeDisableContainerSafetyRestriction] public NativeArray<BatchCullingOutputDrawCommands> BatchCullingOutput;

            public void Execute()
            {
                IndirectCullingOutput indirectCullingOutput = CullingViewOutput[0];
                BatchCullingOutputDrawCommands batchCullingOutput = BatchCullingOutput[0];

                batchCullingOutput.drawRanges = MemoryUtility.Allocate<BatchDrawRange>(UsedDrawRangeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                batchCullingOutput.indirectDrawCommands = MemoryUtility.Allocate<BatchDrawCommandIndirect>(indirectCullingOutput.DrawCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                for (int i = 0; i < indirectCullingOutput.DrawCount; i++)
                {
                    IndirectDrawCommandInfo indirectDrawCommandInfo = indirectCullingOutput.DrawCommandInfos[i];
                    if (indirectDrawCommandInfo.BatchIndex == DrawBatchIndex.None)
                        continue;

                    DrawRangeIndex rangeIndex = DrawBatchRangeIndices[indirectDrawCommandInfo.BatchIndex];
                    int outputIndex = RangeCommandWriteCursors[rangeIndex]++;
                    batchCullingOutput.indirectDrawCommands[outputIndex] = indirectDrawCommandInfo.Command;
                }

                int outRangeCount = 0;
                for (int rangeIndex = 0; rangeIndex < RangeCommandCounts.Length; rangeIndex++)
                {
                    int rangeCommandCount = RangeCommandCounts[rangeIndex];
                    if (rangeCommandCount == 0)
                        continue;

                    DrawRangeKey rangeKey = DrawRangeKeys[rangeIndex];
                    batchCullingOutput.drawRanges[outRangeCount++] = new BatchDrawRange
                    {
                        drawCommandsType = BatchDrawCommandType.Indirect,
                        drawCommandsBegin = (uint)RangeCommandOffsets[rangeIndex],
                        drawCommandsCount = (uint)rangeCommandCount,
                        filterSettings = new BatchFilterSettings
                        {
                            renderingLayerMask = rangeKey.RenderingLayerMask,
                            rendererPriority = rangeKey.RendererPriority,
                            layer = rangeKey.Layer,
                            batchLayer = BatchLayer.InstanceCullingIndirect,
                            motionMode = rangeKey.MotionMode,
                            shadowCastingMode = rangeKey.ShadowCastingMode,
                            receiveShadows = rangeKey.ReceiveShadows,
                            staticShadowCaster = rangeKey.StaticShadowCaster,
                        },
                    };
                }

                batchCullingOutput.drawRangeCount = outRangeCount;
                batchCullingOutput.indirectDrawCommandCount = indirectCullingOutput.DrawCount;
                BatchCullingOutput[0] = batchCullingOutput;
            }
        }
    }
}
