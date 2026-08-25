#ifndef FLORA_OCCLUSION_CULLING_INCLUDED
#define FLORA_OCCLUSION_CULLING_INCLUDED

// If using this the shader should add
// #pragma multi_compile _ OCCLUSION_DEBUG
// before including this file

// Copy of Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/OcclusionCullingCommon.hlsl
// Modified for compatibility with the dxc compiler

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

// Unity 6.5 uses an eight-digit UNITY_VERSION encoding; also support its legacy six-digit encoding.
#if (UNITY_VERSION >= 600050 && UNITY_VERSION < 1000000) || UNITY_VERSION >= 60050000
#include "Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/Culling/OcclusionCullingCommon.cs.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/Culling/OcclusionCullingCommonShaderVariables.cs.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/Culling/OcclusionTestCommon.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/Utilities/GeometryUtilities.hlsl"
#else
#include "Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/OcclusionCullingCommon.cs.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/OcclusionCullingCommonShaderVariables.cs.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/OcclusionTestCommon.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/GeometryUtilities.hlsl"
#endif


#define OCCLUSION_ENABLE_GATHER_TRIM 1

TEXTURE2D(_OccluderDepthPyramid);
SAMPLER(s_linear_clamp_sampler);

#ifdef OCCLUSION_DEBUG
RWStructuredBuffer<uint> _OcclusionDebugOverlay;

uint OcclusionDebugOverlayOffset(uint2 coord)
{
    return OCCLUSIONCULLINGCOMMONCONFIG_DEBUG_PYRAMID_OFFSET + coord.x + _OccluderMipLayoutSizeX * coord.y;
}
#endif

void ChooseMipAndScale(
    float2 centerCoordInTopMip, float radiusInPixels,
    out int mipLevel, out float2 centerCoordInChosenMip)
{
    // Unity original:
    // float mipPart = frexp(radiusInPixels, mipLevel); // mipPart unused; we only want the exponent
    // mipLevel = max(1 + mipLevel, 0);
    // centerCoordInChosenMip = ldexp(centerCoordInTopMip, -mipLevel);

    // UNITY_COMPILER_DXC: (frexp/ldexp not always supported)
    // NOTE: less conservative than above, needs radiusInPixels*2 to match original behavior
    mipLevel = 1 + int(firstbithigh(uint(radiusInPixels)));
    mipLevel = max(mipLevel, 0);
    centerCoordInChosenMip = centerCoordInTopMip * rcp(1 << mipLevel);
}

bool IsOcclusionVisible(float3 frontCenterPosRWS, float2 centerPosNDC, float2 radialPosNDC, int subviewIndex)
{
    bool isVisible = true;
    float queryClosestDepth = ComputeNormalizedDeviceCoordinatesWithZ(frontCenterPosRWS, _ViewProjMatrix[subviewIndex]).z;
    bool isBehindCamera = dot(frontCenterPosRWS, _FacingDirWorldSpace[subviewIndex].xyz) >= 0.0;

    float2 centerCoordInTopMip = centerPosNDC * _DepthSizeInOccluderPixels.xy;
    float radiusInPixels = length((radialPosNDC - centerPosNDC) * _DepthSizeInOccluderPixels.xy);

    int mipLevel;
    float2 centerCoordInChosenMip;
    ChooseMipAndScale(centerCoordInTopMip, radiusInPixels, mipLevel, centerCoordInChosenMip);

    if (mipLevel < OCCLUSIONCULLINGCOMMONCONFIG_MAX_OCCLUDER_MIPS && !isBehindCamera)
    {
        int4 mipBounds = _OccluderMipBounds[mipLevel];
        mipBounds.y += subviewIndex * _OccluderMipLayoutSizeY;

        if ((_OcclusionTestDebugFlags & OCCLUSIONTESTDEBUGFLAG_ALWAYS_PASS) == 0)
        {
            // gather4 occluder depths to cover this radius
            float2 gatherUv = (float2(mipBounds.xy) + clamp(centerCoordInChosenMip, 0.5, float2(mipBounds.zw) - 0.5)) * _OccluderDepthPyramidSize.zw;
            float4 gatherDepths = GATHER_TEXTURE2D(_OccluderDepthPyramid, s_linear_clamp_sampler, gatherUv);
            float occluderDepth = FarthestDepth(gatherDepths);
            isVisible = IsVisibleAfterOcclusion(occluderDepth, queryClosestDepth);
        }

#ifdef OCCLUSION_DEBUG
        // show footprint of gather4 in debug output
        bool countForOverlay = ((_OcclusionTestDebugFlags & OCCLUSIONTESTDEBUGFLAG_COUNT_VISIBLE) != 0);
        if (!isVisible)
            countForOverlay = !countForOverlay;
        if (countForOverlay)
        {
            uint2 debugCoord = mipBounds.xy + uint2(clamp(int2(centerCoordInChosenMip - .5f), 0, mipBounds.zw - 2));
            InterlockedAdd(_OcclusionDebugOverlay[OcclusionDebugOverlayOffset(debugCoord + uint2(0, 0))], 1);
            InterlockedAdd(_OcclusionDebugOverlay[OcclusionDebugOverlayOffset(debugCoord + uint2(1, 0))], 1);
            InterlockedAdd(_OcclusionDebugOverlay[OcclusionDebugOverlayOffset(debugCoord + uint2(0, 1))], 1);
            InterlockedAdd(_OcclusionDebugOverlay[OcclusionDebugOverlayOffset(debugCoord + uint2(1, 1))], 1);

            // accumulate the total in the first slot
            InterlockedAdd(_OcclusionDebugOverlay[0], 1);
        }
#endif
    }

    return isVisible;
}

bool IsOcclusionVisible(BoundingObjectData data, int subviewIndex)
{
    return IsOcclusionVisible(data.frontCenterPosRWS, data.centerPosNDC, data.radialPosNDC, subviewIndex);
}

bool IsOcclusionVisible(SphereBound boundingSphere, int subviewIndex)
{
    BoundingObjectData data = CalculateBoundingObjectData(
        boundingSphere,
        _ViewProjMatrix[subviewIndex],
        _ViewOriginWorldSpace[subviewIndex],
        _RadialDirWorldSpace[subviewIndex],
        _FacingDirWorldSpace[subviewIndex]);
    return IsOcclusionVisible(data, subviewIndex);
}

bool IsOcclusionVisible(CylinderBound cylinderBound, int subviewIndex)
{
    BoundingObjectData data = CalculateBoundingObjectData(
        cylinderBound,
        _ViewProjMatrix[subviewIndex],
        _ViewOriginWorldSpace[subviewIndex],
        _RadialDirWorldSpace[subviewIndex],
        _FacingDirWorldSpace[subviewIndex]);
    return IsOcclusionVisible(data, subviewIndex);
}
#endif
