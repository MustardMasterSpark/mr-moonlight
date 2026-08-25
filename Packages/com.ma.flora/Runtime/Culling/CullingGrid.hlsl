// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_CULLING_GRID_INCLUDED
#define FLORA_CULLING_GRID_INCLUDED

#include "Packages/com.ma.flora/Runtime/Culling/CullingGrid.cs.hlsl"
#include "Packages/com.ma.flora/ShaderLibrary/Packing.hlsl"

//--------------------------------------------------------------------------------------------------
// Cells
//--------------------------------------------------------------------------------------------------

struct CellData
{
    uint blockIndex;
    BlockData blockData;
    uint3 localCellCoord;
    float3 localBoundsCenter;
    float3 localBoundsExtent;
    float maxInstanceRadius;
};

StructuredBuffer<BlockData> _BlockData;

CellData LoadCellData(uint cellIndex)
{
    CellData cellData;

    cellData.blockIndex = cellIndex >> 9; // / 512
    cellData.blockData  = _BlockData[cellData.blockIndex];

    // 8x8x8 grid of cells
    cellData.localCellCoord.x = (cellIndex     ) & 7; // % 8
    cellData.localCellCoord.y = (cellIndex >> 3) & 7; // / 8  % 8
    cellData.localCellCoord.z = (cellIndex >> 6) & 7; // / 64 % 8

    cellData.localBoundsCenter = cellData.localCellCoord * cellData.blockData.cellSize + (cellData.blockData.cellSize * 0.5).xxx;
    cellData.localBoundsExtent = cellData.blockData.cellSize; // Loose-size, so extent is equal to cell size
    cellData.maxInstanceRadius = cellData.blockData.cellSize * 0.5; // Max radius is half the cell size

    return cellData;
}

//--------------------------------------------------------------------------------------------------
// Chunk Info
//--------------------------------------------------------------------------------------------------

static const uint kCullingChunkArchetypeIndexBits   = 20;
static const uint kCullingChunkBatchDomainIndexBits = 12;
static const uint kCullingChunkArchetypeIndexShift  = kCullingChunkBatchDomainIndexBits;
static const uint kCullingChunkBatchDomainIndexMask = (1 << kCullingChunkBatchDomainIndexBits) - 1;

struct CullingChunkInfo
{
    uint archetypeIndex;
    uint batchDomainIndex;
};

StructuredBuffer<PackedCullingChunkInfo> _CullingChunkInfos;
StructuredBuffer<uint> _CullingChunkCells;

PackedCullingChunkInfo LoadPackedCullingChunkInfo(uint chunkIndex)
{
    return _CullingChunkInfos[chunkIndex];
}

CullingChunkInfo UnpackCullingChunkInfo(PackedCullingChunkInfo packed)
{
    CullingChunkInfo info;
    info.archetypeIndex   = packed.data >> kCullingChunkArchetypeIndexShift;
    info.batchDomainIndex = packed.data & kCullingChunkBatchDomainIndexMask;
    return info;
}

CullingChunkInfo LoadCullingChunkInfo(uint chunkIndex)
{
    PackedCullingChunkInfo packed = LoadPackedCullingChunkInfo(chunkIndex);
    return UnpackCullingChunkInfo(packed);
}

//--------------------------------------------------------------------------------------------------
// Chunk Flags
//--------------------------------------------------------------------------------------------------

static const uint kCullingChunkFlagChannel_FlippedWinding = 0;
static const uint kCullingChunkFlagChannel_HasMotion      = 1;
static const uint kCullingChunkFlagChannel_EditorSelected = 2;
static const uint kCullingChunkFlagChannel_EditorHidden   = 3;

struct CullingInstanceFlags
{
    bool hasFlippedWinding;
    bool hasMotion;
};

struct CullingInstanceEditorFlags
{
    bool isSelected;
    bool isHidden;
};

uint _CullingChunkFlagChannelCount;
StructuredBuffer<uint> _CullingChunkFlags;

uint GetCullingChunkFlagStride32()
{
    return _CullingChunkFlagChannelCount * 2;
}

bool IsCullingChunkFlagSet(uint chunkIndex, uint indexInGroup, uint flagChannel)
{
    uint wordIndex  = indexInGroup >> 5;
    uint bitIndex   = indexInGroup & 31;
    uint baseOffset = chunkIndex * GetCullingChunkFlagStride32() + flagChannel * 2;
    uint word       = _CullingChunkFlags[baseOffset + wordIndex];
    return (word & (1 << bitIndex)) != 0;
}

CullingInstanceFlags LoadCullingInstanceFlags(uint chunkIndex, uint indexInChunk)
{
    CullingInstanceFlags flags;
    flags.hasFlippedWinding = IsCullingChunkFlagSet(chunkIndex, indexInChunk, kCullingChunkFlagChannel_FlippedWinding);
    flags.hasMotion         = IsCullingChunkFlagSet(chunkIndex, indexInChunk, kCullingChunkFlagChannel_HasMotion);
    return flags;
}

CullingInstanceEditorFlags LoadCullingInstanceEditorFlags(uint chunkIndex, uint indexInChunk)
{
    CullingInstanceEditorFlags flags;
    flags.isSelected = IsCullingChunkFlagSet(chunkIndex, indexInChunk, kCullingChunkFlagChannel_EditorSelected);
    flags.isHidden   = IsCullingChunkFlagSet(chunkIndex, indexInChunk, kCullingChunkFlagChannel_EditorHidden);
    return flags;
}

// groupshared Flags

static const uint kMaxSharedCullingChunkFlagChannels = 4;
groupshared uint gs_CullingFlagsShared[kMaxSharedCullingChunkFlagChannels * 2]; // Support up to 4 flag channels

void InitSharedCullingFlags(uint chunkIndex)
{
    uint channelCount  = min(kMaxSharedCullingChunkFlagChannels, _CullingChunkFlagChannelCount);
    uint channelStride = GetCullingChunkFlagStride32();
    uint i;

    UNITY_UNROLL
    for (i = 0; i < kMaxSharedCullingChunkFlagChannels * 2; ++i)
        gs_CullingFlagsShared[i] = 0;

    UNITY_UNROLL
    for (i = 0; i < channelCount; ++i)
    {
        uint baseOffset = chunkIndex * channelStride + i * 2;
        gs_CullingFlagsShared[i * 2 + 0] = _CullingChunkFlags[baseOffset + 0];
        gs_CullingFlagsShared[i * 2 + 1] = _CullingChunkFlags[baseOffset + 1];
    }
}

bool IsSharedCullingFlagSet(uint groupIndex, uint flagChannel)
{
    uint wordIndex = groupIndex >> 5;
    uint bitIndex  = groupIndex & 31;
    uint word      = gs_CullingFlagsShared[flagChannel * 2 + wordIndex];
    return (word & (1 << bitIndex)) != 0;
}

CullingInstanceFlags LoadSharedCullingInstanceFlags(uint groupIndex)
{
    CullingInstanceFlags flags;
    flags.hasFlippedWinding = IsSharedCullingFlagSet(groupIndex, kCullingChunkFlagChannel_FlippedWinding);
    flags.hasMotion         = IsSharedCullingFlagSet(groupIndex, kCullingChunkFlagChannel_HasMotion);
    return flags;
}

CullingInstanceEditorFlags LoadSharedCullingInstanceEditorFlags(uint groupIndex)
{
    CullingInstanceEditorFlags flags;
    flags.isSelected = IsSharedCullingFlagSet(groupIndex, kCullingChunkFlagChannel_EditorSelected);
    flags.isHidden   = IsSharedCullingFlagSet(groupIndex, kCullingChunkFlagChannel_EditorHidden);
    return flags;
}

//--------------------------------------------------------------------------------------------------
// Indirect Instance Offsets
//--------------------------------------------------------------------------------------------------

static const uint kCullingIndirectOffsetMask = 0x00ffffffu; // Lower 24 bits for the offset
static const uint kCullingIndirectOffsetBits = 24;          // Upper 8 bits for the count

StructuredBuffer<uint> _CullingIndirectOffsets; // Non-compressed instance indices, ordered by page (not chunk)

//--------------------------------------------------------------------------------------------------
// Chunk Batches
//--------------------------------------------------------------------------------------------------

static const uint kCullingChunkMaxCount       = 64;          // Maximum number of instances in a chunk
static const uint kCullingBatchCompressedFlag = 0x80000000u; // MSB indicates whether the batch is compressed or not
static const uint kCullingBatchCompressedMask = 0x7fffffffu; // Remaining 31 bits for the instance start offset

StructuredBuffer<PackedCullingChunkBatch> _CullingChunkBatches;

struct CullingChunkBatch
{
    bool isCompressed;
    uint instanceStart;
    uint instanceCount;

    bool IsValidIndex(uint indexInChunk)
    {
        return indexInChunk < instanceCount;
    }

    uint LoadInstanceIndex(uint indexInChunk)
    {
        return isCompressed ? (instanceStart + indexInChunk) : _CullingIndirectOffsets[instanceStart + indexInChunk];
    }
};

CullingChunkBatch UnpackCullingChunkBatch(PackedCullingChunkBatch packed)
{
    CullingChunkBatch chunk;
    chunk.isCompressed = (packed.data & kCullingBatchCompressedFlag) != 0;
    if (chunk.isCompressed)
    {
        chunk.instanceStart = packed.data & kCullingBatchCompressedMask;
        chunk.instanceCount = kCullingChunkMaxCount;
    }
    else
    {
        chunk.instanceStart = packed.data & kCullingIndirectOffsetMask;
        chunk.instanceCount = packed.data >> kCullingIndirectOffsetBits;
    }
    return chunk;
}

PackedCullingChunkBatch LoadPackedCullingChunkBatch(uint chunkIndex)
{
    return _CullingChunkBatches[chunkIndex];
}

CullingChunkBatch LoadCullingChunkBatch(uint chunkIndex)
{
    PackedCullingChunkBatch packed = LoadPackedCullingChunkBatch(chunkIndex);
    return UnpackCullingChunkBatch(packed);
}

//--------------------------------------------------------------------------------------------------
// Chunk Attributes
//--------------------------------------------------------------------------------------------------

static const uint kCullingChunkAttributesBoundsBits = 8; // Number of bits used to pack bounds in chunk attributes

struct CullingChunkAttributes
{
    float3 boundsMin;
    float3 boundsMax;
};

StructuredBuffer<PackedCullingChunkAttributes> _CullingChunkAttributes;

PackedCullingChunkAttributes PackCullingChunkAttributes(CullingChunkAttributes attributes)
{
    PackedCullingChunkAttributes packed = (PackedCullingChunkAttributes)0;
    packed.data.xy = PackBoundsMinMaxToUInt(attributes.boundsMin, attributes.boundsMax, kCullingChunkAttributesBoundsBits);
    return packed;
}

CullingChunkAttributes UnpackCullingChunkAttributes(PackedCullingChunkAttributes packed, float boundsScale = 1.0)
{
    CullingChunkAttributes attributes = (CullingChunkAttributes)0;
    UnpackBoundsMinMaxFromUInt(packed.data.xy, kCullingChunkAttributesBoundsBits, boundsScale, attributes.boundsMin, attributes.boundsMax);
    return attributes;
}

CullingChunkAttributes LoadCullingChunkAttributes(uint cellChunkIndex, float boundsScale = 1.0)
{
    return UnpackCullingChunkAttributes(_CullingChunkAttributes[cellChunkIndex], boundsScale);
}

#endif

