//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef CULLINGGRID_CS_HLSL
#define CULLINGGRID_CS_HLSL
//
// MA.Flora.CullingFlagChannel:  static fields
//
#define CULLINGFLAGCHANNEL_FLIPPED_WINDING (0)
#define CULLINGFLAGCHANNEL_HAS_MOTION (1)
#define CULLINGFLAGCHANNEL_EDITOR_SELECTED (2)
#define CULLINGFLAGCHANNEL_EDITOR_HIDDEN (3)
#define CULLINGFLAGCHANNEL_COUNT (4)

//
// MA.Flora.PackedCullingChunkBatch:  static fields
//
#define BATCH_COMPRESSED_FLAG (2147483648)
#define BATCH_MAX_INSTANCES (64)
#define INDIRECT_OFFSET_MASK (16777215)
#define INDIRECT_OFFSET_BITS (24)

//
// MA.Flora.PackedCullingChunkInfo:  static fields
//
#define ARCHETYPE_INDEX_BITS (20)
#define BATCH_DOMAIN_INDEX_BITS (12)
#define ARCHETYPE_INDEX_SHIFT (12)
#define BATCH_DOMAIN_INDEX_MASK (4095)

// Generated from MA.Flora.BlockData
// PackingRules = Exact
struct BlockData
{
    float3 position;
    float cellSize;
};

// Generated from MA.Flora.CullingChunkUpdatePacket
// PackingRules = Exact
struct CullingChunkUpdatePacket
{
    uint chunkIndex;
    uint cellIndex;
    uint packedInfo;
    uint packedBatch;
};

// Generated from MA.Flora.PackedCullingChunkAttributes
// PackingRules = Exact
struct PackedCullingChunkAttributes
{
    uint4 data;
};

// Generated from MA.Flora.PackedCullingChunkBatch
// PackingRules = Exact
struct PackedCullingChunkBatch
{
    uint data;
};

// Generated from MA.Flora.PackedCullingChunkInfo
// PackingRules = Exact
struct PackedCullingChunkInfo
{
    uint data;
};


#endif
