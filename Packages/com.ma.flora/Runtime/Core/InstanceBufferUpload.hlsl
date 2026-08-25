// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_INSTANCE_UPLOAD_DATA_INCLUDED
#define FLORA_INSTANCE_UPLOAD_DATA_INCLUDED

#include "Packages/com.ma.flora/Runtime/Graphics/GraphicsMatrix.cs.hlsl"
#include "Packages/com.ma.flora/Runtime/Core/InstanceBuffer.cs.hlsl"
#include "Packages/com.ma.flora/Runtime/Core/InstanceBufferUpload.cs.hlsl"

float4x4 UnpackGraphicsMatrix(float4 p1, float4 p2, float4 p3)
{
    return float4x4(
        p1.x, p1.w, p2.z, p3.y,
        p1.y, p2.x, p2.w, p3.z,
        p1.z, p2.y, p3.x, p3.w,
        0.0,  0.0,  0.0,  1.0
    );
}

float4x4 UnpackGraphicsMatrix(GraphicsMatrix m)
{
    return UnpackGraphicsMatrix(m.packed0, m.packed1, m.packed2);
}

float4 PackGraphicsMatrix0(float4x4 m) { return m._m00_m10_m20_m01; }
float4 PackGraphicsMatrix1(float4x4 m) { return m._m11_m21_m02_m12; }
float4 PackGraphicsMatrix2(float4x4 m) { return m._m22_m03_m13_m23; }

float3x3 Inverse3x3(float3x3 m)
{
    float3 r0 = m[0];
    float3 r1 = m[1];
    float3 r2 = m[2];

    float3 c0 = cross(r1, r2);
    float3 c1 = cross(r2, r0);
    float3 c2 = cross(r0, r1);

    float determinant = dot(r0, c0);
    float3x3 inverse = transpose(float3x3(c0, c1, c2) / determinant);
    return inverse;
}

float4x4 AffineMatrixInverse(float4x4 m)
{
    float3x3 rotation = (float3x3)m;
    float3   position = m._m03_m13_m23;

    float3x3 invRotation = Inverse3x3(rotation);
    float3   invPosition = -mul(invRotation, position);

    return float4x4(
        invRotation._m00, invRotation._m01, invRotation._m02, invPosition.x,
        invRotation._m10, invRotation._m11, invRotation._m12, invPosition.y,
        invRotation._m20, invRotation._m21, invRotation._m22, invPosition.z,
        0.0f, 0.0f, 0.0f, 1.0f);
}

#endif // FLORA_INSTANCE_UPLOAD_DATA_INCLUDED
