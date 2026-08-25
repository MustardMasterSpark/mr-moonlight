// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_FRUSTUM_INCLUDED
#define FLORA_FRUSTUM_INCLUDED

#include "Packages/com.ma.flora/ShaderLibrary/Matrix.hlsl"

#ifndef MAX_FRUSTUM_PLANE_COUNT
#define MAX_FRUSTUM_PLANE_COUNT 6
#endif

// -------------------------------------------------------------------------
// Utilities
// -------------------------------------------------------------------------

bool IsOutsideNearFar(float minZ, float maxZ)
{
    bool result = false;
#if UNITY_REVERSED_Z
    // Reversed Z
    if (minZ > 1.0) result = true; // behind near plane
    if (maxZ < 0.0) result = true; // beyond far plane
#else
    // Normal Z
    if (maxZ < 0.0) result = true; // behind near plane
    if (minZ > 1.0) result = true; // beyond far plane
#endif
    return result;
}

// -------------------------------------------------------------------------
// Orthographic View Projection
// -------------------------------------------------------------------------

bool CullFrustumOrthographic(float3 centerWS, float3 extentWS, float4x4 worldToClip)
{
    float4 cCS = mul(worldToClip, float4(centerWS, 1.0));
    float3 xCS = extentWS.x * GetXAxis(worldToClip);
    float3 yCS = extentWS.y * GetYAxis(worldToClip);
    float3 zCS = extentWS.z * GetZAxis(worldToClip);

    float3 extCS = abs(xCS) + abs(yCS) + abs(zCS);
    float3 minCS = cCS.xyz - extCS;
    float3 maxCS = cCS.xyz + extCS;

    bool visible = true;

    if (IsOutsideNearFar(minCS.z, maxCS.z))
        visible = false;

    if (any(maxCS.xy < -1.0) || any(minCS.xy > 1.0))
        visible = false;

    return !visible;
}

// -------------------------------------------------------------------------
// Perspective View Projection
// -------------------------------------------------------------------------

bool CullFrustumPerspective(float3 centerWS, float3 extentWS, float4x4 worldToClip)
{
    bool visible = true;

    // 1) Transform the center into clip space
    float4 centerCS = mul(worldToClip, float4(centerWS, 1.0));

    // 2) Compute how each axis in world space transforms into clip-space directions
    float4 xExtentCS = extentWS.xxxx * GetXColumn(worldToClip);
    float4 yExtentCS = extentWS.yyyy * GetYColumn(worldToClip);
    float4 zExtentCS = extentWS.zzzz * GetZColumn(worldToClip);

    // 3) Build the 8 corners in clip space by adding/subtracting these directions
    float4 cornersCS[8];
    cornersCS[0] = centerCS - xExtentCS - yExtentCS - zExtentCS; // (-x, -y, -z)
    cornersCS[1] = centerCS - xExtentCS - yExtentCS + zExtentCS; // (-x, -y, +z)
    cornersCS[2] = centerCS - xExtentCS + yExtentCS - zExtentCS; // (-x, +y, -z)
    cornersCS[3] = centerCS - xExtentCS + yExtentCS + zExtentCS; // (-x, +y, +z)
    cornersCS[4] = centerCS + xExtentCS - yExtentCS - zExtentCS; // (+x, -y, -z)
    cornersCS[5] = centerCS + xExtentCS - yExtentCS + zExtentCS; // (+x, -y, +z)
    cornersCS[6] = centerCS + xExtentCS + yExtentCS - zExtentCS; // (+x, +y, -z)
    cornersCS[7] = centerCS + xExtentCS + yExtentCS + zExtentCS; // (+x, +y, +z)

    // 4) We’ll test min/max depth in NDC (z) and also track plane distances.
    float minZ = +FLT_INF;
    float maxZ = -FLT_INF;
    float4 minPlanesXY = float4(FLT_INF, FLT_INF, FLT_INF, FLT_INF);

    UNITY_UNROLL
    for (uint i = 0; i < 8; i++)
    {
        float3 cornerNDC = cornersCS[i].xyz / cornersCS[i].w;

        // Track minZ and maxZ for near/far culling
        minZ = min(minZ, cornerNDC.z);
        maxZ = max(maxZ, cornerNDC.z);

        // Distance to the four side planes in NDC:
        //   left   plane => cornerNDC.x == -1
        //   right  plane => cornerNDC.x == +1
        //   bottom plane => cornerNDC.y == -1
        //   top    plane => cornerNDC.y == +1

        // So if cornerNDC.x < -1, the distance to left plane is (cornerNDC.x - (-1)) = (cornerNDC.x + 1).
        // We want to see if *all* corners are outside the same side => cull.
        // An easy approach: gather distances so that being positive means "outside."
        float4 planes = float4(cornerNDC.xy, -cornerNDC.xy) - 1.0;
        minPlanesXY = min(minPlanesXY, planes);
    }

    // 5) If we fail near/far test, it is out of the frustum
    if (IsOutsideNearFar(minZ, maxZ))
        visible = false;

    // 6) If for any side plane, minPlanesXY > 0, it means we are 100% beyond that plane
    if (visible && any(minPlanesXY > 0.0))
        visible = false;

    return !visible;
}

// -------------------------------------------------------------------------
// View Projection
// -------------------------------------------------------------------------

bool CullFrustumViewProjection(float3 centerWS, float3 extentWS, float4x4 worldToClip, bool isOrthographic)
{
    return isOrthographic
        ? CullFrustumOrthographic(centerWS, extentWS, worldToClip)
        : CullFrustumPerspective(centerWS, extentWS, worldToClip);
}

// -------------------------------------------------------------------------
// Planes
// -------------------------------------------------------------------------

bool CullFrustumPlanes(float3 center, float3 extent, float4 planes[MAX_FRUSTUM_PLANE_COUNT], uint planeCount = MAX_FRUSTUM_PLANE_COUNT)
{
    bool visible = true;

    UNITY_UNROLL
    for (uint i = 0; i < planeCount; i++)
    {
        float distance = dot(planes[i].xyz, center) + planes[i].w;
        float radius = dot(extent, abs(planes[i].xyz));
        if (distance + radius < 0.0)
        {
            visible = false;
            break;
        }
    }

    return !visible;
}

#endif // FLORA_FRUSTUM_CULLING_INCLUDED
