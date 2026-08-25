
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

// Calculate a 4 fast sine-cosine pairs
// val:     the 4 input values - each must be in the range (0 to 1)
// s:       The sine of each of the 4 values
// c:       The cosine of each of the 4 values
void FastSinCos(float4 val, out float4 s, out float4 c)
{
    val = val * 6.408849 - 3.1415927;

    // Powers for taylor series
    float4 r5 = val * val;                  // wavevec ^ 2
    float4 r6 = r5 * r5;                    // wavevec ^ 4;
    float4 r7 = r6 * r5;                    // wavevec ^ 6;
    float4 r8 = r6 * r6;                    // wavevec ^ 8;

    float4 r1 = r5 * val;                   // wavevec ^ 3
    float4 r2 = r1 * r5;                    // wavevec ^ 5;
    float4 r3 = r2 * r5;                    // wavevec ^ 7;

    // Vectors for taylor's series expansion of sin and cos
    float4 sin7 = { +1.0, -0.161616160, +0.0083333000, -0.000198410000 };
    float4 cos8 = { -0.5, +0.041666666, -0.0013888889, +0.000024801587 };

    // sin
    s = val + r1 * sin7.y + r2 * sin7.z + r3 * sin7.w;
    // cos
    c = 1.0 + r5 * cos8.x + r6 * cos8.y + r7 * cos8.z + r8 * cos8.w;
}

void ScaleFromObject_float(out float Scale)
{
    float4x4 objectToWorld = GetObjectToWorldMatrix();

    float3 scale;
    scale.x = length(objectToWorld._m00_m10_m20);
    scale.y = length(objectToWorld._m01_m11_m21);
    scale.z = length(objectToWorld._m02_m12_m22);
    Scale = max(max(scale.x, scale.y), scale.z);
}

void RandomFromPosition_float(float3 PositionWS, out float RandomID)
{
    uint x = asuint(PositionWS.x);
    uint y = asuint(PositionWS.y);
    uint z = asuint(PositionWS.z);

    uint h = x ^ 2747636419u;
    h *= 2654435769u;
    h *= 2654435769u; h ^= h >> 16;
    h *= 2654435769u; h ^= y; h ^= z;
    RandomID = (float)(h & 0x00FFFFFFu) / 16777215.0;
}

void TerrainBillboardGrass_half(
    float3 Position, float3 Offset, out float3 OutPosition)
{
    float4x4 viewMatrix  = GetViewToWorldMatrix();
    float3 viewRight    = viewMatrix[0].xyz;

    OutPosition      = Position;
    OutPosition.xyz += Offset.x * viewRight.xyz;
    OutPosition.y   += Offset.y;
}

void TerrainWaveGrass_half(
    half4 Albedo, float3 Position, half WindSpeed, half WindAmount, half WaveSize, half WaveAmount, half3 WavingTint,
    out float4 OutColor, out float3 OutPosition
    )
{
    float4x4 viewMatrix = GetViewToWorldMatrix();
    float3 viewPosition = viewMatrix[3].xyz;

    half4 waveSizeX = half4(0.012, 0.02, 0.06, 0.024) * WaveSize;
    half4 waveSizeZ = half4(0.006, 0.02, 0.02, 0.050) * WaveSize;
    half4 waveSpeed = half4(1.200, 2.00, 1.60, 4.800);

    half4 waveMoveX = half4(0.024, 0.04, -0.12, 0.096);
    half4 waveMoveZ = half4(0.006, 0.02, -0.02, 0.100);

    float4 waves = Position.x * waveSizeX;
    waves += Position.z * waveSizeZ;

    // Add in time to model them over time
    waves += WindSpeed * waveSpeed * _Time.y * 0.00033;

    float4 s, c;
    waves = frac(waves);
    FastSinCos(waves, s, c);
    s = s * s;
    s = s * s;

    half lighting = dot(s, normalize(half4(1.0, 1.0, 0.4, 0.2))) * 0.7;
    s = s * WaveAmount;

    half3 waveMove = 0;
    waveMove.x = dot(s, waveMoveX);
    waveMove.z = dot(s, waveMoveZ);

    Position.xz -= waveMove.xz * WindAmount;

    // Apply color animation
    half3 waveColor = lerp(real3(0.5, 0.5, 0.5), WavingTint.rgb, lighting);

    OutPosition = Position;
    OutColor = half4(2.0 * waveColor * Albedo.rgb, Albedo.a);
}
