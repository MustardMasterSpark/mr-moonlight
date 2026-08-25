// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace MA.Flora
{
    internal unsafe partial struct CullingGrid : IDisposable
    {
        // ------------------------------------------------------------------------
        // Constants
        // ------------------------------------------------------------------------

        public const int MinCellLevel   = 4;
        public const int MaxCellLevel   = 12;

        public const int BlockDimLog2   = 3;
        public const int BlockDim       = 1 << BlockDimLog2;
        public const int BlockCellUlongCount = CellsPerBlock / 64;

        public const int CellsPerBlock        = BlockDim * BlockDim * BlockDim;
        public const int CellIndexInBlockMask = CellsPerBlock - 1;

        public const int MinBlockLevel  = MinCellLevel + BlockDimLog2;
        public const int MaxBlockLevel  = MaxCellLevel + BlockDimLog2;

        public const float MinCellSize  = 1 << (1 + MinCellLevel);
        public const float MaxCellSize  = 1 << (1 + MaxCellLevel);

        public const float MinBlockSize = 1 << (1 + MinBlockLevel);
        public const float MaxBlockSize = 1 << (1 + MaxBlockLevel);

        public static readonly int3 MinCellCoord = -65535;
        public static readonly int3 MaxCellCoord =  65535;

        // ------------------------------------------------------------------------
        // Cell Utilities
        // ------------------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CellLevelForSize(float size)
        {
            return math.max(math.floorlog2((int)size), MinCellLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CellSizeForLevel(int level)
        {
            return 1 << (1 + level);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RcpCellSizeForLevel(int level)
        {
            return math.rcp(CellSizeForLevel(level));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int4 LocationForPosition(float3 position, float size)
        {
            int level = CellLevelForSize(size);
            float rcpCellSize = RcpCellSizeForLevel(level);
            int3 cellCoord = (int3)math.clamp(math.floor(position * rcpCellSize), MinCellCoord, MaxCellCoord);
            return new int4(cellCoord, level);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int4 LocationForAABB(float3 center, float3 extent)
        {
            return LocationForPosition(center, math.cmax(extent) * 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 LocationPosition(int4 location)
        {
            float cellSize = CellSizeForLevel(location.w);
            return (float3)location.xyz * cellSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int4 LocalToLevel(int4 location, int level)
        {
            int delta = level - location.w;
            int4 result = location;
            result.w = level;

            if (delta > 0)      result.xyz >>= +delta;
            else if (delta < 0) result.xyz <<= -delta;

            return result;
        }

        // ------------------------------------------------------------------------
        // Block Utilities
        // ------------------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int4 CellToBlock(int4 cell)
        {
            return new int4(cell.xyz >> BlockDimLog2, cell.w + BlockDimLog2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 MinBlockCellCoord(int4 block)
        {
            return block.xyz << BlockDimLog2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 MaxBlockCellCoord(int4 block)
        {
            return (block.xyz + 1) << BlockDimLog2;
        }

        // ------------------------------------------------------------------------
        // Data
        // ------------------------------------------------------------------------

        private NativeDataReference<InstanceManager> m_InstanceManager;
        private NativeDataReference<InstanceBuffer> m_InstanceBuffer;
        private NativeDataReference<TemplateManager> m_TemplateManager;

        private NativeArray<float> m_RcpCellSizes;

        private int m_NextBlockIndex;
        private NativeBitSet m_BlockAllocated;
        private NativeBitSet m_BlockDataDirty;
        private NativeList<BlockIndex> m_FreeBlocks;
        private NativeParallelHashMap<BlockLocation, BlockIndex> m_BlockHash;
        private NativeArray<BlockLocation> m_BlockLocations;
        private NativeArray<BlockData> m_BlockData;
        private BlockIndex m_CachedBlockIndex;
        private BlockLocation m_CachedBlockLocation;

        private NativeBitSet m_CellAllocated;
        private NativeBitSet m_CellHeadersDirty;
        private NativeBufferArray<CullingChunkIndex> m_CellChunks;
        private NativeArray<int> m_CellInstanceCount;
        private CellIndex m_CachedCellIndex;
        private CellLocation m_CachedCellLocation;

        private int m_NextBucketIndex;
        private NativeBitSet m_BucketAllocated;
        private NativeList<CellBucketIndex> m_FreeBuckets;
        private NativeParallelHashMap<CellBucketKey, CellBucketIndex> m_BucketHash;
        private NativeArray<CellIndex> m_BucketCells;
        private NativeArray<ArchetypeIndex> m_BucketArchetypes;
        private NativeArray<int> m_BucketLodCounts;
        private NativeBufferArray<CullingChunkIndex> m_BucketChunks;
        private NativeBufferArray<CullingChunkIndex> m_BucketChunksWithFreeSlots;
        private CellBucketIndex m_CachedBucketIndex;
        private CellBucketKey m_CachedBucketKey;

        private int m_NextCullingChunkIndex;
        private NativeBitSet m_ChunkAllocated;
        private NativeBitSet m_ChunkDynamic;
        private NativeBitSet m_ChunkUncullable;
        private NativeBitSet m_ChunkInfoDirty;
        private NativeBitSet m_ChunkFlagsDirty;
        private NativeBitSet m_ChunkAttributesDirty;
        private NativeList<CullingChunkIndex> m_FreeChunks;
        private NativeArray<CellBucketIndex> m_ChunkBucket;
        private NativeArray<int> m_ChunkCount;
        private NativeArray<CellIndex> m_ChunkCell;
        private NativeArray<ArchetypeIndex> m_ChunkArchetype;
        private NativeArray<PackedCullingChunkBatch> m_ChunkBatch;
        private NativeArray<BatchDomainIndex> m_ChunkBatchDomain;
        private NativeArray<ulong> m_ChunkFlags;
        private NativeArray<int> m_ChunkIndirectPageIndex;
        private NativeArray<int> m_ChunkIndexInCellList;
        private NativeArray<int> m_ChunkIndexInTemplateList;
        private NativeArray<int> m_ChunkIndexInBucketList;
        private NativeArray<int> m_ChunkIndexInBucketFreeSlotList;

        private NativeArray<int> m_ChunkInstanceIndices;

        private NativeList<int> m_IndirectInstanceOffsets;
        private NativeList<int> m_FreeIndirectInstancePages;

        private uint m_ContentVersion;
        private uint m_ContentVersionApplied;
        private uint m_ContentVersionScheduled;

        private GraphicsBufferRef m_BlockDataBuffer;      // Buffer of block data for chunk/cell -> bounding box calculations
        private GraphicsBufferRef m_ChunkCellBuffer;      // Buffer of chunk cell indices
        private GraphicsBufferRef m_ChunkInfoBuffer;      // Buffer of chunk batch domain addresses
        private GraphicsBufferRef m_ChunkFlagBuffer;      // Buffer of chunk flags
        private GraphicsBufferRef m_ChunkBatchBuffer;     // Buffer of packed culling chunk data
        private GraphicsBufferRef m_ChunkAttributeBuffer; // Buffer of chunk attributes calculated on the GPU
        private GraphicsBufferRef m_IndirectOffsetBuffer; // Indirect instance offset buffer for all non-compressed chunks

        // Uploads
        private JobHandle m_PreDispatchHandle;
        private NativeList<BlockIndex> m_PendingBlockIndexUpdates;
        private NativeList<BlockData> m_PendingBlockDataUpdates;

        private NativeList<CullingChunkUpdatePacket> m_PendingChunkUpdatePackets;
        private NativeList<int> m_PendingChunkFlagIndices;
        private NativeList<ulong> m_PendingChunkFlagUpdates;
        private NativeList<int2> m_PendingChunkAttributesUpdates;

        private NativeList<CullingChunkIndex> m_QueuedIndirectChunks;
        private NativeList<uint> m_PendingIndirectPageUpdates;
        private NativeList<int> m_PendingIndirectOffsetUpdates;

        public NativeBitSet BlockAllocated => m_BlockAllocated;
        public NativeArray<BlockLocation> BlockLocations => m_BlockLocations;

        public NativeBitSet CellAllocated => m_CellAllocated;
        public NativeArray<int> CellInstanceCount => m_CellInstanceCount;
        public NativeBufferArray<CullingChunkIndex> CellChunks => m_CellChunks;

        public NativeBitSet ChunkAllocated => m_ChunkAllocated;
        public NativeArray<int> ChunkCount => m_ChunkCount;
        public NativeArray<CellIndex> ChunkCells => m_ChunkCell;
        public NativeArray<ArchetypeIndex> ChunkArchetypes => m_ChunkArchetype;
        public int AllocatedChunkCount => m_ChunkAllocated.MaxLength;
        public NativeArray<ulong> ChunkFlags => m_ChunkFlags;
        public NativeArray<int> ChunkInstanceIndices => m_ChunkInstanceIndices;

        public GraphicsBufferRef BlockDataBuffer => m_BlockDataBuffer;
        public GraphicsBufferRef ChunkCellBuffer => m_ChunkCellBuffer;
        public GraphicsBufferRef ChunkInfoBuffer => m_ChunkInfoBuffer;
        public GraphicsBufferRef ChunkFlagBuffer => m_ChunkFlagBuffer;
        public GraphicsBufferRef ChunkBatchBuffer => m_ChunkBatchBuffer;
        public GraphicsBufferRef ChunkAttributeBuffer => m_ChunkAttributeBuffer;
        public GraphicsBufferRef IndirectOffsetBuffer => m_IndirectOffsetBuffer;

        private readonly CullingGrid* Self => (CullingGrid*)UnsafeUtilityExtensions.AddressOf(this);

        public const int ChunkCapacity = 64;

        private const int IndirectPageSize  = 64;
        private const int IndirectPageMask  = IndirectPageSize - 1;
        private const int IndirectPageShift = 6;

        private const int InitialBlockCapacity  = 64;
        private const int InitialCellCapacity   = InitialBlockCapacity * CellsPerBlock;
        private const int InitialBucketCapacity = 64;
        private const int InitialCullingChunkCapacity = 256;
        private const int InitialCellInstanceCapacity = InitialCullingChunkCapacity * ChunkCapacity;
        private const int InitialInstanceBatchCapacity = 256;
        private const int InitialIndirectInstanceCapacity = InitialInstanceBatchCapacity * IndirectPageSize;

        private const int InitialInstanceIndexCapacity = InstanceManager.ChunkInitialInstanceCapacity;

        public void Initialize(InstanceContext instanceContext, FloraRuntimeResources resources)
        {
            CullingGridCompute.Initialize(resources);

            m_InstanceManager = instanceContext.InstanceManager;
            m_InstanceBuffer = instanceContext.InstanceBuffer;
            m_TemplateManager = instanceContext.TemplateManager;

            m_RcpCellSizes = new NativeArray<float>(MaxCellLevel + 1, Allocator.Persistent);
            for (int level = 0; level <= MaxCellLevel; level++)
                m_RcpCellSizes[level] = RcpCellSizeForLevel(level);

            m_NextBlockIndex = 1;
            m_BlockAllocated = new NativeBitSet(InitialBlockCapacity, Allocator.Persistent);
            m_BlockDataDirty = new NativeBitSet(InitialBlockCapacity, Allocator.Persistent);
            m_FreeBlocks = new NativeList<BlockIndex>(InitialBlockCapacity, Allocator.Persistent);
            m_BlockHash = new NativeParallelHashMap<BlockLocation, BlockIndex>(InitialBlockCapacity, Allocator.Persistent);
            m_BlockLocations = new NativeArray<BlockLocation>(InitialBlockCapacity, Allocator.Persistent);
            m_BlockData = new NativeArray<BlockData>(InitialBlockCapacity, Allocator.Persistent);
            m_CachedBlockIndex = BlockIndex.None;
            m_CachedBlockLocation = BlockLocation.None;

            m_CellAllocated = new NativeBitSet(InitialCellCapacity, Allocator.Persistent);
            m_CellChunks = new NativeBufferArray<CullingChunkIndex>(InitialCellCapacity, 0, Allocator.Persistent);
            m_CellInstanceCount = new NativeArray<int>(InitialCellCapacity, Allocator.Persistent);
            m_CellHeadersDirty = new NativeBitSet(InitialCellCapacity, Allocator.Persistent);
            m_CachedCellIndex = CellIndex.None;
            m_CachedCellLocation = CellLocation.None;

            m_NextBucketIndex = 1;
            m_BucketAllocated = new NativeBitSet(InitialBucketCapacity, Allocator.Persistent);
            m_FreeBuckets = new NativeList<CellBucketIndex>(InitialBucketCapacity, Allocator.Persistent);
            m_BucketHash = new NativeParallelHashMap<CellBucketKey, CellBucketIndex>(InitialBucketCapacity, Allocator.Persistent);
            m_BucketCells = new NativeArray<CellIndex>(InitialBucketCapacity, Allocator.Persistent);
            m_BucketArchetypes = new NativeArray<ArchetypeIndex>(InitialBucketCapacity, Allocator.Persistent);
            m_BucketLodCounts = new NativeArray<int>(InitialBucketCapacity, Allocator.Persistent);
            m_BucketChunks = new NativeBufferArray<CullingChunkIndex>(InitialBucketCapacity, 0, Allocator.Persistent);
            m_BucketChunksWithFreeSlots = new NativeBufferArray<CullingChunkIndex>(InitialBucketCapacity, 0, Allocator.Persistent);
            m_CachedBucketIndex = CellBucketIndex.None;
            m_CachedBucketKey = default;

            m_NextCullingChunkIndex = 1;
            m_ChunkAllocated = new NativeBitSet(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkDynamic = new NativeBitSet(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkInfoDirty = new NativeBitSet(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkFlagsDirty = new NativeBitSet(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkAttributesDirty = new NativeBitSet(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkUncullable = new NativeBitSet(InitialCullingChunkCapacity, Allocator.Persistent);
            m_FreeChunks = new NativeList<CullingChunkIndex>(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkBucket = new NativeArray<CellBucketIndex>(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkCount = new NativeArray<int>(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkCell = new NativeArray<CellIndex>(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkArchetype = new NativeArray<ArchetypeIndex>(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkBatch = new NativeArray<PackedCullingChunkBatch>(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkBatchDomain = new NativeArray<BatchDomainIndex>(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkFlags = new NativeArray<ulong>(InitialCullingChunkCapacity * (int)CullingFlagChannel.Count, Allocator.Persistent);
            m_ChunkIndirectPageIndex = new NativeArray<int>(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkIndexInCellList = new NativeArray<int>(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkIndexInTemplateList = new NativeArray<int>(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkIndexInBucketList = new NativeArray<int>(InitialCullingChunkCapacity, Allocator.Persistent);
            m_ChunkIndexInBucketFreeSlotList = new NativeArray<int>(InitialCullingChunkCapacity, Allocator.Persistent);

            m_ChunkInstanceIndices = new NativeArray<int>(InitialCellInstanceCapacity, Allocator.Persistent);

            m_IndirectInstanceOffsets = new NativeList<int>(InitialIndirectInstanceCapacity, Allocator.Persistent);
            m_FreeIndirectInstancePages = new NativeList<int>(InitialInstanceBatchCapacity, Allocator.Persistent);

            m_ContentVersion = 1;

            m_BlockDataBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, InitialBlockCapacity, sizeof(BlockData), "Flora.CullingGrid.BlockDataBuffer");
            m_ChunkCellBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, InitialCullingChunkCapacity, sizeof(uint), "Flora.CullingGrid.ChunkCellBuffer");
            m_ChunkInfoBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, InitialCullingChunkCapacity, sizeof(uint), "Flora.CullingGrid.ChunkInfoBuffer");
            m_ChunkFlagBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, InitialCullingChunkCapacity * 2 * (int)CullingFlagChannel.Count, sizeof(uint), "Flora.CullingGrid.ChunkFlagBuffer");
            m_ChunkBatchBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, InitialCullingChunkCapacity, sizeof(PackedCullingChunkBatch), "Flora.CullingGrid.ChunkBatchBuffer");
            m_ChunkAttributeBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, InitialCullingChunkCapacity, sizeof(PackedCullingChunkAttributes), "Flora.CullingGrid.ChunkAttributeBuffer");
            m_IndirectOffsetBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, InitialIndirectInstanceCapacity, sizeof(uint), "Flora.CullingGrid.IndirectOffsetBuffer");

            m_PendingBlockIndexUpdates = new NativeList<BlockIndex>(16, Allocator.Persistent);
            m_PendingBlockDataUpdates = new NativeList<BlockData>(16, Allocator.Persistent);

            m_PendingChunkUpdatePackets = new NativeList<CullingChunkUpdatePacket>(256, Allocator.Persistent);
            m_PendingChunkFlagIndices = new NativeList<int>(256, Allocator.Persistent);
            m_PendingChunkFlagUpdates = new NativeList<ulong>(256, Allocator.Persistent);
            m_PendingChunkAttributesUpdates = new NativeList<int2>(256, Allocator.Persistent);

            m_QueuedIndirectChunks = new NativeList<CullingChunkIndex>(256, Allocator.Persistent);
            m_PendingIndirectPageUpdates = new NativeList<uint>(256, Allocator.Persistent);
            m_PendingIndirectOffsetUpdates = new NativeList<int>(256, Allocator.Persistent);
        }

        public void Dispose()
        {
            m_PreDispatchHandle.Complete();

            m_RcpCellSizes.Dispose();

            m_BlockAllocated.Dispose();
            m_BlockDataDirty.Dispose();
            m_FreeBlocks.Dispose();
            m_BlockHash.Dispose();
            m_BlockLocations.Dispose();
            m_BlockData.Dispose();

            m_CellAllocated.Dispose();
            m_ChunkDynamic.Dispose();
            m_CellChunks.Dispose();
            m_CellInstanceCount.Dispose();
            m_CellHeadersDirty.Dispose();

            m_BucketAllocated.Dispose();
            m_FreeBuckets.Dispose();
            m_BucketHash.Dispose();
            m_BucketCells.Dispose();
            m_BucketArchetypes.Dispose();
            m_BucketLodCounts.Dispose();
            m_BucketChunks.Dispose();
            m_BucketChunksWithFreeSlots.Dispose();

            m_ChunkAllocated.Dispose();
            m_ChunkInfoDirty.Dispose();
            m_ChunkFlagsDirty.Dispose();
            m_ChunkAttributesDirty.Dispose();
            m_ChunkUncullable.Dispose();
            m_FreeChunks.Dispose();
            m_ChunkBucket.Dispose();
            m_ChunkCount.Dispose();
            m_ChunkCell.Dispose();
            m_ChunkArchetype.Dispose();
            m_ChunkBatch.Dispose();
            m_ChunkBatchDomain.Dispose();
            m_ChunkFlags.Dispose();
            m_ChunkIndirectPageIndex.Dispose();
            m_ChunkIndexInCellList.Dispose();
            m_ChunkIndexInTemplateList.Dispose();
            m_ChunkIndexInBucketList.Dispose();
            m_ChunkIndexInBucketFreeSlotList.Dispose();

            m_ChunkInstanceIndices.Dispose();

            m_IndirectInstanceOffsets.Dispose();
            m_FreeIndirectInstancePages.Dispose();

            m_BlockDataBuffer.Dispose();
            m_ChunkCellBuffer.Dispose();
            m_ChunkInfoBuffer.Dispose();
            m_ChunkFlagBuffer.Dispose();
            m_ChunkBatchBuffer.Dispose();
            m_ChunkAttributeBuffer.Dispose();
            m_IndirectOffsetBuffer.Dispose();

            m_PendingBlockIndexUpdates.Dispose();
            m_PendingBlockDataUpdates.Dispose();

            m_PendingChunkUpdatePackets.Dispose();
            m_PendingChunkFlagIndices.Dispose();
            m_PendingChunkFlagUpdates.Dispose();
            m_PendingChunkAttributesUpdates.Dispose();

            m_QueuedIndirectChunks.Dispose();
            m_PendingIndirectPageUpdates.Dispose();
            m_PendingIndirectOffsetUpdates.Dispose();
        }

        private uint FrameVersion => m_InstanceManager.ValueRO.FrameVersion;

        private void UpdateContentVersion()
        {
            m_ContentVersion = FrameVersion;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CellLocation GetLocationForAABB(AABB aabb)
        {
            int level = CellLevelForSize(math.cmax(aabb.Extent) * 2.0f);
            float rcpCellSize = m_RcpCellSizes[level];
            int4 cellCoord = (int4)math.floor(aabb.Center * rcpCellSize);
            return new CellLocation(new int4(cellCoord.xyz, level));
        }

        private void SetBlockDataDirty(BlockIndex block)
        {
            m_BlockDataDirty.Add(block);
            UpdateContentVersion();
        }

        public BlockIndex GetOrCreateBlock(BlockLocation blockLocation)
        {
            if (!blockLocation.IsValid())
                return BlockIndex.None;
            if (m_CachedBlockIndex != BlockIndex.None && m_CachedBlockLocation == blockLocation)
                return m_CachedBlockIndex;

            if (!m_BlockHash.TryGetValue(blockLocation, out BlockIndex block))
            {
                block = m_FreeBlocks.Length > 0 ? m_FreeBlocks.Pop() : new BlockIndex(m_NextBlockIndex++);
                if (block >= m_BlockLocations.Length)
                {
                    int newBlockCapacity = m_BlockLocations.Length * 2;
                    m_BlockAllocated.ReserveCapacity(newBlockCapacity);
                    m_BlockDataDirty.ReserveCapacity(newBlockCapacity);
                    m_BlockLocations.ResizeArraySafe(newBlockCapacity);
                    m_BlockData.ResizeArraySafe(newBlockCapacity);

                    int newCellCapacity = newBlockCapacity * CellsPerBlock;
                    m_CellAllocated.ReserveCapacity(newCellCapacity);
                    m_CellHeadersDirty.ReserveCapacity(newCellCapacity);
                    m_CellChunks.Resize(newCellCapacity);
                    m_CellInstanceCount.ResizeArraySafe(newCellCapacity);
                }

                m_BlockAllocated.Add(block);
                m_BlockHash[blockLocation] = block;
                m_BlockLocations[block] = blockLocation;
                m_BlockData[block] = new BlockData { position = blockLocation.Position, cellSize = blockLocation.CellSize };
                SetBlockDataDirty(block);
            }

            m_CachedBlockIndex = block;
            m_CachedBlockLocation = blockLocation;

            return block;
        }

        private void DestroyBlock(BlockIndex block)
        {
            m_BlockAllocated.Remove(block);
            m_BlockDataDirty.Remove(block);
            m_FreeBlocks.Add(block);

            BlockLocation blockLocation = m_BlockLocations[block];
            m_BlockHash.Remove(blockLocation);
            m_BlockLocations[block] = BlockLocation.None;
            m_BlockData[block] = default;

            if (m_CachedBlockIndex == block)
            {
                m_CachedBlockIndex = BlockIndex.None;
                m_CachedBlockLocation = BlockLocation.None;
            }
        }

        public CellIndex GetOrCreateCell(CellLocation cellLocation)
        {
            if (!cellLocation.IsValid())
                return CellIndex.None;
            if (m_CachedCellIndex != CellIndex.None && m_CachedCellLocation == cellLocation)
                return m_CachedCellIndex;

            BlockIndex block = GetOrCreateBlock(cellLocation.Block);
            CellIndex cell = new CellIndex(block * CellsPerBlock + cellLocation.GetIndexInBlock());
            m_CellAllocated.Add(cell);

            m_CachedCellIndex = cell;
            m_CachedCellLocation = cellLocation;

            return cell;
        }

        private void DestroyCell(CellIndex cell)
        {
            m_CellAllocated.Remove(cell);
            m_CellHeadersDirty.Remove(cell);

            m_CellChunks[cell].Clear();
            m_CellInstanceCount[cell] = 0;

            if (m_CachedCellIndex == cell)
            {
                m_CachedCellIndex = CellIndex.None;
                m_CachedCellLocation = CellLocation.None;
            }

            BlockIndex blockIndex = cell.BlockIndex;
            if (!m_CellAllocated.AnyInRange(cell.BlockIndex * CellsPerBlock, CellsPerBlock))
                DestroyBlock(blockIndex);
        }

        private void AddInstancesToBucket(CellBucketIndex bucket, int instanceCount)
        {
            CellIndex cell = m_BucketCells[bucket];
            int lodCount = m_BucketLodCounts[bucket];
            m_CellInstanceCount[cell] += instanceCount * lodCount;
        }

        private void RemoveInstancesFromBucket(CellBucketIndex bucket, int instanceCount)
        {
            CellIndex cell = m_BucketCells[bucket];
            int lodCount = m_BucketLodCounts[bucket];
            Assert.IsTrue(m_CellInstanceCount[cell] >= instanceCount * lodCount, "Removing more instances than exist in cell");
            m_CellInstanceCount[cell] -= instanceCount * lodCount;
        }

        #region Instance Data Helpers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ChunkIndex GetDataChunkIndexForInstance(int instanceIndex)
        {
            return new ChunkIndex(instanceIndex >> InstanceManager.ChunkShift);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static InstanceInChunk GetDataInstanceInChunkForInstance(int instanceIndex)
        {
            ChunkIndex chunk = new ChunkIndex(instanceIndex >> InstanceManager.ChunkShift);
            int indexInChunk = instanceIndex & InstanceManager.ChunkMask;
            return new InstanceInChunk { Chunk = chunk, IndexInChunk = indexInChunk };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBatchOffsetForInstance(int instanceIndex)
        {
            InstanceInChunk instanceInChunk = GetDataInstanceInChunkForInstance(instanceIndex);
            BatchAllocation batchAllocation = instanceInChunk.Chunk.BatchAllocation;
            return batchAllocation.Offset + instanceInChunk.IndexInChunk;
        }

        #endregion

        #region Indirect Instances

        private void FreeIndirectPage(CullingChunkIndex chunk)
        {
            int indirectPage = m_ChunkIndirectPageIndex[chunk];
            if (indirectPage != -1)
            {
                m_FreeIndirectInstancePages.Add(indirectPage);
                m_ChunkIndirectPageIndex[chunk] = -1;
            }
        }

        private int AllocateIndirectPage(CullingChunkIndex chunk)
        {
            int indirectPage = m_ChunkIndirectPageIndex[chunk];
            if (indirectPage == -1)
            {
                if (m_FreeIndirectInstancePages.Length > 0)
                {
                    indirectPage = m_FreeIndirectInstancePages.Pop();
                }
                else
                {
                    indirectPage = m_IndirectInstanceOffsets.Length >> IndirectPageShift;
                    int newSize = m_IndirectInstanceOffsets.Length + IndirectPageSize;
                    m_IndirectInstanceOffsets.Resize(newSize, NativeArrayOptions.ClearMemory);
                }

                m_ChunkIndirectPageIndex[chunk] = indirectPage;
            }

            return indirectPage;
        }

        #endregion

        #region Buckets

        private CellBucketIndex GetOrCreateCellBucket(ArchetypeIndex archetype, CellIndex cell)
        {
            if (archetype == ArchetypeIndex.None || cell == CellIndex.None)
                return CellBucketIndex.None;
            if (m_CachedBucketIndex != CellBucketIndex.None && m_CachedBucketKey.Archetype == archetype && m_CachedBucketKey.Cell == cell)
                return m_CachedBucketIndex;

            CellBucketKey key = new CellBucketKey { Archetype = archetype, Cell = cell };
            if (!m_BucketHash.TryGetValue(key, out CellBucketIndex bucket))
            {
                bucket = m_FreeBuckets.Length > 0 ? m_FreeBuckets.Pop() : new CellBucketIndex(m_NextBucketIndex++);
                if (bucket >= m_BucketCells.Length)
                {
                    int newCapacity = math.max(256, m_BucketCells.Length * 2);
                    m_BucketAllocated.ReserveCapacity(newCapacity);
                    m_BucketCells.ResizeArraySafe(newCapacity);
                    m_BucketArchetypes.ResizeArraySafe(newCapacity);
                    m_BucketLodCounts.ResizeArraySafe(newCapacity);
                    m_BucketChunks.Resize(newCapacity);
                    m_BucketChunksWithFreeSlots.Resize(newCapacity);
                }

                m_BucketAllocated.Add(bucket);
                m_BucketHash[key] = bucket;
                m_BucketCells[bucket] = cell;
                m_BucketArchetypes[bucket] = archetype;
                m_BucketLodCounts[bucket] = archetype.Key.Template.LodCount;
            }

            m_CachedBucketIndex = bucket;
            m_CachedBucketKey = key;

            return bucket;
        }

        private void FreeCellBucket(CellBucketIndex bucket)
        {
            m_BucketAllocated.Remove(bucket);
            m_FreeBuckets.Add(bucket);
            m_BucketHash.Remove(new CellBucketKey(m_BucketArchetypes[bucket], m_BucketCells[bucket]));
            m_BucketCells[bucket] = CellIndex.None;
            m_BucketArchetypes[bucket] = ArchetypeIndex.None;
            m_BucketLodCounts[bucket] = 0;
            m_BucketChunks[bucket].Clear();
            m_BucketChunksWithFreeSlots[bucket].Clear();

            if (m_CachedBucketIndex == bucket)
            {
                m_CachedBucketIndex = CellBucketIndex.None;
                m_CachedBucketKey = default;
            }
        }

        private void BucketAddChunk(CellBucketIndex bucket, CullingChunkIndex chunk)
        {
            ArchetypeIndex archetype = m_BucketArchetypes[bucket];

            NativeBuffer<CullingChunkIndex> bucketChunkList = m_BucketChunks[bucket];
            m_ChunkIndexInBucketList[chunk] = bucketChunkList.Length;
            bucketChunkList.Add(chunk);
            m_ChunkBucket[chunk] = bucket;
            m_ChunkArchetype[chunk] = archetype;
            m_ChunkBatchDomain[chunk] = archetype.Key.Template.BatchDomainIndex;

            TemplateIndex template = archetype.Key.Template;
            Assert.IsTrue(template != TemplateIndex.None, "Archetype must have a valid template");
            m_TemplateManager.ValueRW.AddCullingChunk(template, chunk, m_ChunkIndexInTemplateList);
        }

        private void BucketRemoveChunk(CellBucketIndex bucket, CullingChunkIndex chunk)
        {
            ArchetypeIndex archetype = m_BucketArchetypes[bucket];

            NativeBuffer<CullingChunkIndex> bucketChunkList = m_BucketChunks[bucket];
            int indexInBucket = m_ChunkIndexInBucketList[chunk];
            bucketChunkList.RemoveAtSwapBack(indexInBucket);

            if (indexInBucket < bucketChunkList.Length)
            {
                CullingChunkIndex movedChunk = bucketChunkList[indexInBucket];
                m_ChunkIndexInBucketList[movedChunk] = indexInBucket;
            }

            TemplateIndex template = archetype.Key.Template;
            Assert.IsTrue(template != TemplateIndex.None, "Archetype must have a valid template");
            m_TemplateManager.ValueRW.RemoveCullingChunk(template, chunk, m_ChunkIndexInTemplateList);

            m_ChunkIndexInBucketList[chunk] = -1;
            m_ChunkBucket[chunk] = CellBucketIndex.None;
            m_ChunkArchetype[chunk] = ArchetypeIndex.None;
            m_ChunkBatchDomain[chunk] = BatchDomainIndex.None;

            if (bucketChunkList.Length == 0)
                FreeCellBucket(bucket);
        }

        private void BucketAddChunkToFreeSlots(CellBucketIndex bucket, CullingChunkIndex chunk)
        {
            if (m_ChunkIndexInBucketFreeSlotList[chunk] == -1)
            {
                NativeBuffer<CullingChunkIndex> archetypeFreeSlotList = m_BucketChunksWithFreeSlots[bucket];
                m_ChunkIndexInBucketFreeSlotList[chunk] = archetypeFreeSlotList.Length;
                archetypeFreeSlotList.Add(chunk);
            }
        }

        private void BucketRemoveChunkWithFreeSlots(CellBucketIndex bucket, CullingChunkIndex chunk)
        {
            int indexInFreeSlotList = m_ChunkIndexInBucketFreeSlotList[chunk];
            if (indexInFreeSlotList != -1)
            {
                NativeBuffer<CullingChunkIndex> archetypeFreeSlotList = m_BucketChunksWithFreeSlots[bucket];
                archetypeFreeSlotList.RemoveAtSwapBack(indexInFreeSlotList);

                if (indexInFreeSlotList < archetypeFreeSlotList.Length)
                {
                    CullingChunkIndex movedChunk = archetypeFreeSlotList[indexInFreeSlotList];
                    m_ChunkIndexInBucketFreeSlotList[movedChunk] = indexInFreeSlotList;
                }

                m_ChunkIndexInBucketFreeSlotList[chunk] = -1;
            }
        }

        #endregion

        #region Chunks

        private void SetChunkInfoDirty(CullingChunkIndex chunk)
        {
            m_ChunkInfoDirty.Add(chunk);
            m_ChunkFlagsDirty.Add(chunk);
            m_ChunkAttributesDirty.Add(chunk);
            UpdateContentVersion();
        }

        private void SetChunkFlagsDirty(CullingChunkIndex chunk)
        {
            m_ChunkFlagsDirty.Add(chunk);
            UpdateContentVersion();
        }

        private void SetChunkAttributesDirty(CullingChunkIndex chunk)
        {
            m_ChunkAttributesDirty.Add(chunk);
            UpdateContentVersion();
        }

        internal void UpdateChunkBatchDomain(CullingChunkIndex chunk, BatchDomainIndex batchDomainIndex)
        {
            if (chunk == CullingChunkIndex.None || !m_ChunkAllocated.Contains(chunk))
                return;
            if (m_ChunkBatchDomain[chunk] == batchDomainIndex)
                return;

            m_ChunkBatchDomain[chunk] = batchDomainIndex;
            SetChunkInfoDirty(chunk);
        }

        internal BatchDomainIndex GetChunkBatchDomain(CullingChunkIndex chunk)
        {
            return m_ChunkBatchDomain[chunk];
        }

        private CullingChunkIndex AllocateChunk()
        {
            CullingChunkIndex newChunk = m_FreeChunks.Length > 0 ? m_FreeChunks.Pop() : new CullingChunkIndex(m_NextCullingChunkIndex++);
            if (newChunk >= m_ChunkBucket.Length)
            {
                int newCapacity = m_ChunkBucket.Length * 2;
                m_ChunkAllocated.ReserveCapacity(newCapacity);
                m_ChunkDynamic.ReserveCapacity(newCapacity);
                m_ChunkInfoDirty.ReserveCapacity(newCapacity);
                m_ChunkAttributesDirty.ReserveCapacity(newCapacity);
                m_ChunkUncullable.ReserveCapacity(newCapacity);
                m_ChunkBucket.ResizeArraySafe(newCapacity);
                m_ChunkCount.ResizeArraySafe(newCapacity);
                m_ChunkCell.ResizeArraySafe(newCapacity);
                m_ChunkArchetype.ResizeArraySafe(newCapacity);
                m_ChunkBatch.ResizeArraySafe(newCapacity);
                m_ChunkBatchDomain.ResizeArraySafe(newCapacity);
                m_ChunkFlags.ResizeArraySafe(newCapacity * (int)CullingFlagChannel.Count);
                m_ChunkIndirectPageIndex.ResizeArraySafe(newCapacity);
                m_ChunkIndexInCellList.ResizeArraySafe(newCapacity);
                m_ChunkIndexInTemplateList.ResizeArraySafe(newCapacity);
                m_ChunkIndexInBucketList.ResizeArraySafe(newCapacity);
                m_ChunkIndexInBucketFreeSlotList.ResizeArraySafe(newCapacity);

                int newInstanceCapacity = newCapacity * ChunkCapacity;
                m_ChunkInstanceIndices.ResizeArraySafe(newInstanceCapacity);
            }

            m_ChunkAllocated.Add(newChunk);

            return newChunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int* GetInstanceIndicesInChunkRW(CullingChunkIndex chunk, int indexInChunk, int count)
            => (int*)m_ChunkInstanceIndices.GetSubArray(chunk.AsInstanceOffset() + indexInChunk, count).GetUnsafePtr();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly int* GetInstanceIndicesInChunkRO(CullingChunkIndex chunk, int indexInChunk, int count)
            => (int*)m_ChunkInstanceIndices.GetSubArray(chunk.AsInstanceOffset() + indexInChunk, count).GetUnsafeReadOnlyPtr();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref ulong GetChunkFlagChannelRW(CullingChunkIndex chunk, CullingFlagChannel channel)
            => ref *m_ChunkFlags.GetSubArray(chunk * (int)CullingFlagChannel.Count + (int)channel, 1).GetUnsafePtrT();

        private void CellAddChunk(CellIndex cell, CullingChunkIndex chunk)
        {
            if (cell == CellIndex.None)
                return;

            Assert.IsTrue(m_CellAllocated[cell]);
            NativeBuffer<CullingChunkIndex> cellChunks = m_CellChunks[cell];
            m_ChunkIndexInCellList[chunk] = cellChunks.Length;
            m_ChunkCell[chunk] = cell;
            cellChunks.Add(chunk);
        }

        private void CellRemoveChunk(CellIndex cell, CullingChunkIndex chunk)
        {
            if (cell == CellIndex.None)
                return;

            NativeBuffer<CullingChunkIndex> cellChunks = m_CellChunks[cell];
            int indexInCellList = m_ChunkIndexInCellList[chunk];
            cellChunks.RemoveAtSwapBack(indexInCellList);

            if (indexInCellList < cellChunks.Length)
            {
                CullingChunkIndex movedChunk = cellChunks[indexInCellList];
                m_ChunkIndexInCellList[movedChunk] = indexInCellList;
            }

            m_ChunkIndexInCellList[chunk] = -1;
            m_ChunkCell[chunk] = CellIndex.None;

            if (cellChunks.Length == 0)
            {
                DestroyCell(cell);
            }
        }

        private CullingChunkIndex GetCleanChunk(CellBucketIndex bucket)
        {
            CullingChunkIndex chunk = AllocateChunk();
            m_ChunkCount[chunk] = 0;
            m_ChunkCell[chunk] = CellIndex.None;
            m_ChunkArchetype[chunk] = ArchetypeIndex.None;
            m_ChunkBatchDomain[chunk] = BatchDomainIndex.None;
            m_ChunkBatch[chunk] = PackedCullingChunkBatch.None;
            m_ChunkFlags.MemClear(chunk * (int)CullingFlagChannel.Count, (int)CullingFlagChannel.Count);
            m_ChunkIndirectPageIndex[chunk] = -1;
            m_ChunkIndexInCellList[chunk] = -1;
            m_ChunkIndexInTemplateList[chunk] = -1;
            m_ChunkIndexInBucketList[chunk] = -1;
            m_ChunkIndexInBucketFreeSlotList[chunk] = -1;

            BucketAddChunk(bucket, chunk);
            BucketAddChunkToFreeSlots(bucket, chunk);
            CellAddChunk(m_BucketCells[bucket], chunk);

            return chunk;
        }

        private void ReleaseChunk(CellBucketIndex bucket, CullingChunkIndex chunk)
        {
            m_ChunkDynamic.Remove(chunk);
            m_ChunkUncullable.Remove(chunk);

            // Do NOT upload null chunks
            m_ChunkInfoDirty.Remove(chunk);
            m_ChunkFlagsDirty.Remove(chunk);
            m_ChunkAttributesDirty.Remove(chunk);

            FreeIndirectPage(chunk);
            CellRemoveChunk(m_BucketCells[bucket], chunk);

            if (m_ChunkCount[chunk] < ChunkCapacity)
                BucketRemoveChunkWithFreeSlots(bucket, chunk);

            BucketRemoveChunk(bucket, chunk);

            m_ChunkCount[chunk] = 0;
            m_ChunkAllocated.Remove(chunk);
            m_FreeChunks.Add(chunk);
        }

        private CullingChunkIndex GetChunkWithFreeSlots(CellBucketIndex bucket)
        {
            NativeBuffer<CullingChunkIndex> chunksWithEmptySlots = m_BucketChunksWithFreeSlots[bucket];
            CullingChunkIndex chunk = chunksWithEmptySlots.Length == 0 ? GetCleanChunk(bucket) : chunksWithEmptySlots[0];

            Assert.IsTrue(chunk != CullingChunkIndex.None);
            Assert.IsTrue(m_ChunkCount[chunk] < ChunkCapacity);

            return chunk;
        }

        private void SetChunkCount(CellBucketIndex bucket, CullingChunkIndex chunk, int newCount)
        {
            Assert.AreNotEqual(newCount, m_ChunkCount[chunk]);
            Assert.IsTrue(newCount is >= 0 and <= ChunkCapacity);

            if (newCount == 0)
            {
                ReleaseChunk(bucket, chunk);
            }
            else
            {
                int oldCount = m_ChunkCount[chunk];
                if (newCount == ChunkCapacity)
                {
                    BucketRemoveChunkWithFreeSlots(bucket, chunk);
                }
                else if (oldCount == ChunkCapacity)
                {
                    Assert.IsTrue(newCount < oldCount);
                    BucketAddChunkToFreeSlots(bucket, chunk);
                }

                m_ChunkCount[chunk] = newCount;
                SetChunkInfoDirty(chunk);
            }
        }

        #endregion

        #region Cell Instances

        private struct InstanceBatchInCullingChunk
        {
            public static InstanceBatchInCullingChunk Empty => default;
            public CullingChunkIndex Chunk;
            public int Start;
            public int Count;
        }

        private InstanceBatchInCullingChunk GetFirstInstanceBatchInChunk(int instanceOffset, int count)
        {
            NativeArray<InstanceInCullingChunk> instanceInCullingChunks = m_InstanceManager.ValueRO.InstanceInCullingChunks;
            InstanceInCullingChunk instanceInCullingChunk = instanceInCullingChunks[instanceOffset];
            CullingChunkIndex chunk = instanceInCullingChunk.Chunk;
            int indexInChunk = instanceInCullingChunk.IndexInChunk;

            int batchCount = 0;
            for (; batchCount < count; batchCount++)
            {
                int instanceIndex = instanceOffset + batchCount;
                InstanceInCullingChunk instanceInChunk = instanceInCullingChunks[instanceIndex];
                if (instanceInChunk.Chunk != chunk || instanceInChunk.IndexInChunk != indexInChunk + batchCount)
                {
                    break;
                }
            }

            Assert.IsTrue(chunk == CullingChunkIndex.None || indexInChunk < m_ChunkCount[chunk], "Index in chunk out of range.");
            Assert.IsTrue(chunk == CullingChunkIndex.None || indexInChunk + batchCount <= m_ChunkCount[chunk], "Batch exceeds chunk count.");
            Assert.IsTrue(batchCount > 0, "Invalid batch count.");

            return new InstanceBatchInCullingChunk
            {
                Chunk = chunk,
                Count = batchCount,
                Start = indexInChunk
            };
        }

        private void AddInstancesToChunk(CellBucketIndex bucket, CullingChunkIndex chunk, int instanceOffset, int count)
        {
            Assert.IsTrue(chunk != CullingChunkIndex.None);
            int allocatedIndex = m_ChunkCount[chunk];
            int allocatedCount = math.min(ChunkCapacity - allocatedIndex, count);
            SetChunkCount(bucket, chunk, allocatedIndex + allocatedCount);
            AddInstancesToBucket(bucket, allocatedCount);

            int* chunkInstanceIndices = GetInstanceIndicesInChunkRW(chunk, allocatedIndex, allocatedCount);
            for (int i = 0; i < allocatedCount; i++)
                chunkInstanceIndices[i] = instanceOffset + i;

            NativeArray<InstanceInCullingChunk> instanceInCullingChunks = m_InstanceManager.ValueRW.InstanceInCullingChunks;
            for (int i = 0; i < allocatedCount; i++)
            {
                int instanceIndex = chunkInstanceIndices[i];
                instanceInCullingChunks[instanceIndex] = new InstanceInCullingChunk(chunk, allocatedIndex + i);
            }
        }

        private void RemoveInstancesFromChunk(CellBucketIndex bucket, InstanceBatchInCullingChunk batch)
        {
            CullingChunkIndex chunk = batch.Chunk;
            int batchCount = batch.Count;
            int indexInChunk = batch.Start;

            int chunkCount = m_ChunkCount[chunk];
            Assert.IsTrue(indexInChunk < chunkCount);
            Assert.IsTrue(batchCount > 0 && indexInChunk + batchCount <= chunkCount);

            NativeArray<InstanceInCullingChunk> instanceInCullingChunks = m_InstanceManager.ValueRW.InstanceInCullingChunks;
            int* chunkInstanceIndices = GetInstanceIndicesInChunkRO(chunk, indexInChunk, batchCount);

            // Clear instance mappings
            for (int i = 0; i < batchCount; i++)
                instanceInCullingChunks[chunkInstanceIndices[i]] = InstanceInCullingChunk.None;

            int tailCount = chunkCount - (indexInChunk + batchCount);
            int patchCount = math.min(batchCount, tailCount);
            if (patchCount > 0)
            {
                // Move tail instances to fill the gap
                int copyFromIndex = chunkCount - patchCount;
                int* srcInstanceIndices = GetInstanceIndicesInChunkRO(chunk, copyFromIndex, patchCount);
                int* dstInstanceIndices = GetInstanceIndicesInChunkRW(chunk, indexInChunk, patchCount);

                for (int i = 0; i < patchCount; i++)
                {
                    int instanceIndex = srcInstanceIndices[i];
                    InstanceInCullingChunk instanceInCullingChunk = instanceInCullingChunks[instanceIndex];
                    instanceInCullingChunk = new InstanceInCullingChunk(instanceInCullingChunk.Chunk, indexInChunk + i);
                    instanceInCullingChunks[instanceIndex] = instanceInCullingChunk;
                    dstInstanceIndices[i] = instanceIndex;
                }
            }

            int newChunkCount = chunkCount - batchCount;
            SetChunkCount(bucket, chunk, newChunkCount);
            RemoveInstancesFromBucket(bucket, batchCount);
        }

        private void RemoveInstancesFromChunk(CellBucketIndex bucket, CullingChunkIndex chunk, int indexInChunk, int count)
        {
            InstanceBatchInCullingChunk batch = new InstanceBatchInCullingChunk { Chunk = chunk, Start = indexInChunk, Count = count };
            RemoveInstancesFromChunk(bucket, batch);
        }

        private void AddInstancesToBucket(ArchetypeIndex archetype, CellIndex cell, int instanceOffset, int count)
        {
            CellBucketIndex bucket = GetOrCreateCellBucket(archetype, cell);

            while (count >= ChunkCapacity)
            {
                // First fill whole chunks
                CullingChunkIndex chunk = GetCleanChunk(bucket);
                AddInstancesToChunk(bucket, chunk, instanceOffset, ChunkCapacity);
                count -= ChunkCapacity;
                instanceOffset += ChunkCapacity;
            }

            while (count != 0)
            {
                // Then fill partial chunks
                CullingChunkIndex chunk = GetChunkWithFreeSlots(bucket);
                int unusedCount = ChunkCapacity - m_ChunkCount[chunk];
                int countToAllocate = math.min(count, unusedCount);
                AddInstancesToChunk(bucket, chunk, instanceOffset, countToAllocate);
                count -= countToAllocate;
                instanceOffset += countToAllocate;
            }
        }

        public void AddInstances(ArchetypeIndex archetype, int instanceOffset, int instanceCount)
        {
            if (instanceCount <= 0)
                return;

            Assert.IsTrue(archetype != ArchetypeIndex.None, "Archetype must be valid when adding instances to the culling grid.");
            Assert.IsTrue(archetype.Key.Template != TemplateIndex.None, "Archetype must have a valid template when adding instances to the culling grid.");

            CellIndex batchCell = CellIndex.None;
            CellLocation batchLocation = CellLocation.None;
            int batchCount = 0;
            int batchStart = -1;

            NativeArray<AABB> instanceAABBs = m_InstanceManager.ValueRO.InstanceAABBs;

            for (int i = 0; i < instanceCount; i++)
            {
                int instanceIndex = instanceOffset + i;
                CellLocation instanceLocation = GetLocationForAABB(instanceAABBs[instanceIndex]);

                if (instanceLocation != batchLocation)
                {
                    if (batchCount > 0)
                        AddInstancesToBucket(archetype, batchCell, batchStart, batchCount);

                    batchLocation = instanceLocation;
                    batchCell = GetOrCreateCell(instanceLocation);
                    batchStart = instanceIndex;
                    batchCount = 1;
                }
                else
                {
                    batchCount++;
                }
            }

            if (batchCount > 0)
                AddInstancesToBucket(archetype, batchCell, batchStart, batchCount);
        }

        public void RemoveInstances(int instanceOffset, int count)
        {
            int instanceIndex = 0;

            while (instanceIndex != count)
            {
                InstanceBatchInCullingChunk batch = GetFirstInstanceBatchInChunk(instanceOffset + instanceIndex, count - instanceIndex);
                if (batch.Chunk == CullingChunkIndex.None)
                {
                    instanceIndex += batch.Count;
                    continue;
                }

                RemoveInstancesFromChunk(m_ChunkBucket[batch.Chunk], batch);
                instanceIndex += batch.Count;
            }
        }

        /// <summary>
        /// Called when the parent InstanceManager removes instances from a ChunkIndex.
        /// Remaps surviving instance indices in the culling grid.
        /// </summary>
        internal void RemapInstanceIndices(int baseInstanceIndex, int srcIndex, int dstIndex, int count)
        {
            NativeArray<InstanceInCullingChunk> instanceInCullingChunks = m_InstanceManager.ValueRW.InstanceInCullingChunks;

            for (int i = 0; i < count; i++)
            {
                int srcInstanceIndex = baseInstanceIndex + srcIndex + i;
                int dstInstanceIndex = baseInstanceIndex + dstIndex + i;

                InstanceInCullingChunk srcInstanceInCullingChunk = instanceInCullingChunks[srcInstanceIndex];
                instanceInCullingChunks[dstInstanceIndex] = srcInstanceInCullingChunk;
                instanceInCullingChunks[srcInstanceIndex] = InstanceInCullingChunk.None;

                if (srcInstanceInCullingChunk.Chunk != CullingChunkIndex.None)
                {
                    int cullingInstanceIndex = srcInstanceInCullingChunk.Chunk.AsInstanceOffset() + srcInstanceInCullingChunk.IndexInChunk;
                    m_ChunkInstanceIndices[cullingInstanceIndex] = dstInstanceIndex;
                    SetChunkInfoDirty(srcInstanceInCullingChunk.Chunk);
                }
            }
        }

        #endregion
    }
}
