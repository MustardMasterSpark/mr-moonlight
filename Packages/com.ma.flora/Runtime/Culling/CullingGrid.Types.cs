// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal struct BlockLocation : IEquatable<BlockLocation>
    {
        public static readonly BlockLocation None = new BlockLocation();

        public int4 Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BlockLocation(int4 value)
        {
            Value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BlockLocation(int x, int y, int z, int level)
        {
            Value = new int4(x, y, z, level);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BlockLocation FromPosition(float3 position, float size)
        {
            int4 location = CullingGrid.LocationForPosition(position, size);
            return new BlockLocation(CullingGrid.CellToBlock(location));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BlockLocation FromAABB(float3 center, float3 extent)
        {
            int4 location = CullingGrid.LocationForAABB(center, extent);
            return new BlockLocation(CullingGrid.CellToBlock(location));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BlockLocation FromAABB(AABB aabb)
        {
            int4 location = CullingGrid.LocationForAABB(aabb.Center.xyz, aabb.Extent.xyz);
            return new BlockLocation(CullingGrid.CellToBlock(location));
        }

        public int3 Coords
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Value.xyz;
        }

        public int Level
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Value.w;
        }

        public float BlockSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => CullingGrid.CellSizeForLevel(Level);
        }

        public float CellSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => CullingGrid.CellSizeForLevel(Level - CullingGrid.BlockDimLog2);
        }

        public float3 Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => math.float3(Coords) * CullingGrid.CellSizeForLevel(Level);
        }

        public float3 Center
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Position + (BlockSize * 0.5f);
        }

        public AABB AABB
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                float blockSize = BlockSize;
                float3 center = (float3)Coords * blockSize + (blockSize * 0.5f);
                float3 extent = blockSize * 0.5f;
                return new AABB(center, extent);
            }
        }

        public AABB PaddedAABB
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // Padded by CellSize to account for instances at cell edges
                float blockSize = BlockSize;
                float cellSize = CellSize;
                float3 center = (float3)Coords * blockSize + (blockSize * 0.5f);
                float3 extent = (blockSize * 0.5f) + (cellSize * 0.5f);
                return new AABB(center, extent);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int3 GetLocalCellCoord(CellLocation location)
        {
            int3 minCellInBlock = CullingGrid.MinBlockCellCoord(Value);
            return location.Coords - minCellInBlock;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int3 GetLocalCellCoord(int indexInBlock)
        {
            const int localShiftX = CullingGrid.BlockDimLog2 * 0;
            const int localShiftY = CullingGrid.BlockDimLog2 * 1;
            const int localShiftZ = CullingGrid.BlockDimLog2 * 2;
            const int localMask   = CullingGrid.BlockDim - 1;

            return new int3
            {
                x = (indexInBlock >> localShiftX) & localMask,
                y = (indexInBlock >> localShiftY) & localMask,
                z = (indexInBlock >> localShiftZ) & localMask
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int3 GetLocalCellCoord(CellIndex cellIndex)
        {
            return GetLocalCellCoord(cellIndex.IndexInBlock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid() => Level is > 0 and <= CullingGrid.MaxBlockLevel;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(BlockLocation other) => Value.Equals(other.Value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => obj is BlockLocation other && Equals(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => Value.GetHashCode();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => Equals(None) ? "BlockLocation.None" : $"BlockLocation({Value})";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(BlockLocation left, BlockLocation right) => left.Value.Equals(right.Value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(BlockLocation left, BlockLocation right) => !left.Value.Equals(right.Value);
    }

    internal struct CellLocation : IEquatable<CellLocation>
    {
        public static readonly CellLocation None = new CellLocation();

        public int4 Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CellLocation(int4 value)
        {
            Value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CellLocation(int x, int y, int z, int level)
        {
            Value = new int4(x, y, z, level);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CellLocation FromPositionAndSize(float3 position, float size)
        {
            int4 location = CullingGrid.LocationForPosition(position, size);
            return new CellLocation(location);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CellLocation FromAABB(float3 center, float3 extent)
        {
            int4 location = CullingGrid.LocationForAABB(center, extent);
            return new CellLocation(location);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CellLocation FromAABB(AABB aabb)
        {
            return FromAABB(aabb.Center.xyz, aabb.Extent.xyz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CellLocation FromBlock(BlockLocation block, int indexInBlock)
        {
            int3 minCellCoord = CullingGrid.MinBlockCellCoord(block.Value);
            int3 localCoord = block.GetLocalCellCoord(indexInBlock);
            int4 location = new int4(minCellCoord.xyz + localCoord, block.Level - CullingGrid.BlockDimLog2);
            return new CellLocation(location);
        }

        public int3 Coords
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Value.xyz;
        }

        public int Level
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Value.w;
        }

        public float CellSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => CullingGrid.CellSizeForLevel(Level);
        }

        public BlockLocation Block
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new BlockLocation(CullingGrid.CellToBlock(Value));
        }

        public float3 Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => CullingGrid.LocationPosition(Value);
        }

        public AABB AABB
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                float cellSize = CellSize;
                float3 center = (float3)Value.xyz * cellSize + (cellSize * 0.5f);
                float3 extent = cellSize * 0.5f;
                return new AABB(center, extent);
            }
        }

        public AABB PaddedAABB
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // Cell center at xyz*size + 0.5*size, then pad by another 0.5*size on each side
                float cellSize = CellSize;
                float3 center = (float3)Value.xyz * cellSize + (cellSize * 0.5f);
                float3 extent = cellSize;// (0.5f cell + 0.5f padding) per axis
                return new AABB(center, extent);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int3 GetBlockLocalCellCoords()
        {
            return Block.GetLocalCellCoord(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetIndexInBlock()
        {
            int3 localCoords = GetBlockLocalCellCoords();
            return localCoords.x +
                   localCoords.y * CullingGrid.BlockDim +
                   localCoords.z * (CullingGrid.BlockDim * CullingGrid.BlockDim);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Distance(CellLocation a, CellLocation b)
        {
            if (a.Level != b.Level)
                return int.MaxValue;

            int3 delta = math.abs(a.Coords - b.Coords);
            return math.max(delta.x, math.max(delta.y, delta.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid() => Level is > 0 and <= CullingGrid.MaxCellLevel;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CellLocation other) => Value.Equals(other.Value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => obj is CellLocation other && Equals(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => Value.GetHashCode();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => Equals(None) ? "CellLocation.None" : $"CellLocation({Value})";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CellLocation left, CellLocation right) => left.Value.Equals(right.Value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CellLocation left, CellLocation right) => !left.Value.Equals(right.Value);
    }

    internal struct BlockIndex : IEquatable<BlockIndex>, IComparable<BlockIndex>
    {
        public static BlockIndex None => default;

        public int Index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BlockIndex(int index) => Index = index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(BlockIndex other) => Index.CompareTo(other.Index);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(BlockIndex other) => Index == other.Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => obj is BlockIndex other && Equals(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => Equals(None) ? "BlockIndex.None" : $"BlockIndex({Index})";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(BlockIndex index) => index.Index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(BlockIndex left, BlockIndex right) => left.Index == right.Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(BlockIndex left, BlockIndex right) => left.Index != right.Index;
    }

    internal struct CellIndex : IEquatable<CellIndex>, IComparable<CellIndex>
    {
        public static CellIndex None => default;
        public const int LocalIndexMask = CullingGrid.CellIndexInBlockMask;

        public int Index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CellIndex(int index) => Index = index;

        public int IndexInBlock
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Index & LocalIndexMask;
        }

        public BlockIndex BlockIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new BlockIndex(Index / CullingGrid.CellsPerBlock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(CellIndex other) => Index.CompareTo(other.Index);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CellIndex other) => Index == other.Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => obj is CellIndex other && Equals(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => Equals(None) ? "CellIndex.None" : $"CellIndex({Index})";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(CellIndex cellIndex) => cellIndex.Index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CellIndex left, CellIndex right) => left.Index == right.Index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CellIndex left, CellIndex right) => left.Index != right.Index;
    }

    internal struct CellBucketKey : IEquatable<CellBucketKey>
    {
        public ArchetypeIndex Archetype;
        public CellIndex Cell;

        public CellBucketKey(ArchetypeIndex archetype, CellIndex cell)
        {
            Archetype = archetype;
            Cell = cell;
        }

        public bool Equals(CellBucketKey other) => Archetype == other.Archetype && Cell == other.Cell;
        public override bool Equals(object obj) => obj is CellBucketKey other && Equals(other);
        public override int GetHashCode() => unchecked((Archetype.GetHashCode() * 397) ^ Cell.GetHashCode());
        public override string ToString() => $"CellBucketKey(A:{Archetype}, C:{Cell})";
    }

    internal struct CellBucketIndex : IEquatable<CellBucketIndex>
    {
        public static CellBucketIndex None => default;
        public int Index;

        public CellBucketIndex(int index) => Index = index;

        public static implicit operator int(CellBucketIndex x) => x.Index;
        public bool Equals(CellBucketIndex other) => Index == other.Index;
        public override bool Equals(object obj) => obj is CellBucketIndex other && Equals(other);
        public override int GetHashCode() => Index;
        public override string ToString() => Equals(None) ? "CellBucketIndex.None" : $"CellBucketIndex({Index})";
        public static bool operator ==(CellBucketIndex a, CellBucketIndex b) => a.Index == b.Index;
        public static bool operator !=(CellBucketIndex a, CellBucketIndex b) => a.Index != b.Index;
    }

    internal struct CullingChunkIndex : IEquatable<CullingChunkIndex>
    {
        public static CullingChunkIndex None => default;
        public const int InstancesPerChunkShift = 6; // log2(64)

        public int Index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CullingChunkIndex(int index) => Index = index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AsInstanceOffset() => Index << InstancesPerChunkShift;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(CullingChunkIndex x) => x.Index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CullingChunkIndex other) => Index == other.Index;
        public override bool Equals(object obj) => obj is CullingChunkIndex other && Equals(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => Equals(None) ? "CullingChunkIndex.None" : $"CullingChunkIndex({Index})";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CullingChunkIndex a, CullingChunkIndex b) => a.Index == b.Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CullingChunkIndex a, CullingChunkIndex b) => a.Index != b.Index;
    }

    internal struct InstanceInCullingChunk : IComparable<InstanceInCullingChunk>, IEquatable<InstanceInCullingChunk>
    {
        public static InstanceInCullingChunk None => default;

        private uint m_Data;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InstanceInCullingChunk(int chunk, int indexInChunk)
        {
            m_Data = ((uint)chunk << 6) | (uint)(indexInChunk & 0x3f);
        }

        public CullingChunkIndex Chunk
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new CullingChunkIndex((int)(m_Data >> 6));
        }

        public int IndexInChunk
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (int)(m_Data & 0x3f);
        }

        public int CompareTo(InstanceInCullingChunk other) => m_Data.CompareTo(other.m_Data);
        public override int GetHashCode() => (int)m_Data;
        public bool Equals(InstanceInCullingChunk other) => CompareTo(other) == 0;
        public override string ToString() => Equals(None) ? "InstanceInCullingChunk.None" : $"InstanceInCullingChunk({Chunk}:{IndexInChunk})";
    }

    [GenerateHLSL(PackingRules.Exact, false)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct BlockData
    {
        public Vector3 position;
        public float cellSize;
    }

    [GenerateHLSL(PackingRules.Exact, false)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct PackedCullingChunkInfo
    {
        public const int ArchetypeIndexBits   = 20;
        public const int BatchDomainIndexBits = 12;

        public const int ArchetypeIndexShift  = BatchDomainIndexBits;
        public const int BatchDomainIndexMask = (1 << BatchDomainIndexBits) - 1;

        public uint data;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PackedCullingChunkInfo Create(ArchetypeIndex archetypeIndex, BatchDomainIndex batchDomainIndex)
        {
            Assert.IsTrue(archetypeIndex.Index < (1 << ArchetypeIndexBits), "Archetype index out of range");
            Assert.IsTrue(batchDomainIndex.Index < (1 << BatchDomainIndexBits), "Batch domain index out of range");
            return new PackedCullingChunkInfo
            {
                data = ((uint)archetypeIndex.Index << ArchetypeIndexShift) | (uint)(batchDomainIndex.Index & BatchDomainIndexMask)
            };
        }
    }

    [GenerateHLSL(PackingRules.Exact, false)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct PackedCullingChunkBatch : IEquatable<PackedCullingChunkBatch>
    {
        public static PackedCullingChunkBatch None => default;

        public uint data;

        public const uint BatchCompressedFlag = 0x80000000;
        public const int  BatchMaxInstances   = CullingGrid.ChunkCapacity;

        public const uint IndirectOffsetMask = 0x00ffffff;
        public const int  IndirectOffsetBits = 24;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PackedCullingChunkBatch Indirect(uint indirectOffset, int indirectCount)
        {
            Assert.IsTrue(indirectOffset <= IndirectOffsetMask, "Indirect offset out of range");
            Assert.IsTrue(indirectCount is > 0 and <= BatchMaxInstances, "Indirect count out of range");
            return new PackedCullingChunkBatch
            {
                data = ((uint)indirectCount << IndirectOffsetBits) | indirectOffset
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PackedCullingChunkBatch Compressed(uint instanceOffsetStart)
        {
            return new PackedCullingChunkBatch
            {
                data = instanceOffsetStart | BatchCompressedFlag
            };
        }

        public bool Equals(PackedCullingChunkBatch other) => data == other.data;
        public override bool Equals(object obj) => obj is PackedCullingChunkBatch other && Equals(other);
        public override int GetHashCode() => (int)data;

        public override string ToString()
        {
            bool isCompressed = (data & BatchCompressedFlag) != 0;
            if (isCompressed)
            {
                int instanceOffsetStart = (int)(data & ~BatchCompressedFlag);
                return $"CullingChunkBatch<Compressed>(Offset={instanceOffsetStart}, Count={BatchMaxInstances})";
            }
            else
            {

                int indirectStart = (int)(data & IndirectOffsetMask);
                int indirectCount = (int)(data >> IndirectOffsetBits);
                return $"CullingChunkBatch<Indirect>(Offset={indirectStart}, Count={indirectCount})";
            }
        }
    }

    [GenerateHLSL(PackingRules.Exact, false)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct PackedCullingChunkAttributes
    {
        public uint4 data;
    }

    [GenerateHLSL(PackingRules.Exact, false)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct CullingChunkUpdatePacket
    {
        public uint chunkIndex;
        public uint cellIndex;
        public uint packedInfo;
        public uint packedBatch;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CullingChunkUpdatePacket Create(CullingChunkIndex chunk, CellIndex cell, PackedCullingChunkBatch batch, PackedCullingChunkInfo info)
        {
            return new CullingChunkUpdatePacket
            {
                chunkIndex = (uint)chunk.Index,
                cellIndex = (uint)cell.Index,
                packedInfo = info.data,
                packedBatch = batch.data,
            };
        }
    }

    [GenerateHLSL(PackingRules.Exact, false)]
    internal enum CullingFlagChannel : byte
    {
        FlippedWinding = 0,
        HasMotion      = 1,
#if UNITY_EDITOR
        EditorSelected = 2,
        EditorHidden   = 3,
        Count          = 4,
#else
        Count          = 2,
#endif
    }

}
