// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_SHADERGRAPH_COMMON_INCLUDED
#define FLORA_SHADERGRAPH_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityDOTSInstancing.hlsl"
#include "Packages/com.ma.flora/ShaderLibrary/Instancing.hlsl"

void Flora_ShaderGraph_GetRandomID_float(float defaultValue, out float outInstanceRandomID)
{
    outInstanceRandomID = defaultValue;
#ifdef UNITY_DOTS_INSTANCING_ENABLED
    outInstanceRandomID = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_CUSTOM_DEFAULT(float, flora_RandomID, defaultValue);
#endif
}

void Flora_ShaderGraph_GetVariationColorWithDefault_float(float4 defaultColor, out float4 outInstanceVariationColor)
{
    outInstanceVariationColor = defaultColor;
#ifdef UNITY_DOTS_INSTANCING_ENABLED
    outInstanceVariationColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_CUSTOM_DEFAULT(float4, flora_VariationColor, defaultColor);
#endif
}

#endif // FLORA_SHADERGRAPH_COMMON_INCLUDED
