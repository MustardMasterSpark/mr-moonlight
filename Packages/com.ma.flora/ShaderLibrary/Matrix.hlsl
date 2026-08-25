// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_MATRIX_INCLUDED
#define FLORA_MATRIX_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

float3x3 RotationMatrixFromQuaternion(float4 q)
{
    float3x3 m;

    float xx = q.x * q.x;
    float yy = q.y * q.y;
    float zz = q.z * q.z;
    float xy = q.x * q.y;
    float xz = q.x * q.z;
    float yz = q.y * q.z;
    float wx = q.w * q.x;
    float wy = q.w * q.y;
    float wz = q.w * q.z;

    m[0][0] = 1.0 - 2.0 * (yy + zz);
    m[0][1] = 2.0 * (xy - wz);
    m[0][2] = 2.0 * (xz + wy);

    m[1][0] = 2.0 * (xy + wz);
    m[1][1] = 1.0 - 2.0 * (xx + zz);
    m[1][2] = 2.0 * (yz - wx);

    m[2][0] = 2.0 * (xz - wy);
    m[2][1] = 2.0 * (yz + wx);
    m[2][2] = 1.0 - 2.0 * (xx + yy);

    return m;
}

float4x4 CreateMatrix(float3 x, float3 y, float3 z, float3 t)
{
    return float4x4(
        x.x, y.x, z.x, t.x,
        x.y, y.y, z.y, t.y,
        x.z, y.z, z.z, t.z,
        0.0, 0.0, 0.0, 1.0
    );
}

float4x4 CreateTRSMatrix(float3 t, float4 q, float3 s)
{
    float3x3 r = RotationMatrixFromQuaternion(q);
    float3 x = r[0] * s.x;
    float3 y = r[1] * s.y;
    float3 z = r[2] * s.z;
    return CreateMatrix(x, y, z, t);
}

float4x4 CreateTranslationMatrix(float3 t)
{
    return CreateMatrix(
        float3(1.0, 0.0, 0.0),
        float3(0.0, 1.0, 0.0),
        float3(0.0, 0.0, 1.0),
        t);
}

float4x4 CreateRotationMatrix(float4 q)
{
    float3x3 r = RotationMatrixFromQuaternion(q);
    return CreateMatrix(r[0], r[1], r[2], float3(0.0, 0.0, 0.0));
}

float4x4 CreateScaleMatrix(float3 s)
{
    return float4x4(
        float4(s.x, 0.0, 0.0, 0.0),
        float4(0.0, s.y, 0.0, 0.0),
        float4(0.0, 0.0, s.z, 0.0),
        float4(0.0, 0.0, 0.0, 1.0)
    );
}

float4x4 MatrixInverse(float4x4 trs)
{
    float3x3 invRot;
    invRot[0] = trs[1].yzx * trs[2].zxy - trs[1].zxy * trs[2].yzx;
    invRot[1] = trs[0].zxy * trs[2].yzx - trs[0].yzx * trs[2].zxy;
    invRot[2] = trs[0].yzx * trs[1].zxy - trs[0].zxy * trs[1].yzx;

    float invDet = dot(trs[0].xyz, invRot[0]);
    invRot = transpose(invRot);
    invRot *= rcp(invDet);

    float3 invPos = mul(invRot, -trs._14_24_34);
    float4x4 invTRS;
    invTRS._11_21_31_41 = float4(invRot._11_21_31, 0.0);
    invTRS._12_22_32_42 = float4(invRot._12_22_32, 0.0);
    invTRS._13_23_33_43 = float4(invRot._13_23_33, 0.0);
    invTRS._14_24_34_44 = float4(invPos, 1.0);
    return invTRS;
}

float4 GetXColumn(float4x4 m)
{
    return float4(m[0][0], m[1][0], m[2][0], m[3][0]);
}

float4 GetYColumn(float4x4 m)
{
    return float4(m[0][1], m[1][1], m[2][1], m[3][1]);
}

float4 GetZColumn(float4x4 m)
{
    return float4(m[0][2], m[1][2], m[2][2], m[3][2]);
}

float4 GetWColumn(float4x4 m)
{
    return float4(m[0][3], m[1][3], m[2][3], m[3][3]);
}

float3 GetXAxis(float4x4 m)
{
    return float3(m[0][0], m[1][0], m[2][0]);
}

float3 GetYAxis(float4x4 m)
{
    return float3(m[0][1], m[1][1], m[2][1]);
}

float3 GetZAxis(float4x4 m)
{
    return float3(m[0][2], m[1][2], m[2][2]);
}

float3 GetPosition(float4x4 m)
{
    return float3(m[0][3], m[1][3], m[2][3]);
}

float4x4 TranslateMatrix(float4x4 m, float3 translation)
{
    float4x4 result = m;
    result._14_24_34 += translation;
    return result;
}

void MatrixTransformBounds(float4x4 m, float3 center, float3 extent, out float3 outCenter, out float3 outExtent)
{
    outCenter = mul(m, float4(center, 1.0)).xyz;
    outExtent =
          abs(extent.xxx * GetXAxis(m))
        + abs(extent.yyy * GetYAxis(m))
        + abs(extent.zzz * GetZAxis(m));
}

#endif
