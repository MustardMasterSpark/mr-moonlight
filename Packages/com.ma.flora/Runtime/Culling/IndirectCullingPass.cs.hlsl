//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef INDIRECTCULLINGPASS_CS_HLSL
#define INDIRECTCULLINGPASS_CS_HLSL
//
// MA.Flora.GPUErrorCode:  static fields
//
#define GPUERRORCODE_NONE (0)
#define GPUERRORCODE_PER_INSTANCE_EMIT_OVERFLOW (1)
#define GPUERRORCODE_STATE_KEY_OUT_OF_RANGE (2)
#define GPUERRORCODE_LOD_INDEX_OUT_OF_RANGE (3)
#define GPUERRORCODE_TEMPLATE_LOD_INCONSISTENT (4)
#define GPUERRORCODE_BIN_INDEX_OVERFLOW (5)
#define GPUERRORCODE_COMMAND_COUNT_ZERO (6)
#define GPUERRORCODE_STATE_KEY_NOT_SUPPORTED (7)
#define GPUERRORCODE_PACKED_KEY_OR_LOD_OUT_OF_RANGE (8)
#define GPUERRORCODE_BIN_WRITE_PAST_RESERVED_END (9)

//
// MA.Flora.IndirectDispatchCounter:  static fields
//
#define INDIRECTDISPATCHCOUNTER_VISIBLE_DRAWS (0)
#define INDIRECTDISPATCHCOUNTER_VISIBLE_INSTANCES (1)
#define INDIRECTDISPATCHCOUNTER_OCCLUDED_INSTANCES (2)
#define INDIRECTDISPATCHCOUNTER_COUNT (3)

//
// MA.Flora.IndirectStateFlags:  static fields
//
#define INDIRECTSTATEFLAGS_NONE (0)
#define INDIRECTSTATEFLAGS_HAS_FADE_KEYWORD (1)
#define INDIRECTSTATEFLAGS_HAS_MOTION (2)
#define INDIRECTSTATEFLAGS_HAS_FLIPPED_WINDING (4)
#define INDIRECTSTATEFLAGS_COUNT (3)
#define INDIRECTSTATEFLAGS_ALL (7)
#define INDIRECTSTATEFLAGS_KEY_COUNT (8)

// Generated from MA.Flora.IndirectDrawBin
// PackingRules = Exact
struct IndirectDrawBin
{
    uint visibleStart;
    uint visibleCount;
    uint commandStart;
    uint commandCount;
};

// Generated from MA.Flora.IndirectDrawChunk
// PackingRules = Exact
struct IndirectDrawChunk
{
    uint packedChunkAndSplit;
    uint packedArchetypeAndState;
    uint drawPartitionIndex;
    uint reserved;
};

// Generated from MA.Flora.IndirectDrawInfo
// PackingRules = Exact
struct IndirectDrawInfo
{
    uint indexCountPerInstance;
    uint startIndex;
    uint baseVertexIndex;
    uint startInstance;
};

// Generated from MA.Flora.IndirectDrawPartition
// PackingRules = Exact
struct IndirectDrawPartition
{
    uint binOffset;
    uint slotsPerLod;
    uint stateMask;
    uint stateIndices;
};


#endif
