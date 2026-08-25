//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef DEBUGCULLINGGRID_CS_HLSL
#define DEBUGCULLINGGRID_CS_HLSL
// Generated from MA.Flora.DebugCullingGridShaderVariables
// PackingRules = Exact
CBUFFER_START(DebugCullingGridShaderVariables)
    float4 _FrustumPlanes[6];
    float4 _CameraPositionAndDist;
    float4 _CullingSettings;
CBUFFER_END

// Generated from MA.Flora.DebugLineVertex
// PackingRules = Exact
struct DebugLineVertex
{
    float3 position;
    float weight;
    float4 color;
};


#endif
