// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_RANDOM_INCLUDED
#define FLORA_RANDOM_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Hashes.hlsl"

uint TausStep(uint h, uint sh1, uint sh2, uint sh3, uint mask)
{
    uint b = (((h << sh1) ^ h) >> sh2);
    h = (((h & mask) << sh3) ^ b);
    return h;
}

uint PositionToHash(float3 position)
{
    uint3 s = asuint(position);

    s.x = TausStep(s.x, 13, 19, 12, 4294967294u);
    s.y = TausStep(s.y,  2, 25,  4, 4294967288u);
    s.z = TausStep(s.z,  3, 11, 17, 4294967280u);

    return s.x ^ s.y ^ s.z;
}

uint PositionToHash(float2 position)
{
    uint2 s = asuint(position);

    s.x = TausStep(s.x, 13, 19, 12, 4294967294u);
    s.y = TausStep(s.y,  2, 25,  4, 4294967288u);

    return s.x ^ s.y;
}

#endif
