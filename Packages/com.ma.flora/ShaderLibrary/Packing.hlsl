// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_PACKING_INCLUDED
#define FLORA_PACKING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

//--------------------------------------------------------------------------------------------------
// Float Packing (Floor)
//--------------------------------------------------------------------------------------------------

uint UnpackIntFloor(float f, uint numBits)
{
    uint maxInt = (1u << numBits) - 1u;
    return uint(floor(f * maxInt));
}

uint PackFloatToUIntFloor(float f, uint offset, uint numBits)
{
    return UnpackIntFloor(f, numBits) << offset;
}

uint PackFloat3ToUIntFloor(float3 f, uint3 numBits)
{
    uint x = PackFloatToUIntFloor(f.x, 0, numBits.x);
    uint y = PackFloatToUIntFloor(f.y, numBits.x, numBits.y);
    uint z = PackFloatToUIntFloor(f.z, numBits.x + numBits.y, numBits.z);
    return x | y | z;
}

uint PackFloat3ToUIntFloor(float3 f, uint numBits)
{
    return PackFloat3ToUIntFloor(f, uint3(numBits, numBits, numBits));
}

//--------------------------------------------------------------------------------------------------
// Float Packing (Ceil)
//--------------------------------------------------------------------------------------------------

uint UnpackIntCeil(float f, uint numBits)
{
    uint maxInt = (1u << numBits) - 1u;
    return uint(ceil(f * maxInt));
}

uint PackFloatToUIntCeil(float f, uint offset, uint numBits)
{
    return UnpackIntCeil(f, numBits) << offset;
}

uint PackFloat3ToUIntCeil(float3 f, uint3 numBits)
{
    uint x = PackFloatToUIntCeil(f.x, 0, numBits.x);
    uint y = PackFloatToUIntCeil(f.y, numBits.x, numBits.y);
    uint z = PackFloatToUIntCeil(f.z, numBits.x + numBits.y, numBits.z);
    return x | y | z;
}

uint PackFloat3ToUIntCeil(float3 f, uint numBits)
{
    return PackFloat3ToUIntCeil(f, uint3(numBits, numBits, numBits));
}

//--------------------------------------------------------------------------------------------------
// Float3 Unpacking
//--------------------------------------------------------------------------------------------------

float3 UnpackUIntToFloat3(uint i, uint3 numBits)
{
    float x = UnpackUIntToFloat(i, 0, numBits.x);
    float y = UnpackUIntToFloat(i, numBits.x, numBits.y);
    float z = UnpackUIntToFloat(i, numBits.x + numBits.y, numBits.z);
    return float3(x, y, z);
}

float3 UnpackUIntToFloat3(uint i, uint numBits)
{
    return UnpackUIntToFloat3(i, uint3(numBits, numBits, numBits));
}

//--------------------------------------------------------------------------------------------------
// Bounds Packing
//--------------------------------------------------------------------------------------------------

uint2 PackBoundsMinMaxToUInt(float3 normalizedBoundsMin, float3 normalizedBoundsMax, uint numBits)
{
    uint2 packed;
    packed.x = PackFloat3ToUIntFloor(normalizedBoundsMin, numBits);
    packed.y = PackFloat3ToUIntCeil(normalizedBoundsMax, numBits);
    return packed;
}

void UnpackBoundsMinMaxFromUInt(uint2 packed, uint numBits, float scale, out float3 normalizedBoundsMin, out float3 normalizedBoundsMax)
{
    normalizedBoundsMin = UnpackUIntToFloat3(packed.x, numBits) * scale;
    normalizedBoundsMax = UnpackUIntToFloat3(packed.y, numBits) * scale;
}

#endif

