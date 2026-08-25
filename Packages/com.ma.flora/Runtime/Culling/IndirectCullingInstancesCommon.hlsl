// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_INDIRECT_CULLING_INSTANCES_COMMON_INCLUDED
#define FLORA_INDIRECT_CULLING_INSTANCES_COMMON_INCLUDED

//--------------------------------------------------------------------------------------------------
// Instance Kernel Common Helpers
//--------------------------------------------------------------------------------------------------

static const uint kInstancesPerChunk = 64u;

static const uint kMaxDrawInstancesPerThread = 2u; // Cross-fading can emit 2 instances per thread
static const uint kMaxDrawInstances          = kInstancesPerChunk * kMaxDrawInstancesPerThread;

static const uint kCullingFlagDistance      = 1u << 0u; // Enable distance culling
static const uint kCullingFlagScreenSize    = 1u << 1u; // Enable minimum screen size culling
static const uint kCullingFlagGlobalDensity = 1u << 2u; // Enable global density culling
static const uint kCullingFlagRangeDensity  = 1u << 3u; // Enable range density culling

static const uint kMaxLodCount = 8u;   // Max LODs per archetype supported
static const uint kInvalidLod  = 0xff; // Invalid LOD index

static const uint kDrawStateFlagFade    = INDIRECTSTATEFLAGS_HAS_FADE_KEYWORD;    // Fade keyword indicates cross-faded dithering is active
static const uint kDrawStateFlagMotion  = INDIRECTSTATEFLAGS_HAS_MOTION;           // Motion indicates that the motion vector pass is needed
static const uint kDrawStateFlagFlip    = INDIRECTSTATEFLAGS_HAS_FLIPPED_WINDING;  // Flipped winding indicates that the instance has negative scale on one axis
static const uint kMaxDrawStateKeyCount = INDIRECTSTATEFLAGS_KEY_COUNT;            // Number of possible draw state keys

static const uint kDebugLodModeNone     = 0u;
static const uint kDebugLodModeForceLod = 1u; // Force a specific LOD (any LOD will become the target)
static const uint kDebugLodModeOnlyLod  = 2u; // Only render a specific LOD (other LODs are culled)

static const float kMinScreenTransitionWidth = 0.1;

#define DENSITY_SEED       (0x73e2f3a1u) // Seed for global density randomization
#define DENSITY_RANGE_SEED (0x9e3779b9u) // Seed for range density randomization
#define LOD_JITTER_SEED    (0x4b0f1c3du) // Seed for LOD jitter randomization

//--------------------------------------------------------------------------------------------------
// Types
//--------------------------------------------------------------------------------------------------

struct DrawInstance
{
    uint packedFadeAndIndex; // [31:24]=signedFade8, [23:0]=instanceIndex
    uint packedSplitKeyLod;  // [31:16]=splitMask, [15:8]=binKey, [7:0]=lodIndex
};

//--------------------------------------------------------------------------------------------------
// Common Math
//--------------------------------------------------------------------------------------------------

float CalculateScreenDistance(float size, float screenHeight)
{
    return size / screenHeight;
}

float CalculateScreenDistanceRcp(float size, float rcpScreenHeight)
{
    return size * rcpScreenHeight;
}

float CalculatePerspectiveDistanceSq(float3 objPosition, float3 cameraPosition, float screenRelativeMetricSq)
{
    return Length2(objPosition - cameraPosition) * screenRelativeMetricSq;
}

//--------------------------------------------------------------------------------------------------
// Cross-fading
//--------------------------------------------------------------------------------------------------

static const uint kLODFadeInvalid = 127u;
static const uint kLODFadeOff     = 254u;

// [-1..1] -> unsigned [0..254] (127 never produced)
uint PackCrossFadeUint8(float percent)
{
    uint packed = uint((1.0 + percent) * 127.0 + 0.5);
    return (percent < 0.0) ? clamp(packed,   0u, 126u)
                           : clamp(packed, 128u, 254u);
}

// [0..1] -> fade-out unsigned [128..254] (positive side)
uint PackFadeOutUint8(float percent)
{
    uint packed = uint(percent * 127.0 + 0.5);
    return clamp(128u + packed, 128u, 254u); // 128 -> 254
}

// [0..1] -> fade-in unsigned [0..126] (negative side compliment)
uint PackFadeInUint8(float percent)
{
    uint packed = uint(percent * 127.0 + 0.5);
    return clamp(126u - packed, 0u, 126u); // 126 -> 0
}

int EncodeCrossFadeSint8(uint packed)
{
    return int(packed) - 127;
}

//--------------------------------------------------------------------------------------------------
// Editor Picking Bits
//--------------------------------------------------------------------------------------------------

static const uint2 kBits64_Zero = 0;
static const uint2 kBits64_Full = uint2(0xffffffffu, 0xffffffffu);

bool HasFlag64(uint2 bits, uint bit)
{
    return (bit < 32u ? (bits.x & 1u << bit) : (bits.y & 1u << bit - 32u)) != 0u;
}

//--------------------------------------------------------------------------------------------------
// Debug
//--------------------------------------------------------------------------------------------------

void DebugEmitError(uint code, uint data0, uint data1 = 0, uint data2 = 0)
{
#ifdef DEBUG_ENABLED
    if (_DebugErrorEnabled)
    {
        uint errorIndex;
        InterlockedAdd(_DebugErrorCount[0], 1u, errorIndex);

        if (errorIndex < _DebugErrorCapacity)
            _DebugErrorBuffer[errorIndex] = uint4(code, data0, data1, data2);
    }
#endif
}

#endif // FLORA_INDIRECT_CULLING_INSTANCES_COMMON_INCLUDED
