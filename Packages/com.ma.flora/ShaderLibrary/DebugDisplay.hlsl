// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_DEBUG_DISPLAY_INCLUDED
#define FLORA_DEBUG_DISPLAY_INCLUDED

#if defined(DEBUG_DISPLAY) && defined(UNITY_DOTS_INSTANCING_ENABLED)
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Debug.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityDOTSInstancing.hlsl"
#include "Packages/com.ma.flora/Runtime/Debugging/DebugDisplayFlora.cs.hlsl"

static const float4 flora_LODDebugColors[8] =
{
    float4(1.0, 1.0, 1.0, 1.0), // LOD 0 - White
    float4(1.0, 0.0, 0.0, 1.0), // LOD 1 - Red
    float4(0.0, 1.0, 0.0, 1.0), // LOD 2 - Green
    float4(0.0, 0.0, 1.0, 1.0), // LOD 3 - Blue
    float4(1.0, 1.0, 0.0, 1.0), // LOD 4 - Yellow
    float4(1.0, 0.0, 1.0, 1.0), // LOD 5 - Fuchsia
    float4(0.0, 1.0, 1.0, 1.0), // LOD 6 - Cyan
    float4(0.5, 0.0, 0.5, 1.0)  // LOD 7 - Purple
};

StructuredBuffer<uint> flora_DebugDrawVisibility;
int flora_DebugViewMode;
float flora_DebugOpacity;

half4 FloraColorFromIndex(uint index)
{
    return half4(uint4(index >> 0, index >> 8, index >> 16, index >> 24) & 0xff) / 255.0;
}

half4 FloraDebugRandomColorFromIndex(uint index)
{
    uint h = JenkinsHash(index);
    return FloraColorFromIndex(h);
}

bool GetFloraDebugColor(out half4 debugColor)
{
    debugColor = half4(0.0, 0.0, 0.0, 1.0);

    if (flora_DebugViewMode == DEBUGINSTANCEDRAWMODE_NONE)
        return false;

    if (flora_DebugViewMode == DEBUGINSTANCEDRAWMODE_INSTANCE_HANDLE)
    {
        uint entityId = UNITY_ACCESS_DOTS_INSTANCED_PROP(uint2, unity_EntityId).x;
        debugColor = FloraColorFromIndex(1 + entityId);
        return true;
    }

    uint debugValue = flora_DebugDrawVisibility[unity_SampledDOTSIndirectVisibleIndex];
    if (flora_DebugViewMode == DEBUGINSTANCEDRAWMODE_LOD)
    {
        debugColor = flora_LODDebugColors[debugValue];
        return true;
    }

    if (flora_DebugViewMode == DEBUGINSTANCEDRAWMODE_RANDOM_ID)
    {
        debugColor = FloraColorFromIndex(debugValue);
        return true;
    }

    if (flora_DebugViewMode == DEBUGINSTANCEDRAWMODE_TEMPLATE ||
        flora_DebugViewMode == DEBUGINSTANCEDRAWMODE_DRAW ||
        flora_DebugViewMode == DEBUGINSTANCEDRAWMODE_DRAW_VARIANT ||
        flora_DebugViewMode == DEBUGINSTANCEDRAWMODE_CULLING_BATCH ||
        flora_DebugViewMode == DEBUGINSTANCEDRAWMODE_BATCH_DOMAIN)
    {
        debugColor = FloraDebugRandomColorFromIndex(debugValue);
        return true;
    }

    return false;
}

//-----------------------------------------------------------------------------
// FLORA_UNIVERSAL_PIPELINE
#if defined(FLORA_UNIVERSAL_PIPELINE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    // URP - PBR
    half4 FloraDebugFragmentURP(InputData inputData, SurfaceData surfaceData)
    {
        half4 debugColor;
        if (GetFloraDebugColor(debugColor))
        {
            return lerp(UniversalFragmentPBR(inputData, surfaceData), debugColor, flora_DebugOpacity);
        }

        return UniversalFragmentPBR(inputData, surfaceData);
    }

    #undef UniversalFragmentPBR
    #define UniversalFragmentPBR FloraDebugFragmentURP

    // URP - Baked Lit
    half4 FloraDebugFragmentBakedLit(InputData inputData, SurfaceData surfaceData)
    {
        half4 debugColor;
        if (GetFloraDebugColor(debugColor))
        {
            return lerp(UniversalFragmentBakedLit(inputData, surfaceData), debugColor, flora_DebugOpacity);
        }

        return UniversalFragmentBakedLit(inputData, surfaceData);
    }

    #undef UniversalFragmentBakedLit
    #define UniversalFragmentBakedLit FloraDebugFragmentBakedLit

    // URP - Blinn-Phong
    half4 FloraDebugFragmentBlinnPhong(InputData inputData, SurfaceData surfaceData)
    {
        half4 debugColor;
        if (GetFloraDebugColor(debugColor))
        {
            return lerp(UniversalFragmentBlinnPhong(inputData, surfaceData), debugColor, flora_DebugOpacity);
        }

        return UniversalFragmentBlinnPhong(inputData, surfaceData);
    }

    #undef UniversalFragmentBlinnPhong
    #define UniversalFragmentBlinnPhong FloraDebugFragmentBlinnPhong

    // URP - Deferred Rendering
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

#if UNITY_VERSION >= 60002000
    #define FLORA_URP_GBUFFER_OUTPUT GBufferFragOutput
#else
    #define FLORA_URP_GBUFFER_OUTPUT FragmentOutput
#endif

    FLORA_URP_GBUFFER_OUTPUT FloraDebugBRDFDataToGbuffer(BRDFData brdfData, InputData inputData, half smoothness, half3 globalIllumination, half occlusion = 1.0)
    {
        half4 debugColor;
        if (GetFloraDebugColor(debugColor))
        {
            globalIllumination = lerp(globalIllumination, debugColor.rgb, flora_DebugOpacity);
        }

        return BRDFDataToGbuffer(brdfData, inputData, smoothness, globalIllumination, occlusion);
    }

    #undef BRDFDataToGbuffer
    #define BRDFDataToGbuffer FloraDebugBRDFDataToGbuffer
// FLORA_HDRP_PIPELINE
#elif defined(FLORA_HDRP_PIPELINE)
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Debug/DebugDisplayMaterial.hlsl"

#ifdef UNLIT_CS_HLSL
    bool FloraGetMaterialDebugColor(inout float4 color
    #ifndef VFX_VARYING_PS_INPUTS
        , const FragInputs input
    #endif
        , const BuiltinData builtinData
        , const PositionInputs posInput
        , const SurfaceData surfaceData
        , const BSDFData bsdfData)
    {
        if (GetMaterialDebugColor(color, input, builtinData, posInput, surfaceData, bsdfData))
        {
            return true;
        }

        if (GetFloraDebugColor(color))
        {
            color.rgb = lerp(surfaceData.color, color.rgb, flora_DebugOpacity);
            return true;
        }

        return false;
    }

    #undef GetMaterialDebugColor
    #define GetMaterialDebugColor FloraGetMaterialDebugColor
#else // UNLIT_CS_HLSL
    void FloraApplyDebugToSurfaceData(float3x3 tangentToWorld, inout SurfaceData surfaceData)
    {
        ApplyDebugToSurfaceData(tangentToWorld, surfaceData);

        half4 debugColor;
        if (GetFloraDebugColor(debugColor))
        {
            surfaceData.baseColor = lerp(surfaceData.baseColor, debugColor.rgb, flora_DebugOpacity);
        }
    }

    #undef ApplyDebugToSurfaceData
    #define ApplyDebugToSurfaceData FloraApplyDebugToSurfaceData
#endif

#endif // !FLORA_UNIVERSAL_PIPELINE && !FLORA_HDRP_PIPELINE
#endif // !DEBUG_DISPLAY
#endif // !FLORA_DEBUG_DISPLAY_INCLUDED
