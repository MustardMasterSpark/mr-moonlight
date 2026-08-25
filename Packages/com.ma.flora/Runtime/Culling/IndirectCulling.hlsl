// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_CULLING_INCLUDED
#define FLORA_CULLING_INCLUDED

//--------------------------------------------------------------------------------------------------
// Definitions
//--------------------------------------------------------------------------------------------------

#ifdef DEBUG_OCCLUSION
#define OCCLUSION_DEBUG // Required in OcclusionCullingCommon.hlsl
#endif

#if UNITY_VERSION >= 600020
#define MESH_LOD_AVAILABLE
#endif

//--------------------------------------------------------------------------------------------------
// Includes
//--------------------------------------------------------------------------------------------------

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.ma.flora/Runtime/Culling/CullingGrid.cs.hlsl"
#include "Packages/com.ma.flora/Runtime/Culling/CullingViewShaderVariables.hlsl"
#include "Packages/com.ma.flora/Runtime/Culling/IndirectCullingPass.cs.hlsl"
#include "Packages/com.ma.flora/Runtime/Culling/OcclusionCuller.hlsl"

#define MAX_FRUSTUM_PLANE_COUNT MAX_PLANES_PER_SPLIT // Don't include the far plane
#include "Packages/com.ma.flora/ShaderLibrary/Frustum.hlsl"

#ifdef DEBUG_ENABLED
#include "Packages/com.ma.flora/Runtime/Debugging/DebugDisplayFlora.cs.hlsl"
#endif

//--------------------------------------------------------------------------------------------------
// Constants
//--------------------------------------------------------------------------------------------------

#ifdef VIEW_IS_LIGHT
static const bool kViewIsLight   = true;
static const uint kMaxSplitCount = 4u;
#else
static const bool kViewIsLight   = false;
static const uint kMaxSplitCount = 1u;
#endif

//--------------------------------------------------------------------------------------------------
// Types
//--------------------------------------------------------------------------------------------------

struct DrawChunk
{
    uint chunkIndex;          // Culling chunk index in the grid
    uint splitMask;           // Which splits this chunk is present in
    uint archetypeIndex;      // Index into _ArchetypeData
    uint supportedStateMask;  // Exact draw-state keys supported by this chunk
    uint drawPartitionIndex;  // Exact draw partition index
};

struct CullingWorkGroup
{
    uint chunkIndex;
    uint drawChunkIndexAndSplitMask;
    PackedCullingChunkInfo packedChunkInfo;
    uint supportedStateMask;
    uint drawPartitionIndex;
    uint reserved0;
    uint reserved1;
    uint reserved2;
};

//--------------------------------------------------------------------------------------------------
// Scene View
//--------------------------------------------------------------------------------------------------

#ifdef VIEW_IS_EDITOR
static const uint kSceneViewPassNormal           = 0u;
static const uint kSceneViewPassPicking          = 1u;
static const uint kSceneViewPassSelectionOutline = 2u;

uint _EditorViewPass;

bool IsEditorPass()           { return _EditorViewPass != kSceneViewPassNormal; }
bool IsSelectionOutlinePass() { return _EditorViewPass == kSceneViewPassSelectionOutline; }
bool IsPickingPass()          { return _EditorViewPass == kSceneViewPassPicking; }
#endif

//--------------------------------------------------------------------------------------------------
// Debug Display
//--------------------------------------------------------------------------------------------------

static const uint kDebugCounterTypeDraws    = INDIRECTDISPATCHCOUNTER_VISIBLE_DRAWS;
static const uint kDebugCounterTypeVisible  = INDIRECTDISPATCHCOUNTER_VISIBLE_INSTANCES;
static const uint kDebugCounterTypeOccluded = INDIRECTDISPATCHCOUNTER_OCCLUDED_INSTANCES;
static const uint kDebugCounterTypeCount    = INDIRECTDISPATCHCOUNTER_COUNT;

#ifdef DEBUG_ENABLED
RWStructuredBuffer<uint> _DebugDispatchCounter;
uint _DebugCounterEnabled;
#endif

void IncrementDebugDispatchCounter(uint counterType, uint count = 1u)
{
#ifdef DEBUG_ENABLED
    if (_DebugCounterEnabled && count)
        InterlockedAdd(_DebugDispatchCounter[counterType], count);
#endif
}

//--------------------------------------------------------------------------------------------------
// Culling
//--------------------------------------------------------------------------------------------------

bool CullFrustum(float3 center, float3 extent, inout uint splitMask)
{
#if VIEW_IS_LIGHT
    uint splitCount = min(_ViewSplitCount, 4);
#else
    uint splitCount = 1u;
#endif
    float4 splitPlanes[MAX_PLANES_PER_SPLIT];

    UNITY_UNROLL
    for (uint splitIndex = 0u; splitIndex < splitCount; ++splitIndex)
    {
        if (((1u << splitIndex) & splitMask) == 0u)
            continue;

        uint splitOffset = splitIndex * MAX_PLANES_PER_SPLIT;

        UNITY_UNROLL
        for (uint i = 0u; i < MAX_PLANES_PER_SPLIT; ++i)
            splitPlanes[i] = _ViewFrustumPlanes[splitOffset + i];

        bool cull = CullFrustumPlanes(center, extent, splitPlanes, MAX_PLANES_PER_SPLIT);
        if (cull)
        {
            splitMask &= ~(1u << splitIndex);
        }
#if VIEW_IS_LIGHT
        else
        {
            break; // This instance will also be visible in larger cascades
        }
#endif
    }

    return splitMask == 0u;
}

bool CullOcclusion(float3 center, float radius, inout uint splitMask)
{
    // Unity uses signed ints in OcclusionCommon.hlsl
    // Note: Tests are per-subview (eye), NOT per-split as shadows aren't considered yet
    int visibleMask = 0;
    int occlusionSplitMask = int(splitMask);

    if (_CullingSplitMask & occlusionSplitMask)
    {
        SphereBound bound;
        bound.center = center;
        bound.radius = radius;

        // Currently only 2 subviews supported (stereo)
        int subviewCount = min(_OcclusionTestCount, 2);

        UNITY_UNROLL
        for (int testIndex = 0; testIndex < subviewCount; ++testIndex)
        {
            // Unpack the culling split index and subview index for this test
            int splitIndex = (_CullingSplitIndices >> (4 * testIndex)) & 0xf;
            int subviewIndex = (_OccluderSubviewIndices >> (4 * testIndex)) & 0xf;

            if (((1 << splitIndex) & occlusionSplitMask) == 0)
                continue;

            if (IsOcclusionVisible(bound, subviewIndex))
                visibleMask |= (1 << splitIndex);
        }

        splitMask &= uint(visibleMask);
    }

    return splitMask == 0u;
}

#endif
