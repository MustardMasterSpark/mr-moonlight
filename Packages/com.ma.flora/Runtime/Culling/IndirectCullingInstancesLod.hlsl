// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_INDIRECT_CULLING_INSTANCES_LOD_INCLUDED
#define FLORA_INDIRECT_CULLING_INSTANCES_LOD_INCLUDED

//--------------------------------------------------------------------------------------------------
// Instance LOD and Emission Helpers
//--------------------------------------------------------------------------------------------------

bool TryGetStateIndexFromKey(uint indices, uint key, out uint slot)
{
    slot = (indices >> (key * 4)) & 0xf;
    return slot != 0xf;
}

uint ComputeDrawPartitionBinStride(TemplateData templateData, IndirectDrawPartition drawPartition)
{
    uint splitCount = min(_ViewSplitCount, kMaxSplitCount);
    return splitCount * drawPartition.slotsPerLod * templateData.lodCount;
}

uint ComputeBinIndex(TemplateData templateData, IndirectDrawPartition drawPartition, uint splitIndex, uint stateKeySlot, uint lodIndex)
{
    return drawPartition.binOffset
         + splitIndex * (drawPartition.slotsPerLod * templateData.lodCount)
         + stateKeySlot * templateData.lodCount
         + lodIndex;
}

bool TryMakeDrawStateKey(uint supportedStateMask, bool hasFadeKeyword, bool hasMotion, bool hasFlippedWinding, out uint stateKey)
{
    stateKey = 0u;
    stateKey |= hasFadeKeyword ? kDrawStateFlagFade : 0u;
    stateKey |= hasMotion ? kDrawStateFlagMotion : 0u;
    stateKey |= hasFlippedWinding ? kDrawStateFlagFlip : 0u;
    return ((supportedStateMask >> stateKey) & 1u) != 0u;
}

void EmitDrawInstance(TemplateData templateData, InstanceData instanceData, uint fadeValue, uint splitMask, uint stateKey, uint lodIndex)
{
#ifdef DEBUG_ENABLED
    if (_DebugErrorEnabled)
    {
        if (stateKey >= kMaxDrawStateKeyCount)
        {
            DebugEmitError(GPUERRORCODE_STATE_KEY_OUT_OF_RANGE, templateData.index, stateKey, kMaxDrawStateKeyCount);
            return;
        }

        if (lodIndex >= kMaxLodCount || lodIndex >= templateData.lodCount)
        {
            DebugEmitError(GPUERRORCODE_LOD_INDEX_OUT_OF_RANGE, templateData.index, lodIndex, templateData.lodCount);
            return;
        }
    }
#endif

    uint drawBase = instanceData.indexInChunk * kMaxDrawInstancesPerThread;
    uint drawSlot = gs_PerThreadDrawInstanceCount[instanceData.indexInChunk];
    if (drawSlot >= kMaxDrawInstancesPerThread)
        return; // Overflow, should not happen

    int signedFadeValue = EncodeCrossFadeSint8(fadeValue);
    DrawInstance drawInstance;
    drawInstance.packedFadeAndIndex = (uint(signedFadeValue) << 24u) | (unity_SampledDOTSInstanceIndex & 0x00ffffffu);
    drawInstance.packedSplitKeyLod = (splitMask << 16u) | ((stateKey & 0xffu) << 8u) | (lodIndex & 0xffu);

    gs_DrawInstances[drawBase + drawSlot] = drawInstance;
    gs_PerThreadDrawInstanceCount[instanceData.indexInChunk] = drawSlot + 1u;

    uint splitCount = min(_ViewSplitCount, kMaxSplitCount);

    UNITY_UNROLL
    for (uint split = 0u; split < splitCount; ++split)
    {
        if (((splitMask >> split) & 1u) == 0u)
            continue;

        InterlockedAdd(gs_DrawBinCount[split][stateKey][lodIndex], 1u);
    }
}

struct LODCuller
{
    float3 position;
    float worldSpaceSize;
    float cameraDistSq;
    float cameraDist;
    float cameraDist0Sq;
    float cameraDist1Sq;
    float rcpLodJitter;

    static LODCuller Create(TemplateData templateData, InstanceData instanceData)
    {
        LODCuller culler;
        ZERO_INITIALIZE(LODCuller, culler);

        culler.position = mul(instanceData.localToWorld, float4(templateData.localLodPoint, 1.0)).xyz;
        culler.worldSpaceSize = instanceData.worldSpaceSize;

        UNITY_BRANCH
        if (_CameraIsOrthographic)
        {
            culler.cameraDistSq = _ScreenRelativeMetricSq;
            culler.cameraDist0Sq = _ViewAnimScreenRelativeMetricSq0;
            culler.cameraDist1Sq = _ViewAnimScreenRelativeMetricSq1;
        }
        else
        {
            culler.cameraDistSq = CalculatePerspectiveDistanceSq(culler.position, _CameraPosition, _ScreenRelativeMetricSq);

            UNITY_BRANCH
            if (templateData.hasAnimatedFade)
            {
                culler.cameraDist0Sq = CalculatePerspectiveDistanceSq(culler.position, _ViewAnimLodPosition0, _ViewAnimScreenRelativeMetricSq0);
                culler.cameraDist1Sq = CalculatePerspectiveDistanceSq(culler.position, _ViewAnimLodPosition1, _ViewAnimScreenRelativeMetricSq1);
            }
            else if (templateData.hasCrossFade)
            {
                culler.cameraDist = sqrt(culler.cameraDistSq);
            }
        }

        culler.rcpLodJitter = 1.0;
        UNITY_BRANCH
        if (_CameraRandomizeLodTransition)
        {
            // Add a bit of randomness to the transition distance to prevent all LODs transitioning at the same time
            // [-jitter,+jitter] _CameraRandomizeLodTransition in [0..0.5]
            float rnd = GenerateHashedRandomFloat(uint2(instanceData.randomID, LOD_JITTER_SEED));
            float jitter = ((rnd * 2.0) - 1.0) * _CameraRandomizeLodTransition;
            culler.rcpLodJitter = rcp(1.0 + jitter);
        }

        return culler;
    }

    uint ComputeMeshLodIndex(TemplateData templateData, InstanceData instanceData, uint lodMin, uint lodMax)
    {
        uint lodIndex = 0;

#ifdef MESH_LOD_AVAILABLE
        UNITY_BRANCH
        if (templateData.isMeshLod)
        {
            float meshLodSelectionConstantSq = _MeshLodSelectionConstantSq;
            float radiusSq = Sq(instanceData.worldBoundingRadius);
            float diameterSq = radiusSq * 4.0;
            float cameraHeightAtDistanceSq = _CameraIsOrthographic ? meshLodSelectionConstantSq : CalculatePerspectiveDistanceSq(instanceData.worldCenter, _CameraPosition, meshLodSelectionConstantSq);
            float boundsDesiredPercentage = sqrt(cameraHeightAtDistanceSq / diameterSq);

            float meshLodSlope = templateData.meshLodSlope;
            float meshLodBias = templateData.meshLodBias;
            float meshLodSelectionBias = templateData.meshLodSelectionBias;

            float level = max(0, log2(boundsDesiredPercentage) * meshLodSlope + meshLodBias);
            level += meshLodSelectionBias;
            level = clamp(level, lodMin, lodMax);
            lodIndex = uint(floor(level));
        }
#endif

        return lodIndex;
    }

    void CullLod(float minDistSq, float maxDistSq, inout uint crossFadeValue)
    {
        bool visible = (cameraDistSq >= minDistSq) && (cameraDistSq < maxDistSq);
        crossFadeValue = visible ? kLODFadeOff : kLODFadeInvalid;
    }

    static int ClassifyBand(float distSq, float minSq, float maxSq)
    {
        // minSq <= distSq < maxSq
        if (distSq <  minSq) return -1;
        if (distSq >= maxSq) return +1;
        return 0;
    }

    void CullAnimatedFade(float minDistSq, float maxDistSq, inout uint crossFadeValue, inout bool requiresFadeKeyword)
    {
        // Classify this LOD's band at both snapshots (-1 below, 0 inside, +1 above)
        int prevBand = ClassifyBand(cameraDist0Sq, minDistSq, maxDistSq);
        int currBand = ClassifyBand(cameraDist1Sq, minDistSq, maxDistSq);

        uint prevInside = (prevBand == 0) ? 1u : 0u;
        uint currInside = (currBand == 0) ? 1u : 0u;
        uint crossedOne = prevInside ^ currInside;
        if (crossedOne)
        {
            const float q = 1.0 / 254.0; // One 8-bit step
            const float s = 2.0 * float(currInside) - 1.0; // +1 if entering, -1 if exiting

            float a = _ViewAnimLodAlpha;
            if (a <= q)
            {
                // Start of transition
                crossFadeValue = (s < 0.0) ? kLODFadeOff : kLODFadeInvalid;
            }
            else if (a >= 1.0 - q)
            {
                // End of transition
                crossFadeValue = (s > 0.0) ? kLODFadeOff : kLODFadeInvalid;
            }
            else
            {
                crossFadeValue = PackCrossFadeUint8(s * a);
                requiresFadeKeyword = true;
            }
        }
        else
        {
            // No change, or jumped across the whole band in one step (|delta|==2): don't animate.
            crossFadeValue = currInside ? kLODFadeOff : kLODFadeInvalid;
        }
    }

    void CullPercentageFade(float minDist, float minDistSq, float maxDist, float maxDistSq, inout uint crossFadeValue)
    {
        // Percentage fade, SpeedTree, no fade keyword
        bool visible = (cameraDistSq >= minDistSq) && (cameraDistSq < maxDistSq);
        float start = max(worldSpaceSize, minDist);
        float percent = saturate((cameraDist - start) / (maxDist - start));
        crossFadeValue = visible ? PackFadeOutUint8(percent) : kLODFadeInvalid;
    }

    void CullCrossFade(float minDist, float transitionDist, float maxDist, inout uint crossFadeValue, inout bool requiresFadeKeyword)
    {
        if (cameraDist < minDist)
        {
            crossFadeValue = kLODFadeInvalid;
        }
        else if (cameraDist < transitionDist)
        {
            crossFadeValue = kLODFadeOff;
        }
        else if (cameraDist < maxDist)
        {
            float percent = saturate((maxDist - cameraDist) / (maxDist - transitionDist));
            crossFadeValue = PackFadeOutUint8(percent);
            requiresFadeKeyword = true;
        }
    }

    static bool IsLodEnabled(TemplateData templateData, uint lodIndex)
    {
        if (kViewIsLight && !templateData.IsShadowEnabled(lodIndex))
            return false;

        return true;
    }

    void CullAndEmitVisibleLods(TemplateData templateData, InstanceData instanceData, uint supportedStateMask, uint fadeOutValue, uint subviewMask)
    {
        uint lodMax = min(templateData.lodMax, 7u);
        uint lodMin = min(_ViewMinLOD, lodMax);
#ifdef VIEW_IS_LIGHT
        lodMin = clamp(templateData.lodMinShadow, lodMin, lodMax);
#endif
#ifdef DEBUG_ENABLED
        if (_DebugLODMode)
            templateData.hasCrossFade = false; // Disable cross-fade in debug modes
#endif

#ifdef DEBUG_ENABLED
        if (_DebugErrorEnabled)
        {
            if (templateData.lodCount == 0u ||
                templateData.lodCount > kMaxLodCount ||
                templateData.lodMax >= templateData.lodCount)
            {
                DebugEmitError(GPUERRORCODE_TEMPLATE_LOD_INCONSISTENT, templateData.index, templateData.lodCount, templateData.lodMax);
                return;
            }
        }
#endif

        uint maxVisibleLodCount = min(fadeOutValue == kLODFadeOff ? templateData.maxVisibleLodCount : 1u, kMaxDrawInstancesPerThread);
        bool hasMotion = kViewIsLight == false && instanceData.flags.hasMotion;
        bool flipWinding = instanceData.flags.hasFlippedWinding;

        UNITY_BRANCH
        if (!templateData.isLodGroup)
        {
            uint lodIndex = ComputeMeshLodIndex(templateData, instanceData, lodMin, lodMax);
#ifdef DEBUG_ENABLED
            if (_DebugLODMode == kDebugLodModeForceLod)
                lodIndex = clamp(_DebugLODIndex, lodMin, lodMax);
#endif

            uint binKey;
            if (TryMakeDrawStateKey(supportedStateMask, fadeOutValue != kLODFadeOff, hasMotion, flipWinding, binKey))
            {
                EmitDrawInstance(templateData, instanceData, fadeOutValue, subviewMask, binKey, lodIndex);
            }
#ifdef DEBUG_ENABLED
            else if (_DebugErrorEnabled)
            {
                DebugEmitError(GPUERRORCODE_STATE_KEY_NOT_SUPPORTED, templateData.index, supportedStateMask, (fadeOutValue != kLODFadeOff ? kDrawStateFlagFade : 0u) | (hasMotion ? kDrawStateFlagMotion : 0u) | (flipWinding ? kDrawStateFlagFlip : 0u));
            }
#endif
        }
        else
        {
            float minRange = 0.0;
            float minRangeSq = 0.0;
            float maxCameraDist = templateData.hasAnimatedFade ? max(cameraDist0Sq, cameraDist1Sq) : cameraDistSq;

            uint visibleLodCount = 0u;
            uint fadeInLodIndex = kInvalidLod;
            uint fadeInFadeValue = kLODFadeInvalid;

            UNITY_UNROLL
            for (uint lodIndex = lodMin; lodIndex <= lodMax; ++lodIndex)
            {
#ifndef DEBUG_ENABLED
                if (maxCameraDist < minRangeSq)
                    break; // Camera is below all further LOD ranges
#endif

                float maxRange = rcpLodJitter * CalculateScreenDistanceRcp(worldSpaceSize, templateData.lodHeightRcp[lodIndex]);
                float maxRangeSq = Sq(maxRange);

#ifdef DEBUG_ENABLED
                if (_DebugLODMode == kDebugLodModeOnlyLod && lodIndex != _DebugLODIndex)
                {
                    minRange = maxRange;
                    minRangeSq = maxRangeSq;
                    continue;
                }
#endif

                uint crossFadeValue = kLODFadeInvalid;
                bool wantsFadeKeyword = false;

                if (templateData.hasCrossFade)
                {
                    if (templateData.hasAnimatedFade)
                    {
                        CullAnimatedFade(minRangeSq, maxRangeSq, crossFadeValue, wantsFadeKeyword);
                    }
                    else if (templateData.IsPercentageFade(lodIndex))
                    {
                        CullPercentageFade(minRange, minRangeSq, maxRange, maxRangeSq, crossFadeValue);
                    }
                    else
                    {
                        float transitionDist = rcpLodJitter * CalculateScreenDistanceRcp(worldSpaceSize, templateData.lodTransitionHeightRcp[lodIndex]);
                        CullCrossFade(minRange, transitionDist, maxRange, crossFadeValue, wantsFadeKeyword);

                        if (wantsFadeKeyword && lodIndex < lodMax)
                        {
                            // Transition based cross-fading always fades in the next LOD if available
                            fadeInFadeValue = 254u - crossFadeValue;
                            fadeInLodIndex = lodIndex + 1u;
                        }
                    }
                }
                else
                {
                    CullLod(minRangeSq, maxRangeSq, crossFadeValue);
                }

                if (!IsLodEnabled(templateData, lodIndex))
                    crossFadeValue = kLODFadeInvalid;

                UNITY_BRANCH
                if (crossFadeValue != kLODFadeInvalid)
                {
                    uint emitLodIndex = lodIndex;
                    uint emitFadeValue = crossFadeValue;

                    if (fadeOutValue != kLODFadeOff)
                    {
                        // MinimumScreenSize and RangeDensity take precedence over LOD fading
                        emitFadeValue = fadeOutValue;
                        wantsFadeKeyword = true;
                    }

#ifdef DEBUG_ENABLED
                    if (_DebugLODMode == kDebugLodModeForceLod)
                    {
                        emitLodIndex = clamp(_DebugLODIndex, lodMin, lodMax);
                        wantsFadeKeyword = false;
                    }
#endif

                    uint stateKey;
                    if (!TryMakeDrawStateKey(supportedStateMask, wantsFadeKeyword, hasMotion, flipWinding, stateKey))
                    {
#ifdef DEBUG_ENABLED
                        if (_DebugErrorEnabled)
                            DebugEmitError(GPUERRORCODE_STATE_KEY_NOT_SUPPORTED, templateData.index, supportedStateMask, (wantsFadeKeyword ? kDrawStateFlagFade : 0u) | (hasMotion ? kDrawStateFlagMotion : 0u) | (flipWinding ? kDrawStateFlagFlip : 0u));
#endif
                        minRange = maxRange;
                        minRangeSq = maxRangeSq;
                        continue;
                    }

                    EmitDrawInstance(templateData, instanceData, emitFadeValue, subviewMask, stateKey, emitLodIndex);
                    visibleLodCount++;

                    if (fadeInLodIndex != kInvalidLod && visibleLodCount < maxVisibleLodCount)
                    {
                        if (!IsLodEnabled(templateData, fadeInLodIndex))
                            fadeInFadeValue = kLODFadeInvalid;

                        if (fadeInFadeValue != kLODFadeInvalid)
                        {
                            EmitDrawInstance(templateData, instanceData, fadeInFadeValue, subviewMask, stateKey, fadeInLodIndex);
                            visibleLodCount++;
                            fadeInFadeValue = kLODFadeInvalid;
                            fadeInLodIndex = kInvalidLod;
                        }
                    }

                    if (visibleLodCount >= maxVisibleLodCount)
                        break;
                }

                minRange = maxRange;
                minRangeSq = maxRangeSq;
            }
        }
    }
};

#endif // FLORA_INDIRECT_CULLING_INSTANCES_LOD_INCLUDED
