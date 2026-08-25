// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_INSTANCE_DATA_INCLUDED
#define FLORA_INSTANCE_DATA_INCLUDED

//--------------------------------------------------------------------------------------------------
// Definitions
//--------------------------------------------------------------------------------------------------

#define UNITY_DOTS_INSTANCING_ENABLED
static uint unity_InstanceID;

//--------------------------------------------------------------------------------------------------
// Includes
//--------------------------------------------------------------------------------------------------

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityDOTSInstancing.hlsl"
#include "Packages/com.ma.flora/Runtime/Core/InstanceBuffer.cs.hlsl"
#include "Packages/com.ma.flora/Runtime/Core/TemplateData.hlsl"
#include "Packages/com.ma.flora/Runtime/Culling/CullingGrid.hlsl"
#include "Packages/com.ma.flora/ShaderLibrary/Matrix.hlsl"
#include "Packages/com.ma.flora/ShaderLibrary/Random.hlsl"

//--------------------------------------------------------------------------------------------------
// Batch Domain Addresses
//--------------------------------------------------------------------------------------------------

StructuredBuffer<BatchCullingAddresses> _BatchCullingAddresses;

groupshared BatchCullingAddresses gs_SharedBatchAddresses;

void InitSharedBatchDomainAddresses(uint batchDomainIndex)
{
    gs_SharedBatchAddresses = _BatchCullingAddresses[batchDomainIndex];
}

static BatchCullingAddresses flora_SampledBatchAddresses;

void BindBatchDomainAddresses(BatchCullingAddresses addresses)
{
    flora_SampledBatchAddresses = addresses;
}

//--------------------------------------------------------------------------------------------------
// Types
//--------------------------------------------------------------------------------------------------

struct InstanceData
{
    uint instanceIndex;
    uint indexInChunk;
    float4x4 localToWorld;
    uint randomID;

    // Derived data
    float3 worldCenter;
    float3 worldExtent;
    float worldBoundingRadius;
    float worldSpaceSize;
    CullingInstanceFlags flags;
    CullingInstanceEditorFlags editorFlags;
};

InstanceData LoadInstanceData(uint instanceIndex, uint indexInChunk)
{
    InstanceData instanceData;
    ZERO_INITIALIZE(InstanceData, instanceData);

    unity_SampledDOTSInstanceIndex = instanceIndex; // Required by LoadDOTS* functions
    instanceData.instanceIndex = instanceIndex;
    instanceData.indexInChunk = indexInChunk;

    instanceData.localToWorld = LoadDOTSInstancedDataOverridden_float4x4_from_float3x4(flora_SampledBatchAddresses.localToWorld);

    UNITY_BRANCH
    if (flora_SampledBatchAddresses.randomID)
    {
        instanceData.randomID = LoadDOTSInstancedDataOverridden_uint(flora_SampledBatchAddresses.randomID);
    }
    else
    {
        instanceData.randomID = PositionToHash(GetPosition(instanceData.localToWorld));
    }

    return instanceData;
}

void ComputeInstanceDerivedData(inout InstanceData instanceData, TemplateData templateData, CullingInstanceFlags flags, CullingInstanceEditorFlags editorFlags)
{
    MatrixTransformBounds(instanceData.localToWorld,
        templateData.localCenter, templateData.localExtent,
        instanceData.worldCenter, instanceData.worldExtent);

    instanceData.worldBoundingRadius = length(instanceData.worldExtent);
    instanceData.worldSpaceSize      = 2.0 * Max3(instanceData.worldExtent.x, instanceData.worldExtent.y, instanceData.worldExtent.z);
    instanceData.flags               = flags;
    instanceData.editorFlags         = editorFlags;
}

#endif
