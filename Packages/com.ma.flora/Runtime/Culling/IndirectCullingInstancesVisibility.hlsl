// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_INDIRECT_CULLING_INSTANCES_VISIBILITY_INCLUDED
#define FLORA_INDIRECT_CULLING_INSTANCES_VISIBILITY_INCLUDED

//--------------------------------------------------------------------------------------------------
// Instance Visibility Helpers
//--------------------------------------------------------------------------------------------------

bool ShouldDisableInstanceCrossFade()
{
    uint disableCrossFade = 0u;
#ifdef DEBUG_ENABLED
    disableCrossFade |= _DebugLODMode != kDebugLodModeNone ? 1u : 0u;
#endif
#ifdef VIEW_IS_EDITOR
    disableCrossFade |= IsEditorPass() ? 1u : 0u;
#endif
    return disableCrossFade != 0u;
}

uint BuildInstanceCullingFlags(TemplateData templateData)
{
    uint instanceCullingFlags = 0u;

    uint affectedByDistance = templateData.maxRenderDistance > 0.0 ? 1u : 0u;
    instanceCullingFlags |= affectedByDistance * kCullingFlagDistance;

    uint affectedByGlobalDensity = _DensityCullingEnabled && templateData.AffectedByGlobalDensity() ? 1u : 0u;
    affectedByGlobalDensity &= (_VolumeGlobalDensityLayerMask & (1u << templateData.layer)) != 0u ? 1u : 0u;
    affectedByGlobalDensity &= !templateData.isLodGroup || (templateData.isLodGroup && _VolumeGlobalDensityAffectsLodGroups);
#ifdef VIEW_IS_EDITOR
    affectedByGlobalDensity &= IsEditorPass() ? 0u : 1u;
#endif
    instanceCullingFlags |= affectedByGlobalDensity * kCullingFlagGlobalDensity;

    uint affectedByRangeDensity = _DensityCullingEnabled && templateData.AffectedByRangeDensity() ? 1u : 0u;
    affectedByRangeDensity &= (_VolumeRangeDensityLayerMask & (1u << templateData.layer)) != 0u ? 1u : 0u;
    affectedByRangeDensity &= !templateData.isLodGroup || (templateData.isLodGroup && _VolumeRangeDensityAffectsLodGroups);
#ifdef VIEW_IS_EDITOR
    affectedByRangeDensity &= IsEditorPass() ? 0u : 1u;
#endif
    instanceCullingFlags |= affectedByRangeDensity * kCullingFlagRangeDensity;

    if (!affectedByRangeDensity)
    {
        // RangeDensity is a more complicated version of minimum screen size, both should not be active.
        // Additionally, the minimum range density screen size is scaled by the minimum screen size.
        uint affectedByMinimumScreenSize = templateData.AffectedByMinScreenSize() ? 1u : 0u;
        affectedByMinimumScreenSize &= _MinScreenSize > 0.0 ? 1u : 0u;
        affectedByMinimumScreenSize &= !templateData.isLodGroup || (templateData.isLodGroup && _MinScreenSizeAffectsLodGroups);
        instanceCullingFlags |= affectedByMinimumScreenSize * kCullingFlagScreenSize;
    }

    return instanceCullingFlags;
}

struct InstanceCuller
{
    uint groupIndex;
    uint cullingFlags;

    bool visible;
    uint splitMask;
    bool hasFade;
    float fadeValue;

    static InstanceCuller Create(uint cullingFlags, uint splitMask, uint groupIndex)
    {
        InstanceCuller culler;
        culler.groupIndex = groupIndex;
        culler.cullingFlags = cullingFlags;
        culler.visible = true;
        culler.splitMask = splitMask;
        culler.hasFade = false;
        culler.fadeValue = 1;
        return culler;
    }

    uint PackFadeValue()
    {
        uint packedFade = PackFadeOutUint8(fadeValue);
        hasFade = packedFade != kLODFadeOff;
        return packedFade;
    }

    void CullEditorOnly(InstanceData instanceData)
    {
#ifdef VIEW_IS_EDITOR
        if (visible)
        {
            visible = !instanceData.editorFlags.isHidden;
        }

        if (IsSelectionOutlinePass() && visible)
        {
            visible = instanceData.editorFlags.isSelected;
        }

        if (IsPickingPass())
        {
            visible = HasFlag64(gs_EditorIncludedChunkBits, groupIndex);
        }
#endif
    }

    void CullDevelopmentOnly(TemplateData templateData)
    {
#ifdef DEBUG_ENABLED
        if (templateData.lodMax == 0 && _DebugLODMode == kDebugLodModeOnlyLod)
            visible = visible && _DebugLODIndex == 0;
#endif
    }

    void CullDistance(TemplateData templateData, float worldDistToCameraSq)
    {
        UNITY_BRANCH
        if (visible && (cullingFlags & kCullingFlagDistance))
        {
            float maxDistSq = templateData.maxRenderDistanceSq;
            visible = worldDistToCameraSq < maxDistSq;

            if (visible && !templateData.isLodGroup)
            {
                fadeValue = saturate(2.0 * (maxDistSq - worldDistToCameraSq) * rcp(maxDistSq));
            }
        }
    }

    void CullGlobalDensity(InstanceData instanceData)
    {
        UNITY_BRANCH
        if (visible && (cullingFlags & kCullingFlagGlobalDensity))
        {
            bool enabled = instanceData.worldSpaceSize < _VolumeGlobalDensityObjectSizeThreshold;
            if (enabled)
            {
                float rnd = GenerateHashedRandomFloat(uint2(instanceData.randomID, DENSITY_SEED));
                visible = rnd < _VolumeGlobalDensity;
            }
        }
    }

    void CullRangeDensity(InstanceData instanceData, float screenDistToCamera)
    {
        UNITY_BRANCH
        if (visible && (cullingFlags & kCullingFlagRangeDensity))
        {
            float r = GenerateHashedRandomFloat(uint2(instanceData.randomID, DENSITY_RANGE_SEED));
            float d = _VolumeRangeDensity;

            UNITY_BRANCH
            if (r > d)
            {
                float u = saturate((1.0 - r) * rcp(1.0 - d));
                float t = exp2(_VolumeRangeDensityFalloffExp * log2(max(u, 1e-8)));

                float minHeight = lerp(_VolumeRangeDensityHeightMax, _VolumeRangeDensityHeightMin, t);
                float fadeHeight = minHeight * (1.0 + kMinScreenTransitionWidth); // Last 10% is fade

                float maxDist = CalculateScreenDistance(instanceData.worldSpaceSize, minHeight);
                float fadeStart = CalculateScreenDistance(instanceData.worldSpaceSize, fadeHeight);

                if (screenDistToCamera < fadeStart)
                {
                    // Fully visible
                }
                else if (screenDistToCamera < maxDist)
                {
                    // Fading
                    fadeValue = min(fadeValue, saturate((maxDist - screenDistToCamera) / (maxDist - fadeStart)));
                }
                else
                {
                    // Culled
                    visible = false;
                }
            }
        }
    }

    void CullScreenSize(InstanceData instanceData, float screenDistToCamera)
    {
        UNITY_BRANCH
        if (visible && (cullingFlags & kCullingFlagScreenSize))
        {
            float maxDist = CalculateScreenDistance(instanceData.worldSpaceSize, _MinScreenSize);
            if (screenDistToCamera < maxDist)
            {
                float fadeStart = CalculateScreenDistance(instanceData.worldSpaceSize, _MinScreenSize * (1.0 + kMinScreenTransitionWidth));
                fadeValue = min(fadeValue, saturate((maxDist - screenDistToCamera) / (maxDist - fadeStart)));
            }
            else
            {
                visible = false;
            }
        }
    }

    void CullFrustum(InstanceData instanceData)
    {
        UNITY_BRANCH
        if (visible)
        {
            visible = !::CullFrustum(instanceData.worldCenter, instanceData.worldExtent, splitMask);
        }
    }

    void CullOcclusion(InstanceData instanceData)
    {
#ifdef USE_OCCLUSION
        UNITY_BRANCH
        if (visible)
        {
            visible = !::CullOcclusion(instanceData.worldCenter, instanceData.worldBoundingRadius, splitMask);
            IncrementDebugDispatchCounter(kDebugCounterTypeOccluded, visible ? 0u : 1u);
        }
#endif
    }
};

#endif // FLORA_INDIRECT_CULLING_INSTANCES_VISIBILITY_INCLUDED
