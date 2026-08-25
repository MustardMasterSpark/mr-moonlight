// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_PREFABCULLINGDATA_INCLUDED
#define FLORA_PREFABCULLINGDATA_INCLUDED

#include "Packages/com.ma.flora/Runtime/Core/Archetype.hlsl"

// Keep in sync with FloraPrefabCullingData struct in C#
static const uint k_TemplateDataStride = 16 * 10;

static const uint k_TemplateFlagIsLODGroup              = 1 << 0;
static const uint k_TemplateFlagIsMeshLod               = 1 << 1;
static const uint k_TemplateFlagHasMotionVectors        = 1 << 2;
static const uint k_TemplateFlagHasCrossFade            = 1 << 3;
static const uint k_TemplateFlagHasAnimatedFade         = 1 << 4;
static const uint k_TemplateFlagAffectedByGlobalDensity = 1 << 5;
static const uint k_TemplateFlagAffectedByRangeDensity  = 1 << 6;
static const uint k_TemplateFlagAffectedByMinScreenSize = 1 << 7;
static const uint k_TemplateFlagHasRandomID             = 1 << 8;

struct TemplateData
{
    uint index;
    uint flags;
    uint layer;
    uint batchDomainIndex;
    float maxRenderDistance;
    float maxRenderDistanceSq;

    float3 localCenter;
    float3 localExtent;
    float localBoundingRadius;

    float3 localLodPoint;
    float localSize;

    uint lodCount;
    uint lodMax;
    uint lodMinShadow;
    uint lodPercentageFlags; // 8 bits for percentage flags
    uint lodShadowFlags;     // 8 bits for shadow flags

    // Mesh lod params
    float meshLodSlope;
    float meshLodBias;
    float meshLodSelectionBias;

    // LOD transition data
    float lodHeightRcp[8];
    float lodTransitionHeightRcp[8];

    // Derived
    uint isLodGroup;
    uint isMeshLod;
    uint hasMotionVectors;
    uint hasAnimatedFade;
    uint hasCrossFade;
    uint maxVisibleLodCount;

    bool IsPercentageFade(uint lodIndex)
    {
        return ((lodPercentageFlags >> lodIndex) & 1u) != 0u;
    }

    bool IsShadowEnabled(uint lodIndex)
    {
        return ((lodShadowFlags >> lodIndex) & 1u) != 0u;
    }

    bool AffectedByGlobalDensity()
    {
        return flags & k_TemplateFlagAffectedByGlobalDensity;
    }

    bool AffectedByRangeDensity()
    {
        return flags & k_TemplateFlagAffectedByRangeDensity;
    }

    bool AffectedByMinScreenSize()
    {
        return flags & k_TemplateFlagAffectedByMinScreenSize;
    }
};

ByteAddressBuffer _TemplateData;

uint4 LoadTemplateDataElement(uint templateId, uint elementIndex)
{
    uint baseAddress = templateId * k_TemplateDataStride;
    uint elementAddress = baseAddress + 16 * elementIndex;
    return _TemplateData.Load4(elementAddress);
}

uint LoadTemplateBatchDomainIndex(uint templateId)
{
    return LoadTemplateDataElement(templateId, 0u).z;
}

void LoadTemplateAABB(uint templateIndex, out float3 localCenter, out float3 localExtent)
{
    uint4 d1 = LoadTemplateDataElement(templateIndex, 1u);
    uint4 d2 = LoadTemplateDataElement(templateIndex, 2u);
    localCenter = asfloat(d1.xyz);
    localExtent = asfloat(d2.xyz);
}

float ComposeDistanceCap(float currentDistance, float capDistance)
{
    // A cap of 0 means "unlimited". Otherwise choose the tighter active cap.
    if (capDistance <= 0.0)
        return currentDistance;

    return currentDistance > 0.0 ? min(currentDistance, capDistance) : capDistance;
}

TemplateData LoadTemplateData(PackedArchetypeData archetypeData, float viewMaxDistance, bool forLightPass)
{
    TemplateData data;
    ZERO_INITIALIZE(TemplateData, data);

    uint4 d0 = LoadTemplateDataElement(archetypeData.templateIndex, 0u);
    data.index            = archetypeData.templateIndex;
    data.flags            = d0.x;
    data.layer            = (archetypeData.packedLayerAndMaxRenderDistance >> 16u) & 0xffu;
    data.batchDomainIndex = d0.z;
    // d0.w is maxRenderDistance

    // Commonly used flags
    data.isLodGroup         = (data.flags & k_TemplateFlagIsLODGroup);
    data.isMeshLod          = (data.flags & k_TemplateFlagIsMeshLod);
    data.hasMotionVectors   = (data.flags & k_TemplateFlagHasMotionVectors);
    data.hasCrossFade       = (data.flags & k_TemplateFlagHasCrossFade);
    data.hasAnimatedFade    = (data.flags & k_TemplateFlagHasAnimatedFade);
    data.maxVisibleLodCount = (data.isLodGroup && data.hasCrossFade) ? 2u : 1u;

    uint4 d1 = LoadTemplateDataElement(archetypeData.templateIndex, 1u);
    data.localCenter = asfloat(d1.xyz);
    // d1.w is maxShadowDistance

    float templateMaxDistance = forLightPass ? asfloat(d1.w) : asfloat(d0.w);
    data.maxRenderDistance = 0.0;
    data.maxRenderDistance = ComposeDistanceCap(data.maxRenderDistance, viewMaxDistance);
    data.maxRenderDistance = ComposeDistanceCap(data.maxRenderDistance, templateMaxDistance);

    // Archetype distance (terrain/tree distance) is also a cap for non-LODGroup templates.
    if (!data.isLodGroup)
        data.maxRenderDistance = ComposeDistanceCap(data.maxRenderDistance, archetypeData.packedLayerAndMaxRenderDistance & 0xffffu);

    data.maxRenderDistanceSq = data.maxRenderDistance * data.maxRenderDistance;

    uint4 d2 = LoadTemplateDataElement(archetypeData.templateIndex, 2u);
    data.localExtent         = asfloat(d2.xyz);
    data.localBoundingRadius = asfloat(d2.w);

    uint4 d3 = LoadTemplateDataElement(archetypeData.templateIndex, 3u);
    data.localLodPoint  = asfloat(d3.xyz);
    data.localSize = asfloat(d3.w);

    uint4 d4 = LoadTemplateDataElement(archetypeData.templateIndex, 4u);
    data.lodCount           = d4.x;
    data.lodMax             = d4.y;
    data.lodMinShadow       = d4.z;
    data.lodPercentageFlags = (d4.w >> 0u) & 0xff;
    data.lodShadowFlags     = (d4.w >> 8u) & 0xff;

    uint4 d5 = LoadTemplateDataElement(archetypeData.templateIndex, 5u);
    data.meshLodSlope         = asfloat(d5.x);
    data.meshLodBias          = asfloat(d5.y);
    data.meshLodSelectionBias = asfloat(d5.z);

    UNITY_BRANCH
    if (data.lodCount)
    {
        uint i;
        float4 lodHeightRcp0 = asfloat(LoadTemplateDataElement(archetypeData.templateIndex, 6u));
        float4 lodHeightTransitionHeightRcp0 = asfloat(LoadTemplateDataElement(archetypeData.templateIndex, 8u));

        UNITY_UNROLL
        for (i = 0u; i < 4u; i++)
        {
            data.lodHeightRcp[i] = lodHeightRcp0[i];
            data.lodTransitionHeightRcp[i] = lodHeightTransitionHeightRcp0[i];
        }

        UNITY_BRANCH
        if (data.lodCount > 4u)
        {
            float4 lodHeightRcp1 = asfloat(LoadTemplateDataElement(archetypeData.templateIndex, 7u));
            float4 lodHeightTransitionHeightRcp1 = asfloat(LoadTemplateDataElement(archetypeData.templateIndex, 9u));

            UNITY_UNROLL
            for (i = 0u; i < 4u; i++)
            {
                data.lodHeightRcp[i + 4u] = lodHeightRcp1[i];
                data.lodTransitionHeightRcp[i + 4u] = lodHeightTransitionHeightRcp1[i];
            }
        }
    }

    return data;
}

#endif // FLORA_PROTOTYPE_INCLUDED
